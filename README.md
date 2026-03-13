# 🎮 StreamTweak ![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg) ![Framework](https://img.shields.io/badge/Framework-.NET%208.0-purple.svg) ![Downloads](https://img.shields.io/github/downloads/foggybytes/StreamTweak/total?label=Downloads&color=orange)

**StreamTweak** is born to solve technical bottlenecks between Host and Client, offering an intelligent control center to manage your remote gaming PC with a single click.

## ✅ Compatibility
StreamTweak works seamlessly with [Moonlight](https://github.com/moonlight-stream/moonlight-qt), [Sunshine](https://github.com/LizardByte/Sunshine), [Apollo](https://github.com/ClassicOldSong/Apollo), [Vibeshine](https://github.com/Nonary/vibeshine), and [Vibepollo](https://github.com/Nonary/Vibepollo), on Windows.

> ⚠️ **Note on Installer Warnings:** When downloading the installer, Windows SmartScreen or your browser may show a security warning. This happens because the executable is not signed with a commercial code‑signing certificate — a common situation for open‑source projects. You can safely choose **Keep** / **Keep anyway**. The full source code is available in this repository for inspection.

## 🔗 StreamLight — The Companion Client

[StreamLight](https://github.com/FoggyBytes/StreamLight) is the official companion client for StreamTweak — a fork of [Moonlight](https://github.com/moonlight-stream/moonlight-qt) that integrates StreamTweak directly into the streaming client UI.

With StreamLight, you can manage host NIC speed without leaving the client:

- **Show host NIC speed** — query StreamTweak on the host and see the current Ethernet adapter speed at a glance
- **Set host to 1 Gbps** — send the speed-change command to StreamTweak from the client before connecting, with a built-in 10-second countdown and a 30-second auto-revert if no connection is made

StreamLight and StreamTweak are designed to work together, giving you full control over the streaming setup from both sides of the connection.

> StreamLight is available for **Windows only** and requires StreamTweak to be installed and running on the host PC.

## 🔥 Key Features

### 🛜 Network & Streaming Intelligence
- **Auto Streaming Mode:** Intelligently monitors [Sunshine](https://github.com/LizardByte/Sunshine), [Apollo](https://github.com/ClassicOldSong/Apollo), [Vibeshine](https://github.com/Nonary/vibeshine) and [Vibepollo](https://github.com/Nonary/Vibepollo) logs for incoming Moonlight connections and auto-adjusts network speed. Works with any installation path.
- **Manual Streaming Control:** One-click activation to instantly throttle to 1Gbps with professional UI feedback.
- **Smart Notifications:** Non-intrusive on-screen alerts inform users before network changes occur.
- **Smart Filtering:** Only shows real LAN adapters (no VPNs, Wi-Fi, or virtual adapters).
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations.
- **Completely UAC-free:** A background Windows Service handles all privileged operations silently — no prompts during normal use.
- **Auto-Start:** Launches at Windows logon via a hidden Scheduled Task.

### 🖥️ Display & HDR
- **HDR monitor control:** the Display tab lists all active monitors with resolution, refresh rate, and HDR state — toggle HDR on or off without opening Windows Settings. Works on Windows 10 and all Windows 11 versions including 24H2.
- **Auto HDR toggle:** enable or disable Windows Auto HDR for supported SDR games directly from StreamTweak; the change takes effect immediately.
- **Virtual display awareness:** when Apollo or Vibepollo is detected, the Display tab automatically focuses on the virtual display used for remote streaming.

### 🎧 Audio Enhancements
- **Auto Dolby Atmos for Headphones:** Automatically enables Dolby Atmos for Headphones on Steam Streaming Speakers 30 seconds after a streaming session starts — requires Dolby Access on the host PC.
- **Dolby Access detection:** The Audio tab shows a live indicator (via Windows Spatial Audio API) confirming whether the Dolby Atmos format is available on the system.

### 🖥️ UI & Control Experience
- **Settings Dashboard:** Sleek UI to manage physical adapters and speeds.
- **Tray Control:** Right-click the tray icon to toggle Auto Mode, check current link speed, and monitor streaming session status — all without opening Settings.
- **Streaming App Detection:** StreamTweak locates the log folder of the active streaming server and lets you open it in Explorer with one click.

### 📚 System Info & Diagnostics
- **Logs Tab:** Session history for the last 10 speed changes — trigger mode, duration, and original speed recorded automatically.
- **About Tab:** Version info, GitHub link, license badge, and donation button in a dedicated panel.

## ✨ What's New in Version 4.0.0 — The "Foundation Update"

4.0.0 is a new starting point — the visual identity is consolidated and the interface is ready to grow.

- **Button animations:** all interactive buttons now feature smooth scale animations on hover and a tactile press-down effect on click, bringing the interface closer to a native Windows 11 feel
- **Selective hover behavior:** primary action buttons (Start/Stop Streaming Mode, Apply Settings, View on GitHub, Donate via PayPal) animate on hover without altering their background color, keeping visual focus on key actions
- **Unified dark theme:** the StreamingAdjustmentAlert notification window now fully matches the main Settings window dark palette, ensuring visual consistency across all application surfaces
- **Visual identity:** the accent **#BE5438** — a warm rust tone — is established as the defining color of the StreamTweak interface, consistently applied across all interactive states, panel highlights, and active controls

## 📖 The Technical Story Behind This Project
This project was born out of a specific frustration in the cloud gaming community. When using game streaming software like **[Moonlight](https://github.com/moonlight-stream/moonlight-qt)** with **Sunshine** or **Apollo**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds.

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss and "Slow connection to PC" errors. You can read more about this technical bottleneck on the **[Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714)** and in this highly discussed **[Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/)**.

StreamTweak makes the workaround (throttling the Host PC's Ethernet adapter down to 1.0 Gbps) instantaneous and completely seamless — no interruptions, no prompts.

*Fun fact: This entire application, including the C# code, UI logic, and Inno Setup installer, was developed with the assistance of AI.*

## 🏗️ Architecture

### UAC-free Speed Changes (v2.5.1+)
StreamTweak installs a lightweight background Windows Service (`StreamTweakService`) that runs as `LocalSystem` and listens on a Named Pipe. Whenever the app needs to change the network adapter speed — whether from Auto Mode, Manual Mode, or the tray menu — it sends a command through the pipe. The service executes the change with its existing elevated privileges. No UAC dialog ever appears.

```
StreamTweak.exe  ──(Named Pipe)──►  StreamTweakService  ──►  Network Adapter
 (unprivileged)                        (LocalSystem)
```

### Log Discovery (v2.5.2+)
StreamTweak automatically locates the streaming server log file at startup and re-checks every 10 seconds to handle late-appearing dynamic logs.

```
1. Registry lookup  →  InstallLocation from HKLM\SOFTWARE or Uninstall keys
2. Program Files scan  →  known names first (Vibepollo, Apollo, Vibeshine, Sunshine),
                          then broad scan of all subfolders
3. For each candidate folder  →  config\logs\sunshine-*.log (dynamic)
                               →  config\sunshine.log (static)
```

### Session Logging (v3.0.0+)
Every speed-change event — whether triggered automatically or manually — is recorded as a `SessionEntry` and persisted to `%LOCALAPPDATA%\StreamTweak\sessions.json`. The Logs tab reads this file and displays the last 10 sessions in real time.

```
SessionEntry {
  Id             →  short unique identifier
  StartTime      →  when the session began
  EndTime        →  when the original speed was restored (null if active or interrupted)
  TriggerMode    →  "Auto" | "Manual"
  OriginalSpeed  →  the speed key that will be restored on session end
  EndReason      →  "User" | "Disconnected" | "Interrupted" (null if still active)
}
```

The same discovery pipeline used for log monitoring (`LogParser.FindStreamingAppInfo`) is surfaced in the Logs tab, so the user can verify at a glance which streaming server StreamTweak has detected and navigate directly to its log folder.

### Auto Dolby Atmos for Headphones (v3.1.0+)
When a streaming session is detected and remains active for 30 continuous seconds, `DolbyAudioMonitor` queries the Windows Spatial Audio API (`SpatialAudioDeviceConfiguration`) on Steam Streaming Speakers and sets Dolby Atmos for Headphones as the active spatial audio format. Detection of [Dolby Access](https://apps.microsoft.com/detail/9n0866fs04w8) also uses the same API — if any render device reports `DolbyAtmosForHeadphones` as a supported format, the feature is considered available.

```
Streaming event detected
        │
        ▼
30-second countdown (cancellable on stream stop)
        │
        ▼
SpatialAudioDeviceConfiguration.GetForDeviceId(Steam Streaming Speakers)
        │
        ├─ IsSpatialAudioSupported? ──No──► status: not supported
        ├─ IsSpatialAudioFormatSupported(DolbyAtmos)? ──No──► status: Dolby Access not installed
        └─ SetDefaultSpatialAudioFormatAsync(DolbyAtmos) ──► status: ✓ enabled
```

### Display & HDR Control (v3.2.0+)
`HdrService` reads and controls the HDR state of every active monitor through the Windows **DisplayConfig** API family, entirely via P/Invoke — no external dependencies. Monitor enumeration, resolution, refresh rate, and HDR state are all resolved in a single async pass.

```
GetDisplayConfigBufferSizes  →  allocate path/mode arrays
QueryDisplayConfig           →  enumerate active display topology
        │
        ├─ DisplayConfigGetDeviceInfo (type 2)  →  friendly name + device path
        ├─ DisplayConfigGetDeviceInfo (type 1)  →  GDI device name (\\.\DISPLAYn)
        ├─ EnumDisplaySettings                  →  resolution + refresh rate (Hz)
        └─ DisplayConfigGetDeviceInfo (type 9)  →  HDR supported / enabled state
```

Toggling HDR uses a different `DisplayConfigSetDeviceInfo` request type depending on the Windows build detected at runtime:

```
Build ≥ 26100  (Windows 11 24H2+)   →  type 16  SetHdrState
Build  < 26100  (Windows 10 / 11)   →  type 10  SetAdvancedColorState
```

Auto HDR is controlled via the registry key `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` (value `DirectXUserGlobalSettings`, field `AutoHDREnable=0|1`). After each write, `WM_SETTINGCHANGE` is broadcast system-wide so all running applications pick up the change immediately — no reboot or sign-out required.

`MonitorInfo.IsVirtual` is `true` when the device path returned by `DisplayConfigGetDeviceInfo` contains one of the known virtual display vendor strings (`SudoVDA`, `IDD_`, `MttVDD`). When Apollo or Vibepollo is detected as the active streaming server, the Display tab filters the monitor list to show only virtual displays — falling back to all displays with a contextual hint if none is connected yet.

```
LogParser.FindStreamingAppInfo()
        │
        ├─ Apollo / Vibepollo detected
        │       ├─ IsVirtual monitors found  →  show only virtual display(s)
        │       └─ none found yet            →  show all  +  "connect Moonlight" hint
        └─ Sunshine / Vibeshine / none       →  show all physical displays
```

## 🪄 How It Works

### 🤖 Auto Streaming Mode
1. Enable "Auto Streaming Mode" in the Settings
2. StreamTweak monitors your streaming server logs in real-time (Sunshine, Apollo, Vibeshine, Vibepollo)
3. When Moonlight connects from a client:
   - An on-screen alert appears (4-second delay for awareness)
   - Network speed automatically adjusts from current speed (e.g., 2.5Gbps) down to 1Gbps
4. **Important:** A brief disconnect will occur during the speed adjustment
   - You have **30 seconds to reconnect** in Moonlight
   - If you reconnect within this window, streaming continues normally
   - If no reconnection is detected within 30 seconds, the original speed is automatically restored
   - The inactivity timer prevents the app from reverting speed prematurely (avoiding reconnection loops)
5. When streaming ends, original speed is automatically restored

### 🕹️ Manual Streaming Mode
1. Click "Start Streaming Mode" button anytime
2. On-screen alert informs you of the network adjustment
3. Network throttles to 1Gbps immediately — no UAC prompt
4. Click "Stop Streaming Mode" to restore original speed

### 🖥️ Display & HDR
1. Open the **Display** tab — all active monitors are listed with name, resolution, refresh rate, and current HDR state
2. Toggle HDR on or off on any monitor directly from StreamTweak — no need to open Windows Settings; the change takes effect immediately
3. Enable **Auto HDR** from the same tab to activate Windows Auto HDR for supported SDR games system-wide; the registry change is broadcast instantly to all running applications
4. When Apollo or Vibepollo is detected as the active streaming server, the tab automatically focuses on the virtual display used for the session (SudoVDA / IDD); if no client is connected yet, all physical displays are shown with a contextual hint to connect Moonlight first

### 🎧 Auto Dolby Atmos for Headphones
1. Install [Dolby Access](https://apps.microsoft.com/detail/9n0866fs04w8) on the host PC and enable it at least once
2. Enable "Auto Dolby Atmos for Headphones" in the Audio tab (or from the tray menu)
3. The Audio tab will show a green indicator confirming Dolby Atmos for Headphones is detected
4. When a streaming session starts and stays active for **30 seconds**, StreamTweak automatically sets Dolby Atmos for Headphones as the active spatial audio format on Steam Streaming Speakers
5. The Status box in the Audio tab turns **green** to confirm activation
6. When the streaming session ends, the countdown is cancelled — activation only happens once per session

## 📝 Installation
1. Go to the **Releases** page of this repository.
2. Download the latest `StreamTweak_4.0.0_Installer.exe`
3. Run the installer and enjoy seamless streaming.

## 🙏 Support the Project
If this tool helped you fix your Moonlight stutters or made managing your PC easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## 🤝 Acknowledgements
StreamTweak exists thanks to the outstanding work of the developers behind the tools it integrates with. A heartfelt thank you to:

- [**Moonlight**](https://github.com/moonlight-stream/moonlight-qt) — the open-source game streaming client that inspired this project and the community around it
- [**Sunshine**](https://github.com/LizardByte/Sunshine) — the game streaming host that started it all
- [**Apollo**](https://github.com/ClassicOldSong/Apollo) — the community-driven Sunshine fork
- [**Vibeshine**](https://github.com/Nonary/vibeshine) — the Sunshine fork with dynamic log support that drove the v2.5.2 compatibility work
- [**Vibepollo**](https://github.com/Nonary/Vibepollo) — the Apollo fork, also fully supported since v2.5.2

## License
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-green.svg)](https://www.gnu.org/licenses/gpl-3.0)
