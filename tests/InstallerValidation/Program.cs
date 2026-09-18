using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using BigWalkVRInstaller;
using BigWalkVRInstaller.Installers;
using BigWalkVRInstaller.Services;

namespace InstallerValidation
{
    static class Program
    {
        static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "CircuitLordInstallerValidation-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "R2Northstar"));
                File.WriteAllText(Path.Combine(root, "Titanfall2.exe"), "game");
                File.WriteAllText(Path.Combine(root, "NorthstarLauncher.exe"), "standard-launcher");
                File.WriteAllText(Path.Combine(root, "R2Northstar", "Northstar.dll"), "standard-profile");

                var sameBeta = new ManifestMod { version = "0.1.0", beta = new ModRelease { version = "0.1.0" } };
                var newerBeta = new ManifestMod { version = "0.1.0", beta = new ModRelease { version = "0.2.0" } };
                Assert(!sameBeta.HasNewerBeta, "stable-equivalent beta was available");
                Assert(newerBeta.HasNewerBeta, "newer beta was unavailable");

                var installer = new Titanfall2Installer(new AppSettings { Titanfall2Path = root });
                var release = new ManifestMod { version = "0.1.0" };
                installer.Install(NorthstarPackage("vr-launcher-v1", true), release, ModPackage("vr-plugin-v1"), false);

                Assert(File.ReadAllText(Path.Combine(root, "NorthstarLauncher.exe")) == "standard-launcher", "standard launcher changed");
                Assert(File.ReadAllText(Path.Combine(root, "R2Northstar", "Northstar.dll")) == "standard-profile", "standard profile changed");
                Assert(File.ReadAllText(Path.Combine(root, "Titanfall2VRLauncher.exe")) == "vr-launcher-v1", "renamed launcher missing");
                Assert(File.ReadAllText(Path.Combine(root, "TF2VR", "Northstar.dll")) == "vr-profile", "VR profile missing");
                Assert(File.ReadAllText(Path.Combine(root, "TF2VR", "plugins", "Titanfall2VR.dll")) == "vr-plugin-v1", "VR plugin missing");

                var launch = Titanfall2Installer.CreateLaunchInfo(root);
                Assert(launch.FileName == Path.Combine(root, "Titanfall2VRLauncher.exe"), "wrong launcher path");
                Assert(launch.Arguments == "-profile=TF2VR", "wrong profile argument");
                Assert(launch.WorkingDirectory == root, "wrong working directory");

                var userFile = Path.Combine(root, "TF2VR", "save_data", "user.json");
                Directory.CreateDirectory(Path.GetDirectoryName(userFile));
                File.WriteAllText(userFile, "user-data");
                installer.Install(NorthstarPackage("vr-launcher-v2", false), new ManifestMod { version = "0.2.0" }, ModPackage("vr-plugin-v2"), true);
                Assert(!File.Exists(Path.Combine(root, "TF2VR", "plugins", "ranim.dll")), "stale owned file survived update");
                Assert(File.ReadAllText(userFile) == "user-data", "user file changed during update");
                Assert(installer.Record.beta, "beta channel was not recorded");

                var logs = Path.Combine(root, "TF2VR", "logs");
                var diagnostics = Path.Combine(root, "TF2VR", "plugins", "Titanfall2VR-data");
                Directory.CreateDirectory(logs);
                Directory.CreateDirectory(diagnostics);
                File.WriteAllText(Path.Combine(logs, "nslog-test.txt"), "northstar-log");
                File.WriteAllText(Path.Combine(logs, "nsdump-test.dmp"), "minidump");
                File.WriteAllText(Path.Combine(diagnostics, "engine.txt"), "engine-log");
                File.WriteAllText(Path.Combine(diagnostics, "events.txt"), "events");
                var report = CrashReportService.CreateTitanfall(root, root);
                using (var archive = ZipFile.OpenRead(report))
                {
                    Assert(archive.GetEntry("Northstar/nslog-test.txt") != null, "Northstar log missing from crash report");
                    Assert(archive.GetEntry("Northstar/nsdump-test.dmp") != null, "minidump missing from crash report");
                    Assert(archive.GetEntry("Titanfall2VR/engine.txt") != null, "engine log missing from crash report");
                    Assert(archive.GetEntry("report.json") != null, "crash metadata missing from report");
                }

                installer.Uninstall();
                Assert(!File.Exists(Path.Combine(root, "Titanfall2VRLauncher.exe")), "renamed launcher survived uninstall");
                Assert(!File.Exists(Path.Combine(root, "TF2VR", "Northstar.dll")), "VR profile survived uninstall");
                Assert(File.ReadAllText(userFile) == "user-data", "user file changed during uninstall");
                Assert(File.ReadAllText(Path.Combine(root, "NorthstarLauncher.exe")) == "standard-launcher", "standard launcher changed after uninstall");
                Assert(File.ReadAllText(Path.Combine(root, "R2Northstar", "Northstar.dll")) == "standard-profile", "standard profile changed after uninstall");

                Console.WriteLine("validated TF2VR install, beta updates, crash reports, uninstall, and profile switching");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        static byte[] NorthstarPackage(string launcher, bool includeRanim)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    Add(archive, "NorthstarLauncher.exe", launcher);
                    Add(archive, "R2Northstar/Northstar.dll", "vr-profile");
                    if (includeRanim) Add(archive, "R2Northstar/plugins/ranim.dll", "ranim");
                }
                return stream.ToArray();
            }
        }

        static byte[] ModPackage(string plugin)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    Add(archive, "Titanfall2VR.dll", plugin);
                return stream.ToArray();
            }
        }

        static void Add(ZipArchive archive, string path, string contents)
        {
            var entry = archive.CreateEntry(path);
            using (var output = entry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(contents);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
