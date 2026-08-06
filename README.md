# Big Walk VR Installer

Installs the Big Walk VR mod in a couple of clicks. No unzipping, no dragging files into game folders.

**[Download BigWalkVRInstaller.exe](https://github.com/CircuitLord/BigWalkVRInstaller/releases/download/dist/BigWalkVRInstaller.exe)**

Run it and follow the three steps. It keeps itself and the mod up to date, and can put your game back to vanilla whenever you want.

## What it does

1. **Finds Big Walk** through your Steam install. If it guesses wrong, point it at the folder yourself.
2. **Sets up MelonLoader**, the mod loader Big Walk VR runs on. One click.
3. **Installs the mod**, plus any optional add-ons.

Then start SteamVR and press Launch in VR.

## About the mod

Big Walk VR adds full VR support to the game Big Walk by House House. It includes stereo rendering support, full 6dof motion controls with support for grabbing and throwing objects, and more!

It needs a SteamVR-compatible headset. Multiplayer works with anyone, although tracked hands and props only show up for other players who also have the mod.


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

## Reporting a problem

Open an issue with what you did and what happened. The Logs tab in the app has the installer's own history, and the Mod logs button opens `MelonLoader\Latest.log` in your game folder, which is where the mod itself reports errors.
