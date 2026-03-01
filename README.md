# Network Speed Toggle
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0-purple.svg)

WPF system tray app that lets you **switch between ALL supported Ethernet speeds** (100/1000/2.5G/5G/10G) with one click - no more manual adapter settings.

> ⚠️ **Browser Warning:** When downloading the installer, Edge or Chrome may show a warning saying the file could be harmful. This is a **false positive** caused by the lack of a paid code-signing certificate — common for new open-source projects. Click **"Keep anyway"** (Edge) or **"Keep"** (Chrome) to proceed. The source code is fully available here for inspection.

<table>
  <tr>
    <td><strong>Light Mode</strong></td>
    <td><strong>Dark Mode</strong></td>
  </tr>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/e2ef2526-dbfc-4ff8-82f7-9263a10eca1e" width="350"/></td>
    <td><img src="https://github.com/user-attachments/assets/d5b0ad14-1a3f-4c43-befa-85c13c706176" width="350"/></td>
  </tr>
</table>

## 🔧 What's New in Version 1.2.1

- **Self-contained build:** The .NET 8 runtime is now bundled inside the installer — no need to install .NET separately on the target machine.

## ✨ What's New in Version 1.2
- **Real-time speed indicator:** The Settings window now shows your current link speed live.
- **Modern Windows 11 UI:** Rounded corners, clean layout, and a fully custom ComboBox design.
- **Complete Dark/Light theme:** Theme switching now covers the title bar, dropdowns, and all UI elements.
- **Automatic accent color detection:** The UI adapts to your Windows personalization color automatically.
- **Persistent config:** The app remembers your selected adapter and theme preference across restarts.
- **Physical adapters only:** No VPNs, no virtual adapters, no filters needed — just your real hardware.

## 📖 The Story Behind This Project
This is an amateur, open-source project born out of a specific frustration in the cloud gaming community. When using game streaming software like **[Moonlight](https://github.com/moonlight-stream/moonlight-qt)** with **[Sunshine](https://github.com/LizardByte/Sunshine)** (or its fork **[Apollo](https://github.com/ClassicOldSong/Apollo)**), a known issue occurs if the host PC and the client have mismatched Ethernet link speeds (e.g., the Host is connected at 2.5 Gbps while the Client/Switch is at 1 Gbps).

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss, stuttering, and "Slow connection to PC" errors. You can read more about this technical bottleneck on the [Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714) and in this highly discussed [Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/).

The most effective workaround is to manually throttle the Host PC's Ethernet adapter down to 1.0 Gbps before starting a streaming session. Since doing this manually through Windows Device Manager every time is tedious, I created **Network Speed Toggle** to make the switch instantaneous right from the taskbar.

*Fun fact: This entire application, including the C# code, the Inno Setup installer, and the UI logic, was developed completely with the assistance of AI, specifically using **Perplexity Pro** powered by the **Gemini 3.1 Pro** LLM model.*

## 🔥 Features
- **Settings Dashboard:** Double-click the system tray icon to open a sleek UI to manage your adapters and speeds.
- **Real-time Speed Indicator:** See your current hardware link speed live inside the Settings window.
- **Dark/Light Theme:** Switch between themes with one click — title bar included.
- **Windows Accent Color:** The UI automatically picks up your personalization color from Windows Settings.
- **Silent & Unobtrusive:** Runs entirely in the background with zero CPU footprint.
- **Auto-Start:** Automatically launches on Windows startup using a hidden scheduled task.
- **Smart Validation:** Automatically filters out virtual networks, VPNs, and Wi-Fi adapters to only show physical LAN connections.
- **Dynamic Tooltips:** Hover over the tray icon to see your real-time hardware link speed and connection status.
- **Driver Bypass:** Automatically detects and bypasses Realtek/Intel driver localization limitations when setting link speeds.
- **UAC On-Demand:** Runs silently in the background, only requesting Admin Privileges exactly when you apply a new speed.

## 📝 Installation & Usage
1. Go to the **Releases** page on the right side of this GitHub repository.
2. Download the latest `NetworkSpeedToggle_1.2.1_Installer.exe`.
3. Run the installer (you can choose to enable Auto-Start on Windows startup).
4. Once running, double-click the tray icon to open the **Settings** window.
5. Select your Ethernet adapter from the dropdown, choose your desired speed, and click **Apply Settings**. The app will handle the rest!

## 🙏 Support the Project
If this tool helped you fix your Moonlight streaming stutters or made your network management easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License
![License](https://img.shields.io/badge/License-MIT-green.svg)
