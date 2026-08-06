# Big Walk VR Installer

### NOTE: The mod is not done yet, join the [Discord](https://discord.gg/MTKwud2cCP) to get updates when it releases!

...

Big Walk VR adds full multiplayer-compatible SteamVR support to the game Big Walk by House House. It includes stereo rendering support, full 6dof motion controls with support for grabbing and throwing objects, and more!

This is a utility to automatically install the VR mod and associated files, and keep them up-to-date.


**[Download BigWalkVRInstaller](https://github.com/CircuitLord/BigWalkVRInstaller/releases/download/dist/BigWalkVRInstaller.exe)**

## What it does

1. **Finds Big Walk** through your Steam install.
2. **Sets up MelonLoader**, the mod loader Big Walk VR depends on.
3. **Installs the mod**, plus any optional add-ons.

Then start SteamVR and press Launch in VR.

## Building from source

Needs the .NET Framework 4.8 SDK.

```
dotnet build src/Installer -c Release
```

Output is a single self-contained `src/Installer/bin/Release/net48/BigWalkVRInstaller.exe`. Costura merges the dependencies into it.

## How it works

`manifest.json` in this repo lists the installer version, the MelonLoader build to use, and every available mod, each with a download URL and a SHA-256 hash. The app reads it on startup, compares against what's installed, and shows Install or Update accordingly.

A mod package is a zip that mirrors the Big Walk folder, so installing is extract-in-place. Each entry can declare:

- `core`: the headline mod. Everything else lists under Optional add-ons.
- `preserve`: files left alone if they already exist, so your calibration and configs survive updates.
- `tokenize`: files where `{{GAMEDIR}}` and `{{GAMEDIR_JSON}}` are replaced with your game folder on install, used for the SteamVR app manifest.

Every install records the exact list of files it wrote to `<game>\UserData\BigWalkVRInstaller\<id>.json`. Updates delete files the previous version shipped that the new one no longer does, and uninstall removes exactly what was recorded, nothing else.

## License

MIT, see [LICENSE](LICENSE). Bundled dependencies are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Reporting a problem

Open an issue with what you did and what happened. The Logs tab in the app has the installer's own history, and the Mod logs button opens `MelonLoader\Latest.log` in your game folder, which is where the mod itself reports errors.
