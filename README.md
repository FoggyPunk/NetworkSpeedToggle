# StreamTweak
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0-purple.svg)
![Downloads](https://img.shields.io/github/downloads/foggypunk/StreamTweak/total?label=Downloads)

**StreamTweak** is your ultimate local streaming wingman. Born to solve technical bottlenecks between Host and Client, it is evolving into a lightweight, intelligent control center to manage your remote gaming PC with a single click.

> ⚠️ **Browser Warning:** When downloading the installer, Edge or Chrome may show a security warning. This is a **false positive** caused by the lack of a paid code-signing certificate — common for open-source projects. Click **"Keep"** or **"Keep anyway"** to proceed. The source code is fully available here for inspection.

<img width="406" height="514" alt="streamtweak" src="https://github.com/user-attachments/assets/d155b874-3445-4385-93be-2b820c1de04d" />

## 🚀 The Evolution: From Network Speed Toggle to StreamTweak 🎮
The project originally started as **Network Speed Toggle**, a utility focused exclusively on solving a specific issue in the cloud gaming community: Ethernet link speed mismatches (e.g., Host at 2.5 Gbps and Client at 1 Gbps) causing stuttering and UDP packet loss on unmanaged switches.

With **Version 2.0**, the project has evolved. It is no longer just a network tool; it has become **StreamTweak**. Our mission is to progressively add all the "quality-of-life" features needed by those who manage a Host PC running **[Sunshine](https://github.com/LizardByte/Sunshine)** or its fork **[Apollo](https://github.com/ClassicOldSong/Apollo)**, eliminating tedious manual configurations before every gaming session.

## ✨ What's New in Version 2.0
- **Total Rebrand:** New name, new spirit, and a fresh modern icon.
- **HDR Toggle:** Easily enable or disable HDR on your monitor with one click directly from the tray (essential for streaming to non-HDR devices).
- **Hardware Monitor:** Instantly view current resolution, refresh rate, and your installed GPU model.
- **Intelligent Tooltips:** Hover over the tray icon to immediately understand your network and system status.

## 📖 The Technical Story Behind This Project
This project was born out of a specific frustration in the cloud gaming community. When using game streaming software like **[Moonlight](https://github.com/moonlight-stream/moonlight-qt)** with **Sunshine** or **Apollo**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds.

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss and "Slow connection to PC" errors. You can read more about this technical bottleneck on the **[Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714)** and in this highly discussed **[Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/)**.

StreamTweak makes the workaround (throttling the Host PC's Ethernet adapter down to 1.0 Gbps) instantaneous, and it looks forward to adding more features to streamline the remote gaming experience.

*Fun fact: This entire application, including the C# code, UI logic, and Inno Setup installer, was developed with the assistance of AI.*

## 🔥 Key Features
- **HDR Toggle:** Instantly switch HDR on or off from the system tray—perfect for streaming to clients that don't support High Dynamic Range.
- **Settings Dashboard:** Sleek UI to manage physical adapters and speeds.
- **Smart Filtering:** Only shows real LAN adapters (no VPNs, Wi-Fi, or virtual adapters).
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations.
- **Auto-Start:** Launches at Windows logon via a hidden Scheduled Task.
- **UAC On-Demand:** Runs silently in the background, requesting Admin privileges only when applying changes.

## 📝 Installation
1. Go to the **Releases** page of this repository.
2. Download the latest `StreamTweak_2.0_Installer.exe`.
3. Run the installer and enjoy seamless streaming.

## 🙏 Support the Project
If this tool helped you fix your Moonlight stutters or made managing your PC easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License
![License](https://img.shields.io/badge/License-MIT-green.svg)
