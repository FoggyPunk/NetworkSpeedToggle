using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Devices;

namespace StreamTweak
{
    internal sealed class DolbyAudioMonitor
    {
        private const string SteamSpeakersName = "Steam Streaming Speakers";
        private const int WaitSeconds = 30;

        private CancellationTokenSource? _cts;
        private bool _activatedThisSession;

        public bool IsEnabled { get; private set; }

        public event Action<string>? StatusChanged;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        public void Enable()
        {
            if (IsEnabled) return;
            IsEnabled = true;
            NotifyStatus("Ready — waiting for next stream…");
        }

        public void Disable()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            CancelPending();
            NotifyStatus("Disabled.");
        }

        // ─── Streaming events (called by App.xaml.cs) ────────────────────────

        public void OnStreamingStarted()
        {
            if (!IsEnabled || _activatedThisSession) return;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => DelayedEnableDolbyAsync(_cts.Token));
            NotifyStatus($"Stream detected — waiting {WaitSeconds}s…");
        }

        public void OnStreamingStopped()
        {
            CancelPending();
            _activatedThisSession = false;
            if (IsEnabled)
                NotifyStatus("Ready — waiting for next stream…");
        }

        private void CancelPending()
        {
            _cts?.Cancel();
            _cts = null;
        }

        // ─── Delayed activation ──────────────────────────────────────────────

        private async Task DelayedEnableDolbyAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(WaitSeconds), token);
                if (token.IsCancellationRequested) return;

                string? deviceId = await FindSteamSpeakersDeviceIdAsync();
                if (deviceId == null)
                {
                    NotifyStatus("Steam Streaming Speakers not found.");
                    return;
                }

                await TryEnableDolbyAtmosAsync(deviceId);
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ─── Device discovery ────────────────────────────────────────────────

        private static async Task<string?> FindSteamSpeakersDeviceIdAsync()
        {
            try
            {
                string selector = MediaDevice.GetAudioRenderSelector();
                var devices = await DeviceInformation.FindAllAsync(selector);
                var match = devices.FirstOrDefault(d =>
                    d.Name.Contains(SteamSpeakersName, StringComparison.OrdinalIgnoreCase));
                return match?.Id;
            }
            catch { return null; }
        }

        // ─── Dolby Atmos activation ──────────────────────────────────────────

        private async Task TryEnableDolbyAtmosAsync(string deviceId)
        {
            try
            {
                string dolbyFormat = SpatialAudioFormatSubtype.DolbyAtmosForHeadphones;
                var config = SpatialAudioDeviceConfiguration.GetForDeviceId(deviceId);

                if (!config.IsSpatialAudioSupported)
                {
                    NotifyStatus("Spatial audio not supported for Steam Streaming Speakers.");
                    return;
                }

                if (!config.IsSpatialAudioFormatSupported(dolbyFormat))
                {
                    NotifyStatus("Dolby Atmos for Headphones not available (Dolby Access not installed).");
                    return;
                }

                if (config.ActiveSpatialAudioFormat
                        .Equals(dolbyFormat, StringComparison.OrdinalIgnoreCase))
                {
                    _activatedThisSession = true;
                    NotifyStatus("✓ Dolby Atmos for Headphones already active.");
                    return;
                }

                await config.SetDefaultSpatialAudioFormatAsync(dolbyFormat);
                _activatedThisSession = true;
                NotifyStatus("✓ Dolby Atmos for Headphones enabled.");
            }
            catch (Exception ex)
            {
                NotifyStatus($"Failed: {ex.Message}");
            }
        }

        // ─── Dolby Atmos for Headphones availability check (via Spatial Audio API) ─

        public static async Task<bool> IsDolbyAtmosAvailableAsync()
        {
            try
            {
                string selector = MediaDevice.GetAudioRenderSelector();
                var devices = await DeviceInformation.FindAllAsync(selector);
                string dolbyFormat = SpatialAudioFormatSubtype.DolbyAtmosForHeadphones;

                foreach (var device in devices)
                {
                    try
                    {
                        var config = SpatialAudioDeviceConfiguration.GetForDeviceId(device.Id);
                        if (config.IsSpatialAudioSupported &&
                            config.IsSpatialAudioFormatSupported(dolbyFormat))
                            return true;
                    }
                    catch { }
                }
                return false;
            }
            catch { return false; }
        }

        private void NotifyStatus(string message) => StatusChanged?.Invoke(message);
    }
}
