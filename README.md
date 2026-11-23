# XakaLauncher

A Windows Minecraft launcher project with a dark purple / neon visual style.

## Current milestone — 0.1 UI foundation

- WPF desktop application targeting .NET 8.
- Nickname field for an offline profile.
- Minecraft version and loader selectors (UI foundation).
- Game directory picker.
- RAM allocation control.
- Java discovery and version check.
- No HWID collection or telemetry.
- GitHub Actions workflow publishes a self-contained Windows x64 executable.

## Build

On Windows with .NET 8 SDK:

```powershell
dotnet publish XakaLauncher/XakaLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Roadmap

1. UI foundation and Java detection.
2. Official Minecraft version manifest, game files, libraries and assets installation.
3. Fabric and Forge profile installation.
4. Offline profile launch for single-player/local testing and servers that explicitly allow offline-mode accounts.
5. Settings persistence, logs and release packaging.

## Important

This first milestone is the launcher UI and Java detection only. Minecraft version installation, Fabric/Forge profile installation, asset/library management, and actual game launch are not implemented yet. Offline nicknames do not replace Microsoft authentication on online-mode servers.
