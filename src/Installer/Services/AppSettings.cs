using System;
using System.IO;

namespace BigWalkVRInstaller.Services
{
    public class AppSettings
    {
        public const string ManifestUrl = "https://raw.githubusercontent.com/CircuitLord/BigWalkVRInstaller/main/manifest-v2.json";
        public string GamePath;
        public bool EnableBetaUpdates;

        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BigWalkVRInstaller");
        static string FilePath => Path.Combine(Dir, "settings.json");

        public static string LoadError;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtil.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonUtil.Serialize(this));
        }
    }
}
