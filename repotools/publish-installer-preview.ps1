param([switch]$NoPush)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

if (!$NoPush -and (Read-Native git @("-C", $PublicDir, "status", "--porcelain"))) {
    throw "commit the installer repo before publishing"
}

$project = Join-Path $PublicDir "src\Installer\Installer.csproj"
Write-Host "building CircuitLord's VR Mods Installer..."
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }

$exe = Join-Path $PublicDir "src\Installer\bin\Release\net48\CircuitLordsVRModsInstaller.exe"
if (!(Test-Path $exe)) { throw "installer output is missing" }
$version = ([version](Get-Item $exe).VersionInfo.FileVersion).ToString(3)
$tag = "vr-mods-installer-v$version"
$url = Release-AssetUrl $tag (Split-Path $exe -Leaf)
if ($NoPush) {
    Write-Host "prepared installer v$version"
    Write-Host "release URL: $url"
    return
}

Publish-ReleaseAsset -Path $exe -Tag $tag -Title "CircuitLord's VR Mods Installer v$version" -Notes "Preview release of CircuitLord's multi-game VR mod installer." -Replace
Write-Host "published installer: $url"
