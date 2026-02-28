# Network Speed Toggle

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%2010.0-purple.svg)

A lightweight WPF system tray application to instantly toggle your Ethernet adapter speed between 1.0 Gbps and 2.5 Gbps with a simple double-click, completely bypassing Windows menus.

<img width="509" height="442" alt="networkspeedtoogle" src="https://github.com/user-attachments/assets/4021747e-22c3-4904-9008-c1945df5cb5e" />

## 🌟 What's New in Version 1.1
- **Smart Settings UI:** A brand new configuration window to dynamically detect your physical network adapters and select the desired speeds.
- **Hardware-Level Polling:** The app now reads real-time kernel-level NDIS link speeds for 100% accurate tray icon updates.
- **Toast Notifications:** Added native Windows notifications to confirm when the router/switch handshake is successfully completed.
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations when setting link speeds.
- **UAC On-Demand:** The app now runs completely silently in the background, only requesting Admin Privileges exactly when you apply a new speed.

## The Story Behind This Project
This is an amateur, open-source project born out of a specific frustration in the cloud gaming community. When using game streaming software like **Moonlight** and **Sunshine**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds (e.g., the Host is connected at 2.5 Gbps while the Client/Switch is at 1 Gbps). 

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss, stuttering, and "Slow connection to PC" errors. You can read more about this technical bottleneck on the [Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714) and in this highly discussed [Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/).

The most effective workaround is to manually throttle the Host PC's Ethernet adapter down to 1.0 Gbps before starting a streaming session. Since doing this manually through Windows Device Manager every time is tedious, I created **Network Speed Toggle** to make the switch instantaneous right from the taskbar.

*Fun fact: This entire application, including the C# code, the Inno Setup installer, and the UI logic, was developed completely with the assistance of AI, specifically using **Perplexity Pro** powered by the **Gemini 3.1 Pro** LLM model.*

## Features
- **Settings Dashboard:** Double-click the system tray icon to open a sleek UI to manage your adapters and speeds.
- **Silent & Unobtrusive:** Runs entirely in the background with zero CPU footprint.
- **Auto-Start:** Automatically launches on Windows startup using a hidden scheduled task.
- **Smart Validation:** Automatically filters out virtual networks, VPNs, and Wi-Fi adapters to only show physical LAN connections.
- **Dynamic Tooltips:** Hover over the tray icon to see your real-time hardware link speed and connection status.

## Installation & Usage
1. Go to the **Releases** page on the right side of this GitHub repository.
2. Download the latest `NetworkSpeedToggle_1.1_Setup.exe`.
3. Run the installer (you can choose to create a Start Menu shortcut and enable Auto-Start).
4. Once running, double-click the tray icon to open the **Settings** window.
5. Select your Ethernet adapter from the dropdown, choose your desired speed, and click **Apply Settings**. The app will handle the rest!

## Support the Project
If this tool helped you fix your Moonlight streaming stutters or made your network management easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License

![License](https://img.shields.io/badge/License-MIT-green.svg)

