using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Management.Infrastructure;
using Microsoft.Win32;

namespace StreamTweak
{
    public partial class SettingsWindow : Window
    {
        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE  = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE       = 38;
        private const int DWMSBT_MICA                     = 2;
        private const int DWMSBT_MICA_ALT                 = 4;

        private readonly string configFilePath;
        private Dictionary<string, string>? currentAdapterSpeeds;
        private bool isStreamingMode = false;
        private string originalSpeed = string.Empty;
        private bool isAutoStreamingEnabled = true;
        private bool _updateAvailable = false;
        private string currentAdapterName = string.Empty;

        public event EventHandler? SpeedApplied;
        public event EventHandler? StreamingModeChanged;
        public event EventHandler? AutoStreamingEnabledChanged;
        public event EventHandler? AudioMonitorEnabledChanged;

        private bool _isAudioMonitorEnabled = false;

        private static readonly SolidColorBrush StreamingStartBrush    = new(Color.FromRgb(168, 213, 162));
        private static readonly SolidColorBrush StreamingStopBrush     = new(Color.FromRgb(244, 168, 168));
        private static readonly SolidColorBrush StreamingDisabledBrush = new(Color.FromRgb(180, 180, 180));
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        private List<MonitorInfo> _currentMonitors = new();
        private bool _hdrToggleBusy = false;
        private bool _autoHdrBusy = false;

        public SettingsWindow()
        {
            InitializeComponent();

            this.Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    @"Resources\streamtweak.ico"), UriKind.Absolute));

            string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StreamTweak");
            Directory.CreateDirectory(appFolder);
            configFilePath = Path.Combine(appFolder, "config.json");

            this.SourceInitialized += (_, _) =>
            {
                UpdateTitleBarTheme();
                ApplyBackdrop();
                var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if (hwndSource != null)
                    hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
                hwndSource?.AddHook(WndProc);
            };

            ApplySystemAccentColor();
            LoadConfig();
            LoadNetworkAdapters();
            RefreshSessionHistory();
            RefreshDolbyAccessStatusAsync();
        }

        // ─── Public sync methods called from App.xaml.cs ───────────────────

        public void SyncStreamingState(bool streamingActive, string originalSpeedKey)
        {
            isStreamingMode = streamingActive;
            originalSpeed = originalSpeedKey;
            UpdateStreamingButtonAppearance();
        }

        public void SyncAutoStreamingState(bool enabled)
        {
            isAutoStreamingEnabled = enabled;
            AutoStreamingToggle.IsChecked = enabled;
        }

        public void RefreshCurrentSpeedDisplay()
        {
            if (!string.IsNullOrEmpty(currentAdapterName))
                UpdateCurrentSpeedDisplay(currentAdapterName);
        }

        public void RefreshSessionHistory()
        {
            var sessions = SessionLogger.Load();
            if (sessions.Count == 0)
            {
                SessionHistoryList.ItemsSource = null;
                NoHistoryLabel.Visibility = Visibility.Visible;
            }
            else
            {
                NoHistoryLabel.Visibility = Visibility.Collapsed;
                SessionHistoryList.ItemsSource = sessions;
            }
        }

        // ─── Theme ──────────────────────────────────────────────────────────

        private void ApplySystemAccentColor()
        {
            var brand = Color.FromRgb(190, 84, 56);   // #BE5438
            var hover = Color.FromRgb(160, 54, 26);   // #A0361A

            var brush = new SolidColorBrush(brand);
            brush.Freeze();
            var hoverBrush = new SolidColorBrush(hover);
            hoverBrush.Freeze();

            this.Resources["AccentColor"]      = brush;
            this.Resources["AccentHoverColor"] = hoverBrush;
            Application.Current.Resources["AccentColor"]      = brush;
            Application.Current.Resources["AccentHoverColor"] = hoverBrush;
        }

        private void NetworkTabButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPanel.Visibility  = Visibility.Visible;
            DisplayPanel.Visibility  = Visibility.Collapsed;
            AudioPanel.Visibility    = Visibility.Collapsed;
            LogsPanel.Visibility     = Visibility.Collapsed;
            AboutPanel.Visibility    = Visibility.Collapsed;
            NetworkTabButton.Style   = (Style)this.Resources["TabButtonActive"];
            DisplayTabButton.Style   = (Style)this.Resources["TabButton"];
            AudioTabButton.Style     = (Style)this.Resources["TabButton"];
            LogsTabButton.Style      = (Style)this.Resources["TabButton"];
            AboutTabButton.Style     = (Style)this.Resources["TabButton"];
        }

        private void AudioTabButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPanel.Visibility  = Visibility.Collapsed;
            DisplayPanel.Visibility  = Visibility.Collapsed;
            AudioPanel.Visibility    = Visibility.Visible;
            LogsPanel.Visibility     = Visibility.Collapsed;
            AboutPanel.Visibility    = Visibility.Collapsed;
            NetworkTabButton.Style   = (Style)this.Resources["TabButton"];
            DisplayTabButton.Style   = (Style)this.Resources["TabButton"];
            AudioTabButton.Style     = (Style)this.Resources["TabButtonActive"];
            LogsTabButton.Style      = (Style)this.Resources["TabButton"];
            AboutTabButton.Style     = (Style)this.Resources["TabButton"];
            RefreshDolbyAccessStatusAsync();
        }

        private void LogsTabButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPanel.Visibility  = Visibility.Collapsed;
            DisplayPanel.Visibility  = Visibility.Collapsed;
            AudioPanel.Visibility    = Visibility.Collapsed;
            LogsPanel.Visibility     = Visibility.Visible;
            AboutPanel.Visibility    = Visibility.Collapsed;
            NetworkTabButton.Style   = (Style)this.Resources["TabButton"];
            DisplayTabButton.Style   = (Style)this.Resources["TabButton"];
            AudioTabButton.Style     = (Style)this.Resources["TabButton"];
            LogsTabButton.Style      = (Style)this.Resources["TabButtonActive"];
            AboutTabButton.Style     = (Style)this.Resources["TabButton"];
            RefreshStreamingAppInfo();
        }

        private void AboutTabButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPanel.Visibility  = Visibility.Collapsed;
            DisplayPanel.Visibility  = Visibility.Collapsed;
            AudioPanel.Visibility    = Visibility.Collapsed;
            LogsPanel.Visibility     = Visibility.Collapsed;
            AboutPanel.Visibility    = Visibility.Visible;
            NetworkTabButton.Style   = (Style)this.Resources["TabButton"];
            DisplayTabButton.Style   = (Style)this.Resources["TabButton"];
            AudioTabButton.Style     = (Style)this.Resources["TabButton"];
            LogsTabButton.Style      = (Style)this.Resources["TabButton"];
            AboutTabButton.Style     = (Style)this.Resources["TabButtonActive"];
            PopulateAboutInfo();
            _ = CheckForUpdatesAsync();
        }

        private void ApplyDarkTheme()
        {
            this.Resources["WindowBackground"]        = new SolidColorBrush(Color.FromArgb(0x1A, 32, 32, 32));
            this.Resources["PanelBackground"]         = new SolidColorBrush(Color.FromArgb(0x33, 45, 45, 45));
            this.Resources["TextForeground"]          = new SolidColorBrush(Colors.White);
            this.Resources["BorderColor"]             = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            this.Resources["SecondaryTextForeground"] = new SolidColorBrush(Color.FromRgb(171, 171, 171));
            this.Resources["WarningForeground"]       = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            Application.Current.Resources["PanelBackground"]  = new SolidColorBrush(Color.FromArgb(0x33, 45, 45, 45));
            Application.Current.Resources["TextForeground"]   = new SolidColorBrush(Colors.White);
            Application.Current.Resources["BorderColor"]      = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            UpdateTitleBarTheme();
        }

        private void UpdateTitleBarTheme()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, new[] { 1 }, 4);
            }
            catch { }
        }

        private void ApplyBackdrop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int build = Environment.OSVersion.Version.Build;
                if (build >= 22000)
                {
                    int type = build >= 22621 ? DWMSBT_MICA_ALT : DWMSBT_MICA;
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, new[] { type }, 4);
                }
            }
            catch { }
        }

        private void RefreshStreamingAppInfo()
        {
            try
            {
                var info = LogParser.FindStreamingAppInfo();
                if (info != null)
                {
                    StreamingAppFoundPanel.Visibility    = Visibility.Visible;
                    StreamingAppNotFoundText.Visibility  = Visibility.Collapsed;
                    StreamingAppNameText.Text            = info.AppName;
                    StreamingAppLogPathText.Text         = info.LogFolderPath ?? "Log folder not found";
                    StreamingAppIconImage.Source         = ExtractExeIcon(info.ExePath);
                }
                else
                {
                    StreamingAppFoundPanel.Visibility    = Visibility.Collapsed;
                    StreamingAppNotFoundText.Visibility  = Visibility.Visible;
                }
            }
            catch
            {
                StreamingAppFoundPanel.Visibility   = Visibility.Collapsed;
                StreamingAppNotFoundText.Visibility = Visibility.Visible;
            }
        }

        private static System.Windows.Media.ImageSource? ExtractExeIcon(string? exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return null;
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch { return null; }
        }

        private void StreamingAppLogPathText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string? path = StreamingAppLogPathText.Text;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            SessionLogger.ClearAll();
            RefreshSessionHistory();
        }

        private void PopulateAboutInfo()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            AboutVersionText.Text = version != null
                ? $"Version {version.Major}.{version.Minor}.{version.Build}"
                : "Version 3.0.0";

            string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            AboutBuildDateText.Text = File.Exists(location)
                ? $"Build: {File.GetLastWriteTime(location):dd MMM yyyy}"
                : string.Empty;

            UpdateStatusText.Text = string.Empty;
            UpdateStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
            UpdateStatusText.Opacity = 0.7;
            UpdateStatusText.FontWeight = FontWeights.Normal;
            UpdateStatusText.Cursor = System.Windows.Input.Cursors.Arrow;
            _updateAvailable = false;
            CheckUpdateButton.IsEnabled = true;
        }

        private async Task CheckForUpdatesAsync()
        {
            UpdateStatusText.Text = "Checking for updates…";
            CheckUpdateButton.IsEnabled = false;

            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("StreamTweak-UpdateCheck");
                string json = await _httpClient.GetStringAsync(
                    "https://api.github.com/repos/FoggyBytes/StreamTweak/releases/latest");

                using var doc = JsonDocument.Parse(json);
                string? tagName = doc.RootElement.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tagName))
                {
                    UpdateStatusText.Text = "Could not check for updates.";
                    return;
                }

                string latestStr = tagName.TrimStart('v');
                var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

                if (current != null && Version.TryParse(latestStr, out var latest))
                {
                    var currentNorm = new Version(current.Major, current.Minor, current.Build);
                    if (latest > currentNorm)
                    {
                        UpdateStatusText.Text = $"⬆ Update available: v{latestStr}";
                        UpdateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                        UpdateStatusText.FontWeight = FontWeights.Bold;
                        UpdateStatusText.Opacity = 1.0;
                        UpdateStatusText.Cursor = System.Windows.Input.Cursors.Hand;
                        _updateAvailable = true;
                    }
                    else
                    {
                        UpdateStatusText.Text = "✓ You have the latest version";
                        UpdateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        UpdateStatusText.Opacity = 1.0;
                    }
                }
                else
                {
                    UpdateStatusText.Text = "Could not check for updates.";
                }
            }
            catch
            {
                UpdateStatusText.Text = "Could not check for updates.";
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private void UpdateStatusText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_updateAvailable) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/FoggyBytes/StreamTweak/releases/latest",
                UseShellExecute = true
            });
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://paypal.me/foggypunk",
                UseShellExecute = true
            });
        }

        private void GplBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.gnu.org/licenses/gpl-3.0",
                UseShellExecute = true
            });
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/FoggyBytes/StreamTweak",
                UseShellExecute = true
            });
        }

        // ─── Config

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configFilePath)) { ApplyDarkTheme(); AutoStreamingToggle.IsChecked = true; return; }

                using var doc = JsonDocument.Parse(File.ReadAllText(configFilePath));
                var root = doc.RootElement;

                ApplyDarkTheme();

                if (root.TryGetProperty("StreamingMode", out var streamingEl))
                    isStreamingMode = streamingEl.GetBoolean();

                if (root.TryGetProperty("OriginalSpeed", out var origEl))
                    originalSpeed = origEl.GetString() ?? string.Empty;

                if (root.TryGetProperty("AutoStreamingEnabled", out var autoEl))
                    isAutoStreamingEnabled = autoEl.GetBoolean();

                if (root.TryGetProperty("AudioMonitorEnabled", out var audioEl))
                    _isAudioMonitorEnabled = audioEl.GetBoolean();

                AutoStreamingToggle.IsChecked = isAutoStreamingEnabled;
            }
            catch { ApplyDarkTheme(); }
        }

        private void SaveConfig(bool saveAdapter)
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();

                if (saveAdapter && AdapterComboBox.SelectedItem is string adapter)
                    data["NetworkAdapterName"] = adapter;

                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveStreamingStateToConfig(bool streamingMode, string originalSpeedKey)
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();
                data["StreamingMode"] = streamingMode;
                data["OriginalSpeed"] = originalSpeedKey;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveAutoStreamingStateToConfig()
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();
                data["AutoStreamingEnabled"] = isAutoStreamingEnabled;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ─── Adapters ───────────────────────────────────────────────────────

        private void LoadNetworkAdapters()
        {
            AdapterComboBox.Items.Clear();
            List<string> physicalAdapters = new();

            try
            {
                using var session = CimSession.Create(null);
                var instances = session.QueryInstances(@"root\StandardCimv2", "WQL",
                    "SELECT * FROM MSFT_NetAdapter WHERE ConnectorPresent = True AND Virtual = False");
                foreach (var inst in instances)
                {
                    string name = inst.CimInstanceProperties["Name"].Value?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(name)) physicalAdapters.Add(name);
                }
            }
            catch
            {
                physicalAdapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(a => a.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .Select(a => a.Name).ToList();
            }

            var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => physicalAdapters.Contains(a.Name) && a.OperationalStatus == OperationalStatus.Up)
                .Select(a => a.Name).ToList();

            foreach (var a in activeAdapters) AdapterComboBox.Items.Add(a);

            // Restore saved adapter selection
            string savedAdapter = string.Empty;
            try
            {
                if (File.Exists(configFilePath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configFilePath));
                    if (doc.RootElement.TryGetProperty("NetworkAdapterName", out var el))
                        savedAdapter = el.GetString() ?? string.Empty;
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(savedAdapter) && AdapterComboBox.Items.Contains(savedAdapter))
                AdapterComboBox.SelectedItem = savedAdapter;
            else if (AdapterComboBox.Items.Count > 0)
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
            string selected = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(selected) && selected != "No active physical adapters found")
            {
                currentAdapterName = selected;
                LoadAdapterSpeeds(selected);
                UpdateCurrentSpeedDisplay(selected);
                UpdateStreamingButtonAppearance();
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
                    long mbps = adapter.Speed / 1_000_000;
                    CurrentSpeedTextBlock.Text = mbps >= 1000 ? $"{mbps / 1000.0:0.##} Gbps" : $"{mbps} Mbps";
                }
                else
                    CurrentSpeedTextBlock.Text = "Disconnected";
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
                foreach (var key in currentAdapterSpeeds.Keys)
                    if (key != null && !IsSpeedAtOrBelow100Mbps(key)) SpeedComboBox.Items.Add(key);

                // Select the item that matches the actual current link speed
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
                long currentMbps = ni?.Speed / 1_000_000 ?? 0;

                int selectedIndex = 0;
                for (int i = 0; i < SpeedComboBox.Items.Count; i++)
                {
                    string item = SpeedComboBox.Items[i]?.ToString()?.ToLower() ?? string.Empty;
                    bool match = currentMbps >= 2000
                        ? item.Contains("2.5") || item.Contains("2500")
                        : currentMbps >= 900 && currentMbps <= 1100
                            ? (item.Contains("1") && item.Contains("gbps") && item.Contains("full"))
                            : item.Contains(currentMbps.ToString());
                    if (match) { selectedIndex = i; break; }
                }
                SpeedComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                SpeedComboBox.Items.Add("Speed detection not supported");
                SpeedComboBox.SelectedIndex = 0;
                SpeedComboBox.IsEnabled = false;
            }
        }

        // ─── Streaming Mode ─────────────────────────────────────────────────

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

        private static bool IsSpeedAtOrBelow100Mbps(string displayName)
        {
            var lower = displayName.ToLower();
            if (lower.Contains("gbps")) return false;
            int idx = lower.IndexOf("mbps");
            if (idx > 0)
            {
                var parts = lower[..idx].Trim().Split(' ');
                if (parts.Length > 0 && int.TryParse(parts[^1], out int mbps))
                    return mbps <= 100;
            }
            return false;
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

        private void ApplySpeedInternal(string speedKey)
        {
            if (currentAdapterSpeeds == null || !currentAdapterSpeeds.TryGetValue(speedKey, out string? registryValue)) return;

            string selectedAdapter = AdapterComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedAdapter)) return;

            bool ok = SpeedChanger.Apply(selectedAdapter, registryValue);
            if (!ok) SpeedChanger.ApplyWithUac(selectedAdapter, registryValue);

            SpeedApplied?.Invoke(this, EventArgs.Empty);
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
                    long mbps = adapter.Speed / 1_000_000;
                    CurrentSpeedTextBlock.Text = mbps >= 1000 ? $"{mbps / 1000.0} Gbps" : $"{mbps} Mbps";
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
                        string kl = kvp.Key.ToLower();
                        if (((kl.Contains("1 gbps") || kl.Contains("1gbps") || kl.Contains("1000"))
                             && kl.Contains("full")) || kvp.Value == "6")
                        { oneGbpsKey = kvp.Key; break; }
                    }
                }

                if (oneGbpsKey == null)
                {
                    MessageBox.Show("1 Gbps Full Duplex not found for this adapter.",
                        "Streaming Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SpeedComboBox.SelectedItem = oneGbpsKey;
                isStreamingMode = true;
                UpdateStreamingButtonAppearance();
                SaveStreamingStateToConfig(true, originalSpeed);
                SessionLogger.StartSession("Manual", originalSpeed);
                StreamingModeChanged?.Invoke(this, EventArgs.Empty);
                ApplySpeedInternal(oneGbpsKey);

                if (!string.IsNullOrEmpty(selectedAdapter))
                    await RefreshSpeedAfterChange(selectedAdapter);

                RefreshSessionHistory();
            }
            else
            {
                if (!string.IsNullOrEmpty(originalSpeed) && SpeedComboBox.Items.Contains(originalSpeed))
                {
                    SpeedComboBox.SelectedItem = originalSpeed;
                    ApplySpeedInternal(originalSpeed);
                }

                SessionLogger.EndSession();
                isStreamingMode = false;
                UpdateStreamingButtonAppearance();
                SaveStreamingStateToConfig(false, string.Empty);
                StreamingModeChanged?.Invoke(this, EventArgs.Empty);

                if (!string.IsNullOrEmpty(selectedAdapter))
                    await RefreshSpeedAfterChange(selectedAdapter);

                RefreshSessionHistory();
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

            SaveConfig(saveAdapter: true);
            ApplySpeedInternal(selectedSpeedKey);
            await RefreshSpeedAfterChange(selectedAdapter);
        }

        private void AutoStreamingToggle_Changed(object sender, RoutedEventArgs e)
        {
            isAutoStreamingEnabled = AutoStreamingToggle.IsChecked ?? true;
            SaveAutoStreamingStateToConfig();
            AutoStreamingEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DolbyMonitorToggle_Changed(object sender, RoutedEventArgs e)
        {
            _isAudioMonitorEnabled = DolbyMonitorToggle.IsChecked ?? false;
            SaveAudioMonitorStateToConfig();
            AudioMonitorEnabledChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveAudioMonitorStateToConfig()
        {
            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();
                data["AudioMonitorEnabled"] = _isAudioMonitorEnabled;
                File.WriteAllText(configFilePath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void SyncAudioMonitorState(bool enabled)
        {
            _isAudioMonitorEnabled = enabled;
            DolbyMonitorToggle.IsChecked = enabled;
        }

        public void SyncDolbyMonitorStatus(string status)
        {
            DolbyStatusText.Text = status;
            if (status.StartsWith("✓"))
            {
                DolbyStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                DolbyStatusText.Opacity = 1.0;
            }
            else
            {
                DolbyStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                DolbyStatusText.Opacity = 0.7;
            }
        }

        private async void RefreshDolbyAccessStatusAsync()
        {
            DolbyAccessIcon.Foreground = System.Windows.Media.Brushes.Gray;
            DolbyAccessText.Text = "Checking Dolby Atmos for Headphones\u2026";

            bool available = await DolbyAudioMonitor.IsDolbyAtmosAvailableAsync();
            if (available)
            {
                DolbyAccessIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                DolbyAccessText.Text = "Dolby Atmos for Headphones: detected";
            }
            else
            {
                DolbyAccessIcon.Foreground = new SolidColorBrush(Color.FromRgb(220, 70, 50));
                DolbyAccessText.Text = "Dolby Atmos for Headphones: not found";
            }
        }
        public void RefreshDisplayPanelIfVisible()
        {
            if (DisplayPanel.Visibility == Visibility.Visible)
                RefreshDisplayPanelAsync();
        }

        private void DisplayTabButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPanel.Visibility = Visibility.Collapsed;
            AudioPanel.Visibility = Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Visible;
            LogsPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;

            NetworkTabButton.Style = (Style)this.Resources["TabButton"];
            AudioTabButton.Style = (Style)this.Resources["TabButton"];
            DisplayTabButton.Style = (Style)this.Resources["TabButtonActive"];
            LogsTabButton.Style = (Style)this.Resources["TabButton"];
            AboutTabButton.Style = (Style)this.Resources["TabButton"];

            RefreshDisplayPanelAsync();
        }

        // ─── Refresh — called on tab open and after every toggle ─────────────────────

        private async void RefreshDisplayPanelAsync()
        {
            DisplayLoadingText.Visibility = Visibility.Visible;
            MonitorStackPanel.Visibility = Visibility.Collapsed;
            AutoHdrSection.Visibility = Visibility.Collapsed;
            DisplayContextLabel.Visibility = Visibility.Collapsed;

            try
            {
                _currentMonitors = await HdrService.GetMonitorsAsync();
                bool autoHdr = await HdrService.GetAutoHdrAsync();

                // Determine context: Apollo/Vibepollo → prefer virtual display
                var appInfo = LogParser.FindStreamingAppInfo();
                bool apolloMode = appInfo != null &&
                    (appInfo.AppName.IndexOf("Apollo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     appInfo.AppName.IndexOf("Vibepollo", StringComparison.OrdinalIgnoreCase) >= 0);

                List<MonitorInfo> toShow;

                if (apolloMode)
                {
                    // Try to find virtual display(s)
                    var virtuals = _currentMonitors.FindAll(m => m.IsVirtual);
                    if (virtuals.Count > 0)
                    {
                        toShow = virtuals;
                        DisplayContextIcon.Source = ExtractExeIcon(appInfo!.ExePath);
                        DisplayContextText.Text = $"Showing virtual display  ·  {appInfo!.AppName}";
                        DisplayContextLabel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Virtual display not (yet) connected — show all
                        toShow = _currentMonitors;
                        DisplayContextIcon.Source = ExtractExeIcon(appInfo!.ExePath);
                        DisplayContextText.Text = $"{appInfo!.AppName} detected — connect Moonlight to see virtual display";
                        DisplayContextLabel.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    toShow = _currentMonitors;
                    if (appInfo != null)
                    {
                        DisplayContextIcon.Source = ExtractExeIcon(appInfo.ExePath);
                        DisplayContextText.Text = $"Physical display  ·  {appInfo.AppName}";
                        DisplayContextLabel.Visibility = Visibility.Visible;
                    }
                }

                BuildMonitorCards(toShow);

                // AutoHDR toggle
                _autoHdrBusy = true;
                AutoHdrToggle.IsChecked = autoHdr;
                _autoHdrBusy = false;

                bool anyHdrOn = _currentMonitors.Exists(m => m.HdrEnabled);
                AutoHdrWarning.Visibility = (autoHdr && !anyHdrOn)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                DisplayLoadingText.Visibility = Visibility.Collapsed;
                MonitorStackPanel.Visibility = Visibility.Visible;
                AutoHdrSection.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                DisplayLoadingText.Text = $"Error reading display info: {ex.Message}";
                DisplayLoadingText.Visibility = Visibility.Visible;
            }
        }

        // ─── Build monitor cards dynamically ─────────────────────────────────────────

        private void BuildMonitorCards(List<MonitorInfo> monitors)
        {
            MonitorStackPanel.Children.Clear();

            if (monitors.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No active displays found.",
                    FontSize = 12,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8),
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                MonitorStackPanel.Children.Add(empty);
                return;
            }

            foreach (var m in monitors)
            {
                // ── outer card border ──
                var card = new Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                };
                card.SetResourceReference(Border.BackgroundProperty, "WindowBackground");
                card.SetResourceReference(Border.BorderBrushProperty, "BorderColor");

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // ── left: name + info ──
                var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
                var nameText = new TextBlock
                {
                    Text = m.FriendlyName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                };
                nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                nameRow.Children.Add(nameText);

                if (m.IsVirtual)
                {
                    var chip = new Border
                    {
                        Margin = new Thickness(8, 0, 0, 0),
                        Padding = new Thickness(5, 1, 5, 1),
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    chip.SetResourceReference(Border.BackgroundProperty, "AccentColor");
                    chip.Child = new TextBlock
                    {
                        Text = "Virtual",
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = System.Windows.Media.Brushes.White,
                    };
                    nameRow.Children.Add(chip);
                }

                leftStack.Children.Add(nameRow);

                string resText = (m.Width > 0 && m.Height > 0)
                    ? $"{m.Width} × {m.Height}   {m.RefreshRateHz} Hz"
                    : "Resolution unknown";

                var infoText = new TextBlock
                {
                    Text = resText,
                    FontSize = 11,
                    Opacity = 0.65,
                    Margin = new Thickness(0, 3, 0, 0),
                };
                infoText.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                leftStack.Children.Add(infoText);

                // HDR supported/not label
                string hdrLabel = m.HdrSupported
                    ? (m.HdrEnabled ? "HDR  ON" : "HDR  OFF")
                    : "HDR not supported";

                var hdrStateText = new TextBlock
                {
                    Text = hdrLabel,
                    FontSize = 11,
                    Opacity = 0.65,
                    Margin = new Thickness(0, 2, 0, 0),
                };
                hdrStateText.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                if (m.HdrEnabled)
                {
                    hdrStateText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    hdrStateText.Opacity = 1.0;
                }
                leftStack.Children.Add(hdrStateText);

                Grid.SetColumn(leftStack, 0);
                grid.Children.Add(leftStack);

                // ── right: HDR toggle ──
                var toggle = new CheckBox
                {
                    Style = (Style)this.Resources["ToggleSwitchStyle"],
                    IsChecked = m.HdrEnabled,
                    IsEnabled = m.HdrSupported,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = m,           // carry MonitorInfo
                };
                toggle.Checked += HdrToggle_Changed;
                toggle.Unchecked += HdrToggle_Changed;

                Grid.SetColumn(toggle, 1);
                grid.Children.Add(toggle);

                card.Child = grid;
                MonitorStackPanel.Children.Add(card);
            }

            // Placeholder cards for undetected monitor slots (always show up to 3 total)
            for (int slot = monitors.Count + 1; slot <= 3; slot++)
            {
                var placeholder = new Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    Opacity = 0.5,
                };
                placeholder.SetResourceReference(Border.BackgroundProperty, "WindowBackground");
                placeholder.SetResourceReference(Border.BorderBrushProperty, "BorderColor");

                var pg = new Grid();
                pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var pLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var pName = new TextBlock
                {
                    Text = $"Display {slot}",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                };
                pName.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");
                pLeft.Children.Add(pName);

                var pStatus = new TextBlock
                {
                    Text = "⚠  Not connected",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                    Margin = new Thickness(0, 3, 0, 0),
                };
                pLeft.Children.Add(pStatus);

                Grid.SetColumn(pLeft, 0);
                pg.Children.Add(pLeft);

                var pToggle = new CheckBox
                {
                    Style = (Style)this.Resources["ToggleSwitchStyle"],
                    IsChecked = false,
                    IsEnabled = false,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(pToggle, 1);
                pg.Children.Add(pToggle);

                placeholder.Child = pg;
                MonitorStackPanel.Children.Add(placeholder);
            }
        }

        // ─── HDR toggle handler ───────────────────────────────────────────────────────

        private async void HdrToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_hdrToggleBusy) return;
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo m) return;

            bool enable = cb.IsChecked ?? false;
            cb.IsEnabled = false;

            try
            {
                await HdrService.SetHdrAsync(m.AdapterId, m.TargetId, enable);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not change HDR state:\n{ex.Message}",
                    "StreamTweak — HDR",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                _hdrToggleBusy = true;
                cb.IsChecked = !enable;
                _hdrToggleBusy = false;
                cb.IsEnabled = true;
                return;
            }

            // Immediately update the in-memory model — do not wait for Windows
            m.HdrEnabled = enable;
            BuildMonitorCards(_currentMonitors);

            // Update the Auto HDR warning
            bool anyHdrOn = _currentMonitors.Exists(x => x.HdrEnabled);
            bool autoHdrOn = AutoHdrToggle.IsChecked ?? false;
            AutoHdrWarning.Visibility = (autoHdrOn && !anyHdrOn)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── AutoHDR toggle handler ───────────────────────────────────────────────────

        private async void AutoHdrToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_autoHdrBusy) return;

            bool enable = AutoHdrToggle.IsChecked ?? false;

            try
            {
                await HdrService.SetAutoHdrAsync(enable);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not change Auto HDR state:\n{ex.Message}",
                    "StreamTweak — Auto HDR",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                _hdrToggleBusy = true;
                AutoHdrToggle.IsChecked = !enable;
                _hdrToggleBusy = false;
                return;
            }

            bool anyHdrOn = _currentMonitors.Exists(m => m.HdrEnabled);
            AutoHdrWarning.Visibility = (enable && !anyHdrOn)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ─── Refresh button ───────────────────────────────────────────────────────────

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_NCACTIVATE    = 0x0086;

            if (msg == WM_DISPLAYCHANGE && DisplayPanel.Visibility == Visibility.Visible)
                RefreshDisplayPanelAsync();

            if (msg == WM_NCACTIVATE)
            {
                handled = true;
                return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        private void DisplayRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDisplayPanelAsync();
        }
    }
}
