# StreamTweak
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0-purple.svg)
![Downloads](https://img.shields.io/github/downloads/foggypunk/StreamTweak/total?label=Downloads)

**StreamTweak** is born to solve technical bottlenecks between Host and Client, offering an intelligent control center to manage your remote gaming PC with a single click.

> ⚠️ **Browser Warning:** When downloading the installer, Edge or Chrome may show a security warning. This is a **false positive** caused by the lack of a paid code-signing certificate — common for open-source projects. Click **"Keep"** or **"Keep anyway"** to proceed. The source code is fully available here for inspection.

<img width="406" height="514" alt="streamtweak" src="https://github.com/user-attachments/assets/d155b874-3445-4385-93be-2b820c1de04d" />

## 🚀 The ReBrand: From Network Speed Toggle to StreamTweak 🎮
The project originally started as **Network Speed Toggle**, a utility focused on solving a specific issue in the cloud gaming community: Ethernet link speed mismatches (e.g., Host at 2.5 Gbps and Client at 1 Gbps) causing stuttering and UDP packet loss on unmanaged switches.

With **Version 2.0**, the project has evolved.

## ✨ What's New in Version 2.5.0
- **Enhanced Auto Streaming Mode:** Improved monitoring logic and state synchronization
- **Redesigned Settings UI:** Professional modern layout with toggle switch styled after Windows 11
- **Intelligent Alert System:** 4-second awareness delay before network changes, auto-dismissal after 8 seconds
- **Better State Management:** Seamless coordination between Auto Mode and Manual Mode controls
- **Improved Stability:** Enhanced error handling and resource cleanup

**Previous v2.0 Features Still Included:**
- Total Rebrand with modern icon
- Manual Streaming Control button
- Hardware Monitor (resolution, refresh rate, GPU info)

## 📖 The Technical Story Behind This Project
This project was born out of a specific frustration in the cloud gaming community. When using game streaming software like **[Moonlight](https://github.com/moonlight-stream/moonlight-qt)** with **Sunshine** or **Apollo**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds.

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss and "Slow connection to PC" errors. You can read more about this technical bottleneck on the **[Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714)** and in this highly discussed **[Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/)**.

StreamTweak makes the workaround (throttling the Host PC's Ethernet adapter down to 1.0 Gbps) instantaneous, and it looks forward to adding more features to streamline the remote gaming experience.

*Fun fact: This entire application, including the C# code, UI logic, and Inno Setup installer, was developed with the assistance of AI.*

## 🔥 Key Features
- **Settings Dashboard:** Sleek UI to manage physical adapters and speeds.
- **Smart Filtering:** Only shows real LAN adapters (no VPNs, Wi-Fi, or virtual adapters).
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations.
- **Auto Streaming Mode:** Intelligently monitors Sunshine/Apollo logs for incoming Moonlight connections and auto-adjusts network speed.
- **Manual Streaming Control:** One-click activation to instantly throttle to 1Gbps with professional UI feedback.
- **Smart Notifications:** Non-intrusive on-screen alerts inform users before network changes occur.
- **Auto-Start:** Launches at Windows logon via a hidden Scheduled Task.
- **UAC On-Demand:** Runs silently in the background, requesting Admin privileges only when applying changes.

## 🎮 How It Works

### Auto Streaming Mode
1. Enable "Auto Streaming Mode" in the Settings
2. StreamTweak monitors your Sunshine/Apollo streaming server logs in real-time
3. When Moonlight connects from a client:
   - An on-screen alert appears (4-second delay for awareness)
   - Network speed automatically adjusts from current speed (e.g., 2.5Gbps) down to 1Gbps
4. **Important:** Your stream will briefly disconnect during the speed adjustment
   - You have **30 seconds to reconnect** in Moonlight
   - If you reconnect within this window, streaming continues normally
   - The inactivity timer prevents the app from reverting speed prematurely (avoiding reconnection loops)
5. When streaming ends:
   - If no new connection is detected within 30 seconds, original speed is automatically restored
   - If you reconnect within 30 seconds, the speed stays at 1Gbps (timer resets)
6. **Requirements:** Sunshine/Apollo must be installed in default installation folders for log monitoring to work

**Why the 30-second window?** This prevents an infinite loop: when the network speed changes, it causes a temporary disconnect. The 30-second inactivity timer gives you time to reconnect without the app thinking the streaming session has ended.

### Manual Streaming Mode
1. Click "Start Streaming Mode" button anytime
2. On-screen alert informs you of the network adjustment
3. Network throttles to 1Gbps immediately
4. Click "Stop Streaming Mode" to restore original speed

## 📝 Installation
1. Go to the **Releases** page of this repository.
2. Download the latest `StreamTweak_2.5.0_Installer.exe`
3. Run the installer and enjoy seamless streaming.

## 🙏 Support the Project
If this tool helped you fix your Moonlight stutters or made managing your PC easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License
![License](https://img.shields.io/badge/License-MIT-green.svg)
