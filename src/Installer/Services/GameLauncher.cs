using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BigWalkVRInstaller.Services
{
    public static class GameLauncher
    {
        public const string ProcessName = "Big Walk";

        public static bool IsRunning() => Process.GetProcessesByName(ProcessName).Any();

        public static void LaunchNonVr(string gamePath) => Launch(gamePath, "");

        public static void LaunchVr(string gamePath) => Launch(gamePath, "-bigwalkvr-vr -force-d3d11");

        // steam has to own the launch so the game gets its app context
        static void Launch(string gamePath, string arguments)
        {
            var steam = GameLocator.SteamExePath();
            if (steam != null)
            {
                Process.Start(steam, $"-applaunch {GameLocator.AppId} {arguments}");
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(gamePath, GameLocator.ExeName),
                Arguments = arguments,
                WorkingDirectory = gamePath,
            });
        }

        public static void OpenFolder(string path)
        {
            if (!Directory.Exists(path)) throw new Exception($"folder not found: {path}");
            Process.Start("explorer.exe", $"\"{path}\"");
        }

        public static void SelectFile(string path) => Process.Start("explorer.exe", $"/select,\"{path}\"");

        public static void OpenUrl(string url) => Process.Start(url);
    }
}
