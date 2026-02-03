# Build Instructions

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Building from Source

### Clone the Repository

```bash
git clone https://github.com/ericnewton76/WinNUT-Client.git
cd WinNUT-Client
```

### Build

```bash
cd WinNUT_V2/WinNUT_GUI
dotnet build
```

### Run (Development)

```bash
dotnet run
```

### Run Tests

```bash
dotnet test
```

## Publishing

### Windows (Self-Contained)

```bash
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win-x64
```

### Windows (Framework-Dependent)

Requires .NET 8 runtime installed on target machine:

```bash
dotnet publish -c Release -r win-x64 --no-self-contained -o ./publish/win-x64-fdd
```

### Linux

```bash
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux-x64
```

### macOS

```bash
dotnet publish -c Release -r osx-x64 --self-contained -o ./publish/osx-x64
```

For Apple Silicon (M1/M2):

```bash
dotnet publish -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
```

## Project Structure

```
WinNUT-Client/
├── WinNUT_V2/
│   └── WinNUT_GUI/           # Main application project
│       ├── Assets/           # Icons and images
│       ├── Services/         # Business logic (UPS communication, settings, etc.)
│       ├── ViewModels/       # MVVM view models
│       ├── Views/            # Avalonia XAML views
│       ├── App.axaml         # Application definition
│       ├── Program.cs        # Entry point
│       └── NLog.config       # Logging configuration
├── docs/                     # Documentation
└── README.md
```

## Dependencies

The project uses the following NuGet packages:

| Package | Purpose |
|---------|---------|
| Avalonia | Cross-platform UI framework |
| Avalonia.Desktop | Desktop platform support |
| Avalonia.Themes.Fluent | Fluent design theme |
| CommunityToolkit.Mvvm | MVVM toolkit with source generators |
| NLog | Logging framework |
| System.Management | Windows management (for shutdown) |

## Development Notes

### IDE Support

- **Visual Studio 2022** — Full support with Avalonia extension
- **JetBrains Rider** — Excellent Avalonia support built-in
- **VS Code** — Use with C# Dev Kit and Avalonia extensions

### Avalonia Previewer

To see XAML previews in your IDE, install the Avalonia extension:
- Visual Studio: "Avalonia for Visual Studio"
- Rider: Built-in support
- VS Code: "Avalonia for VS Code"

### Debugging

Logs are written to:
- **Windows:** `%APPDATA%\WinNUT-Client\logs\`
- **Linux/macOS:** `~/.config/WinNUT-Client/logs/`

Set log level in Preferences or edit `NLog.config` for detailed debugging.
