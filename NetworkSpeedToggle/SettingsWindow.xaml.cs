using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Management.Infrastructure;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace NetworkSpeedToggle
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

        public SettingsWindow()
        {
            InitializeComponent();
            this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\25g.ico")));
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "NetworkSpeedToggle");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            configFilePath = Path.Combine(appFolder, "config.json");

            this.SourceInitialized += SettingsWindow_SourceInitialized;

            ApplySystemAccentColor();
            LoadConfig();
            LoadNetworkAdapters();
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
                    // Formato registro: AABBGGRR → convertiamo in AARRGGBB
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

                    // Window.Resources: per elementi nell'albero visuale della finestra
                    this.Resources["AccentColor"] = brush;
                    this.Resources["AccentHoverColor"] = hoverBrush;

                    // Application.Resources: il Popup ha un albero visuale separato
                    // e risale fino qui per trovare le DynamicResource
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

                string newJson = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, newJson);
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
                        {
                            bool savedDarkMode = themeElement.GetBoolean();
                            ToggleTheme(savedDarkMode);
                        }
                        else
                        {
                            ToggleTheme(false);
                        }

                        if (root.TryGetProperty("NetworkAdapterName", out JsonElement adapterElement))
                        {
                            string savedAdapter = adapterElement.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(savedAdapter) && AdapterComboBox.Items.Contains(savedAdapter))
                            {
                                AdapterComboBox.SelectedItem = savedAdapter;
                            }
                        }
                    }
                }
                else
                {
                    ToggleTheme(false);
                }
            }
            catch
            {
                ToggleTheme(false);
            }
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
                    {
                        physicalAdapters.Add(adapterName);
                    }
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
            {
                AdapterComboBox.Items.Add(adapter);
            }

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
                    long speedMbps = adapter.Speed / 1000000;

                    if (speedMbps >= 1000)
                    {
                        double speedGbps = speedMbps / 1000.0;
                        CurrentSpeedTextBlock.Text = $"{speedGbps} Gbps";
                    }
                    else
                    {
                        CurrentSpeedTextBlock.Text = $"{speedMbps} Mbps";
                    }
                }
                else
                {
                    CurrentSpeedTextBlock.Text = "Disconnected";
                }
            }
            catch
            {
                CurrentSpeedTextBlock.Text = "Unknown";
            }
        }

        private void LoadAdapterSpeeds(string adapterName)
        {
            SpeedComboBox.Items.Clear();
            SpeedComboBox.IsEnabled = true;

            currentAdapterSpeeds = NetworkManager.GetSupportedSpeeds(adapterName);

            if (currentAdapterSpeeds != null && currentAdapterSpeeds.Count > 0)
            {
                foreach (var speedKey in currentAdapterSpeeds.Keys)
                {
                    if (speedKey != null)
                    {
                        SpeedComboBox.Items.Add(speedKey);
                    }
                }
                SpeedComboBox.SelectedIndex = 0;
            }
            else
            {
                SpeedComboBox.Items.Add("Speed detection not supported");
                SpeedComboBox.SelectedIndex = 0;
                SpeedComboBox.IsEnabled = false;
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdapterComboBox.SelectedItem == null || SpeedComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select both a network adapter and a target speed.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedAdapter = AdapterComboBox.SelectedItem.ToString() ?? string.Empty;
            string selectedSpeedKey = SpeedComboBox.SelectedItem.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(selectedAdapter) || string.IsNullOrEmpty(selectedSpeedKey) || selectedAdapter == "No active physical adapters found")
            {
                return;
            }

            try
            {
                string json = File.Exists(configFilePath) ? File.ReadAllText(configFilePath) : "{}";
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

                configData["NetworkAdapterName"] = selectedAdapter;
                configData["IsDarkMode"] = isDarkMode;

                string newJson = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, newJson);
            }
            catch { }

            if (currentAdapterSpeeds != null && currentAdapterSpeeds.ContainsKey(selectedSpeedKey))
            {
                string targetRegistryValue = currentAdapterSpeeds[selectedSpeedKey];

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
                    {
                        process?.WaitForExit();
                    }

                    this.HasAppliedChanges = true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    MessageBox.Show("Administrator privileges are required to change network adapter speeds.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                finally
                {
                    if (File.Exists(tempScriptPath))
                    {
                        try { File.Delete(tempScriptPath); } catch { }
                    }
                }
            }

            this.Close();
        }
    }
}
