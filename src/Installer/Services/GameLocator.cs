using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BigWalkVRInstaller.Services
{
    public static class GameLocator
    {
        public const string AppId = "1478500";
        public const string ExeName = "Big Walk.exe";
        public const string DataDir = "Big Walk_Data";

        public static bool IsValidGamePath(string path) =>
            !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, ExeName));

        public static string DetectGamePath()
        {
            foreach (var library in SteamLibraries())
            {
                var candidate = Path.Combine(library, "steamapps", "common", "Big Walk");
                if (IsValidGamePath(candidate)) return Canonical(candidate);
            }
            return null;
        }

        // registry steam path is lowercase with forward slashes, so rebuild it with the real on-disk casing
        public static string Canonical(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                var dir = new DirectoryInfo(Path.GetFullPath(path));
                var parts = new Stack<string>();
                while (dir.Parent != null)
                {
                    // keep the typed segment if it isn't on disk, one missing folder shouldn't drop the whole fix
                    parts.Push(dir.Parent.EnumerateDirectories(dir.Name).FirstOrDefault()?.Name ?? dir.Name);
                    dir = dir.Parent;
                }
                var result = dir.Name.ToUpperInvariant();
                foreach (var part in parts) result = Path.Combine(result, part);
                return result;
            }
            catch { return path; }
        }

        public static string SteamExePath()
        {
            var steam = SteamRoot();
            if (steam == null) return null;
            var exe = Path.Combine(steam, "steam.exe");
            return File.Exists(exe) ? exe : null;
        }

        static string SteamRoot()
        {
            var fromRegistry = RegistryValue(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
                ?? RegistryValue(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(fromRegistry) && Directory.Exists(fromRegistry)) return fromRegistry;

            var fallback = @"C:\Program Files (x86)\Steam";
            return Directory.Exists(fallback) ? fallback : null;
        }

        static string RegistryValue(RegistryKey hive, string subKey, string name)
        {
            try
            {
                using (var key = hive.OpenSubKey(subKey))
                    return key?.GetValue(name) as string;
            }
            catch { return null; }
        }

        // steam root plus every library listed in libraryfolders.vdf
        static IEnumerable<string> SteamLibraries()
        {
            var steam = SteamRoot();
            if (steam == null) yield break;
            yield return steam;

            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) yield break;

            string text;
            try { text = File.ReadAllText(vdf); }
            catch { yield break; }

            foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"(.+?)\""))
            {
                var path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(path)) yield return path;
            }
        }
    }
}
