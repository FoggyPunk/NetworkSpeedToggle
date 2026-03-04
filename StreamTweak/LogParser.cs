using System;
using System.IO;

namespace StreamTweak
{
    /// <summary>
    /// Parses Sunshine/Apollo log files to detect streaming events
    /// </summary>
    public class LogParser
    {
        public enum StreamingEvent
        {
            None,
            StreamStarted,
            StreamStopped
        }

        /// <summary>
        /// Detects streaming events from a log line
        /// </summary>
        public static StreamingEvent ParseLogLine(string logLine)
        {
            if (string.IsNullOrWhiteSpace(logLine))
                return StreamingEvent.None;

            string lowerLine = logLine.ToLower();

            // Check StreamStopped FIRST (more specific patterns)
            if (lowerLine.Contains("client disconnected") ||
                lowerLine.Contains("stream ended") ||
                lowerLine.Contains("stream stopped") ||
                lowerLine.Contains("stopping stream"))
            {
                DebugLog($"StreamStopped detected: {logLine}");
                return StreamingEvent.StreamStopped;
            }

            // Then check StreamStarted
            if (lowerLine.Contains("client connected") ||
                lowerLine.Contains("starting stream") ||
                lowerLine.Contains("stream started") ||
                lowerLine.Contains("client ip") ||
                lowerLine.Contains("moonlight"))
            {
                DebugLog($"StreamStarted detected: {logLine}");
                return StreamingEvent.StreamStarted;
            }

            return StreamingEvent.None;
        }

        /// <summary>
        /// Tries to find the log file for Sunshine or Apollo
        /// </summary>
        public static string? FindStreamingServiceLogFile()
        {
            string? apolloLog = FindApolloLog();
            if (!string.IsNullOrEmpty(apolloLog) && File.Exists(apolloLog))
                return apolloLog;

            string? sunshineLog = FindSunshineLog();
            if (!string.IsNullOrEmpty(sunshineLog) && File.Exists(sunshineLog))
                return sunshineLog;

            return null;
        }

        private static string? FindApolloLog()
        {
            try
            {
                string programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                string apolloLogPath = Path.Combine(programFiles, "Apollo", "config", "sunshine.log");
                if (File.Exists(apolloLogPath)) return apolloLogPath;
            }
            catch { }
            return null;
        }

        private static string? FindSunshineLog()
        {
            try
            {
                string programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                string sunshineLogPath = Path.Combine(programFiles, "Sunshine", "config", "sunshine.log");
                if (File.Exists(sunshineLogPath)) return sunshineLogPath;
            }
            catch { }
            return null;
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
