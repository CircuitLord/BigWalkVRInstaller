using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace BigWalkVRInstaller.Services
{
    public static class CrashReportService
    {
        public static string Create(string gamePath)
        {
            var logs = new Dictionary<string, string>
            {
                ["BepInEx-LogOutput.log"] = Path.Combine(gamePath, "BepInEx", "LogOutput.log"),
                ["Unity-Player.log"] = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "House House", "Big Walk", "Player.log")
            };

            if (!logs.Any(log => File.Exists(log.Value))) throw new Exception("No Big Walk log files were found.");

            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"BigWalkVR-CrashReport-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

            using (var stream = File.Create(reportPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var log in logs)
                    if (File.Exists(log.Value)) archive.CreateEntryFromFile(log.Value, log.Key, CompressionLevel.Optimal);
            }

            return reportPath;
        }
    }
}
