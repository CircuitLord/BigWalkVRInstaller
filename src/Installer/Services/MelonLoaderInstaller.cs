using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace BigWalkVRInstaller.Services
{
    public static class MelonLoaderInstaller
    {
        // used when the manifest doesn't pin a version
        public static readonly ReleaseInfo Fallback = new ReleaseInfo
        {
            version = "0.7.3",
            url = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip",
            sha256 = "5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416",
        };

        public static bool IsInstalled(string gamePath) =>
            !string.IsNullOrEmpty(gamePath) &&
            File.Exists(Path.Combine(gamePath, "version.dll")) &&
            Directory.Exists(Path.Combine(gamePath, "MelonLoader"));

        public static string InstalledVersion(string gamePath)
        {
            var dll = Path.Combine(gamePath, "MelonLoader", "net6", "MelonLoader.dll");
            if (!File.Exists(dll)) return null;
            var version = FileVersionInfo.GetVersionInfo(dll).FileVersion;
            return Version.TryParse(version, out var parsed) ? parsed.ToString(3) : version;
        }

        // everything MelonLoader ships or generates, the game itself owns none of these
        static readonly string[] Files = { "version.dll", "dobby.dll", "NOTICE.txt" };
        static readonly string[] Dirs = { "MelonLoader", "Mods", "Plugins", "UserLibs", "UserData" };

        public static void Remove(string gamePath)
        {
            foreach (var name in Files)
            {
                var path = Path.Combine(gamePath, name);
                if (File.Exists(path)) File.Delete(path);
            }
            foreach (var name in Dirs)
            {
                var path = Path.Combine(gamePath, name);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }

        public static void Extract(string gamePath, byte[] zipBytes)
        {
            using (var stream = new MemoryStream(zipBytes))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name.Length == 0) continue;
                    var dest = Path.Combine(gamePath, entry.FullName.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                }
            }
            Directory.CreateDirectory(Path.Combine(gamePath, "Mods"));
        }
    }
}
