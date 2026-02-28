# Network Speed Toggle

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%2010.0-purple.svg)

A lightweight, invisible WPF system tray application to instantly toggle your Ethernet adapter speed between 1.0 Gbps and 2.5 Gbps with a simple double-click.

![networkspeedtoggle](https://github.com/user-attachments/assets/40cdd7b4-b2e0-4ff5-9047-cbadd7540ecc)

## The Story Behind This Project
This is an amateur, open-source project born out of a specific frustration in the cloud gaming community. When using game streaming software like **Moonlight** and **Sunshine**, a known issue occurs if the host PC and the client have mismatched Ethernet link speeds (e.g., the Host is connected at 2.5 Gbps while the Client/Switch is at 1 Gbps). 

Due to how UDP packet buffering works on network switches, this mismatch often leads to severe packet loss, stuttering, and "Slow connection to PC" errors. You can read more about this technical bottleneck on the [Moonlight GitHub Issue #714](https://github.com/moonlight-stream/moonlight-qt/issues/714) and in this highly discussed [Reddit thread](https://www.reddit.com/r/MoonlightStreaming/comments/1m35zo7/fix_moonlight_streaming_issues_on_25gbps_lan_try/).

The most effective workaround is to manually throttle the Host PC's Ethernet adapter down to 1.0 Gbps before starting a streaming session. Since doing this manually through Windows Device Manager every time is tedious, I created **Network Speed Toggle** to make the switch instantaneous right from the taskbar.

*Fun fact: This entire application, including the C# code, the Inno Setup installer, and the UI logic, was developed completely with the assistance of AI, specifically using **Perplexity Pro** powered by the **Gemini 3.1 Pro** LLM model.*

## Features
- **One-click toggle:** Instantly switch network speeds directly from the system tray.
- **Silent & Unobtrusive:** Runs entirely in the background with zero visible windows.
- **Auto-Start:** Automatically launches on Windows startup with required admin privileges (no annoying UAC prompts).
- **Customizable:** Easily configure your specific network adapter name during installation.
- **Smart Tooltips:** Hover over the tray icon to see the current adapter speed.

## Installation
1. Go to the **Releases** page on the right side of this GitHub repository.
2. Download the latest `NetworkSpeedToggle_Installer.exe`.
3. Run the installer and follow the instructions to specify your network adapter name (e.g., "Ethernet").
4. The app will automatically start and sit in your system tray.

## Support the Project
If this tool helped you fix your Moonlight streaming stutters or made your network management easier, consider buying me a coffee! ☕

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-blue.svg)](https://paypal.me/foggypunk)

## License

![License](https://img.shields.io/badge/License-MIT-green.svg)


