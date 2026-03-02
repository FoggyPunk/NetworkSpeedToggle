using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Management.Infrastructure;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace StreamTweak
{
    public partial class SettingsWindow : Window
    {
        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        #region Native display interop
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string? lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, int dwFlags);

        private readonly string configFilePath;
        public bool HasAppliedChanges { get; private set; } = false;
        private Dictionary<string, string>? currentAdapterSpeeds;
        private bool isDarkMode = false;
        private bool isStreamingMode = false;
        private string originalSpeed = string.Empty;

        public event EventHandler? SpeedApplied;
        public event EventHandler? StreamingModeChanged;

        private static readonly SolidColorBrush StreamingStartBrush = new SolidColorBrush(Color.FromRgb(168, 213, 162));
        private static readonly SolidColorBrush StreamingStopBrush = new SolidColorBrush(Color.FromRgb(244, 168, 168));
        private static readonly SolidColorBrush StreamingDisabledBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));

        public SettingsWindow()
        {
            InitializeComponent();

            this.Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\streamtweak.ico"), UriKind.Absolute));

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "StreamTweak");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            configFilePath = Path.Combine(appFolder, "config.json");

            this.SourceInitialized += SettingsWindow_SourceInitialized;

            ApplySystemAccentColor();
            LoadConfig();
            LoadNetworkAdapters();
            LoadDisplayInfo();
        }

        private void LoadDisplayInfo()
        {
            var (width, height, refreshRate) = DisplayHelper.GetPrimaryDisplayInfo();

            ResolutionTextBlock.Text = width > 0 ? $"{width} × {height}" : "Unknown";
            RefreshRateTextBlock.Text = refreshRate > 0 ? $"{refreshRate} Hz" : "Unknown";

            // Load HDR state without triggering the change event
            HdrToggle.Checked -= HdrToggle_Changed;
            HdrToggle.Unchecked -= HdrToggle_Changed;

            var hdrState = DisplayHelper.GetHdrState();
            switch (hdrState)
            {
                case DisplayHelper.HdrState.Enabled:
                    HdrToggle.IsChecked = true;
                    HdrToggle.IsEnabled = true;
                    HdrStatusTextBlock.Text = "On";
                    break;
                case DisplayHelper.HdrState.Disabled:
                    HdrToggle.IsChecked = false;
                    HdrToggle.IsEnabled = true;
                    HdrStatusTextBlock.Text = "Off";
                    break;
                case DisplayHelper.HdrState.NotSupported:
                    HdrToggle.IsChecked = false;
                    HdrToggle.IsEnabled = false;
                    HdrStatusTextBlock.Text = "Not supported by this display";
                    break;
            }

            HdrToggle.Checked += HdrToggle_Changed;
            HdrToggle.Unchecked += HdrToggle_Changed;

            // Populate extended display and GPU information for the new UI fields
            try
            {
                PopulateExtendedDisplayInfo();
            }
            catch { /* best-effort, ignore errors */ }
        }

        private void PopulateExtendedDisplayInfo()
        {
            // Try to populate the display field with the monitor's reported name (previously shown as "Monitor Name / Model").
            var tb = this.FindName("GpuTextBlock") as System.Windows.Controls.TextBlock;
            try
            {
                string? monitorName = null;
                try
                {
                    DISPLAY_DEVICE d = new DISPLAY_DEVICE();
                    d.cb = Marshal.SizeOf(d);
                    if (EnumDisplayDevices(null, 0, ref d, 0))
                        monitorName = string.IsNullOrWhiteSpace(d.DeviceString) ? null : d.DeviceString.Trim();
                }
                catch { /* ignore monitor name failures */ }
                #endregion

                if (!string.IsNullOrWhiteSpace(monitorName))
                {
                    if (tb != null) tb.Text = monitorName!;
                    return;
                }

                // Fallback: use GPU name/driver if monitor name not available
                using var session = Microsoft.Management.Infrastructure.CimSession.Create(null);
                var instances = session.QueryInstances("root\\cimv2", "WQL", "SELECT Name, DriverVersion FROM Win32_VideoController");
                foreach (var inst in instances)
                {
                    var name = inst.CimInstanceProperties["Name"].Value?.ToString() ?? string.Empty;
                    var drv = inst.CimInstanceProperties["DriverVersion"].Value?.ToString() ?? string.Empty;
                    if (tb != null) tb.Text = string.IsNullOrWhiteSpace(drv) ? name : $"{name} / {drv}";
                    break;
                }
                if (tb != null && string.IsNullOrWhiteSpace(tb.Text)) tb.Text = "Unknown";
            }
            catch { if (tb != null) tb.Text = "Unknown"; }
        }

        private void HdrToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool enable = HdrToggle.IsChecked == true;
            bool success = DisplayHelper.SetHdrState(enable);

            if (success)
            {
                HdrStatusTextBlock.Text = enable ? "On" : "Off";
            }
            else
            {
                // Revert on failure
                HdrToggle.Checked -= HdrToggle_Changed;
                HdrToggle.Unchecked -= HdrToggle_Changed;
                HdrToggle.IsChecked = !enable;
                HdrToggle.Checked += HdrToggle_Changed;
                HdrToggle.Unchecked += HdrToggle_Changed;
                HdrStatusTextBlock.Text = "Failed to change HDR state";
            }
        }

        public void SyncStreamingState(bool streamingActive, string originalSpeedKey)
        {
            isStreamingMode = streamingActive;
            originalSpeed = originalSpeedKey;
            UpdateStreamingButtonAppearance();

            if (isStreamingMode && SpeedComboBox.Items.Contains("1.0 Gbps Full Duplex"))
                SpeedComboBox.SelectedItem = "1.0 Gbps Full Duplex";
        }

        private void UpdateStreamingButtonAppearance()
        {
            if (isStreamingMode)
            {
                StreamingModeButton.Content = "Stop Streaming Mode";
                StreamingModeButton.Background = StreamingStopBrush;
                StreamingModeButton.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                StreamingModeButton.IsEnabled = true;
                StreamingModeButton.Opacity = 1.0;
            }
            else
            {
                bool alreadyAt1G = IsCurrentSpeedAlready1G();
                StreamingModeButton.Content = "Start Streaming Mode";
                StreamingModeButton.IsEnabled = !alreadyAt1G;
                StreamingModeButton.Opacity = alreadyAt1G ? 0.4 : 1.0;
                StreamingModeButton.Background = alreadyAt1G ? StreamingDisabledBrush : StreamingStartBrush;
                StreamingModeButton.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }
        }

        private bool IsCurrentSpeedAlready1G()
        {
            string adapterName = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(adapterName)) return false;

            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(a => a.Name == adapterName && a.OperationalStatus == OperationalStatus.Up);

            if (adapter != null)
            {
                long mbps = adapter.Speed / 1_000_000;
                return mbps >= 900 && mbps <= 1100;
            }
            return false;
        }

        private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
        {
            UpdateTitleBarTheme();
        }

        private void ApplySystemAccentColor()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is int abgr)
                {
                    byte r = (byte)(abgr & 0xFF);
                    byte g = (byte)((abgr >> 8) & 0xFF);
                    byte b = (byte)((abgr >> 16) & 0xFF);

                    var color = Color.FromArgb(255, r, g, b);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();

                    var hoverColor = Color.FromArgb(255,
                        (byte)Math.Max(0, r - 20),
                        (byte)Math.Max(0, g - 20),
                        (byte)Math.Max(0, b - 20));
                    var hoverBrush = new SolidColorBrush(hoverColor);
                    hoverBrush.Freeze();

                    this.Resources["AccentColor"] = brush;
                    this.Resources["AccentHoverColor"] = hoverBrush;
                    Application.Current.Resources["AccentColor"] = brush;
                    Application.Current.Resources["AccentHoverColor"] = hoverBrush;
                }
            }
            catch { }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTheme(!isDarkMode);
            SaveThemeToConfig();
        }

        private void ToggleTheme(bool setDark)
        {
            isDarkMode = setDark;

            if (isDarkMode)
            {
                this.Resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                this.Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                this.Resources["TextForeground"] = new SolidColorBrush(Colors.White);
                this.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                Application.Current.Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Application.Current.Resources["TextForeground"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                ThemeToggleButton.Content = "☀️ Light Mode";
            }
            else
            {
                this.Resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                this.Resources["PanelBackground"] = new SolidColorBrush(Colors.White);
                this.Resources["TextForeground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                this.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(209, 209, 209));
                Application.Current.Resources["PanelBackground"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["TextForeground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                Application.Current.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(209, 209, 209));
                ThemeToggleButton.Content = "🌙 Dark Mode";
            }

            UpdateTitleBarTheme();
        }

        private void UpdateTitleBarTheme()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int[] darkThemeEnabled = new int[] { isDarkMode ? 1 : 0 };
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, darkThemeEnabled, 4);
                }
            }
            catch { }
        }

        private void SaveThemeToConfig()
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                configData["IsDarkMode"] = isDarkMode;
                File.WriteAllText(configFilePath, JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveStreamingStateToConfig(bool streamingMode, string originalSpeedKey)
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                configData["StreamingMode"] = streamingMode;
                configData["OriginalSpeed"] = originalSpeedKey;
                File.WriteAllText(configFilePath, JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("IsDarkMode", out JsonElement themeElement))
                            ToggleTheme(themeElement.GetBoolean());
                        else
                            ToggleTheme(false);

                        if (root.TryGetProperty("NetworkAdapterName", out JsonElement adapterElement))
                        {
                            string savedAdapter = adapterElement.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(savedAdapter) && AdapterComboBox.Items.Contains(savedAdapter))
                                AdapterComboBox.SelectedItem = savedAdapter;
                        }

                        if (root.TryGetProperty("StreamingMode", out JsonElement streamingElement))
                            isStreamingMode = streamingElement.GetBoolean();

                        if (root.TryGetProperty("OriginalSpeed", out JsonElement originalSpeedElement))
                            originalSpeed = originalSpeedElement.GetString() ?? string.Empty;
                    }
                }
                else
                {
                    ToggleTheme(false);
                }
            }
            catch { ToggleTheme(false); }
        }

        private void LoadNetworkAdapters()
        {
            AdapterComboBox.Items.Clear();
            List<string> physicalAdapters = new List<string>();

            try
            {
                using CimSession session = CimSession.Create(null);
                string query = "SELECT * FROM MSFT_NetAdapter WHERE ConnectorPresent = True AND Virtual = False";
                var instances = session.QueryInstances(@"root\StandardCimv2", "WQL", query);

                foreach (var instance in instances)
                {
                    string adapterName = instance.CimInstanceProperties["Name"].Value?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(adapterName))
                        physicalAdapters.Add(adapterName);
                }
            }
            catch
            {
                physicalAdapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(a => a.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .Select(a => a.Name)
                    .ToList();
            }

            var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => physicalAdapters.Contains(a.Name) && a.OperationalStatus == OperationalStatus.Up)
                .Select(a => a.Name)
                .ToList();

            foreach (var adapter in activeAdapters)
                AdapterComboBox.Items.Add(adapter);

            if (AdapterComboBox.Items.Count > 0)
                AdapterComboBox.SelectedIndex = 0;
            else
            {
                AdapterComboBox.Items.Add("No active physical adapters found");
                AdapterComboBox.SelectedIndex = 0;
                AdapterComboBox.IsEnabled = false;
                SpeedComboBox.IsEnabled = false;
                CurrentSpeedTextBlock.Text = "N/A";
            }

            UpdateStreamingButtonAppearance();
        }

        private void AdapterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AdapterComboBox.SelectedItem != null)
            {
                string selectedAdapter = AdapterComboBox.SelectedItem.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(selectedAdapter) && selectedAdapter != "No active physical adapters found")
                {
                    LoadAdapterSpeeds(selectedAdapter);
                    UpdateCurrentSpeedDisplay(selectedAdapter);
                    UpdateStreamingButtonAppearance();
                }
            }
        }

        private void UpdateCurrentSpeedDisplay(string adapterName)
        {
            try
            {
                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(a => a.Name == adapterName && a.OperationalStatus == OperationalStatus.Up);

                if (adapter != null)
                {
                    long speedMbps = adapter.Speed / 1_000_000;
                    CurrentSpeedTextBlock.Text = speedMbps >= 1000
                        ? $"{speedMbps / 1000.0} Gbps"
                        : $"{speedMbps} Mbps";
                }
                else
                {
                    CurrentSpeedTextBlock.Text = "Disconnected";
                }
            }
            catch { CurrentSpeedTextBlock.Text = "Unknown"; }
        }

        private void LoadAdapterSpeeds(string adapterName)
        {
            SpeedComboBox.Items.Clear();
            SpeedComboBox.IsEnabled = true;

            currentAdapterSpeeds = NetworkManager.GetSupportedSpeeds(adapterName);

            if (currentAdapterSpeeds != null && currentAdapterSpeeds.Count > 0)
            {
                foreach (var speedKey in currentAdapterSpeeds.Keys)
                    if (speedKey != null) SpeedComboBox.Items.Add(speedKey);
                SpeedComboBox.SelectedIndex = 0;
            }
            else
            {
                SpeedComboBox.Items.Add("Speed detection not supported");
                SpeedComboBox.SelectedIndex = 0;
                SpeedComboBox.IsEnabled = false;
            }
        }

        private void ApplySpeedInternal(string speedKey)
        {
            if (currentAdapterSpeeds == null || !currentAdapterSpeeds.ContainsKey(speedKey)) return;

            string selectedAdapter = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedAdapter)) return;

            string targetRegistryValue = currentAdapterSpeeds[speedKey];
            string tempScriptPath = Path.Combine(Path.GetTempPath(), "NetSpeedChanger.ps1");
            string psScript = $@"
$adapterName = '{selectedAdapter}'
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
                    process?.WaitForExit();

                HasAppliedChanges = true;
                SpeedApplied?.Invoke(this, EventArgs.Empty);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Administrator privileges are required to change network adapter speeds.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (File.Exists(tempScriptPath))
                    try { File.Delete(tempScriptPath); } catch { }
            }
        }

        private async Task RefreshSpeedAfterChange(string adapterName)
        {
            CurrentSpeedTextBlock.Text = "Negotiating...";

            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(1000);

                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(a => a.Name == adapterName && a.OperationalStatus == OperationalStatus.Up);

                if (adapter != null)
                {
                    long speedMbps = adapter.Speed / 1_000_000;
                    CurrentSpeedTextBlock.Text = speedMbps >= 1000
                        ? $"{speedMbps / 1000.0} Gbps"
                        : $"{speedMbps} Mbps";

                    UpdateStreamingButtonAppearance();
                    return;
                }
            }

            CurrentSpeedTextBlock.Text = "Unknown";
            UpdateStreamingButtonAppearance();
        }

        private async void StreamingModeButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedAdapter = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;

            if (!isStreamingMode)
            {
                if (SpeedComboBox.SelectedItem != null)
                    originalSpeed = SpeedComboBox.SelectedItem.ToString() ?? string.Empty;

                string? oneGbpsKey = null;
                if (currentAdapterSpeeds != null)
                {
                    foreach (var kvp in currentAdapterSpeeds)
                    {
                        string keyLower = kvp.Key.ToLower();
                        bool nameMatch = (keyLower.Contains("1 gbps") || keyLower.Contains("1gbps") ||
                                          keyLower.Contains("1000")) && keyLower.Contains("full");
                        bool valueMatch = kvp.Value == "6";
                        if (nameMatch || valueMatch) { oneGbpsKey = kvp.Key; break; }
                    }
                }

                if (oneGbpsKey == null)
                {
                    MessageBox.Show("1 Gbps Full Duplex not found for this adapter.", "Streaming Mode",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SpeedComboBox.SelectedItem = oneGbpsKey;
                isStreamingMode = true;
                UpdateStreamingButtonAppearance();
                SaveStreamingStateToConfig(true, originalSpeed);
                StreamingModeChanged?.Invoke(this, EventArgs.Empty);
                ApplySpeedInternal(oneGbpsKey);

                if (!string.IsNullOrEmpty(selectedAdapter))
                    await RefreshSpeedAfterChange(selectedAdapter);
            }
            else
            {
                if (!string.IsNullOrEmpty(originalSpeed) && SpeedComboBox.Items.Contains(originalSpeed))
                {
                    SpeedComboBox.SelectedItem = originalSpeed;
                    ApplySpeedInternal(originalSpeed);
                }

                isStreamingMode = false;
                UpdateStreamingButtonAppearance();
                SaveStreamingStateToConfig(false, string.Empty);
                StreamingModeChanged?.Invoke(this, EventArgs.Empty);

                if (!string.IsNullOrEmpty(selectedAdapter))
                    await RefreshSpeedAfterChange(selectedAdapter);
            }
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdapterComboBox.SelectedItem == null || SpeedComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select both a network adapter and a target speed.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedAdapter = AdapterComboBox.SelectedItem.ToString() ?? string.Empty;
            string selectedSpeedKey = SpeedComboBox.SelectedItem.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(selectedAdapter) || string.IsNullOrEmpty(selectedSpeedKey) ||
                selectedAdapter == "No active physical adapters found") return;

            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                configData["NetworkAdapterName"] = selectedAdapter;
                configData["IsDarkMode"] = isDarkMode;
                File.WriteAllText(configFilePath, JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            ApplySpeedInternal(selectedSpeedKey);
            await RefreshSpeedAfterChange(selectedAdapter);
        }

        // removed: connection/device heuristics and native display P/Invoke (no longer needed)
    }
}