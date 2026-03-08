using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace StreamTweak
{
    public static class SpeedChanger
    {
        private const string ServiceName = "StreamTweakService";
        private const string PipeName = "StreamTweakService";
        private const int PipeTimeoutMs = 5000;

        /// <summary>
        /// Sends the speed change command to the StreamTweakService via Named Pipe.
        /// The service runs as LocalSystem and executes the change without UAC.
        /// </summary>
        public static bool Apply(string adapterName, string registryValue)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                client.Connect(PipeTimeoutMs);

                using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, leaveOpen: true);

                string json = JsonSerializer.Serialize(new
                {
                    AdapterName = adapterName,
                    RegistryValue = registryValue
                });

                writer.WriteLine(json);

                string? response = reader.ReadLine();
                return response == "OK";
            }
            catch { return false; }
        }

        /// <summary>
        /// Fallback for environments without the service installed (e.g. development).
        /// Launches PowerShell with Verb = "runas" — triggers a UAC prompt.
        /// </summary>
        public static bool ApplyWithUac(string adapterName, string registryValue)
        {
            string tempScript = Path.Combine(Path.GetTempPath(), "NetSpeedChanger.ps1");
            string psScript = $@"
$adapterName = '{adapterName}'
$registryValue = '{registryValue}'
Set-NetAdapterAdvancedProperty -Name $adapterName -RegistryKeyword '*SpeedDuplex' -RegistryValue $registryValue -NoRestart
Restart-NetAdapter -Name $adapterName -Confirm:$false
";
            try
            {
                File.WriteAllText(tempScript, psScript);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempScript}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit();
                return true;
            }
            catch { return false; }
            finally
            {
                if (File.Exists(tempScript))
                    try { File.Delete(tempScript); } catch { }
            }
        }
    }
}