using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamTweak
{
    public class StreamingLogMonitor : IDisposable
    {
        private StreamReader? logStreamReader;
        private string? currentLogFilePath;
        private string? monitoredDirectory;
        private Task? monitoringTask;
        private CancellationTokenSource? cancellationTokenSource;
        private bool isDisposed = false;

        // How often to re-run the full discovery to catch dynamic logs
        // that appear after startup (e.g. Vibepollo creating logs\ after StreamTweak starts)
        private const int REDISCOVERY_INTERVAL_MS = 10000;
        private DateTime lastRediscoveryTime = DateTime.MinValue;

        public event EventHandler<StreamingEventArgs>? StreamingEventDetected;

        public class StreamingEventArgs : EventArgs
        {
            public LogParser.StreamingEvent Event { get; set; }
        }

        public void StartMonitoring()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(StreamingLogMonitor));

            currentLogFilePath = LogParser.FindStreamingServiceLogFile();

            if (string.IsNullOrEmpty(currentLogFilePath))
            {
                DebugLog("No log file found at startup — will keep retrying via rediscovery");
            }
            else
            {
                monitoredDirectory = Path.GetDirectoryName(currentLogFilePath);
                DebugLog($"Starting log monitoring in directory: {monitoredDirectory}");
                DebugLog($"Initial log file: {Path.GetFileName(currentLogFilePath)}");
            }

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

        private async Task MonitorLogFileAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentLogFilePath) && File.Exists(currentLogFilePath))
                    OpenStreamReader();

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Periodic full rediscovery — catches dynamic logs that appear after startup
                    await CheckForRediscoveryAsync(cancellationToken);

                    if (logStreamReader == null)
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

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
                        // No new lines — check for rotation within current directory, then wait
                        CheckForLogRotation();
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog("Monitoring cancelled");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in monitoring loop: {ex.Message}");
            }
        }

        /// <summary>
        /// Every REDISCOVERY_INTERVAL_MS, re-runs the full FindStreamingServiceLogFile() discovery.
        /// This handles the case where a dynamic log file (e.g. Vibepollo logs\sunshine-*.log)
        /// appears after StreamTweak has already started monitoring a static fallback file.
        /// If a better or different log is found, switches to it.
        /// </summary>
        private async Task CheckForRediscoveryAsync(CancellationToken cancellationToken)
        {
            if ((DateTime.Now - lastRediscoveryTime).TotalMilliseconds < REDISCOVERY_INTERVAL_MS)
                return;

            lastRediscoveryTime = DateTime.Now;

            try
            {
                string? discovered = LogParser.FindStreamingServiceLogFile();

                if (string.IsNullOrEmpty(discovered)) return;

                // Switch if: no file monitored yet, or a different (better) file found
                if (string.IsNullOrEmpty(currentLogFilePath) ||
                    !string.Equals(discovered, currentLogFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog($"Rediscovery: switching from '{Path.GetFileName(currentLogFilePath ?? "none")}' to '{Path.GetFileName(discovered)}'");
                    currentLogFilePath = discovered;
                    monitoredDirectory = Path.GetDirectoryName(discovered);
                    OpenStreamReader(discovered);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error during rediscovery: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a newer dynamic log file has appeared in the same directory (log rotation).
        /// Only relevant for directories that contain sunshine-*.log files.
        /// </summary>
        private void CheckForLogRotation()
        {
            if (string.IsNullOrEmpty(monitoredDirectory)) return;

            try
            {
                string? latestLog = FindMostRecentLogFileInDir(monitoredDirectory);

                if (latestLog != null &&
                    !string.Equals(latestLog, currentLogFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog($"Log rotation: switching from '{Path.GetFileName(currentLogFilePath)}' to '{Path.GetFileName(latestLog)}'");
                    currentLogFilePath = latestLog;
                    OpenStreamReader(latestLog);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error during rotation check: {ex.Message}");
            }
        }

        private string? FindMostRecentLogFileInDir(string directory)
        {
            if (!Directory.Exists(directory)) return null;
            try
            {
                return Directory.GetFiles(directory, "sunshine-*.log")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        // FileStream intentionally NOT in a using block — it must outlive the StreamReader
        private void OpenStreamReader(string? filePath = null)
        {
            try { logStreamReader?.Dispose(); } catch { }
            logStreamReader = null;

            string targetPath = filePath ?? currentLogFilePath ?? string.Empty;
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return;

            try
            {
                var fileStream = new FileStream(
                    targetPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                fileStream.Seek(0, SeekOrigin.End);
                logStreamReader = new StreamReader(fileStream);
                DebugLog($"StreamReader opened on: {Path.GetFileName(targetPath)}");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR opening stream reader: {ex.Message}");
            }
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