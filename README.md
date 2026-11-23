# XakaLauncher

Windows Minecraft launcher project with a dark purple / neon visual style.

## Current milestone — 0.1 launcher foundation

- WPF desktop application targeting .NET 8.
- Nickname field for an offline profile (only for local play and servers that allow offline-mode accounts).
- Minecraft version and loader selectors (1.21.4, 1.21.1, 1.20.1; Vanilla, Fabric, Forge).
- Game directory picker.
- RAM allocation control.
- Java discovery and version check.
- Local settings persistence for nickname, version, loader, RAM and game directory.
- Quick buttons to open the selected game's `mods` and `resourcepacks` folders.
- Modrinth catalog search for mods and resource packs, with version/loader filtering and downloads into the selected game directory.
- Download filename collision protection; existing files are not intentionally overwritten.
- No HWID collection or telemetry.

## Build

On Windows with .NET 8 SDK:

```powershell
dotnet publish XakaLauncher/XakaLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

GitHub Actions also builds a self-contained Windows x64 executable on pushes to `main`.

## Roadmap

1. Complete and verify the launcher UI, settings and Modrinth integration.
2. Official Minecraft version manifest, game files, libraries and assets installation.
3. Fabric and Forge profile installation.
4. Game launch with clear authentication limitations.
5. Logs, diagnostics and release packaging.

## Security and account notes

- The launcher does not collect HWID or send telemetry.
- Modrinth project moderation is not a guarantee that every downloaded file is harmless. Review projects and only download content you trust.
- Offline nicknames do not replace Microsoft authentication on online-mode servers.
- Minecraft installation and actual game launch are not implemented yet.
