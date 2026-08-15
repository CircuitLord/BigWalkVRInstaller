using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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
        const string DiscordUrl = "https://discord.gg/MTKwud2cCP";
        const string SupportUrl = "https://ko-fi.com/circuitlord";

        readonly ObservableCollection<ModEntry> _core = new ObservableCollection<ModEntry>();
        readonly ObservableCollection<ModEntry> _optional = new ObservableCollection<ModEntry>();
        readonly ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        AppSettings _settings;
        ReleaseInfo _selfUpdate;
        ReleaseInfo _bepInExSource;
        bool _bepInExBusy;
        GameStartupWatcher _watcher;

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
            BetaUpdatesToggle.IsChecked = _settings.EnableBetaUpdates;
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
            if (HasGame && BepInExInstaller.IsMelonLoaderInstalled(_settings.GamePath))
            {
                Status("Remove MelonLoader in step 2 to continue.");
                return;
            }
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
            _bepInExSource = null;

            try
            {
                var manifest = await RepoClient.FetchManifest(AppSettings.ManifestUrl);
                foreach (var available in manifest.mods)
                {
                    var useBeta = _settings.EnableBetaUpdates && available.beta != null;
                    var remote = available.SelectRelease(_settings.EnableBetaUpdates);
                    var entry = previous.TryGetValue(remote.id, out var live) && live.Busy
                        ? live
                        : new ModEntry { Remote = remote, IsBeta = useBeta };
                    (remote.core ? _core : _optional).Add(entry);
                }
                _bepInExSource = manifest.bepinex;
                if (SelfUpdater.IsUpdateAvailable(manifest.installer)) _selfUpdate = manifest.installer;
            }
            catch (Exception ex)
            {
                Status($"Couldn't reach the download list: {ex.Message}", true);
            }

            if (HasGame)
            {
                var listedIds = new HashSet<string>(AllMods.Select(mod => mod.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var record in PackageInstaller.ReadRecords(_settings.GamePath).Where(record => listedIds.Add(record.id)))
                {
                    _optional.Add(new ModEntry
                    {
                        Remote = new ManifestMod { id = record.id, version = record.version },
                        IsBeta = record.beta
                    });
                }
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
            {
                var record = HasGame ? PackageInstaller.ReadRecord(_settings.GamePath, mod.Id) : null;
                mod.InstalledBeta = record?.beta ?? false;
                mod.InstalledVersion = record?.version;
            }
            UpdateSetupState();
        }

        void UpdateSetupState()
        {
            var bepinex = HasGame && BepInExInstaller.IsInstalled(_settings.GamePath);
            var melonLoader = HasGame && BepInExInstaller.IsMelonLoaderInstalled(_settings.GamePath);
            var unknownBootstrap = HasGame && BepInExInstaller.HasUnknownBootstrap(_settings.GamePath);
            var loaderReady = bepinex && !melonLoader && !unknownBootstrap;

            GamePathText.Text = HasGame ? _settings.GamePath : "Not found. Press Change and pick your Big Walk folder.";
            OpenFolderButton.IsEnabled = HasGame;
            SetStep(GameBadge, GameBadgeText, HasGame);

            var bepinexVersion = bepinex ? BepInExInstaller.InstalledVersion(_settings.GamePath) : null;
            BepInExText.Text = unknownBootstrap
                ? "version.dll is active but is not part of a complete MelonLoader install. Restore vanilla or remove it manually."
                : melonLoader
                    ? "MelonLoader must be removed before Big Walk can launch."
                    : bepinex
                        ? $"Ready{(bepinexVersion != null ? $"  •  v{bepinexVersion}" : "")}"
                        : "The mod loader Big Walk VR depends on.";
            BepInExButton.Content = melonLoader ? "Remove MelonLoader" : bepinex ? "Reinstall" : "Install";
            BepInExButton.IsEnabled = HasGame && _bepInExSource != null && !_bepInExBusy && !unknownBootstrap;
            SetStep(BepInExBadge, BepInExBadgeText, loaderReady);

            // no launching until step 3 is done, a modless launch just confuses people
            var canLaunch = HasGame && loaderReady && _core.Any(m => m.IsInstalled);
            LaunchNonVrButton.IsEnabled = canLaunch;
            LaunchButton.IsEnabled = canLaunch;
            LaunchNonVrButton.ToolTip = canLaunch
                ? "Play normally while seeing VR players' tracked movement"
                : "Finish steps 1-3 first";
            LaunchButton.ToolTip = canLaunch
                ? "Launch Big Walk in VR, start SteamVR first"
                : "Finish steps 1-3 first";
            RestoreVanillaButton.IsEnabled = HasGame;
            CrashReportButton.IsEnabled = HasGame;
            SetStep(ModsBadge, ModsBadgeText, loaderReady && AllMods.Any(m => m.IsCurrent));

            // mods stay greyed out until the game folder and BepInEx are sorted
            var ready = HasGame && loaderReady;
            ModsSection.IsEnabled = ready;
            ModsSection.Opacity = ready ? 1 : 0.4;
        }

        void SetStep(Border badge, TextBlock label, bool done)
        {
            badge.Background = Brush(done ? "GreenFill" : "Hover");
            badge.BorderBrush = Brush(done ? "GreenFillBorder" : "Stroke");
            label.Text = done ? "✓" : (string)label.Tag;
            label.Foreground = Brush(done ? "Green" : "TextDim");
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
                await Task.Run(() => PackageInstaller.Install(_settings.GamePath, mod.Remote, bytes, mod.IsBeta));
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
            await Refresh();
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
                "Permanently removes every installed mod, BepInEx, and MelonLoader.",
                "Restore vanilla")) return;

            try
            {
                foreach (var mod in AllMods.Where(m => m.IsInstalled).ToList())
                    PackageInstaller.Uninstall(_settings.GamePath, mod.Id);
                BepInExInstaller.Remove(_settings.GamePath);
                Status("Big Walk is back to vanilla");
            }
            catch (Exception ex)
            {
                Status($"Restore failed: {ex.Message}", true);
            }
            await Refresh();
        }

        // ---- BepInEx ----

        async void InstallBepInEx_Click(object sender, RoutedEventArgs e)
        {
            if (await InstallBepInEx()) await Refresh();
        }

        async Task<bool> InstallBepInEx()
        {
            if (!Ready()) return false;
            if (BepInExInstaller.IsMelonLoaderInstalled(_settings.GamePath) && !await Confirm(
                "Remove MelonLoader",
                "Installing BepInEx will permanently remove MelonLoader and everything in its Mods, Plugins, and UserLibs folders. BigWalkVR settings will be migrated. No backup will be created.",
                "Remove and install")) return false;

            var installed = false;
            _bepInExBusy = true;
            BepInExButton.IsEnabled = false;
            BepInExProgressPanel.Visibility = Visibility.Visible;
            BepInExProgress.Value = 0;
            BepInExProgressText.Text = "Downloading BepInEx...";
            Status("Downloading BepInEx...");
            try
            {
                // mirrored onto the status bar, the install can be kicked off from the Mods tab banner
                var progress = new Progress<double>(p =>
                {
                    BepInExProgress.Value = p;
                    BepInExProgressText.Text = StatusText.Text = $"Downloading BepInEx  {p * 100:0}%";
                });
                var bytes = await RepoClient.Download(_bepInExSource.url, _bepInExSource.sha256, progress);

                BepInExProgressText.Text = StatusText.Text = "Installing BepInEx...";
                BepInExProgress.Value = 1;
                var migration = await Task.Run(() => BepInExInstaller.Extract(_settings.GamePath, bytes));
                Status(migration.Migrated ? migration.Details : $"BepInEx v{_bepInExSource.version} installed");
                installed = true;
            }
            catch (Exception ex)
            {
                Status($"BepInEx install failed: {ex.Message}", true);
            }
            finally
            {
                _bepInExBusy = false;
                BepInExProgressPanel.Visibility = Visibility.Collapsed;
                UpdateSetupState();
            }
            return installed;
        }

        // ---- shell actions ----

        void LaunchNonVr_Click(object sender, RoutedEventArgs e) => Launch(
            () => GameLauncher.LaunchNonVr(_settings.GamePath), "Launching Big Walk in Non-VR mode", "");

        void Launch_Click(object sender, RoutedEventArgs e) => Launch(
            () => GameLauncher.LaunchVr(_settings.GamePath), "Launching Big Walk in VR",
            "Make sure SteamVR is running and your headset is connected.");

        void Launch(Action launch, string title, string description)
        {
            try
            {
                launch();
                Status(title);
                ShowLaunchModal(title + "...", description);
            }
            catch (Exception ex)
            {
                Status($"Launch failed: {ex.Message}", true);
            }
        }

        // ---- launch modal ----

        void ShowLaunchModal(string title, string description)
        {
            StopWatcher();
            LaunchTitle.Text = title;
            LaunchDescription.Text = description;
            LaunchDescription.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
            GenPanel.Visibility = Visibility.Collapsed;
            LaunchOverlay.Visibility = Visibility.Visible;

            _watcher = new GameStartupWatcher(_settings.GamePath, new Progress<LaunchPhase>(OnLaunchPhase));
            _watcher.Start();
        }

        async void OnLaunchPhase(LaunchPhase phase)
        {
            switch (phase)
            {
                case LaunchPhase.Generating:
                    GenPanel.Visibility = Visibility.Visible;
                    GenCheck.Visibility = Visibility.Collapsed;
                    GenTitle.Text = "Doing one-time setup for this game version";
                    GenText.Text = "This can take a minute, please wait.";
                    GenText.Visibility = Visibility.Visible;
                    Status("BepInEx is doing one-time setup for this game version");
                    break;

                case LaunchPhase.GenerationDone:
                    GenCheck.Visibility = Visibility.Visible;
                    GenTitle.Text = "Setup complete";
                    GenText.Visibility = Visibility.Collapsed;
                    Status("One-time setup finished");
                    break;

                // the modal stays up for the whole session, it is the only way to stop the game again
                case LaunchPhase.Ready:
                    LaunchTitle.Text = "Big Walk is running";
                    LaunchDescription.Visibility = Visibility.Collapsed;
                    Status("Big Walk is running");
                    break;

                case LaunchPhase.Exited:
                    CloseLaunchModal();
                    Status("Big Walk closed");
                    break;

                case LaunchPhase.Crashed:
                    CloseLaunchModal();
                    Status("Big Walk may have crashed", true);
                    await PromptCrashReport("Big Walk may have crashed");
                    break;
            }
        }

        void CloseLaunchModal()
        {
            StopWatcher();
            LaunchOverlay.Visibility = Visibility.Collapsed;
        }

        void StopWatcher()
        {
            _watcher?.Stop();
            _watcher = null;
        }

        void LaunchStop_Click(object sender, RoutedEventArgs e)
        {
            _watcher?.StopGame(); // stop before the watcher is dropped, it owns the process handle
            CloseLaunchModal();
            Status("Stopping Big Walk");
        }

        async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Status("Refreshing...");
            await Refresh();
        }

        async void BetaUpdates_Click(object sender, RoutedEventArgs e)
        {
            _settings.EnableBetaUpdates = BetaUpdatesToggle.IsChecked == true;
            _settings.Save();
            Status(_settings.EnableBetaUpdates ? "Beta updates enabled. Beta builds are unstable." : "Stable updates enabled");
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
            var log = Path.Combine(_settings.GamePath, "BepInEx", "LogOutput.log");
            if (File.Exists(log)) GameLauncher.OpenUrl(log);
            else GameLauncher.OpenFolder(Path.Combine(_settings.GamePath, "BepInEx"));
        });

        void OpenDiscord_Click(object sender, RoutedEventArgs e) => Open(() => GameLauncher.OpenUrl(DiscordUrl));

        void OpenSupport_Click(object sender, RoutedEventArgs e) => Open(() => GameLauncher.OpenUrl(SupportUrl));

        void OpenRepo_Click(object sender, RoutedEventArgs e) => Open(() => GameLauncher.OpenUrl(RepoUrl));

        async void CreateCrashReport_Click(object sender, RoutedEventArgs e) => await PromptCrashReport("Create crash report");

        async Task PromptCrashReport(string title)
        {
            if (await Confirm(
                title,
                "Create a ZIP containing the BepInEx and Unity logs. Logs may contain personal or device details, so review them before sharing.",
                "Create report",
                false)) await CreateCrashReport();
        }

        async Task CreateCrashReport()
        {
            try
            {
                var report = CrashReportService.Create(_settings.GamePath);
                GameLauncher.SelectFile(report);
                Status("Crash report created on the Desktop.");
                if (await Confirm(
                    "Crash report ready",
                    "The ZIP is selected in Explorer. You can send it in the #support channel in the Big Walk VR Discord.",
                    "Open Discord",
                    false)) GameLauncher.OpenUrl(DiscordUrl);
            }
            catch (Exception ex)
            {
                Status($"Couldn't create the crash report: {ex.Message}", true);
            }
        }

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

        Task<bool> Confirm(string title, string text, string okLabel, bool danger = true)
        {
            if (_confirm != null) return Task.FromResult(false); // already asking something else
            ConfirmTitle.Text = title;
            ConfirmText.Text = text;
            ConfirmOk.Content = okLabel;
            ConfirmOk.Style = (Style)FindResource(danger ? "Danger" : "Primary");
            ConfirmOverlay.Visibility = Visibility.Visible;
            _confirm = new TaskCompletionSource<bool>();
            return _confirm.Task;
        }

        void CloseConfirm(bool result)
        {
            ConfirmOverlay.Visibility = Visibility.Collapsed;
            var confirm = _confirm;
            _confirm = null;
            confirm?.TrySetResult(result);
        }

        void ConfirmOk_Click(object sender, RoutedEventArgs e) => CloseConfirm(true);
        void ConfirmCancel_Click(object sender, RoutedEventArgs e) => CloseConfirm(false);

        void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // escape does not dismiss the launch modal, Stop is the only way out so the game can't be orphaned
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
