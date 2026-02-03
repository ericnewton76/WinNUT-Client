# WinNUT-Client

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate?hosted_button_id=FAFJ3ZKMENGCU)

A NUT (Network UPS Tools) client for Windows, built with .NET 8 and Avalonia UI.

## Features

- Monitor UPS status via NUT protocol
- System tray integration with minimize/close to tray
- Automatic shutdown on low battery or runtime thresholds
- Windows toast notifications for connection events
- Auto-connect on startup
- Start with Windows option
- Cross-platform compatible (Windows, Linux, macOS via Avalonia)

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```bash
cd WinNUT_V2/WinNUT_GUI
dotnet build
```

### Run

```bash
dotnet run
```

### Publish

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Configuration

Settings are stored in JSON format at:
- **Windows:** `%APPDATA%\WinNUT-Client\settings.json`
- **Linux/macOS:** `~/.config/WinNUT-Client/settings.json`

### Command Line Options

- `--config <path>` or `-c <path>`: Use a custom configuration file path

### Legacy Migration

If upgrading from WinNUT v1.x, place your `ups.ini` file in the configuration directory for automatic import on first launch.

## Installation

### From Releases
1. Download the latest release from [Releases](https://github.com/ericnewton76/WinNUT-Client/releases)
2. Extract and run `WinNUT-Client.exe`

### For Synology NAS Users
If your NUT server is hosted on a Synology NAS, use the default credentials:
- **Login:** `upsmon`
- **Password:** `secret`

You may need to allow the WinNUT-Client IP in your NUT server configuration.

## Third Party Components

WinNUT-Client uses:
- [Avalonia UI](https://avaloniaui.net/) - Cross-platform .NET UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit
- [NLog](https://nlog-project.org/) - Logging framework
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json) - JSON serialization

## License

WinNUT-Client is a NUT Windows client for monitoring your UPS.
Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

## Donation

If you want to support this project:

[![paypal](https://www.paypalobjects.com/en_US/FR/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate?hosted_button_id=FAFJ3ZKMENGCU)
