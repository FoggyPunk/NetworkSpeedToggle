using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Management.Infrastructure;

namespace StreamTweakService;

public class PipeWorker : BackgroundService
{
    public const string PipeName = "StreamTweakService";
    private readonly ILogger<PipeWorker> _logger;

    public PipeWorker(ILogger<PipeWorker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StreamTweakService pipe worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Allow any authenticated local user to connect
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                // Create server without using — ownership is transferred to HandleClientAsync
                var server = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096, 4096,
                    pipeSecurity);

                await server.WaitForConnectionAsync(stoppingToken);

                // Fire and forget — HandleClientAsync disposes the server stream
                _ = HandleClientAsync(server, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipe server error — restarting in 1s");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("StreamTweakService pipe worker stopped.");
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            using (server)
            {
                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                string? line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line))
                {
                    await writer.WriteLineAsync("ERROR:empty command");
                    return;
                }

                SpeedCommand? cmd;
                try
                {
                    cmd = JsonSerializer.Deserialize<SpeedCommand>(line);
                }
                catch
                {
                    await writer.WriteLineAsync("ERROR:invalid json");
                    return;
                }

                if (cmd == null || string.IsNullOrWhiteSpace(cmd.AdapterName) || string.IsNullOrWhiteSpace(cmd.RegistryValue))
                {
                    await writer.WriteLineAsync("ERROR:missing fields");
                    return;
                }

                _logger.LogInformation("Applying speed: adapter={Adapter} value={Value}", cmd.AdapterName, cmd.RegistryValue);

                bool success = ApplySpeedViaCim(cmd.AdapterName, cmd.RegistryValue)
                            || ApplySpeedViaPowerShell(cmd.AdapterName, cmd.RegistryValue);

                await writer.WriteLineAsync(success ? "OK" : "ERROR:apply failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client handler error");
        }
    }

    // Primary method: use CIM (WMI) directly — no child process needed
    private bool ApplySpeedViaCim(string adapterName, string registryValue)
    {
        try
        {
            using var session = CimSession.Create(null);

            string query = $"SELECT * FROM MSFT_NetAdapterAdvancedPropertySettingData " +
                           $"WHERE Name = '{adapterName}' AND RegistryKeyword = '*SpeedDuplex'";

            var instances = session.QueryInstances(@"root\StandardCimv2", "WQL", query).ToList();
            if (instances.Count == 0) return false;

            var instance = instances[0];
            instance.CimInstanceProperties["RegistryValue"].Value = registryValue;
            session.ModifyInstance(instance);

            // Restart the adapter to apply the new speed
            string adapterQuery = $"SELECT * FROM MSFT_NetAdapter WHERE Name = '{adapterName}'";
            var adapters = session.QueryInstances(@"root\StandardCimv2", "WQL", adapterQuery).ToList();
            if (adapters.Count > 0)
                session.InvokeMethod(adapters[0], "Restart", null);

            _logger.LogInformation("CIM speed change applied successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CIM method failed, will try PowerShell fallback");
            return false;
        }
    }

    // Fallback: PowerShell — still no UAC because the service already runs as LocalSystem
    private bool ApplySpeedViaPowerShell(string adapterName, string registryValue)
    {
        try
        {
            string script =
                $"Set-NetAdapterAdvancedProperty -Name '{adapterName}' " +
                $"-RegistryKeyword '*SpeedDuplex' -RegistryValue '{registryValue}' -NoRestart; " +
                $"Restart-NetAdapter -Name '{adapterName}' -Confirm:$false";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit();

            _logger.LogInformation("PowerShell speed change applied successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerShell fallback also failed");
            return false;
        }
    }

    private record SpeedCommand(string AdapterName, string RegistryValue);
}
