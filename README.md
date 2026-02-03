# WinNUT-Client

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate?hosted_button_id=FAFJ3ZKMENGCU)

A NUT (Network UPS Tools) client that protects your workstation by monitoring UPS devices and automatically responding to power events.

## What is WinNUT-Client?

WinNUT-Client connects to one or more NUT servers to monitor UPS status. When a UPS reports a critical condition (low battery, extended power outage, or forced shutdown signal), WinNUT-Client can automatically **hibernate, suspend, or shut down your workstation** to prevent data loss and allow a graceful system shutdown before battery power is exhausted.

This is essential for:
- **Workstations** that need protection during power outages
- **Machines powered by multiple UPS devices** where any UPS going critical should trigger a shutdown
- **Remote or unattended systems** that must safely power down when utility power fails

WinNUT-Client is a **client application** — it does not manage or control UPS hardware directly. It communicates with a NUT server (running on Linux, Synology NAS, FreeNAS, etc.) that is connected to your UPS.

## Features

- **Automatic Workstation Protection** — Shutdown, hibernate, or suspend when UPS reaches critical state
- **Multiple UPS Monitoring** — Monitor several UPS devices; trigger protection if any (or all) become critical
- **Configurable Thresholds** — Set battery percentage and runtime limits to trigger shutdown
- **System Tray Integration** — Runs quietly in the background with status indicator
- **Real-time UPS Data** — View battery charge, load, voltage, runtime, and more
- **Windows Toast Notifications** — Alerts for power events and connection issues
- **Auto-connect on Startup** — Automatically connect to configured UPS servers
- **Start with Windows** — Launch automatically when you log in
- **Cross-platform** — Built with Avalonia UI for Windows, Linux, and macOS

## How It Works

```
      UPS Server Machine                          Your Workstation
  ┌────────────────────────┐                 ┌────────────────────────┐
  │                        │                 │                        │
  │  ┌─────────────────┐   │   TCP/IP :3493  │   ┌────────────────┐   │
  │  │   UPS Device    │   │                 │   │  WinNUT-Client │   │
  │  │   (Hardware)    │   │ ═══════════════►│   │   (This App)   │   │
  │  └────────┬────────┘   │   NUT Protocol  │   └───────┬────────┘   │
  │           │            │                 │           │            │
  │      USB/Serial        │                 │   Shutdown/Hibernate   │
  │           │            │                 │           │            │
  │  ┌────────▼────────┐   │                 │   ┌───────▼────────┐   │
  │  │   NUT Server    │   │                 │   │    Windows/    │   │
  │  │  (Linux/NAS)    │   │                 │   │   Linux/macOS  │   │
  │  └─────────────────┘   │                 │   └────────────────┘   │
  │                        │                 │                        │
  └────────────────────────┘                 └────────────────────────┘
```

1. Your UPS is connected to a NUT server (e.g., Synology NAS, Raspberry Pi, Linux server)
2. WinNUT-Client connects to the NUT server over the network
3. When power fails, WinNUT-Client monitors battery status
4. When thresholds are reached, WinNUT-Client safely shuts down your workstation

## Installation

### From Releases
1. Download the latest release from [Releases](https://github.com/ericnewton76/WinNUT-Client/releases)
2. Extract and run `WinNUT-Client.exe`

### Building from Source

See [docs/BUILD_INSTRUCTIONS.md](docs/BUILD_INSTRUCTIONS.md) for detailed build instructions.

## Configuration

Settings are stored in JSON format at:
- **Windows:** `%APPDATA%\WinNUT-Client\settings.json`
- **Linux/macOS:** `~/.config/WinNUT-Client/settings.json`

### Command Line Options

- `--config <path>` or `-c <path>`: Use a custom configuration file path

### Shutdown Triggers

Configure when WinNUT-Client should protect your workstation:
- **Battery Charge Threshold** — Shutdown when battery falls below X% (default: 30%)
- **Runtime Threshold** — Shutdown when estimated runtime falls below X seconds (default: 120s)
- **Follow FSD** — Immediately respond to Forced Shutdown signals from NUT server

### For Synology NAS Users

If your NUT server is hosted on a Synology NAS, use the default credentials:
- **Login:** `upsmon`
- **Password:** `secret`

You may need to allow the WinNUT-Client IP in your NUT server configuration (`/etc/ups/upsd.users` or via Synology UI).

### Legacy Migration

If upgrading from WinNUT v1.x, place your `ups.ini` file in the configuration directory for automatic import on first launch.

## Third Party Components

WinNUT-Client uses:
- [Avalonia UI](https://avaloniaui.net/) — Cross-platform .NET UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM toolkit
- [NLog](https://nlog-project.org/) — Logging framework
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json) — JSON serialization

## License

WinNUT-Client is a NUT client for monitoring your UPS and protecting your workstation.
Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

## Donation

If you want to support this project:

[![paypal](https://www.paypalobjects.com/en_US/FR/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate?hosted_button_id=FAFJ3ZKMENGCU)
