using System.Diagnostics;
using System.Globalization;
using Moonwell.GameData;

namespace Moonwell.GameRun;

/// <summary>What to play and how: the map, its run's file, the game, and the rules of the watch.</summary>
public sealed record GameRunOptions
{
    /// <summary>The built map, a packed <c>.w3x</c>.</summary>
    public required string Map { get; init; }

    /// <summary>The run's file, relative to <c>CustomMapData</c>: the name the map gives <c>PreloadGenEnd</c>.</summary>
    public required string File { get; init; }

    /// <summary>The game's program file.</summary>
    public string Program { get; init; } = DefaultProgram;

    /// <summary>The game's documents folder, <c>Documents\Warcraft III</c>.</summary>
    public string Documents { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Warcraft III");

    /// <summary>The lines and times the run is watched by.</summary>
    public RunRules Rules { get; init; } = new RunRules();

    /// <summary>The starts of a line that makes the run fail: the step runner's errors and the cases' failures by default.</summary>
    public IReadOnlyList<string> FailWhen { get; init; } = new[] { "ERROR", "FAIL" };

    /// <summary>How long to wait for a game this runner did not start to end before giving up; nothing is ended.</summary>
    public TimeSpan WaitForFree { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Where the runner says what it does, as it does it; null for nowhere.</summary>
    public Action<string>? Progress { get; init; }

    /// <summary>The game's program in the install Moonwell finds (MOONWELL_WAR3, else the usual folder).</summary>
    public static string DefaultProgram => Path.Combine(GameInstall.Places()[0], "_retail_", "x86_64", "Warcraft III.exe");

    /// <summary>Why a launch test cannot play here, or null when it can: it plays only with MOONWELL_LAUNCH=1, on Windows, with the game installed.</summary>
    public static string? WhyNoLaunch()
    {
        if (Environment.GetEnvironmentVariable("MOONWELL_LAUNCH") != "1")
        {
            return "Starts the game: set MOONWELL_LAUNCH=1 to run it.";
        }

        if (!OperatingSystem.IsWindows())
        {
            return "The game runs on Windows only.";
        }

        return System.IO.File.Exists(DefaultProgram) ? null : $"No game at {DefaultProgram}; set {GameInstall.Variable} to the install's folder.";
    }

    /// <summary>The full path of the run's file.</summary>
    public string FilePath => Path.Combine(Documents, "CustomMapData", File);
}

/// <summary>How a run ended, what its file held, and what the runner saw on the way.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Lines">The run's lines, from a file written after the game's start; empty when none came.</param>
/// <param name="Failing">The lines that start with one of <see cref="GameRunOptions.FailWhen"/>.</param>
/// <param name="Detail">What the runner saw: when the file came, when the game started, how many keys were posted, how long it took.</param>
/// <param name="GameVersion">The game program's file version.</param>
/// <param name="MapOpened">Whether the game opened the map (it logged in and loaded it).</param>
public sealed record GameRunResult(RunOutcome Outcome, IReadOnlyList<string> Lines, IReadOnlyList<string> Failing, string Detail, string? GameVersion, bool MapOpened = true)
{
    /// <summary>Whether the run passed: it is over and no line fails.</summary>
    public bool Passed => Outcome == RunOutcome.Over && Failing.Count == 0;

    /// <summary>The one line a person reads: PASS or FAIL, why, and the game's version.</summary>
    public string Verdict
    {
        get
        {
            var why = Outcome switch
            {
                RunOutcome.Over when Failing.Count == 0 => $"the run is over; {Lines.Count} line(s), none failing",
                RunOutcome.Over => $"the run is over; {Failing.Count} of {Lines.Count} line(s) failing",
                RunOutcome.NoFile when MapOpened => "the game loaded the map, but no run file came: the map wrote none (a Lua error before it?), or the game never got past its loading screen",
                RunOutcome.NoFile => "the game never opened the map: is the Battle.net app running and logged in? (started from a sandboxed shell the game may never log in: start the run from PowerShell or a normal terminal)",
                RunOutcome.StuckLoading => "the run's file kept its loading line, also after the game was resumed: the game did not get past its loading screen, or the map wrote nothing after it",
                RunOutcome.Stalled => "the run's file stopped changing, also after the game was resumed: no game time passes, or the map waits for something (a step waits for Esc?)",
                RunOutcome.TimedOut => "the time limit was over before the run was",
                RunOutcome.GameEnded => "the game ended before the run was over (a crash? see Documents\\Warcraft III\\Errors)",
                RunOutcome.AnotherGame => "another Warcraft III ran during this run, so this run tells nothing; it was not ended: run again when no other program starts the game",
                _ => "not run",
            };
            return $"{(Passed ? "PASS" : "FAIL")}: {why}; game {GameVersion ?? "?"}";
        }
    }
}

/// <summary>
/// Plays a built map in the game with nobody at the keyboard. It waits until no Warcraft III runs (two games at once do not
/// both report) and takes the machine's turn, starts the game on the map in a job of its own, posts space from the moment
/// the game opens the map until the run's file shows the game started (no file yet, or only the loading line), resumes a game
/// whose file stops changing once, and ends
/// the game and what it started as soon as the file holds the over line, the game ends, another game starts, or a limit is over.
/// </summary>
public static class GameRunner
{
    /// <summary>The longest full path of a map the game loads with <c>-loadfile</c> (game facts, 3.0.1.24371, 11-08; the CLI's own LongestMapPath).</summary>
    public const int LongestMapPath = 173;

    /// <summary>The name of the game's process.</summary>
    public const string ProcessName = "Warcraft III";

    // Runs of this runner on one machine take turns.
    private const string Turn = "Local\\Moonwell.GameRun";

    /// <summary>Plays the map and gives how the run ended.</summary>
    /// <exception cref="InvalidOperationException">The game or the map is not there, another game did not end in time, or Windows refused to start the game.</exception>
    public static GameRunResult Run(GameRunOptions options)
    {
        if (!System.IO.File.Exists(options.Program))
        {
            throw new InvalidOperationException($"the game is not at {options.Program}");
        }

        var map = Path.GetFullPath(options.Map);
        if (!System.IO.File.Exists(map))
        {
            throw new InvalidOperationException($"the map is not built: {map}");
        }

        var say = options.Progress ?? (_ => { });
        using var turn = new Mutex(false, Turn);
        var waited = Stopwatch.StartNew();
        try
        {
            if (!turn.WaitOne(options.WaitForFree))
            {
                throw new InvalidOperationException($"another run of this runner still plays after {options.WaitForFree.TotalMinutes:0} minutes");
            }
        }
        catch (AbandonedMutexException)
        {
            // A run that ended without letting go: the turn is this one's now.
        }

        try
        {
            WaitUntilFree(options.WaitForFree - waited.Elapsed, say);
            var copy = map.Length > LongestMapPath ? ShortCopy(map) : null;
            try
            {
                return Play(options, copy ?? map, say);
            }
            finally
            {
                if (copy != null)
                {
                    System.IO.File.Delete(copy);
                }
            }
        }
        finally
        {
            turn.ReleaseMutex();
        }
    }

    /// <summary>The lines that start with one of the starts.</summary>
    public static IReadOnlyList<string> FailingLines(IReadOnlyList<string> lines, IReadOnlyList<string> starts)
    {
        return lines.Where(line => starts.Any(start => line.StartsWith(start, StringComparison.Ordinal))).ToList();
    }

    private static GameRunResult Play(GameRunOptions options, string map, Action<string> say)
    {
        var watch = new RunWatch(options.Rules);
        var path = options.FilePath;
        var version = FileVersionInfo.GetVersionInfo(options.Program).FileVersion;
        var started = DateTime.UtcNow;
        var clock = Stopwatch.StartNew();
        say($"starting the game on {map}; the run's file is {path}");
        using (var game = GameJob.Start(options.Program, $"-launch -nowfpause -windowmode windowedfullscreen -loadfile \"{map}\""))
        {
            var lastOtherLook = TimeSpan.Zero;
            while (true)
            {
                var elapsed = clock.Elapsed;
                var action = watch.Look(elapsed, RunFile.ReadIfWrittenSince(path, started), !game.HasExited, watch.MapOpenedAt.HasValue || HeldOpen(map));
                if (watch.Changed)
                {
                    say($"{elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s: the file holds {watch.Lines.Count} line(s), the last \"{watch.Lines.LastOrDefault()}\"");
                }

                if (action == RunAction.Stop)
                {
                    break;
                }

                if (action == RunAction.PostSpace)
                {
                    game.PostKey(GameKey.Space);
                }
                else if (action == RunAction.Resume)
                {
                    say($"the run's file has not changed for {options.Rules.Stall.TotalSeconds:0} s: resuming the game (F10, then Escape)");
                    game.PostKey(GameKey.F10);
                    Thread.Sleep(1000);
                    game.PostKey(GameKey.Escape);
                }

                if (elapsed - lastOtherLook >= TimeSpan.FromSeconds(2))
                {
                    lastOtherLook = elapsed;
                    if (OtherGame(game) is { } other)
                    {
                        say($"another Warcraft III (process {other}) runs: this run tells nothing, and the other game is left running");
                        watch.End(RunOutcome.AnotherGame);
                        break;
                    }
                }

                Thread.Sleep(250);
            }
        }

        var lines = watch.Lines;
        var detail = string.Join(
            "; ",
            $"map opened {Seconds(watch.MapOpenedAt)}",
            $"file {Seconds(watch.FileAt)}",
            $"game started {Seconds(watch.StartedAt)}",
            $"{watch.Spaces} space(s) posted",
            watch.Resumed ? "resumed once" : "not resumed",
            $"run took {clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");
        return new GameRunResult(watch.Outcome, lines, FailingLines(lines, options.FailWhen), detail, version, watch.MapOpenedAt.HasValue);
    }

    // Whether another process holds the file open: the game holds the map from the moment it opens it (once it has logged in).
    private static bool HeldOpen(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static string Seconds(TimeSpan? at)
    {
        return at is { } value ? "after " + value.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" : "never";
    }

    // A Warcraft III that is not in this run's job, or null.
    private static int? OtherGame(GameJob game)
    {
        var own = game.ProcessIds;
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                if (process.Id != game.ProcessId && !own.Contains(process.Id))
                {
                    return process.Id;
                }
            }
        }

        return null;
    }

    // Waits until no Warcraft III runs, saying so once; a game this runner did not start is never ended.
    private static void WaitUntilFree(TimeSpan limit, Action<string> say)
    {
        var waited = Stopwatch.StartNew();
        var said = false;
        while (AnyGameRuns())
        {
            if (waited.Elapsed >= limit)
            {
                throw new InvalidOperationException($"a Warcraft III this runner did not start still runs after {Math.Max(0, limit.TotalMinutes):0} minutes of waiting; it was not ended. Run again when it has.");
            }

            if (!said)
            {
                say("a Warcraft III runs: waiting for it to end (two games at once do not both report)");
                said = true;
            }

            Thread.Sleep(2000);
        }
    }

    private static bool AnyGameRuns()
    {
        var games = Process.GetProcessesByName(ProcessName);
        foreach (var game in games)
        {
            game.Dispose();
        }

        return games.Length > 0;
    }

    // A copy of the map in a short folder of the runner's own, for a map whose path the game would never load.
    private static string ShortCopy(string map)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moonwell", "runs");
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, Guid.NewGuid().ToString("N").Substring(0, 8) + ".w3x");
        if (copy.Length > LongestMapPath)
        {
            throw new InvalidOperationException($"the map's path is {map.Length} characters and the game never loads one over {LongestMapPath}; even a copy in {folder} would be {copy.Length}");
        }

        System.IO.File.Copy(map, copy);
        return copy;
    }
}
