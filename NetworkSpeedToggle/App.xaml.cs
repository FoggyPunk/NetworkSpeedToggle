using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace NetworkSpeedToggle
{
    public partial class App : Application
    {
        private TaskbarIcon tb = default!;
        private string adapterName = "Ethernet";
        private readonly string icon1GPath = @"Resources\1g.ico";
        private readonly string icon25GPath = @"Resources\25g.ico";

        private SettingsWindow? settingsWindow = null;

        private string GetConfigPath()
        {
            string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NetworkSpeedToggle");
            return Path.Combine(appFolder, "config.json");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoadConfig();

            tb = (TaskbarIcon)FindResource("MyNotifyIcon")!;

            // False: no popup on PC startup
            UpdateIconBasedOnSpeed(false);
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
                        {
                            adapterName = adapterElement.GetString() ?? "Ethernet";
                        }
                    }
                }
            }
            catch { }
        }

        private bool UpdateIconBasedOnSpeed(bool showNotification = false)
        {
            try
            {
                long speedBps = 0;

                // We use the native .NET NetworkInterface which is always updated in real time
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                            .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

                if (ni != null && ni.OperationalStatus == OperationalStatus.Up)
                {
                    speedBps = ni.Speed; // Returns the true Link Speed in bits per second
                }

                string currentSpeedText = "";

                if (speedBps <= 0)
                {
                    tb.Icon = new System.Drawing.Icon(icon1GPath);
                    currentSpeedText = "Disconnected / Negotiating";
                    tb.ToolTipText = $"Network: {adapterName}\nStatus: {currentSpeedText}";
                    return false;
                }
                else if (speedBps >= 2500000000) // 2.5 Gbps or higher
                {
                    tb.Icon = new System.Drawing.Icon(icon25GPath);
                    currentSpeedText = $"{(speedBps / 1000000000.0):0.##} Gbps";
                    tb.ToolTipText = $"Network: {adapterName}\nCurrent Link Speed: {currentSpeedText}";
                }
                else // 1.0 Gbps or lower
                {
                    tb.Icon = new System.Drawing.Icon(icon1GPath);
                    currentSpeedText = speedBps >= 1000000000
                        ? $"{(speedBps / 1000000000.0):0.##} Gbps"
                        : $"{(speedBps / 1000000.0):0.##} Mbps";
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
                tb.ToolTipText = "Network Speed Monitor\n(Status Unknown)";
                return false;
            }
        }

        // Main method to open Settings
        private void OpenSettings()
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();

                settingsWindow.Closed += async (s, args) =>
                {
                    // Check if the user actually pressed "Apply" instead of the "X" button
                    bool hasAppliedChanges = settingsWindow?.HasAppliedChanges == true;
                    settingsWindow = null;

                    if (hasAppliedChanges)
                    {
                        LoadConfig();

                        Application.Current.Dispatcher.Invoke(() => { tb.ToolTipText = "Renegotiating link speed... please wait."; });

                        // Give the driver time to actually shut down the connection
                        await Task.Delay(3000);

                        int attempts = 0;
                        bool isConnected = false;

                        // Polling: wait up to 15 seconds for the link to come back up
                        while (attempts < 15)
                        {
                            isConnected = UpdateIconBasedOnSpeed(false);
                            if (isConnected) break;

                            await Task.Delay(1000);
                            attempts++;
                        }

                        if (isConnected)
                        {
                            UpdateIconBasedOnSpeed(true);
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                tb.ShowBalloonTip("Renegotiation Timeout",
                                                  $"The adapter '{adapterName}' took too long to reconnect. Please check the cable or adapter status.",
                                                  BalloonIcon.Warning);
                            });
                        }
                    }
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

        // Double Click = Open Settings
        private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        // Right Click -> Settings = Open Settings
        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            if (tb != null) tb.Dispose();
            Application.Current.Shutdown();
        }
    }
}
