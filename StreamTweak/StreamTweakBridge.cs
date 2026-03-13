using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace StreamTweak
{
    /// <summary>
    /// Listens on TCP port 47998 for commands from the StreamTweak Moonlight fork.
    /// 
    /// Protocol (plain text, one command per connection):
    ///   PREPARE  — client is about to launch a stream; set NIC to 1 Gbps now,
    ///              before the stream starts, so no disconnect occurs mid-session.
    ///   RESTORE  — client has ended the stream; restore original NIC speed.
    ///              Acts as an explicit fallback alongside the log monitor.
    ///   STATUS   — client queries the current NIC link speed.
    ///              Server replies with the speed in Mbps (e.g. "1000") or "UNKNOWN".
    /// 
    /// Each connection is short-lived: client sends one line, server replies "OK",
    /// the speed string, or "ERR".
    /// 
    /// Wire up StatusProvider in App.xaml.cs after _bridge.Start():
    ///   _bridge.StatusProvider = () => GetCurrentLinkSpeedMbps().ToString();
    /// </summary>
    public sealed class StreamTweakBridge : IDisposable
    {
        public const int Port = 47998;

        /// <summary>
        /// Optional delegate that returns the current NIC link speed in Mbps
        /// as a string (e.g. "1000", "2500"). Called when a STATUS command arrives.
        /// Set this in App.xaml.cs after creating the bridge.
        /// </summary>
        public Func<string>? StatusProvider { get; set; }

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private bool _disposed;

        public event Action? PrepareRequested;
        public event Action? RestoreRequested;

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(StreamTweakBridge));
            if (_listener != null) return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _listenTask = ListenAsync(_cts.Token);
                DebugLog($"StreamTweakBridge listening on 0.0.0.0:{Port}");
            }
            catch (Exception ex)
            {
                DebugLog($"StreamTweakBridge failed to start: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener = null;
            }
            catch { }
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync(ct);
                    _ = HandleClientAsync(client, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        DebugLog($"StreamTweakBridge accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    string? command = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        await writer.WriteLineAsync("ERR");
                        return;
                    }

                    command = command.Trim().ToUpperInvariant();
                    DebugLog($"StreamTweakBridge received: {command} from {client.Client.RemoteEndPoint}");

                    switch (command)
                    {
                        case "PREPARE":
                            PrepareRequested?.Invoke();
                            await writer.WriteLineAsync("OK");
                            break;

                        case "RESTORE":
                            RestoreRequested?.Invoke();
                            await writer.WriteLineAsync("OK");
                            break;

                        case "STATUS":
                            string status = StatusProvider?.Invoke() ?? "UNKNOWN";
                            await writer.WriteLineAsync(status);
                            break;

                        default:
                            DebugLog($"StreamTweakBridge unknown command: {command}");
                            await writer.WriteLineAsync("ERR");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"StreamTweakBridge client handler error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cts?.Dispose();
            _disposed = true;
        }

        private static void DebugLog(string message)
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamTweak", "debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
