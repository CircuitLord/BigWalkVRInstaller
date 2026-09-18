using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BigWalkVRInstaller.Services;

namespace BigWalkVRInstaller.Installers
{
    public sealed class Titanfall2Installer : IVrModInstaller
    {
        public const string InstallerId = "Titanfall2VR";
        public const string NorthstarVersion = "1.31.13";
        public const string NorthstarUrl = "https://github.com/R2Northstar/Northstar/releases/download/v1.31.13/Northstar.release.v1.31.13.zip";
        public const string NorthstarSha256 = "e622b96e7609912060a61ba3eed382eeafd6cc7b62c105ea609a36bc36e75322";
        public const long NorthstarSize = 107146100;
        public const string LauncherName = "Titanfall2VRLauncher.exe";
        public const string ProfileName = "TF2VR";

        static readonly SteamGameLocator Locator = new SteamGameLocator(
            "Titanfall2", "Titanfall2.exe", @"SOFTWARE\Respawn\Titanfall2", "Install Dir");

        readonly AppSettings _settings;

        public Titanfall2Installer(AppSettings settings) => _settings = settings;

        public string Id => "titanfall-2-vr";
        public string Name => "Titanfall 2 VR";
        public string GamePath => _settings.Titanfall2Path;
        public bool HasGame => Locator.IsValid(GamePath);
        public InstallRecord Record => HasGame ? OwnedFileStore.Read(GamePath, InstallerId) : null;
        public bool IsInstalled => Record != null
            && File.Exists(Path.Combine(GamePath, LauncherName))
            && File.Exists(Path.Combine(GamePath, ProfileName, "Northstar.dll"))
            && File.Exists(Path.Combine(GamePath, ProfileName, "plugins", "Titanfall2VR.dll"));
        public string InstalledVersion => IsInstalled ? Record.version : null;

        public static ReleaseInfo PinnedNorthstar => new ReleaseInfo
        {
            version = NorthstarVersion,
            url = NorthstarUrl,
            sha256 = NorthstarSha256,
            size = NorthstarSize
        };

        public string DetectGamePath()
        {
            var path = Locator.Detect();
            if (path != null) SetGamePath(path);
            return path;
        }

        public void SetGamePath(string path)
        {
            if (!Locator.IsValid(path)) throw new Exception("That folder doesn't contain Titanfall2.exe");
            _settings.Titanfall2Path = SteamGameLocator.Canonical(path);
            _settings.Save();
        }

        public bool CanUpdate(ManifestMod modRelease, bool beta) => IsInstalled &&
            (Record.beta != beta
             || VersionUtil.IsNewer(modRelease.version, Record.version)
             || !string.Equals(Record.northstarVersion, NorthstarVersion, StringComparison.OrdinalIgnoreCase));

        public void Install(byte[] northstarPackage, ManifestMod modRelease, byte[] modPackage, bool beta)
        {
            var previous = Record;
            if (previous == null && (Directory.Exists(Path.Combine(GamePath, ProfileName)) || File.Exists(Path.Combine(GamePath, LauncherName))))
                throw new Exception("TF2VR already exists but is not owned by this installer");

            var written = new List<string>();
            using (var northstarStream = new MemoryStream(northstarPackage))
            using (var northstarArchive = new ZipArchive(northstarStream, ZipArchiveMode.Read))
            using (var modStream = new MemoryStream(modPackage))
            using (var modArchive = new ZipArchive(modStream, ZipArchiveMode.Read))
            {
                var launcher = northstarArchive.GetEntry("NorthstarLauncher.exe")
                    ?? throw new Exception("Northstar package is missing NorthstarLauncher.exe");
                const string sourcePrefix = "R2Northstar/";
                var profileEntries = northstarArchive.Entries
                    .Where(entry => entry.Name.Length > 0 && entry.FullName.StartsWith(sourcePrefix, StringComparison.Ordinal))
                    .ToList();
                if (!profileEntries.Any(entry => entry.FullName == "R2Northstar/Northstar.dll"))
                    throw new Exception("Northstar package is missing R2Northstar/Northstar.dll");

                var plugin = modArchive.Entries.SingleOrDefault(entry =>
                    string.Equals(entry.Name, "Titanfall2VR.dll", StringComparison.OrdinalIgnoreCase));
                if (plugin == null) throw new Exception("Titanfall 2 VR package is missing Titanfall2VR.dll");

                Extract(launcher, LauncherName, written);
                foreach (var entry in profileEntries)
                {
                    var relative = ProfileName + "/" + entry.FullName.Substring(sourcePrefix.Length);
                    Extract(entry, relative, written);
                }
                Extract(plugin, ProfileName + "/plugins/Titanfall2VR.dll", written);
            }

            if (previous?.files != null) InstallerFileSystem.RemoveStaleFiles(GamePath, previous.files, written);
            OwnedFileStore.Write(GamePath, new InstallRecord
            {
                id = InstallerId,
                version = modRelease.version,
                northstarVersion = NorthstarVersion,
                runtime = "Northstar",
                beta = beta,
                files = written.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            });
        }

        void Extract(ZipArchiveEntry entry, string relativePath, ICollection<string> written)
        {
            var normalized = InstallerFileSystem.Normalize(relativePath);
            var destination = InstallerFileSystem.ResolveInside(GamePath, normalized);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            entry.ExtractToFile(destination, true);
            written.Add(normalized);
        }

        public void Uninstall() => OwnedFileStore.Remove(GamePath, InstallerId);

        public static ProcessStartInfo CreateLaunchInfo(string gamePath) => new ProcessStartInfo
        {
            FileName = Path.Combine(gamePath, LauncherName),
            Arguments = "-profile=" + ProfileName,
            WorkingDirectory = gamePath,
            UseShellExecute = false
        };

        public void Play() => Process.Start(CreateLaunchInfo(GamePath));

        public static bool IsRunning() =>
            Process.GetProcessesByName("Titanfall2").Any() || Process.GetProcessesByName("Titanfall2VRLauncher").Any();
    }
}
