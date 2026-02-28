using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NetworkSpeedToggle
{
    public partial class SettingsWindow : Window
    {
        private readonly string configFilePath = "config.json";
        private bool isInitializing = true;

        // Custom variable to notify App.xaml.cs that changes were actually applied
        public bool HasAppliedChanges { get; private set; } = false;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadAdapters();
            SelectCurrentAdapter();

            isInitializing = false;

            if (CmbAdapters.SelectedItem != null)
                LoadStandardSpeeds();
        }

        private void LoadAdapters()
        {
            CmbAdapters.Items.Clear();

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                             ni.OperationalStatus == OperationalStatus.Up &&
                             !ni.Description.ToLower().Contains("virtual") &&
                             !ni.Description.ToLower().Contains("pseudo") &&
                             !ni.Description.ToLower().Contains("bluetooth") &&
                             !ni.Description.ToLower().Contains("filter") &&
                             !ni.Description.ToLower().Contains("debugger"))
                .Select(ni => ni.Name)
                .Distinct()
                .ToList();

            foreach (var adapterName in interfaces)
                CmbAdapters.Items.Add(adapterName);

            if (CmbAdapters.Items.Count == 0)
            {
                var fallbackInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                   .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                                !ni.Description.ToLower().Contains("virtual") &&
                                !ni.Description.ToLower().Contains("bluetooth") &&
                                !ni.Description.ToLower().Contains("filter"))
                   .Select(ni => ni.Name)
                   .Distinct()
                   .ToList();

                foreach (var adapterName in fallbackInterfaces)
                    CmbAdapters.Items.Add(adapterName);
            }

            if (CmbAdapters.Items.Count > 0)
                CmbAdapters.SelectedIndex = 0;
            else
                CmbAdapters.Items.Add("No physical LAN adapters found");
        }

        private void SelectCurrentAdapter()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        if (document.RootElement.TryGetProperty("NetworkAdapterName", out JsonElement adapterElement))
                        {
                            string currentName = adapterElement.GetString() ?? "";
                            if (CmbAdapters.Items.Contains(currentName))
                                CmbAdapters.SelectedItem = currentName;
                        }
                    }
                }
            }
            catch { }
        }

        private void CmbAdapters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || CmbAdapters.SelectedItem == null || CmbAdapters.SelectedItem.ToString()!.StartsWith("No physical")) return;
            LoadStandardSpeeds();
        }

        private void LoadStandardSpeeds()
        {
            CmbSpeeds.Items.Clear();

            CmbSpeeds.Items.Add("Auto Negotiation");
            CmbSpeeds.Items.Add("100 Mbps Full Duplex");
            CmbSpeeds.Items.Add("1.0 Gbps Full Duplex");
            CmbSpeeds.Items.Add("2.5 Gbps Full Duplex");

            CmbSpeeds.SelectedIndex = 0;
            CmbSpeeds.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            isInitializing = true;
            LoadAdapters();
            isInitializing = false;
            if (CmbAdapters.SelectedItem != null && !CmbAdapters.SelectedItem.ToString()!.StartsWith("No physical"))
                LoadStandardSpeeds();
        }

        // Forces the application of the chosen speed via an external temporary .ps1 script run as Admin
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbAdapters.SelectedItem == null || CmbAdapters.SelectedItem.ToString()!.StartsWith("No physical")) return;
            string selectedAdapter = CmbAdapters.SelectedItem.ToString()!;

            string newJson = $"{{\n  \"NetworkAdapterName\": \"{selectedAdapter}\"\n}}";
            try { File.WriteAllText(configFilePath, newJson); } catch { }

            if (CmbSpeeds.IsEnabled && CmbSpeeds.SelectedItem != null)
            {
                string selectedSpeed = CmbSpeeds.SelectedItem.ToString()!;

                BtnSave.Content = "Applying...";
                BtnSave.IsEnabled = false;
                CmbAdapters.IsEnabled = false;
                CmbSpeeds.IsEnabled = false;

                await Task.Run(() =>
                {
                    // Create a robust PowerShell script content
                    string psScript = $@"
                        $Adapter = '{selectedAdapter}'
                        $TargetValue = '{selectedSpeed}'
                        
                        $AllProps = Get-NetAdapterAdvancedProperty -Name $Adapter -ErrorAction SilentlyContinue
                        $SpeedProp = $AllProps | Where-Object {{ 
                            $_.RegistryKeyword -match 'SpeedDuplex' -or 
                            $_.DisplayName -match 'Speed' -or 
                            $_.DisplayName -match 'Velocit'
                        }} | Select-Object -First 1

                        if ($SpeedProp) {{
                            $PropName = $SpeedProp.DisplayName
                            
                            if ($TargetValue -eq '1.0 Gbps Full Duplex') {{
                                $PossibleValues = $SpeedProp.ValidDisplayValues
                                if ($PossibleValues -match '1 Gbps Full Duplex') {{
                                    $TargetValue = '1 Gbps Full Duplex'
                                }} elseif ($PossibleValues -match '1000 Mbps Full Duplex') {{
                                    $TargetValue = '1000 Mbps Full Duplex'
                                }}
                            }}

                            Set-NetAdapterAdvancedProperty -Name $Adapter -DisplayName $PropName -DisplayValue $TargetValue
                        }}";

                    // 1. Create a temporary .ps1 file to avoid quote escaping issues when running as Admin
                    string tempScriptPath = Path.Combine(Path.GetTempPath(), "NetSpeedChange.ps1");
                    File.WriteAllText(tempScriptPath, psScript);

                    // 2. Launch PowerShell to execute the file with UAC elevated privileges
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempScriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    };

                    try
                    {
                        using (var process = Process.Start(processInfo))
                        {
                            process?.WaitForExit();
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // User cancelled the UAC prompt
                    }
                    finally
                    {
                        // 3. Clean up the temporary file silently
                        if (File.Exists(tempScriptPath))
                        {
                            try { File.Delete(tempScriptPath); } catch { }
                        }
                    }
                });
            }

            this.HasAppliedChanges = true;
            this.Close();
        }
    }
}
