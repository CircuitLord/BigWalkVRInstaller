<h1 align="center">CircuitLord's VR Mods Installer</h1>

Install and manage CircuitLord's VR mods from one app.

## Supported mods

### Big Walk VR

- Locates Big Walk through Steam
- Installs BepInEx and Big Walk VR
- Preserves stable and beta updates, optional add-ons, non-VR launch, crash reports, and restore-to-vanilla

### Titanfall 2 VR

- Locates Steam and EA installations
- Downloads pinned Northstar v1.31.13 and verifies SHA-256
- Installs Northstar into `<Titanfall2>\TF2VR`
- Installs `Titanfall2VRLauncher.exe` and `TF2VR\plugins\Titanfall2VR.dll`
- Launches `Titanfall2VRLauncher.exe -profile=TF2VR` from the Titanfall 2 directory
- Leaves `NorthstarLauncher.exe` and `R2Northstar` untouched
- Tracks owned files for stable and beta updates and uninstall
- Packages the latest Northstar log, minidump, and focused VR diagnostics for crash reports

Join the [Discord](https://discord.gg/MTKwud2cCP) for support and feedback.

## Building

Requires the .NET Framework 4.8 SDK.

```text
dotnet build BigWalkVRInstaller.sln -c Release
```

The installer is written to `src/Installer/bin/Release/net48/CircuitLordsVRModsInstaller.exe`.

Run the install isolation validation with:

```text
tests/InstallerValidation/bin/Release/net48/InstallerValidation.exe
```

## Packages

`manifest-v2.json` pins download URLs and SHA-256 hashes. Big Walk packages mirror its game directory. The Titanfall package contains `Titanfall2VR.dll`; its installer class handles the Northstar profile layout.

Big Walk install records remain in `<game>\UserData\BigWalkVRInstaller`. Titanfall records are stored in `<game>\.circuitlord-vr-mods`.

The shared `repotools/publish-package.ps1` publishes immutable stable or beta assets to the `mod-packages` release. Use `repotools/publish-installer-preview.ps1 -NoPush` to validate the separate preview installer release locally.

## License

MIT. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Support

https://ko-fi.com/circuitlord
