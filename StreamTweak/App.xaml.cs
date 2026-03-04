using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace StreamTweak
{
    public partial class App : Application
    {
        private TaskbarIcon tb = default!;
        private string adapterName = "Ethernet";
        private readonly string iconOkPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streammodeok.ico");
        private readonly string iconKoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streammodeko.ico");

        private SettingsWindow? settingsWindow = null;
        private bool isStreamingModeActive = false;
        private string originalSpeedForStreaming = string.Empty;

        // Auto-monitoring fields
        private StreamingLogMonitor? logMonitor = null;
        private bool isAutoStreamingActive = false;
        private string? originalSpeedForAutoStreaming = null;
        private bool isAutoStreamingEnabled = true;

        // Inactivity timer to prevent reconnect loops after speed change
        private System.Windows.Threading.DispatcherTimer? inactivityTimer = null;
        private const int INACTIVITY_TIMEOUT_MS = 30000;

        private string GetConfigPath()
        {
            string appFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StreamTweak");
            return System.IO.Path.Combine(appFolder, "config.json");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoadConfig();

            tb = (TaskbarIcon)FindResource("MyNotifyIcon")!;
            UpdateIconBasedOnSpeed(false);
            UpdateStreamingMenuItem();
            StartAutoStreamingMonitor();
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = GetConfigPath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        var root = document.RootElement;

                        if (root.TryGetProperty("NetworkAdapterName", out JsonElement adapterElement))
                            adapterName = adapterElement.GetString() ?? "Ethernet";

                        if (root.TryGetProperty("StreamingMode", out JsonElement streamingElement))
                            isStreamingModeActive = streamingElement.GetBoolean();

                        if (root.TryGetProperty("OriginalSpeed", out JsonElement originalSpeedElement))
                            originalSpeedForStreaming = originalSpeedElement.GetString() ?? string.Empty;

                        if (root.TryGetProperty("AutoStreamingEnabled", out JsonElement autoStreamingElement))
                            isAutoStreamingEnabled = autoStreamingElement.GetBoolean();
                        else
                            isAutoStreamingEnabled = true;
                    }
                }
            }
            catch { }
        }

        // Returns true if current speed is ~1 Gbps (streaming sweet spot)
        private bool IsCurrentSpeed1G()
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
            if (ni != null && ni.OperationalStatus == OperationalStatus.Up)
            {
                long mbps = ni.Speed / 1_000_000;
                return mbps >= 900 && mbps <= 1100;
            }
            return false;
        }

        private bool UpdateIconBasedOnSpeed(bool showNotification = false)
        {
            try
            {
                long speedBps = 0;

                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

                if (ni != null && ni.OperationalStatus == OperationalStatus.Up)
                    speedBps = ni.Speed;

                string currentSpeedText;

                if (speedBps <= 0)
                {
                    tb.Icon = new System.Drawing.Icon(iconKoPath);
                    currentSpeedText = "Disconnected / Negotiating";
                    tb.ToolTipText = $"Network: {adapterName}\nStatus: {currentSpeedText}";
                    return false;
                }

                long mbps = speedBps / 1_000_000;
                bool is1G = mbps >= 900 && mbps <= 1100;

                // streammodeok = 1 Gbps (streaming sweet spot), streammodeko = everything else
                tb.Icon = new System.Drawing.Icon(is1G ? iconOkPath : iconKoPath);

                currentSpeedText = speedBps >= 1_000_000_000
                    ? $"{(speedBps / 1_000_000_000.0):0.##} Gbps"
                    : $"{mbps:0.##} Mbps";

                tb.ToolTipText = $"Network: {adapterName}\nCurrent Link Speed: {currentSpeedText}";

                if (showNotification)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.ShowBalloonTip("Network Speed Applied",
                                          $"{adapterName} is now connected at {currentSpeedText}.",
                                          BalloonIcon.Info);
                    });
                }

                return true;
            }
            catch
            {
                tb.Icon = new System.Drawing.Icon(iconKoPath);
                tb.ToolTipText = "StreamTweak\n(Status Unknown)";
                return false;
            }
        }

        private void UpdateStreamingMenuItem()
        {
            var menuItem = tb?.ContextMenu?.Items
                .OfType<MenuItem>()
                .FirstOrDefault(m => m.Header?.ToString()?.Contains("Streaming") == true);

            if (menuItem == null) return;

            if (isStreamingModeActive)
            {
                menuItem.Header = "Stop Streaming Mode";
                menuItem.IsEnabled = true;
            }
            else
            {
                menuItem.Header = "Start Streaming Mode";
                menuItem.IsEnabled = !IsCurrentSpeed1G();
            }
        }

        // Applies a speed change via SpeedChanger (UAC-free if service is running, UAC fallback otherwise)
        private void ApplySpeedFromTray(string speedKey)
        {
            var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
            if (speeds == null || !speeds.ContainsKey(speedKey)) return;

            string registryValue = speeds[speedKey];

            if (SpeedChanger.IsTaskInstalled())
                SpeedChanger.Apply(adapterName, registryValue);
            else
                SpeedChanger.ApplyWithUac(adapterName, registryValue);
        }

        private string? Find1GbpsKey()
        {
            var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
            if (speeds == null) return null;

            foreach (var kvp in speeds)
            {
                string keyLower = kvp.Key.ToLower();
                bool nameMatch = (keyLower.Contains("1 gbps") || keyLower.Contains("1gbps") ||
                                  keyLower.Contains("1000")) && keyLower.Contains("full");
                bool valueMatch = kvp.Value == "6";
                if (nameMatch || valueMatch) return kvp.Key;
            }
            return null;
        }

        private void SaveStreamingStateToConfig(bool streamingMode, string originalSpeedKey)
        {
            try
            {
                string configPath = GetConfigPath();
                string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                configData["StreamingMode"] = streamingMode;
                configData["OriginalSpeed"] = originalSpeedKey;
                File.WriteAllText(configPath, JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private async void MenuStreamingMode_Click(object sender, RoutedEventArgs e)
        {
            if (!isStreamingModeActive)
            {
                var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
                if (speeds != null)
                {
                    var ni = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
                    if (ni != null)
                    {
                        long mbps = ni.Speed / 1_000_000;
                        foreach (var kvp in speeds)
                        {
                            string kl = kvp.Key.ToLower();
                            bool match = mbps >= 2000
                                ? kl.Contains("2.5") || kl.Contains("2500")
                                : kl.Contains(mbps.ToString());
                            if (match) { originalSpeedForStreaming = kvp.Key; break; }
                        }
                    }
                }

                string? oneGbpsKey = Find1GbpsKey();
                if (oneGbpsKey == null) return;

                var alert = new StreamingAdjustmentAlert();
                alert.Show();
                await Task.Delay(7900);

                isStreamingModeActive = true;
                SaveStreamingStateToConfig(true, originalSpeedForStreaming);
                UpdateStreamingMenuItem();
                ApplySpeedFromTray(oneGbpsKey);
                await PollForIconUpdate(true);
            }
            else
            {
                if (!string.IsNullOrEmpty(originalSpeedForStreaming))
                    ApplySpeedFromTray(originalSpeedForStreaming);

                isStreamingModeActive = false;
                SaveStreamingStateToConfig(false, string.Empty);
                UpdateStreamingMenuItem();
                await PollForIconUpdate(true);
            }

            settingsWindow?.SyncStreamingState(isStreamingModeActive, originalSpeedForStreaming);
        }

        private async Task PollForIconUpdate(bool showNotification)
        {
            tb.ToolTipText = "Renegotiating link speed... please wait.";
            await Task.Delay(3000);

            int attempts = 0;
            bool isConnected = false;

            while (attempts < 15)
            {
                isConnected = UpdateIconBasedOnSpeed(false);
                if (isConnected) break;
                await Task.Delay(1000);
                attempts++;
            }

            if (isConnected)
                UpdateIconBasedOnSpeed(showNotification);

            UpdateStreamingMenuItem();
            settingsWindow?.RefreshCurrentSpeedDisplay();
        }

        private void OpenSettings()
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();

                settingsWindow.SpeedApplied += async (s, args) =>
                {
                    LoadConfig();
                    await PollForIconUpdate(true);
                };

                settingsWindow.StreamingModeChanged += (s, args) =>
                {
                    LoadConfig();
                    UpdateStreamingMenuItem();
                };

                settingsWindow.AutoStreamingEnabledChanged += (s, args) =>
                {
                    LoadConfig();
                    if (isAutoStreamingEnabled)
                        StartAutoStreamingMonitor();
                    else
                        StopAutoStreamingMonitor();
                };

                settingsWindow.Closed += (s, args) =>
                {
                    LoadConfig();
                    UpdateStreamingMenuItem();
                    settingsWindow = null;
                };

                settingsWindow.Show();
            }
            else
            {
                if (settingsWindow.WindowState == WindowState.Minimized)
                    settingsWindow.WindowState = WindowState.Normal;
                settingsWindow.Activate();
            }
        }

        private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => OpenSettings();
        private void MenuSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            StopAutoStreamingMonitor();
            tb?.Dispose();
            Application.Current.Shutdown();
        }

        #region Auto-Streaming Monitoring

        private void StartAutoStreamingMonitor()
        {
            try
            {
                if (!isAutoStreamingEnabled || logMonitor != null) return;

                logMonitor = new StreamingLogMonitor();
                logMonitor.StreamingEventDetected += LogMonitor_StreamingEventDetected;
                logMonitor.StartMonitoring();
            }
            catch { }
        }

        private void StopAutoStreamingMonitor()
        {
            try
            {
                if (logMonitor != null)
                {
                    logMonitor.StreamingEventDetected -= LogMonitor_StreamingEventDetected;
                    logMonitor.StopMonitoring();
                    logMonitor.Dispose();
                    logMonitor = null;
                }
            }
            catch { }
        }

        private void LogMonitor_StreamingEventDetected(object? sender, StreamingLogMonitor.StreamingEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (e.Event == LogParser.StreamingEvent.StreamStarted && !isAutoStreamingActive)
                    {
                        HandleAutoStreamStart();
                    }
                    else if (e.Event == LogParser.StreamingEvent.StreamStopped && isAutoStreamingActive)
                    {
                        // Ignore disconnect if inactivity timer is running (temporary disconnect due to speed change)
                        if (inactivityTimer == null || !inactivityTimer.IsEnabled)
                            HandleAutoStreamStop();
                        else
                            DebugLog("StreamStopped ignored: inactivity timer active (likely temporary disconnect)");
                    }
                });
            }
            catch { }
        }

        private static void DebugLog(string message)
        {
            try
            {
                string debugLogPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamTweak", "debug.log");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugLogPath) ?? "");
                File.AppendAllText(debugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private async void HandleAutoStreamStart()
        {
            try
            {
                if (isStreamingModeActive) return;

                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

                if (ni == null || ni.OperationalStatus != OperationalStatus.Up) return;

                long mbps = ni.Speed / 1_000_000;

                // Only adjust if speed is higher than 1 Gbps
                if (mbps < 1200) return;

                var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
                if (speeds != null)
                {
                    foreach (var kvp in speeds)
                    {
                        string kl = kvp.Key.ToLower();
                        bool match = mbps >= 2000
                            ? kl.Contains("2.5") || kl.Contains("2500")
                            : kl.Contains(mbps.ToString());
                        if (match) { originalSpeedForAutoStreaming = kvp.Key; break; }
                    }
                }

                string? oneGbpsKey = Find1GbpsKey();
                if (oneGbpsKey == null) return;

                var alert = new StreamingAdjustmentAlert();
                alert.Show();
                await Task.Delay(4000);

                isAutoStreamingActive = true;
                isStreamingModeActive = true;
                ApplySpeedFromTray(oneGbpsKey);
                SaveStreamingStateToConfig(true, originalSpeedForAutoStreaming ?? string.Empty);
                StartInactivityTimer();
                UpdateStreamingMenuItem();
                await PollForIconUpdate(true);

                tb.ShowBalloonTip("Streaming Detected",
                    "Network speed automatically adjusted to 1 Gbps for optimal streaming.",
                    BalloonIcon.Info);

                settingsWindow?.SyncStreamingState(true, originalSpeedForAutoStreaming ?? string.Empty);
            }
            catch { }
        }

        private async void HandleAutoStreamStop()
        {
            try
            {
                if (!isAutoStreamingActive) return;

                if (!string.IsNullOrEmpty(originalSpeedForAutoStreaming))
                    ApplySpeedFromTray(originalSpeedForAutoStreaming);

                isAutoStreamingActive = false;
                isStreamingModeActive = false;
                originalSpeedForAutoStreaming = null;
                StopInactivityTimer();
                UpdateStreamingMenuItem();
                await PollForIconUpdate(true);
                SaveStreamingStateToConfig(false, string.Empty);

                tb.ShowBalloonTip("Streaming Ended",
                    "Network speed automatically restored.",
                    BalloonIcon.Info);

                settingsWindow?.SyncStreamingState(false, string.Empty);
            }
            catch { }
        }

        private void StartInactivityTimer()
        {
            try
            {
                if (inactivityTimer == null)
                {
                    inactivityTimer = new System.Windows.Threading.DispatcherTimer();
                    inactivityTimer.Interval = TimeSpan.FromMilliseconds(INACTIVITY_TIMEOUT_MS);
                    inactivityTimer.Tick += (s, e) =>
                    {
                        StopInactivityTimer();
                        DebugLog("Inactivity timer expired — no reconnection detected");
                    };
                }
                inactivityTimer.Start();
                DebugLog($"Inactivity timer started ({INACTIVITY_TIMEOUT_MS}ms)");
            }
            catch { }
        }

        private void StopInactivityTimer()
        {
            try
            {
                inactivityTimer?.Stop();
                DebugLog("Inactivity timer stopped");
            }
            catch { }
        }

        #endregion
    }
}
