using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace BigWalkVRInstaller.Services
{
    public static class SelfUpdater
    {
        const string OldSuffix = ".old.exe";

        public static string CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public static string ExePath => Process.GetCurrentProcess().MainModule.FileName;

        // delete leftover renamed exe from a previous update
        public static void CleanupOldExe()
        {
            try
            {
                foreach (var old in Directory.GetFiles(Path.GetDirectoryName(ExePath), "*" + OldSuffix))
                    File.Delete(old);
            }
            catch { } // still locked by exiting process, next launch gets it
        }

        public static bool IsUpdateAvailable(ReleaseInfo mgr) =>
            mgr != null && !string.IsNullOrEmpty(mgr.url) && VersionUtil.IsNewer(mgr.version, CurrentVersion);

        // rename-swap: running exe can be renamed but not overwritten
        public static async Task ApplyUpdate(ReleaseInfo mgr, IProgress<double> progress = null)
        {
            var bytes = await RepoClient.Download(mgr.url, mgr.sha256, progress);
            var exe = ExePath;
            var old = Path.ChangeExtension(exe, null) + OldSuffix;
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.WriteAllBytes(exe, bytes);
            }
            catch
            {
                File.Move(old, exe); // roll back rename
                throw;
            }
            Process.Start(exe);
        }
    }
}
