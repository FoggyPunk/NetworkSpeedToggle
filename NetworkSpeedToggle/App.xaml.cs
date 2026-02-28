using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Management;
using System.Threading.Tasks;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace NetworkSpeedToggle
{
    public partial class App : Application
    {
        private TaskbarIcon tb = default!;
        private string adapterName = "Ethernet"; // Default adapter name
        private readonly string icon1GPath = @"Resources\1g.ico";
        private readonly string icon25GPath = @"Resources\25g.ico";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // CRITICAL FIX: Prevent the application from shutting down when the Context Menu closes!
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load settings from config.json
            LoadConfig();

            // Initialize the hidden system tray icon
            tb = (TaskbarIcon)FindResource("MyNotifyIcon")!;

            // Set the initial icon based on the actual network adapter speed
            UpdateIconBasedOnSpeed();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists("config.json"))
                {
                    string json = File.ReadAllText("config.json");
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
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read config.json. Using default 'Ethernet'.\nError: {ex.Message}",
                                "Configuration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateIconBasedOnSpeed()
        {
            try
            {
                // Use WMI to fetch the current duplex speed of the specified Ethernet adapter
                string query = $"SELECT * FROM MSft_NetAdapterAdvancedPropertySettingData WHERE Name='{adapterName}' AND RegistryKeyword='*SpeedDuplex'";
                using (var searcher = new ManagementObjectSearcher(@"root\StandardCimv2", query))
                {
                    string currentValue = "1.0 Gbps"; // Assume 1 Gbps if reading fails

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        currentValue = obj["DisplayValue"]?.ToString() ?? "1.0 Gbps";
                    }

                    // Update the tray icon and tooltip based on the returned value
                    if (currentValue.Contains("2.5"))
                    {
                        tb.Icon = new System.Drawing.Icon(icon25GPath);
                        tb.ToolTipText = $"Network: {adapterName}\nCurrent Speed: 2.5 Gbps\n(Double-click to toggle)";
                    }
                    else
                    {
                        tb.Icon = new System.Drawing.Icon(icon1GPath);
                        tb.ToolTipText = $"Network: {adapterName}\nCurrent Speed: 1.0 Gbps\n(Double-click to toggle)";
                    }
                }
            }
            catch
            {
                // Safety fallback in case the WMI query fails (e.g., adapter not found)
                tb.Icon = new System.Drawing.Icon(icon1GPath);
                tb.ToolTipText = "Network Speed Toggle\n(Status Unknown)";
            }
        }

        private async Task ToggleSpeed()
        {
            try
            {
                // Set the icon tooltip to show it is working
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tb.ToolTipText = "Applying new speed... please wait.";
                });

                // Offload the heavy process to a background thread
                await Task.Run(async () =>
                {
                    string psScript = $@"
                        $Adapter = '{adapterName}'
                        $Prop = Get-NetAdapterAdvancedProperty -Name $Adapter -RegistryKeyword '*SpeedDuplex' -ErrorAction SilentlyContinue
                        if (-not $Prop) {{ exit }}
                        $PropName = $Prop.DisplayName
                        $Val1G = $Prop.ValidDisplayValues | Where-Object {{ $_ -match '1\.0 Gbps' -or $_ -match '1 Gbps' }} | Select-Object -First 1
                        $Val2_5G = $Prop.ValidDisplayValues | Where-Object {{ $_ -match '2\.5 Gbps' }} | Select-Object -First 1
                        $Current = $Prop.DisplayValue

                        if ($Current -match '1\.0' -or $Current -match '1 ') {{
                            Set-NetAdapterAdvancedProperty -Name $Adapter -DisplayName $PropName -DisplayValue $Val2_5G
                        }} else {{
                            Set-NetAdapterAdvancedProperty -Name $Adapter -DisplayName $PropName -DisplayValue $Val1G
                        }}";

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true // Prevents the console window from flashing
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                });

                // Refresh the tray icon on the UI thread once the background task is done
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateIconBasedOnSpeed();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error during speed switch:\n{ex.Message}", "Action Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async void TrayIcon_DoubleClick(object sender, RoutedEventArgs e)
        {
            await ToggleSpeed();
        }

        private async void MenuToggle_Click(object sender, RoutedEventArgs e)
        {
            await ToggleSpeed();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            // Clean up the icon from the system tray before shutting down the application
            if (tb != null)
            {
                tb.Dispose();
            }
            // Explicitly shut down the application now
            Application.Current.Shutdown();
        }
    }
}