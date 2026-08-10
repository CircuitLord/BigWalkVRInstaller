using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BigWalkVRInstaller
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Notify(name);
        }

        protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // manifest-v2.json shapes
    public class Manifest
    {
        public int schemaVersion;
        public string name;
        public ReleaseInfo installer;
        public ReleaseInfo bepinex;
        public List<ManifestMod> mods = new List<ManifestMod>();
    }

    public class ReleaseInfo
    {
        public string version;
        public string url;
        public string sha256;
    }

    public class ManifestMod
    {
        public string id;
        public string name;
        public string author;
        public string version;
        public string description;
        public string url;
        public string sha256;
        public long size;
        // the headline mod, everything else lists as an optional add-on
        public bool core;
        // files kept if they already exist, user calibration and configs
        public List<string> preserve = new List<string>();
        // files with {{GAMEDIR}} / {{GAMEDIR_JSON}} placeholders filled in on install
        public List<string> tokenize = new List<string>();
    }

    // written into the game folder so installs are tracked per game install
    public class InstallRecord
    {
        public string id;
        public string version;
        public string runtime;
        public List<string> files = new List<string>();
    }

    public class ModEntry : ObservableObject
    {
        public ManifestMod Remote;

        public string Id => Remote.id;
        public string Name => string.IsNullOrEmpty(Remote.name) ? Remote.id : Remote.name;
        public string Description => Remote.description;
        public bool HasDescription => !string.IsNullOrEmpty(Remote.description);

        string _installedVersion;
        public string InstalledVersion
        {
            get => _installedVersion;
            set { Set(ref _installedVersion, value); NotifyState(); }
        }

        bool _busy;
        public bool Busy { get => _busy; set { Set(ref _busy, value); NotifyState(); } }

        double _progress;
        public double Progress { get => _progress; set => Set(ref _progress, value); }

        string _busyText;
        public string BusyText { get => _busyText; set => Set(ref _busyText, value); }

        public bool IsInstalled => InstalledVersion != null;
        public bool CanUpdate => IsInstalled && VersionUtil.IsNewer(Remote.version, InstalledVersion);
        public bool IsCurrent => IsInstalled && !CanUpdate;

        public bool ShowInstall => !Busy && !IsInstalled;
        public bool ShowUpdate => !Busy && CanUpdate;
        public bool ShowCurrent => !Busy && IsCurrent;
        public bool ShowUninstall => !Busy && IsInstalled;

        public string Subtitle
        {
            get
            {
                var author = string.IsNullOrEmpty(Remote.author) ? "unknown" : Remote.author;
                var version = !IsInstalled ? "Not installed" : CanUpdate ? $"v{InstalledVersion} → v{Remote.version}" : $"v{InstalledVersion}";
                var size = Remote.size > 0 ? $"  •  {FormatSize(Remote.size)}" : "";
                return $"{author}  •  {version}{size}";
            }
        }

        static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        public string UpdateLabel => $"Update to v{Remote.version}";

        void NotifyState()
        {
            foreach (var name in new[] { nameof(IsInstalled), nameof(CanUpdate), nameof(IsCurrent), nameof(ShowInstall),
                nameof(ShowUpdate), nameof(ShowCurrent), nameof(ShowUninstall), nameof(Subtitle) })
                Notify(name);
        }
    }

    public static class VersionUtil
    {
        public static bool IsNewer(string remote, string local) => Parse(remote) > Parse(local);

        static Version Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new Version(0, 0, 0, 0);
            s = s.Trim().TrimStart('v', 'V');
            var core = new string(s.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
            if (!core.Contains(".")) core += ".0";
            if (!Version.TryParse(core, out var v)) return new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }
    }
}
