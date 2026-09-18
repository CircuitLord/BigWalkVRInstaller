namespace BigWalkVRInstaller.Installers
{
    public interface IVrModInstaller
    {
        string Id { get; }
        string Name { get; }
        string GamePath { get; }
        bool HasGame { get; }
        bool IsInstalled { get; }
        string InstalledVersion { get; }
        string DetectGamePath();
        void SetGamePath(string path);
        void Uninstall();
        void Play();
    }
}
