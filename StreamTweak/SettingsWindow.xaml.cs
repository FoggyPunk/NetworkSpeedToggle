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

        private readonly string configFilePath;
        public bool HasAppliedChanges { get; private set; } = false;
        private Dictionary<string, string>? currentAdapterSpeeds;
        private bool isDarkMode = false;
        private bool isStreamingMode = false;
        private string originalSpeed = string.Empty;
        private bool isAutoStreamingEnabled = true;
        private string currentAdapterName = string.Empty;

        public event EventHandler? SpeedApplied;
        public event EventHandler? StreamingModeChanged;
        public event EventHandler? AutoStreamingEnabledChanged;

        private static readonly SolidColorBrush StreamingStartBrush    = new SolidColorBrush(Color.FromRgb(168, 213, 162));
        private static readonly SolidColorBrush StreamingStopBrush     = new SolidColorBrush(Color.FromRgb(244, 168, 168));
        private static readonly SolidColorBrush StreamingDisabledBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));

        public SettingsWindow()
        {
            InitializeComponent();

            this.Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    @"Resources\streamtweak.ico"), UriKind.Absolute));

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "StreamTweak");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            configFilePath = Path.Combine(appFolder, "config.json");

            this.SourceInitialized += SettingsWindow_SourceInitialized;

            ApplySystemAccentColor();
            LoadConfig();
            LoadNetworkAdapters();
        }

        public void SyncStreamingState(bool streamingActive, string originalSpeedKey)
        {
            isStreamingMode = streamingActive;
            originalSpeed = originalSpeedKey;
            UpdateStreamingButtonAppearance();

            if (isStreamingMode && SpeedComboBox.Items.Contains("1.0 Gbps Full Duplex"))
                SpeedComboBox.SelectedItem = "1.0 Gbps Full Duplex";
        }

        public void RefreshCurrentSpeedDisplay()
        {
            if (!string.IsNullOrEmpty(currentAdapterName))
                UpdateCurrentSpeedDisplay(currentAdapterName);
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
                this.Resources["PanelBackground"]  = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                this.Resources["TextForeground"]   = new SolidColorBrush(Colors.White);
                this.Resources["BorderColor"]      = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                Application.Current.Resources["PanelBackground"]  = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Application.Current.Resources["TextForeground"]   = new SolidColorBrush(Colors.White);
                Application.Current.Resources["BorderColor"]      = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                ThemeToggleButton.Content = "☀️ Light Mode";
            }
            else
            {
                this.Resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                this.Resources["PanelBackground"]  = new SolidColorBrush(Colors.White);
                this.Resources["TextForeground"]   = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                this.Resources["BorderColor"]      = new SolidColorBrush(Color.FromRgb(209, 209, 209));
                Application.Current.Resources["PanelBackground"]  = new SolidColorBrush(Colors.White);
                Application.Current.Resources["TextForeground"]   = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                Application.Current.Resources["BorderColor"]      = new SolidColorBrush(Color.FromRgb(209, 209, 209));
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
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                 ?? new Dictionary<string, object>();
                configData["IsDarkMode"] = isDarkMode;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveStreamingStateToConfig(bool streamingMode, string originalSpeedKey)
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                 ?? new Dictionary<string, object>();
                configData["StreamingMode"] = streamingMode;
                configData["OriginalSpeed"] = originalSpeedKey;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveAutoStreamingStateToConfig()
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                 ?? new Dictionary<string, object>();
                configData["AutoStreamingEnabled"] = isAutoStreamingEnabled;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
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

                        if (root.TryGetProperty("AutoStreamingEnabled", out JsonElement autoStreamingElement))
                            isAutoStreamingEnabled = autoStreamingElement.GetBoolean();
                        else
                            isAutoStreamingEnabled = true;

                        AutoStreamingToggle.IsChecked = isAutoStreamingEnabled;
                    }
                }
                else
                {
                    ToggleTheme(false);
                    AutoStreamingToggle.IsChecked = true;
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
                    currentAdapterName = selectedAdapter;
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

                // Default selection: match the actual current link speed of the adapter,
                // not a hardcoded preference — prevents originalSpeed from being set wrongly
                int selectedIndex = 0;
                try
                {
                    var ni = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(a => a.Name == adapterName && a.OperationalStatus == OperationalStatus.Up);
                    if (ni != null)
                    {
                        long mbps = ni.Speed / 1_000_000;
                        for (int i = 0; i < SpeedComboBox.Items.Count; i++)
                        {
                            string item = SpeedComboBox.Items[i]?.ToString()?.ToLower() ?? string.Empty;
                            bool match = mbps >= 2000
                                ? item.Contains("2.5") || item.Contains("2500")
                                : item.Contains(mbps.ToString());
                            if (match) { selectedIndex = i; break; }
                        }
                    }
                }
                catch { }

                SpeedComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                SpeedComboBox.Items.Add("Speed detection not supported");
                SpeedComboBox.SelectedIndex = 0;
                SpeedComboBox.IsEnabled = false;
            }
        }

        // Applies a speed change via SpeedChanger (UAC-free if service is running, UAC fallback otherwise)
        private void ApplySpeedInternal(string speedKey)
        {
            if (currentAdapterSpeeds == null || !currentAdapterSpeeds.ContainsKey(speedKey)) return;

            string selectedAdapter = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedAdapter)) return;

            string registryValue = currentAdapterSpeeds[speedKey];
            bool success;

            if (SpeedChanger.IsTaskInstalled())
                success = SpeedChanger.Apply(selectedAdapter, registryValue);
            else
                success = SpeedChanger.ApplyWithUac(selectedAdapter, registryValue);

            if (success)
            {
                HasAppliedChanges = true;
                SpeedApplied?.Invoke(this, EventArgs.Empty);
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
                // Determine original speed from actual adapter speed — not from ComboBox selection.
                // The ComboBox default may not match the real current speed, which would cause
                // Stop Streaming Mode to attempt restoring the wrong speed.
                string? detectedOriginalSpeed = null;
                if (currentAdapterSpeeds != null)
                {
                    var ni = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(a => a.Name == selectedAdapter && a.OperationalStatus == OperationalStatus.Up);
                    if (ni != null)
                    {
                        long mbps = ni.Speed / 1_000_000;
                        foreach (var kvp in currentAdapterSpeeds)
                        {
                            string kl = kvp.Key.ToLower();
                            bool match = mbps >= 2000
                                ? kl.Contains("2.5") || kl.Contains("2500")
                                : kl.Contains(mbps.ToString());
                            if (match) { detectedOriginalSpeed = kvp.Key; break; }
                        }
                    }
                }
                originalSpeed = detectedOriginalSpeed ?? SpeedComboBox.SelectedItem?.ToString() ?? string.Empty;

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
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                 ?? new Dictionary<string, object>();
                configData["NetworkAdapterName"] = selectedAdapter;
                configData["IsDarkMode"] = isDarkMode;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            ApplySpeedInternal(selectedSpeedKey);
            await RefreshSpeedAfterChange(selectedAdapter);
        }

        private void AutoStreamingToggle_Changed(object sender, RoutedEventArgs e)
        {
            isAutoStreamingEnabled = AutoStreamingToggle.IsChecked ?? true;
            SaveAutoStreamingStateToConfig();
            AutoStreamingEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
