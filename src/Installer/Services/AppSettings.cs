using System;
using System.IO;
using Newtonsoft.Json;

namespace BigWalkVRInstaller.Services
{
    public class AppSettings
    {
        public string GamePath;
        public string ManifestUrl = "https://raw.githubusercontent.com/CircuitLord/BigWalkVRInstaller/main/manifest.json";

        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BigWalkVRInstaller");
        static string FilePath => Path.Combine(Dir, "settings.json");

        public static string LoadError;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
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
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
