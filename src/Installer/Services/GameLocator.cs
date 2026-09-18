namespace BigWalkVRInstaller.Services
{
    public static class GameLocator
    {
        public const string AppId = "1478500";
        public const string ExeName = "Big Walk.exe";
        public const string DataDir = "Big Walk_Data";

        static readonly SteamGameLocator Locator = new SteamGameLocator("Big Walk", ExeName);

        public static bool IsValidGamePath(string path) => Locator.IsValid(path);

        public static string DetectGamePath() => Locator.Detect();

        public static string Canonical(string path) => SteamGameLocator.Canonical(path);

        public static string SteamExePath() => SteamGameLocator.SteamExePath();
    }
}
