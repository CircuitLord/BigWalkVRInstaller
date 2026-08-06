using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BigWalkVRInstaller.Services;

namespace BigWalkVRInstaller
{
    public class LogEntry
    {
        public string Time { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
    }

    public partial class MainWindow : Window
    {
        const string RepoUrl = "https://github.com/CircuitLord/BigWalkVRInstaller";

        readonly ObservableCollection<ModEntry> _core = new ObservableCollection<ModEntry>();
        readonly ObservableCollection<ModEntry> _optional = new ObservableCollection<ModEntry>();
        readonly ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        AppSettings _settings;
        ReleaseInfo _selfUpdate;
        ReleaseInfo _melonSource = MelonLoaderInstaller.Fallback;
        bool _melonBusy;

        public MainWindow()
        {
            InitializeComponent();
            CoreList.ItemsSource = _core;
            OptionalList.ItemsSource = _optional;
            LogsList.ItemsSource = _logs;
            VersionText.Text = "v" + SelfUpdater.CurrentVersion;
        }

        IEnumerable<ModEntry> AllMods => _core.Concat(_optional);

        async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = AppSettings.Load();
            if (AppSettings.LoadError != null)
                Status($"Settings load failed, using defaults: {AppSettings.LoadError}", true);

            // older settings files hold the raw registry path, fix the casing on the way in
            var stored = GameLocator.Canonical(_settings.GamePath);
            if (stored != _settings.GamePath)
            {
                _settings.GamePath = stored;
                _settings.Save();
            }

            if (!GameLocator.IsValidGamePath(_settings.GamePath))
            {
                _settings.GamePath = GameLocator.DetectGamePath();
                if (_settings.GamePath != null)
                {
                    _settings.Save();
                    Status($"Found Big Walk at {_settings.GamePath}");
                }
                else Status("Couldn't find Big Walk, press Change in step 1 to pick the folder", true);
            }

            await Refresh();
            var vr = _core.FirstOrDefault();
            if (vr == null) return;
            if (vr.CanUpdate) Status($"An update to v{vr.Remote.version} is available");
            else if (vr.IsInstalled) Status("Everything is up to date");
        }

        bool HasGame => GameLocator.IsValidGamePath(_settings?.GamePath);

        // ---- refresh ----

        async Task Refresh()
        {
            await FetchManifest();
            RefreshLocalState();
        }

        async Task FetchManifest()
        {
            // reuse live entries so an in-flight install keeps its progress bar across a refresh
            var previous = AllMods.ToDictionary(m => m.Id, m => m);
            _core.Clear();
            _optional.Clear();
            _selfUpdate = null;

            try
            {
                var manifest = await RepoClient.FetchManifest(_settings.ManifestUrl);
                foreach (var remote in manifest.mods)
                {
                    var entry = previous.TryGetValue(remote.id, out var live) && live.Busy ? live : new ModEntry { Remote = remote };
                    (remote.core ? _core : _optional).Add(entry);
                }
                if (manifest.melonLoader?.url != null) _melonSource = manifest.melonLoader;
                if (SelfUpdater.IsUpdateAvailable(manifest.installer)) _selfUpdate = manifest.installer;
            }
            catch (Exception ex)
            {
                Status($"Couldn't reach the download list: {ex.Message}", true);
            }

            OfflineNotice.Visibility = _core.Count + _optional.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            OptionalSection.Visibility = _optional.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SelfUpdateBanner.Visibility = _selfUpdate != null ? Visibility.Visible : Visibility.Collapsed;
            if (_selfUpdate != null)
                SelfUpdateTitle.Text = $"Installer update available: v{_selfUpdate.version}";
        }

        void RefreshLocalState()
        {
            foreach (var mod in AllMods)
                mod.InstalledVersion = HasGame ? PackageInstaller.ReadRecord(_settings.GamePath, mod.Id)?.version : null;
            UpdateSetupState();
        }

        void UpdateSetupState()
        {
            var melon = HasGame && MelonLoaderInstaller.IsInstalled(_settings.GamePath);

            GamePathText.Text = HasGame ? _settings.GamePath : "Not found. Press Change and pick your Big Walk folder.";
            OpenFolderButton.IsEnabled = HasGame;
            SetStep(GameBadge, GameBadgeText, HasGame);

            var melonVersion = melon ? MelonLoaderInstaller.InstalledVersion(_settings.GamePath) : null;
            MelonText.Text = melon
                ? $"Ready{(melonVersion != null ? $"  •  v{melonVersion}" : "")}"
                : "The mod loader Big Walk VR depends on.";
            MelonButton.Content = melon ? "Reinstall" : "Install";
            MelonButton.IsEnabled = HasGame && !_melonBusy;
            SetStep(MelonBadge, MelonBadgeText, melon);

            // no launching until step 3 is done, a modless launch just confuses people
            var canLaunch = HasGame && melon && _core.Any(m => m.IsInstalled);
            LaunchNonVrButton.IsEnabled = canLaunch;
            LaunchButton.IsEnabled = canLaunch;
            LaunchNonVrButton.ToolTip = canLaunch
                ? "Play normally while seeing VR players' tracked movement"
                : "Finish steps 1-3 first";
            LaunchButton.ToolTip = canLaunch
                ? "Launch Big Walk in VR, start SteamVR first"
                : "Finish steps 1-3 first";
            RestoreVanillaButton.IsEnabled = HasGame;
            SetStep(ModsBadge, ModsBadgeText, melon && AllMods.Any(m => m.IsCurrent));

            // mods stay greyed out until the game folder and MelonLoader are sorted
            var ready = HasGame && melon;
            ModsSection.IsEnabled = ready;
            ModsSection.Opacity = ready ? 1 : 0.4;
        }

        void SetStep(Border badge, TextBlock label, bool done)
        {
            badge.Background = Brush(done ? "Green" : "Hover");
            badge.BorderBrush = Brush(done ? "Green" : "Stroke");
            label.Text = done ? "✓" : (string)label.Tag;
            label.Foreground = Brush(done ? "Bg" : "TextDim");
        }

        Brush Brush(string key) => (Brush)FindResource(key);

        // ---- mod install ----

        async void Install_Click(object sender, RoutedEventArgs e)
        {
            var mod = (ModEntry)((FrameworkElement)sender).DataContext;
            await InstallMod(mod);
        }

        async Task InstallMod(ModEntry mod)
        {
            if (!Ready()) return;

            mod.Busy = true;
            mod.Progress = 0;
            mod.BusyText = "Downloading...";
            try
            {
                var progress = new Progress<double>(p =>
                {
                    mod.Progress = p;
                    mod.BusyText = $"Downloading  {p * 100:0}%";
                });
                var bytes = await RepoClient.Download(mod.Remote.url, mod.Remote.sha256, progress);

                mod.BusyText = "Installing...";
                mod.Progress = 1;
                await Task.Run(() => PackageInstaller.Install(_settings.GamePath, mod.Remote, bytes));
                Status($"{mod.Name} v{mod.Remote.version} installed");
            }
            catch (Exception ex)
            {
                Status($"{mod.Name} install failed: {ex.Message}", true);
            }
            finally
            {
                mod.Busy = false;
                RefreshLocalState();
            }
        }

        async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            var mod = (ModEntry)((FrameworkElement)sender).DataContext;
            if (!Ready()) return;
            if (!await Confirm("Uninstall " + mod.Name, "All mod files and configuration will be removed.", "Uninstall")) return;

            try
            {
                PackageInstaller.Uninstall(_settings.GamePath, mod.Id);
                Status($"{mod.Name} removed");
            }
            catch (Exception ex)
            {
                Status($"{mod.Name} uninstall failed: {ex.Message}", true);
            }
            RefreshLocalState();
        }

        // game holds its dlls open, writing over them mid-session corrupts the install
        bool Ready()
        {
            if (!HasGame)
            {
                Status("Pick your Big Walk folder first", true);
                return false;
            }
            if (GameLauncher.IsRunning())
            {
                Status("Close Big Walk before changing mods", true);
                return false;
            }
            return true;
        }

        async void RestoreVanilla_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready()) return;
            if (!await Confirm("Restore vanilla Big Walk",
                "Removes every installed mod, MelonLoader, and the Mods, Plugins, UserLibs and UserData folders. Your game saves are kept.",
                "Restore vanilla")) return;

            try
            {
                foreach (var mod in AllMods.Where(m => m.IsInstalled).ToList())
                    PackageInstaller.Uninstall(_settings.GamePath, mod.Id);
                MelonLoaderInstaller.Remove(_settings.GamePath);
                Status("Big Walk is back to vanilla");
            }
            catch (Exception ex)
            {
                Status($"Restore failed: {ex.Message}", true);
            }
            RefreshLocalState();
        }

        // ---- melonloader ----

        async void InstallMelon_Click(object sender, RoutedEventArgs e) => await InstallMelon();

        async Task InstallMelon()
        {
            if (!Ready()) return;

            _melonBusy = true;
            MelonButton.IsEnabled = false;
            MelonProgressPanel.Visibility = Visibility.Visible;
            MelonProgress.Value = 0;
            MelonProgressText.Text = "Downloading MelonLoader...";
            Status("Downloading MelonLoader...");
            try
            {
                // mirrored onto the status bar, the install can be kicked off from the Mods tab banner
                var progress = new Progress<double>(p =>
                {
                    MelonProgress.Value = p;
                    MelonProgressText.Text = StatusText.Text = $"Downloading MelonLoader  {p * 100:0}%";
                });
                var bytes = await RepoClient.Download(_melonSource.url, _melonSource.sha256, progress);

                MelonProgressText.Text = StatusText.Text = "Installing MelonLoader...";
                MelonProgress.Value = 1;
                await Task.Run(() => MelonLoaderInstaller.Extract(_settings.GamePath, bytes));
                Status($"MelonLoader v{_melonSource.version} installed");
            }
            catch (Exception ex)
            {
                Status($"MelonLoader install failed: {ex.Message}", true);
            }
            finally
            {
                _melonBusy = false;
                MelonProgressPanel.Visibility = Visibility.Collapsed;
                UpdateSetupState();
            }
        }

        // ---- shell actions ----

        void LaunchNonVr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GameLauncher.LaunchNonVr(_settings.GamePath);
                Status("Launching Big Walk in Non-VR mode");
            }
            catch (Exception ex)
            {
                Status($"Launch failed: {ex.Message}", true);
            }
        }

        void Launch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GameLauncher.LaunchVr(_settings.GamePath);
                Status("Launching Big Walk in VR, make sure SteamVR is running");
            }
            catch (Exception ex)
            {
                Status($"Launch failed: {ex.Message}", true);
            }
        }

        async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Status("Refreshing...");
            await Refresh();
        }

        async void ChangeGamePath_Click(object sender, RoutedEventArgs e)
        {
            if (PromptGamePath()) await Refresh();
        }

        bool PromptGamePath()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the Big Walk game folder";
                if (HasGame) dialog.SelectedPath = _settings.GamePath;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
                if (!GameLocator.IsValidGamePath(dialog.SelectedPath))
                {
                    Status($"That folder doesn't contain {GameLocator.ExeName}", true);
                    return false;
                }
                _settings.GamePath = GameLocator.Canonical(dialog.SelectedPath);
                _settings.Save();
                Status($"Game folder set to {_settings.GamePath}");
                return true;
            }
        }

        void OpenGameFolder_Click(object sender, RoutedEventArgs e) => Open(() => GameLauncher.OpenFolder(_settings.GamePath));

        void OpenModLogs_Click(object sender, RoutedEventArgs e) => Open(() =>
        {
            var log = Path.Combine(_settings.GamePath, "MelonLoader", "Latest.log");
            if (File.Exists(log)) GameLauncher.OpenUrl(log);
            else GameLauncher.OpenFolder(Path.Combine(_settings.GamePath, "MelonLoader"));
        });

        void OpenRepo_Click(object sender, RoutedEventArgs e) => Open(() => GameLauncher.OpenUrl(RepoUrl));

        void Open(Action action)
        {
            try { action(); }
            catch (Exception ex) { Status($"Couldn't open that: {ex.Message}", true); }
        }

        async void SelfUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_selfUpdate == null) return;
            SelfUpdateButton.IsEnabled = false;
            SelfUpdateButton.Content = "Updating...";
            try
            {
                Status("Downloading installer update...");
                await SelfUpdater.ApplyUpdate(_selfUpdate);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                SelfUpdateButton.IsEnabled = true;
                SelfUpdateButton.Content = "Update now";
                Status($"Installer update failed: {ex.Message}", true);
            }
        }

        // ---- chrome ----

        void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (InstallView == null) return; // fires during InitializeComponent
            InstallView.Visibility = NavInstall.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            LogsView.Visibility = NavLogs.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        TaskCompletionSource<bool> _confirm;

        Task<bool> Confirm(string title, string text, string okLabel)
        {
            if (_confirm != null) return Task.FromResult(false); // already asking something else
            ConfirmTitle.Text = title;
            ConfirmText.Text = text;
            ConfirmOk.Content = okLabel;
            ConfirmOverlay.Visibility = Visibility.Visible;
            _confirm = new TaskCompletionSource<bool>();
            return _confirm.Task;
        }

        void CloseConfirm(bool result)
        {
            ConfirmOverlay.Visibility = Visibility.Collapsed;
            _confirm?.TrySetResult(result);
            _confirm = null;
        }

        void ConfirmOk_Click(object sender, RoutedEventArgs e) => CloseConfirm(true);
        void ConfirmCancel_Click(object sender, RoutedEventArgs e) => CloseConfirm(false);

        void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_confirm == null || e.Key != System.Windows.Input.Key.Escape) return;
            CloseConfirm(false);
            e.Handled = true;
        }

        void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Close_Click(object sender, RoutedEventArgs e) => Close();

        void Status(string text, bool error = false)
        {
            _logs.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = text, IsError = error });
            StatusText.Text = text;
            StatusText.Foreground = Brush(error ? "Red" : "TextDim");
            LogsView.ScrollToEnd();
        }
    }
}
