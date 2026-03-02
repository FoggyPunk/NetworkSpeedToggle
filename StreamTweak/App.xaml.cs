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
        private readonly string icon1GPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\1g.ico");
        private readonly string icon25GPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\25g.ico");

        private SettingsWindow? settingsWindow = null;
        private bool isStreamingModeActive = false;
        private string originalSpeedForStreaming = string.Empty;

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
            UpdateDisplayInfoMenuItems();
        }

        private void UpdateDisplayInfoMenuItems()
        {
            try
            {
                var (width, height, refreshRate) = DisplayHelper.GetPrimaryDisplayInfo();

                var items = tb?.ContextMenu?.Items.OfType<MenuItem>().ToList();
                if (items == null) return;

                var resItem = items.FirstOrDefault(m => m.Header?.ToString()?.StartsWith("Resolution") == true);
                var refItem = items.FirstOrDefault(m => m.Header?.ToString()?.StartsWith("Refresh Rate") == true);

                if (resItem != null)
                    resItem.Header = width > 0 ? $"Resolution: {width} × {height}" : "Resolution: Unknown";

                if (refItem != null)
                    refItem.Header = refreshRate > 0 ? $"Refresh Rate: {refreshRate} Hz" : "Refresh Rate: Unknown";
            }
            catch { }
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
                    }
                }
            }
            catch { }
        }

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

                string currentSpeedText = "";

                if (speedBps <= 0)
                {
                    tb.Icon = new System.Drawing.Icon(icon1GPath);
                    currentSpeedText = "Disconnected / Negotiating";
                    tb.ToolTipText = $"Network: {adapterName}\nStatus: {currentSpeedText}";
                    return false;
                }
                else if (speedBps >= 2_500_000_000)
                {
                    tb.Icon = new System.Drawing.Icon(icon25GPath);
                    currentSpeedText = $"{(speedBps / 1_000_000_000.0):0.##} Gbps";
                    tb.ToolTipText = $"Network: {adapterName}\nCurrent Link Speed: {currentSpeedText}";
                }
                else
                {
                    tb.Icon = new System.Drawing.Icon(icon1GPath);
                    currentSpeedText = speedBps >= 1_000_000_000
                        ? $"{(speedBps / 1_000_000_000.0):0.##} Gbps"
                        : $"{(speedBps / 1_000_000.0):0.##} Mbps";
                    tb.ToolTipText = $"Network: {adapterName}\nCurrent Link Speed: {currentSpeedText}";
                }

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
                tb.Icon = new System.Drawing.Icon(icon1GPath);
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

        private void ApplySpeedFromTray(string speedKey)
        {
            var speeds = NetworkManager.GetSupportedSpeeds(adapterName);
            if (speeds == null || !speeds.ContainsKey(speedKey)) return;

            string targetRegistryValue = speeds[speedKey];
            string tempScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NetSpeedChanger.ps1");
            string psScript = $@"
$adapterName = '{adapterName}'
$registryValue = '{targetRegistryValue}'
Set-NetAdapterAdvancedProperty -Name $adapterName -RegistryKeyword '*SpeedDuplex' -RegistryValue $registryValue -NoRestart
Restart-NetAdapter -Name $adapterName -Confirm:$false
";
            File.WriteAllText(tempScriptPath, psScript);

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempScriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    process?.WaitForExit();
                }
            }
            catch { }
            finally
            {
                if (File.Exists(tempScriptPath))
                    try { File.Delete(tempScriptPath); } catch { }
            }
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

            if (settingsWindow != null)
                settingsWindow.SyncStreamingState(isStreamingModeActive, originalSpeedForStreaming);
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
            if (tb != null) tb.Dispose();
            Application.Current.Shutdown();
        }
    }
}