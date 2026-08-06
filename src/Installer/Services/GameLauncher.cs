using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BigWalkVRInstaller.Services
{
    public static class GameLauncher
    {
        public static bool IsRunning() => Process.GetProcessesByName("Big Walk").Any();

        // steam has to own the launch so the game gets its app context, -force-d3d11 is required by the XR provider
        public static void LaunchVr(string gamePath)
        {
            var steam = GameLocator.SteamExePath();
            if (steam != null)
            {
                Process.Start(steam, $"-applaunch {GameLocator.AppId} -force-d3d11");
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(gamePath, GameLocator.ExeName),
                Arguments = "-force-d3d11",
                WorkingDirectory = gamePath,
            });
        }

        public static void OpenFolder(string path)
        {
            if (!Directory.Exists(path)) throw new Exception($"folder not found: {path}");
            Process.Start("explorer.exe", $"\"{path}\"");
        }

        public static void OpenUrl(string url) => Process.Start(url);
    }
}
