using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace StreamTweak
{
    public class LogParser
    {
        public enum StreamingEvent
        {
            None,
            StreamStarted,
            StreamStopped
        }

        // Known app names to look for in the registry and Program Files
        private static readonly string[] KnownAppNames =
        {
            "Vibepollo", "Vibeshine", "Apollo", "Sunshine"
        };

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

        public static string? FindStreamingServiceLogFile()
        {
            // Step 1: try registry — fast and precise
            string? log = FindLogViaRegistry();
            if (log != null) return log;

            // Step 2: fallback — scan Program Files for known config structures
            log = FindLogViaProgramFilesScan();
            if (log != null) return log;

            DebugLog("No streaming service log file found");
            return null;
        }

        #region Registry discovery

        private static string? FindLogViaRegistry()
        {
            foreach (string appName in KnownAppNames)
            {
                string? installDir = GetInstallDirFromRegistry(appName);
                if (string.IsNullOrEmpty(installDir)) continue;

                string? log = FindLogInInstallDir(installDir, appName);
                if (log != null) return log;
            }
            return null;
        }

        private static string? GetInstallDirFromRegistry(string appName)
        {
            // Try direct software key first
            string? dir = ReadRegistryInstallDir($@"SOFTWARE\{appName}")
                       ?? ReadRegistryInstallDir($@"SOFTWARE\WOW6432Node\{appName}");

            if (!string.IsNullOrEmpty(dir)) return dir;

            // Try Uninstall entries
            return FindInUninstallKeys(appName);
        }

        private static string? ReadRegistryInstallDir(string subKey)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                if (key == null) return null;

                // Common value names used by installers
                foreach (string valueName in new[] { "InstallLocation", "InstallDir", "Path" })
                {
                    string? val = key.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                    {
                        DebugLog($"Registry: found {subKey} → {val}");
                        return val;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string? FindInUninstallKeys(string appName)
        {
            string[] uninstallPaths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string uninstallPath in uninstallPaths)
            {
                try
                {
                    using var uninstallKey = Registry.LocalMachine.OpenSubKey(uninstallPath);
                    if (uninstallKey == null) continue;

                    foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = uninstallKey.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            string? displayName = subKey.GetValue("DisplayName") as string;
                            if (string.IsNullOrEmpty(displayName)) continue;

                            if (!displayName.Contains(appName, StringComparison.OrdinalIgnoreCase)) continue;

                            string? installDir = subKey.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                            {
                                DebugLog($"Uninstall registry: found {appName} → {installDir}");
                                return installDir;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return null;
        }

        #endregion

        #region Program Files scan fallback

        private static string? FindLogViaProgramFilesScan()
        {
            DebugLog("Registry lookup failed — scanning Program Files...");

            // Collect all candidate Program Files directories
            var searchRoots = new List<string>();

            string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            string pfx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

            if (Directory.Exists(pf)) searchRoots.Add(pf);
            if (Directory.Exists(pfx86) && pfx86 != pf) searchRoots.Add(pfx86);

            // Search in known-name folders first (faster), then any folder
            foreach (string root in searchRoots)
            {
                // Priority scan: known app names first
                foreach (string appName in KnownAppNames)
                {
                    string candidate = Path.Combine(root, appName);
                    if (!Directory.Exists(candidate)) continue;

                    string? log = FindLogInInstallDir(candidate, appName);
                    if (log != null) return log;
                }

                // Broad scan: any subfolder with a sunshine config structure
                try
                {
                    foreach (string dir in Directory.GetDirectories(root))
                    {
                        // Skip already-checked known names
                        string dirName = Path.GetFileName(dir);
                        if (KnownAppNames.Any(n => n.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        string? log = FindLogInInstallDir(dir, dirName);
                        if (log != null) return log;
                    }
                }
                catch { }
            }

            return null;
        }

        #endregion

        #region Log file resolution

        private static string? FindLogInInstallDir(string installDir, string appName)
        {
            try
            {
                string configDir = Path.Combine(installDir, "config");
                if (!Directory.Exists(configDir)) return null;

                // Dynamic logs subfolder (Vibeshine/Vibepollo style)
                string logsDir = Path.Combine(configDir, "logs");
                if (Directory.Exists(logsDir))
                {
                    string? dynamic = FindMostRecentLogFile(logsDir);
                    if (dynamic != null) return dynamic;
                }

                // Static log file (Sunshine/Apollo style)
                string staticLog = Path.Combine(configDir, "sunshine.log");
                if (File.Exists(staticLog))
                {
                    DebugLog($"Found static log for {appName}: {staticLog}");
                    return staticLog;
                }
            }
            catch { }
            return null;
        }

        private static string? FindMostRecentLogFile(string logDirectory, string searchPattern = "sunshine-*.log")
        {
            if (!Directory.Exists(logDirectory)) return null;
            try
            {
                var latest = Directory.GetFiles(logDirectory, searchPattern)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(latest))
                {
                    DebugLog($"Found dynamic log file: {Path.GetFileName(latest)} in {logDirectory}");
                    return latest;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error scanning directory {logDirectory}: {ex.Message}");
            }
            return null;
        }

        #endregion

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

        // ─── Streaming App Detection ─────────────────────────────────────────

        public static StreamingAppInfo? FindStreamingAppInfo()
        {
            foreach (string appName in KnownAppNames)
            {
                string? installDir = GetInstallDirFromRegistry(appName);
                if (!string.IsNullOrEmpty(installDir))
                {
                    var info = BuildStreamingAppInfo(appName, installDir);
                    if (info != null) return info;
                }
            }

            var searchRoots = new List<string>();
            string pf    = Environment.GetEnvironmentVariable("ProgramFiles")       ?? @"C:\Program Files";
            string pfx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
            if (Directory.Exists(pf))              searchRoots.Add(pf);
            if (Directory.Exists(pfx86) && pfx86 != pf) searchRoots.Add(pfx86);

            foreach (string root in searchRoots)
                foreach (string appName in KnownAppNames)
                {
                    string candidate = Path.Combine(root, appName);
                    if (Directory.Exists(candidate))
                    {
                        var info = BuildStreamingAppInfo(appName, candidate);
                        if (info != null) return info;
                    }
                }

            return null;
        }

        private static StreamingAppInfo? BuildStreamingAppInfo(string appName, string installDir)
        {
            var info = new StreamingAppInfo { AppName = appName };

            try
            {
                var exes = Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly);
                info.ExePath = exes.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Contains(appName, StringComparison.OrdinalIgnoreCase))
                    ?? exes.FirstOrDefault();
            }
            catch { }

            if (string.IsNullOrEmpty(info.ExePath))
                return null;

            string configDir = Path.Combine(installDir, "config");
            string logsDir   = Path.Combine(configDir, "logs");
            if      (Directory.Exists(logsDir))   info.LogFolderPath = logsDir;
            else if (Directory.Exists(configDir)) info.LogFolderPath = configDir;
            else                                  info.LogFolderPath = installDir;

            return info;
        }
    }

    public class StreamingAppInfo
    {
        public string  AppName       { get; set; } = string.Empty;
        public string? ExePath        { get; set; }
        public string? LogFolderPath  { get; set; }
    }
}