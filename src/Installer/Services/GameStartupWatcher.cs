using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigWalkVRInstaller.Services
{
    public enum LaunchPhase { Generating, GenerationDone, Ready, Exited, Crashed }

    // tails the BepInEx log so the launcher can explain one-time interop generation
    public class GameStartupWatcher
    {
        const int PollMs = 400;
        // the process can blink while steam hands over, so only give up after a few misses in a row
        const int MissesBeforeExit = 5;
        const string GenerationNeeded = "Running Cpp2IL to generate dummy assemblies";
        const string ModAlive = "Chainloader startup complete";

        readonly string _logPath;
        readonly string _crashMarkerPath;
        readonly IProgress<LaunchPhase> _report;
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        long _offset;
        Process _game;
        bool _sawGame;
        bool _ready;
        bool _generating;
        int _misses;

        public GameStartupWatcher(string gamePath, IProgress<LaunchPhase> report)
        {
            _logPath = Path.Combine(gamePath, "BepInEx", "LogOutput.log");
            _crashMarkerPath = Path.Combine(gamePath, "UserData", "BigWalkVRInstaller", "GameSession.lock");
            _report = report;
            _offset = Length(); // everything already in there belongs to the previous run
            Directory.CreateDirectory(Path.GetDirectoryName(_crashMarkerPath));
            File.WriteAllText(_crashMarkerPath, DateTime.UtcNow.ToString("O"));
        }

        public void Start() => Task.Run(() => Loop(_cancel.Token));

        public void Stop() => _cancel.Cancel();

        async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // once the mod is up there is nothing left to read, so just watch the process
                if (!_ready)
                {
                    foreach (var line in ReadNewLines())
                    {
                        if (line.IndexOf(GenerationNeeded, StringComparison.Ordinal) >= 0)
                        {
                            _generating = true;
                            Report(LaunchPhase.Generating);
                        }
                        else if (line.IndexOf(ModAlive, StringComparison.Ordinal) >= 0)
                        {
                            if (_generating) Report(LaunchPhase.GenerationDone);
                            _ready = true;
                            Report(LaunchPhase.Ready);
                            break;
                        }
                    }
                }

                if (Alive()) { _sawGame = true; _misses = 0; }
                else if (_sawGame && ++_misses >= MissesBeforeExit)
                {
                    Report(ConsumeCrashMarker() ? LaunchPhase.Crashed : LaunchPhase.Exited);
                    return;
                }

                try { await Task.Delay(PollMs, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        // steam owns the launch, so the game has to be found by name once and then it is tracked by handle
        bool Alive()
        {
            if (_game != null)
            {
                try { if (!_game.HasExited) return true; }
                catch (InvalidOperationException) { }
            }
            _game = Find();
            return _game != null;
        }

        static Process Find()
        {
            try { return Process.GetProcessesByName(GameLauncher.ProcessName).FirstOrDefault(); }
            catch { return null; }
        }

        bool ConsumeCrashMarker()
        {
            var crashed = File.Exists(_crashMarkerPath);
            if (crashed) File.Delete(_crashMarkerPath);
            return crashed;
        }

        // best effort, the tracked process is the one that gets stopped
        public void StopGame()
        {
            File.Delete(_crashMarkerPath);
            var game = _game ?? Find();
            if (game == null) return;
            try { game.Kill(); }
            catch (Exception) { }
        }

        void Report(LaunchPhase phase)
        {
            if (!_cancel.IsCancellationRequested) _report.Report(phase);
        }

        // reads whole lines only, a half written tail stays for the next poll
        IEnumerable<string> ReadNewLines()
        {
            var empty = new string[0];
            try
            {
                if (!File.Exists(_logPath)) return empty;
                using (var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length < _offset) _offset = 0; // BepInEx recreated the log
                    var pending = stream.Length - _offset;
                    if (pending <= 0) return empty;

                    stream.Seek(_offset, SeekOrigin.Begin);
                    var buffer = new byte[pending];
                    var read = stream.Read(buffer, 0, buffer.Length);
                    var text = Encoding.UTF8.GetString(buffer, 0, read);

                    var cut = text.LastIndexOf('\n');
                    if (cut < 0) return empty;
                    text = text.Substring(0, cut);
                    _offset += Encoding.UTF8.GetByteCount(text) + 1;
                    return text.Split('\n');
                }
            }
            catch (IOException) { return empty; }
            catch (UnauthorizedAccessException) { return empty; }
        }

        long Length()
        {
            try
            {
                var info = new FileInfo(_logPath);
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }
    }
}
