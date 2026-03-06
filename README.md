# StreamTweak
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0-purple.svg)
![Downloads](https://img.shields.io/github/downloads/foggypunk/StreamTweak/total?label=Downloads&color=orange)

**StreamTweak** is born to solve technical bottlenecks between Host and Client, offering an intelligent control center to manage your remote gaming PC with a single click.

> ⚠️ **Browser Warning:** When downloading the installer, Edge or Chrome may show a security warning. This is a **false positive** caused by the lack of a paid code-signing certificate — common for open-source projects. Click **"Keep"** or **"Keep anyway"** to proceed. The source code is fully available here for inspection.

## ✨ What's New in Version 2.5.2 — The "Vibe Update"
Native support for **Vibepollo** and **Vibeshine** — the most popular Sunshine/Apollo forks — is now fully integrated into Auto Streaming Mode.

- **Vibepollo and Vibeshine supported:** Auto Streaming Mode now detects Moonlight connections via their log files, including the dynamic rotating format (`sunshine-YYYYMMDD-HHMMSS-mmm.log`)
- **Dynamic installation discovery:** StreamTweak automatically finds your streaming server regardless of where it's installed. The Windows registry is checked first; if that fails, Program Files is scanned as fallback. Custom installation paths work out of the box — no configuration needed
- **Periodic rediscovery:** StreamingLogMonitor re-runs full log discovery every 10 seconds, catching dynamic log files that appear after StreamTweak has already started
- **Broad compatibility:** Any current or future Sunshine-based fork is automatically supported as long as it follows the standard `config\logs\` or `config\sunshine.log` structure

## ✨ What's New in Version 2.5.1
- **Truly UAC-free experience:** A background Windows Service (`StreamTweakService`) handles all network speed changes silently — no UAC prompts during Manual Mode, Auto Mode, or tray menu operations
- **Clean privilege separation:** The UI process stays unprivileged; only the service touches the network adapter settings
- **Graceful fallback:** Without the installer, the app falls back to the standard UAC prompt automatically

## ✨ What's New in Version 2.5.0
- **Auto Streaming Mode:** StreamTweak monitors Sunshine/Apollo logs and automatically adjusts network speed when Moonlight connects
- **Redesigned Settings UI:** Professional modern layout with Windows 11-style toggle switch
- **Intelligent Alert System:** 4-second awareness delay before network changes, auto-dismissal after 8 seconds

## 📖 The Technical Story Behind This Project
This project was born out of a specific frustration in the cloud gaming community. When using game streaming software like **[Moonlight](https://github.com/moonlight-stream/moonlight-qt)** with **Sunshine** or **Apollo**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds.

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss and "Slow connection to PC" errors. You can read more about this technical bottleneck on the **[Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714)** and in this highly discussed **[Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/)**.

StreamTweak makes the workaround (throttling the Host PC's Ethernet adapter down to 1.0 Gbps) instantaneous and completely seamless — no interruptions, no prompts.

*Fun fact: This entire application, including the C# code, UI logic, and Inno Setup installer, was developed with the assistance of AI.*

## 🔥 Key Features
- **Settings Dashboard:** Sleek UI to manage physical adapters and speeds.
- **Smart Filtering:** Only shows real LAN adapters (no VPNs, Wi-Fi, or virtual adapters).
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations.
- **Auto Streaming Mode:** Intelligently monitors Sunshine, Apollo, Vibeshine and Vibepollo logs for incoming Moonlight connections and auto-adjusts network speed. Works with any installation path.
- **Manual Streaming Control:** One-click activation to instantly throttle to 1Gbps with professional UI feedback.
- **Smart Notifications:** Non-intrusive on-screen alerts inform users before network changes occur.
- **Completely UAC-free:** A background Windows Service handles all privileged operations silently — no prompts during normal use.
- **Auto-Start:** Launches at Windows logon via a hidden Scheduled Task.

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

## 🎮 How It Works

### Auto Streaming Mode
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

### Manual Streaming Mode
1. Click "Start Streaming Mode" button anytime
2. On-screen alert informs you of the network adjustment
3. Network throttles to 1Gbps immediately — no UAC prompt
4. Click "Stop Streaming Mode" to restore original speed

## 📝 Installation
1. Go to the **Releases** page of this repository.
2. Download the latest `StreamTweak_2.5.2_Installer.exe`
3. Run the installer and enjoy seamless streaming.

## 🙏 Support the Project
If this tool helped you fix your Moonlight stutters or made managing your PC easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-green.svg)](https://www.gnu.org/licenses/gpl-3.0)
