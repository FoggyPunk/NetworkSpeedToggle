using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]

namespace StreamTweak
{
    public partial class App : Application
    {
        private TaskbarIcon tb = default!;
        private string adapterName = "Ethernet";
        private readonly string iconOkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streammodeok.ico");
        private readonly string iconKoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streammodeko.ico");

        private SettingsWindow? settingsWindow = null;
        private bool isStreamingModeActive = false;
        private string originalSpeedForStreaming = string.Empty;

        // Auto-monitoring
        private StreamingLogMonitor? logMonitor = null;
        private bool isAutoStreamingActive = false;
        private string? originalSpeedForAutoStreaming = null;
        private bool isAutoStreamingEnabled = true;

        // Dolby audio monitoring
        private readonly DolbyAudioMonitor _dolbyMonitor = new();
        private bool isAudioMonitorEnabled = false;

        // HDR tray state
        private bool _trayHdrEnabled = false;
        private bool _trayAutoHdrEnabled = false;
        private MonitorInfo? _primaryMonitor = null;

        // Moonlight fork TCP bridge
        private readonly StreamTweakBridge _bridge = new();

        // Inactivity timer — prevents restoring speed on temporary reconnect disconnects
        private System.Windows.Threading.DispatcherTimer? inactivityTimer = null;
        private const int INACTIVITY_TIMEOUT_MS = 30000;

        private string GetConfigPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StreamTweak", "config.json");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoadConfig();
            SessionLogger.Initialize();

            tb = (TaskbarIcon)FindResource("MyNotifyIcon")!;
            UpdateIconBasedOnSpeed(false);
            UpdateTrayMenu();

            // Initialize WinRT toast notifications
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streamtweak.ico");
            ToastHelper.Initialize("StreamTweak", iconPath);

            StartAutoStreamingMonitor();
            _dolbyMonitor.StatusChanged += OnDolbyStatusChanged;
            StartDolbyMonitor();
            _ = InitHdrStateAsync();

            // Start Moonlight fork TCP bridge
            _bridge.PrepareRequested += OnBridgePrepareRequested;
            _bridge.RestoreRequested += OnBridgeRestoreRequested;
            _bridge.Start();
            _bridge.StatusProvider = () => {
                var (mbps, connected) = GetCurrentSpeed();
                return connected ? mbps.ToString() : "UNKNOWN";
            };
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath)) return;

                string json = File.ReadAllText(configPath);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("NetworkAdapterName", out var adapterEl))
                    adapterName = adapterEl.GetString() ?? "Ethernet";

                if (root.TryGetProperty("StreamingMode", out var streamingEl))
                    isStreamingModeActive = streamingEl.GetBoolean();

                if (root.TryGetProperty("OriginalSpeed", out var originalSpeedEl))
                    originalSpeedForStreaming = originalSpeedEl.GetString() ?? string.Empty;

                if (root.TryGetProperty("AutoStreamingEnabled", out var autoEl))
                    isAutoStreamingEnabled = autoEl.GetBoolean();
                else
                    isAutoStreamingEnabled = true;

                if (root.TryGetProperty("AudioMonitorEnabled", out var audioEl))
                    isAudioMonitorEnabled = audioEl.GetBoolean();
            }
            catch { }
        }

        private bool IsCurrentSpeed1G()
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
            if (ni?.OperationalStatus == OperationalStatus.Up)
            {
                long mbps = ni.Speed / 1_000_000;
                return mbps >= 900 && mbps <= 1100;
            }
            return false;
        }

        private (long mbps, bool connected) GetCurrentSpeed()
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
            if (ni?.OperationalStatus == OperationalStatus.Up)
                return (ni.Speed / 1_000_000, true);
            return (0, false);
        }

        private bool UpdateIconBasedOnSpeed(bool showToast = false)
        {
            try
            {
                var (mbps, connected) = GetCurrentSpeed();

                if (!connected)
                {
                    tb.Icon = new System.Drawing.Icon(iconKoPath);
                    tb.ToolTipText = $"StreamTweak\n{adapterName}: Disconnected / Negotiating";
                    return false;
                }

                bool is1G = mbps >= 900 && mbps <= 1100;
                tb.Icon = new System.Drawing.Icon(is1G ? iconOkPath : iconKoPath);

                string speedText = mbps >= 1000
                    ? $"{mbps / 1000.0:0.##} Gbps"
                    : $"{mbps} Mbps";

                tb.ToolTipText = $"StreamTweak\n{adapterName}: {speedText}";

                if (showToast)
                    ToastHelper.Show("Speed Applied", $"{adapterName} is now at {speedText}.");

                return true;
            }
            catch
            {
                tb.Icon = new System.Drawing.Icon(iconKoPath);
                tb.ToolTipText = "StreamTweak\n(Status Unknown)";
                return false;
            }
        }

        /// <summary>
        /// Updates all dynamic tray menu items: speed, streaming status, auto mode checkbox.
        /// </summary>
        private void UpdateTrayMenu()
        {
            if (tb?.ContextMenu == null) return;

            var (mbps, connected) = GetCurrentSpeed();
            string speedText = !connected
                ? "Speed: Disconnected"
                : mbps >= 1000
                    ? $"Speed: {mbps / 1000.0:0.##} Gbps"
                    : $"Speed: {mbps} Mbps";

            if (SpeedStatusMenuItem != null)
                SpeedStatusMenuItem.Header = speedText;

            if (StreamingStatusMenuItem != null)
                StreamingStatusMenuItem.Header = isStreamingModeActive
                    ? "Streaming: Active ●"
                    : "Streaming: Inactive";

            if (StreamingModeMenuItem != null)
            {
                if (isStreamingModeActive)
                {
                    StreamingModeMenuItem.Header = "Stop Streaming Mode";
                    StreamingModeMenuItem.IsEnabled = true;
                }
                else
                {
                    StreamingModeMenuItem.Header = "Start Streaming Mode";
                    StreamingModeMenuItem.IsEnabled = !IsCurrentSpeed1G();
                }
            }

            if (AutoModeMenuItem != null)
            {
                AutoModeMenuItem.IsChecked = isAutoStreamingEnabled;
                AutoModeMenuItem.Header = isAutoStreamingEnabled
                    ? "Auto Mode: Enabled"
                    : "Auto Mode: Disabled";
            }

            if (DolbyModeMenuItem != null)
            {
                DolbyModeMenuItem.IsChecked = isAudioMonitorEnabled;
                DolbyModeMenuItem.Header = isAudioMonitorEnabled
                    ? "Dolby Atmos: Enabled"
                    : "Dolby Atmos: Disabled";
            }

            if (HdrModeMenuItem != null)
            {
                HdrModeMenuItem.IsEnabled = _primaryMonitor?.HdrSupported ?? false;
                HdrModeMenuItem.IsChecked = _trayHdrEnabled;
                HdrModeMenuItem.Header = _trayHdrEnabled ? "HDR: On" : "HDR: Off";
            }

            if (AutoHdrModeMenuItem != null)
            {
                AutoHdrModeMenuItem.IsChecked = _trayAutoHdrEnabled;
                AutoHdrModeMenuItem.Header = _trayAutoHdrEnabled ? "Auto HDR: On" : "Auto HDR: Off";
            }
        }

        private MenuItem? SpeedStatusMenuItem =>
            GetMenuItem("SpeedStatusMenuItem");
        private MenuItem? StreamingStatusMenuItem =>
            GetMenuItem("StreamingStatusMenuItem");
        private MenuItem? StreamingModeMenuItem =>
            GetMenuItem("StreamingModeMenuItem");
        private MenuItem? AutoModeMenuItem =>
            GetMenuItem("AutoModeMenuItem");
        private MenuItem? DolbyModeMenuItem =>
            GetMenuItem("DolbyModeMenuItem");
        private MenuItem? HdrModeMenuItem =>
            GetMenuItem("HdrModeMenuItem");
        private MenuItem? AutoHdrModeMenuItem =>
            GetMenuItem("AutoHdrModeMenuItem");

        private MenuItem? GetMenuItem(string name) =>
            tb?.ContextMenu?.Items.OfType<MenuItem>()
              .FirstOrDefault(m => m.Name == name);

        private void ApplySpeed(string speedKey)
        {
            var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
            if (speeds == null || !speeds.TryGetValue(speedKey, out string? registryValue)) return;

            bool ok = SpeedChanger.Apply(adapterName, registryValue);
            if (!ok) SpeedChanger.ApplyWithUac(adapterName, registryValue);
        }

        private string? Find1GbpsKey()
        {
            var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
            if (speeds == null) return null;

            foreach (var kvp in speeds)
            {
                string kl = kvp.Key.ToLower();
                bool nameMatch = (kl.Contains("1 gbps") || kl.Contains("1gbps") ||
                                  kl.Contains("1000")) && kl.Contains("full");
                if (nameMatch || kvp.Value == "6") return kvp.Key;
            }
            return null;
        }

        private void SaveStreamingStateToConfig(bool streamingMode, string originalSpeedKey)
        {
            try
            {
                string configPath = GetConfigPath();
                string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                var configData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json)
                                 ?? new System.Collections.Generic.Dictionary<string, object>();
                configData["StreamingMode"] = streamingMode;
                configData["OriginalSpeed"] = originalSpeedKey;
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, JsonSerializer.Serialize(configData,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private async void MenuStreamingMode_Click(object sender, RoutedEventArgs e)
        {
            if (!isStreamingModeActive)
            {
                // Capture current speed as original
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
                SessionLogger.StartSession("Manual", originalSpeedForStreaming);
                ApplySpeed(oneGbpsKey);
                await PollForIconUpdate(true);
            }
            else
            {
                if (!string.IsNullOrEmpty(originalSpeedForStreaming))
                    ApplySpeed(originalSpeedForStreaming);

                SessionLogger.EndSession("User");
                isStreamingModeActive = false;
                SaveStreamingStateToConfig(false, string.Empty);
                await PollForIconUpdate(true);
                ToastHelper.Show("Streaming Ended", "Network speed restored to original.");
            }

            UpdateTrayMenu();
            settingsWindow?.SyncStreamingState(isStreamingModeActive, originalSpeedForStreaming);
            settingsWindow?.RefreshSessionHistory();
        }

        private void MenuAutoMode_Click(object sender, RoutedEventArgs e)
        {
            isAutoStreamingEnabled = !isAutoStreamingEnabled;
            SaveAutoStreamingToConfig(isAutoStreamingEnabled);
            UpdateTrayMenu();

            if (isAutoStreamingEnabled)
                StartAutoStreamingMonitor();
            else
                StopAutoStreamingMonitor();

            settingsWindow?.SyncAutoStreamingState(isAutoStreamingEnabled);
        }

        private void MenuDolbyMode_Click(object sender, RoutedEventArgs e)
        {
            isAudioMonitorEnabled = !isAudioMonitorEnabled;
            SaveAudioMonitorToConfig(isAudioMonitorEnabled);
            UpdateTrayMenu();

            if (isAudioMonitorEnabled)
            {
                StartDolbyMonitor();
                StartAutoStreamingMonitor();
            }
            else
            {
                _dolbyMonitor.Disable();
                StopAutoStreamingMonitor();
            }

            settingsWindow?.SyncAudioMonitorState(isAudioMonitorEnabled);
            settingsWindow?.SyncDolbyMonitorStatus(
                _dolbyMonitor.IsEnabled ? "Ready — waiting for next stream…" : "Disabled.");
        }

        private async Task InitHdrStateAsync()
        {
            try
            {
                var monitors = await HdrService.GetMonitorsAsync();
                _primaryMonitor = monitors.FirstOrDefault(m => !m.IsVirtual && m.HdrSupported)
                                  ?? monitors.FirstOrDefault(m => !m.IsVirtual);
                _trayHdrEnabled = _primaryMonitor?.HdrEnabled ?? false;
                _trayAutoHdrEnabled = await HdrService.GetAutoHdrAsync();
                UpdateTrayMenu();
            }
            catch { }
        }

        private async void MenuHdrMode_Click(object sender, RoutedEventArgs e)
        {
            if (_primaryMonitor == null) return;
            bool enable = !_trayHdrEnabled;
            try
            {
                await HdrService.SetHdrAsync(_primaryMonitor.AdapterId, _primaryMonitor.TargetId, enable);
                _primaryMonitor.HdrEnabled = enable;
                _trayHdrEnabled = enable;
                UpdateTrayMenu();
                settingsWindow?.RefreshDisplayPanelIfVisible();
            }
            catch { }
        }

        private async void MenuAutoHdrMode_Click(object sender, RoutedEventArgs e)
        {
            bool enable = !_trayAutoHdrEnabled;
            try
            {
                await HdrService.SetAutoHdrAsync(enable);
                _trayAutoHdrEnabled = enable;
                UpdateTrayMenu();
                settingsWindow?.RefreshDisplayPanelIfVisible();
            }
            catch { }
        }

        private void SaveAudioMonitorToConfig(bool enabled)
        {
            try
            {
                string configPath = GetConfigPath();
                string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                var configData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json)
                                 ?? new System.Collections.Generic.Dictionary<string, object>();
                configData["AudioMonitorEnabled"] = enabled;
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, JsonSerializer.Serialize(configData,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveAutoStreamingToConfig(bool enabled)
        {
            try
            {
                string configPath = GetConfigPath();
                string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                var configData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json)
                                 ?? new System.Collections.Generic.Dictionary<string, object>();
                configData["AutoStreamingEnabled"] = enabled;
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, JsonSerializer.Serialize(configData,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private async Task PollForIconUpdate(bool showToast)
        {
            tb.ToolTipText = $"StreamTweak\n{adapterName}: Renegotiating...";
            await Task.Delay(3000);

            int attempts = 0;
            bool connected = false;

            while (attempts < 15)
            {
                connected = UpdateIconBasedOnSpeed(false);
                if (connected) break;
                await Task.Delay(1000);
                attempts++;
            }

            if (connected) UpdateIconBasedOnSpeed(showToast);
            UpdateTrayMenu();
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
                    UpdateTrayMenu();
                };

                settingsWindow.AutoStreamingEnabledChanged += (s, args) =>
                {
                    LoadConfig();
                    UpdateTrayMenu();
                    if (isAutoStreamingEnabled)
                        StartAutoStreamingMonitor();
                    else
                        StopAutoStreamingMonitor();
                };

                settingsWindow.AudioMonitorEnabledChanged += (s, args) =>
                {
                    LoadConfig();
                    if (isAudioMonitorEnabled)
                    {
                        StartDolbyMonitor();
                        StartAutoStreamingMonitor();
                    }
                    else
                    {
                        _dolbyMonitor.Disable();
                        StopAutoStreamingMonitor();
                    }
                    UpdateTrayMenu();
                };

                settingsWindow.Closed += (s, args) =>
                {
                    LoadConfig();
                    UpdateTrayMenu();
                    settingsWindow = null;
                };

                settingsWindow.SyncAudioMonitorState(isAudioMonitorEnabled);
                settingsWindow.SyncDolbyMonitorStatus(
                    _dolbyMonitor.IsEnabled ? "Monitoring for Steam Streaming Speakers…" : "Disabled.");

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
            _bridge.PrepareRequested -= OnBridgePrepareRequested;
            _bridge.RestoreRequested -= OnBridgeRestoreRequested;
            _bridge.Dispose();
            StopLogMonitorForced();
            _dolbyMonitor.Disable();
            tb?.Dispose();
            Application.Current.Shutdown();
        }

        #region Auto-Streaming Monitoring

        private void StartAutoStreamingMonitor()
        {
            if (logMonitor != null) return;
            if (!isAutoStreamingEnabled && !isAudioMonitorEnabled) return;

            try
            {
                logMonitor = new StreamingLogMonitor();
                logMonitor.StreamingEventDetected += LogMonitor_StreamingEventDetected;
                logMonitor.StartMonitoring();
            }
            catch { }
        }

        private void StopAutoStreamingMonitor()
        {
            if (isAutoStreamingEnabled || isAudioMonitorEnabled) return;
            StopLogMonitorForced();
        }

        private void StopLogMonitorForced()
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
                    if (e.Event == LogParser.StreamingEvent.StreamStarted)
                    {
                        _dolbyMonitor.OnStreamingStarted();
                        if (!isAutoStreamingActive)
                            HandleAutoStreamStart();
                        else
                            StopInactivityTimer(); // reconnected within grace period
                    }
                    else if (e.Event == LogParser.StreamingEvent.StreamStopped)
                    {
                        _dolbyMonitor.OnStreamingStopped();
                        if (isAutoStreamingActive)
                            StartInactivityTimer(); // start grace period — wait for possible reconnect
                    }
                });
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
                if (mbps < 1200) return; // Already at or below 1 Gbps — nothing to do

                // Capture original speed
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
                await Task.Delay(7900);

                isAutoStreamingActive = true;
                isStreamingModeActive = true;
                ApplySpeed(oneGbpsKey);
                SaveStreamingStateToConfig(true, originalSpeedForAutoStreaming ?? string.Empty);
                SessionLogger.StartSession("Auto", originalSpeedForAutoStreaming ?? string.Empty);

                UpdateTrayMenu();
                await PollForIconUpdate(false);

                ToastHelper.Show("Streaming Detected",
                    "Network speed set to 1 Gbps. Reconnect within 30 seconds.");

                settingsWindow?.SyncStreamingState(true, originalSpeedForAutoStreaming ?? string.Empty);
                settingsWindow?.RefreshSessionHistory();
            }
            catch { }
        }

        private async void HandleAutoStreamStop(string endReason = "User")
        {
            try
            {
                if (!isAutoStreamingActive) return;

                if (!string.IsNullOrEmpty(originalSpeedForAutoStreaming))
                    ApplySpeed(originalSpeedForAutoStreaming);

                SessionLogger.EndSession(endReason);
                isAutoStreamingActive = false;
                isStreamingModeActive = false;
                originalSpeedForAutoStreaming = null;

                StopInactivityTimer();
                UpdateTrayMenu();
                await PollForIconUpdate(false);
                SaveStreamingStateToConfig(false, string.Empty);

                string toastBody = endReason == "Disconnected"
                    ? "Connection lost. Network speed restored."
                    : "Network speed restored to original.";
                ToastHelper.Show("Streaming Ended", toastBody);

                settingsWindow?.SyncStreamingState(false, string.Empty);
                settingsWindow?.RefreshSessionHistory();
            }
            catch { }
        }

        private void StartInactivityTimer()
        {
            if (inactivityTimer == null)
            {
                inactivityTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(INACTIVITY_TIMEOUT_MS)
                };
                inactivityTimer.Tick += (s, e) =>
                {
                    StopInactivityTimer();
                    DebugLog("Inactivity timer expired — no reconnect detected, ending session as disconnected");
                    if (isAutoStreamingActive)
                        HandleAutoStreamStop("Disconnected");
                };
            }
            inactivityTimer.Stop(); // reset countdown if already running
            inactivityTimer.Start();
            DebugLog($"Inactivity timer started ({INACTIVITY_TIMEOUT_MS}ms)");
        }

        private void StopInactivityTimer()
        {
            inactivityTimer?.Stop();
            DebugLog("Inactivity timer stopped");
        }

        #endregion

        #region Moonlight Fork Bridge

        /// <summary>
        /// Called when Moonlight fork sends PREPARE.
        /// The user has not launched the stream yet — we set the NIC to 1 Gbps now
        /// so no disconnect occurs when the session starts.
        /// </summary>
        private void OnBridgePrepareRequested()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    if (isStreamingModeActive) return;

                    var ni = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

                    if (ni == null || ni.OperationalStatus != OperationalStatus.Up) return;

                    long mbps = ni.Speed / 1_000_000;
                    if (mbps < 1200) return; // Already at 1 Gbps — nothing to do

                    // Capture original speed
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

                    // Show alert — user sees it while NIC renegotiates,
                    // before they launch the stream in Moonlight
                    var alert = new StreamingAdjustmentAlert();
                    alert.Show();

                    isAutoStreamingActive = true;
                    isStreamingModeActive = true;
                    ApplySpeed(oneGbpsKey);
                    SaveStreamingStateToConfig(true, originalSpeedForAutoStreaming ?? string.Empty);
                    SessionLogger.StartSession("Bridge", originalSpeedForAutoStreaming ?? string.Empty);

                    UpdateTrayMenu();
                    await PollForIconUpdate(false);

                    ToastHelper.Show("StreamTweak Ready",
                        "Network set to 1 Gbps. You can now launch the stream.");

                    settingsWindow?.SyncStreamingState(true, originalSpeedForAutoStreaming ?? string.Empty);
                    settingsWindow?.RefreshSessionHistory();

                    DebugLog("Bridge: PREPARE handled — NIC set to 1 Gbps before stream launch");
                }
                catch (Exception ex)
                {
                    DebugLog($"Bridge: PREPARE error — {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Called when Moonlight fork sends RESTORE after session ends.
        /// Acts as an explicit fallback alongside the log monitor.
        /// If the log monitor already restored the speed, this is a no-op.
        /// </summary>
        private void OnBridgeRestoreRequested()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DebugLog("Bridge: RESTORE received");
                if (isAutoStreamingActive)
                    HandleAutoStreamStop("User");
            });
        }

        #endregion

        #region Dolby Audio Monitoring

        private void StartDolbyMonitor()
        {
            if (!isAudioMonitorEnabled || _dolbyMonitor.IsEnabled) return;
            _dolbyMonitor.Enable();
        }

        private void OnDolbyStatusChanged(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
                settingsWindow?.SyncDolbyMonitorStatus(status));
        }

        #endregion

        private static void DebugLog(string message)
        {
            try
            {
                string debugLogPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamTweak", "debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(debugLogPath)!);
                File.AppendAllText(debugLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
