using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StreamTweak
{
    /// <summary>
    /// Monitors Sunshine/Apollo log files for streaming events
    /// </summary>
    public class StreamingLogMonitor : IDisposable
    {
        private StreamReader? logStreamReader;
        private string? currentLogFilePath;
        private Task? monitoringTask;
        private CancellationTokenSource? cancellationTokenSource;
        private bool isDisposed = false;

        public event EventHandler<StreamingEventArgs>? StreamingEventDetected;

        public class StreamingEventArgs : EventArgs
        {
            public LogParser.StreamingEvent Event { get; set; }
        }

        /// <summary>
        /// Starts monitoring the Sunshine/Apollo log file
        /// </summary>
        public void StartMonitoring()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(StreamingLogMonitor));

            currentLogFilePath = LogParser.FindStreamingServiceLogFile();
            if (string.IsNullOrEmpty(currentLogFilePath))
            {
                DebugLog("ERROR: Sunshine/Apollo log file not found!");
                return;
            }

            DebugLog($"Starting log monitoring: {currentLogFilePath}");

            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                monitoringTask = MonitorLogFileAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR starting monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops monitoring the log file
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                logStreamReader?.Dispose();
                logStreamReader = null;
            }
            catch { }
        }

        /// <summary>
        /// Asynchronously monitors the log file for new content
        /// </summary>
        private async Task MonitorLogFileAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(currentLogFilePath) || !File.Exists(currentLogFilePath))
                return;

            try
            {
                using var fileStream = new FileStream(currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.Seek(0, SeekOrigin.End);
                logStreamReader = new StreamReader(fileStream);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await logStreamReader.ReadLineAsync();

                    if (line != null)
                    {
                        LogParser.StreamingEvent streamingEvent = LogParser.ParseLogLine(line);

                        if (streamingEvent != LogParser.StreamingEvent.None)
                        {
                            DebugLog($"Event raised: {streamingEvent}");
                            StreamingEventDetected?.Invoke(this, new StreamingEventArgs { Event = streamingEvent });
                        }
                    }
                    else
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch { }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            StopMonitoring();
            cancellationTokenSource?.Dispose();
            isDisposed = true;
        }

        private static void DebugLog(string message)
        {
            try
            {
                string debugLogPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamTweak", "debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(debugLogPath) ?? "");
                File.AppendAllText(debugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
