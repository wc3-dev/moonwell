namespace Moonwell.GameRun;

/// <summary>What the runner does after a look at the game and its run's file.</summary>
public enum RunAction
{
    /// <summary>Nothing yet: look again on the next turn.</summary>
    Wait,

    /// <summary>Post the space bar to the game's window: the loading screen waits for a key ("Press any key to continue").</summary>
    PostSpace,

    /// <summary>Resume a game that may be paused, as a person does: F10 (the game's menu), then Escape. Asked once a run.</summary>
    Resume,

    /// <summary>The run is over, or will not be: end the game and give the outcome.</summary>
    Stop,
}

/// <summary>How a run ended.</summary>
public enum RunOutcome
{
    /// <summary>Not ended yet.</summary>
    None,

    /// <summary>The file holds the over line.</summary>
    Over,

    /// <summary>No file written after the game's start came within the load limit: see <see cref="RunWatch.MapOpenedAt"/> for whether the game loaded the map.</summary>
    NoFile,

    /// <summary>The file kept its loading line: the game did not get past the loading screen.</summary>
    StuckLoading,

    /// <summary>The file did not change for the stall time, even after the game was resumed: no game time passes, or the map waits for something.</summary>
    Stalled,

    /// <summary>The time limit was over before the over line came.</summary>
    TimedOut,

    /// <summary>The game ended by itself before the over line came.</summary>
    GameEnded,

    /// <summary>A game this run did not start ran during it: two games at once do not both report, so the run tells nothing.</summary>
    AnotherGame,
}

/// <summary>The times and lines a run is watched by.</summary>
public sealed record RunRules
{
    /// <summary>The line a map may write in its file while it loads; space is posted while it is the file's last line.</summary>
    public const string DefaultLoadingLine = "-- loading";

    /// <summary>The line a map writes last, when its run is over (the step runner's).</summary>
    public const string DefaultOverLine = "-- run over";

    /// <summary>The line a map may write while it loads (a Moonwell map's components are added only once the loading screen is past, so it cannot); space is posted while it is the file's last line.</summary>
    public string LoadingLine { get; init; } = DefaultLoadingLine;

    /// <summary>The line that ends the run.</summary>
    public string OverLine { get; init; } = DefaultOverLine;

    /// <summary>How long the whole run may take from the game's start.</summary>
    public TimeSpan Limit { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long after the game's start the file may take to come: the game logs in to Battle.net (10 to 14 s, game facts)
    /// and loads the map first.
    /// </summary>
    public TimeSpan LoadLimit { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>How long the file may stay the same before the game is taken as paused; resumed once, then given up.</summary>
    public TimeSpan Stall { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How often space is posted while the loading screen waits.</summary>
    public TimeSpan SpaceEvery { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long after the game opened the map the first space is posted: the game opens it behind its door screen, a moment before the loading screen.</summary>
    public TimeSpan SpaceAfterOpen { get; init; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// The runner's watch of one run, with no game or clock of its own: each turn it is given the time since the game's start,
/// the run's lines (null while no file written since the start is there), whether the game still runs and whether it holds
/// the map open, and says what to do. The game opens the map once it has logged in (it holds the file open from then on),
/// and its loading screen then waits for a key: space is posted from the open until the file shows the game started.
/// </summary>
public sealed class RunWatch
{
    private readonly RunRules rules;
    private IReadOnlyList<string>? last;
    private TimeSpan changedAt;
    private TimeSpan? spacedAt;
    private TimeSpan? resumedAt;

    /// <summary>A watch by the rules.</summary>
    public RunWatch(RunRules rules)
    {
        this.rules = rules;
    }

    /// <summary>How the run ended; <see cref="RunOutcome.None"/> until <see cref="Look"/> says <see cref="RunAction.Stop"/>.</summary>
    public RunOutcome Outcome { get; private set; }

    /// <summary>The run's lines last seen; empty while there is no file.</summary>
    public IReadOnlyList<string> Lines => last ?? Array.Empty<string>();

    /// <summary>When the game was first seen holding the map open, from the game's start; null while it has not been.</summary>
    public TimeSpan? MapOpenedAt { get; private set; }

    /// <summary>When the file was first seen, from the game's start; null while it has not been.</summary>
    public TimeSpan? FileAt { get; private set; }

    /// <summary>When the file first held more than its loading line (the game had started), from the game's start; null while it has not.</summary>
    public TimeSpan? StartedAt { get; private set; }

    /// <summary>Whether the last look found the file new or changed.</summary>
    public bool Changed { get; private set; }

    /// <summary>How many times space was posted.</summary>
    public int Spaces { get; private set; }

    /// <summary>Whether the game was resumed (F10, then Escape).</summary>
    public bool Resumed => resumedAt.HasValue;

    /// <summary>Looks at the game and its file, and says what to do.</summary>
    /// <param name="elapsed">The time since the game was started.</param>
    /// <param name="lines">The file's lines, or null when no file written since the start is there.</param>
    /// <param name="gameRuns">Whether the game's process still runs.</param>
    /// <param name="mapOpen">Whether the game holds the map open (it has opened it).</param>
    public RunAction Look(TimeSpan elapsed, IReadOnlyList<string>? lines, bool gameRuns, bool mapOpen)
    {
        if (Outcome != RunOutcome.None)
        {
            return RunAction.Stop;
        }

        Changed = lines != null && (last == null || !last.SequenceEqual(lines));
        if (Changed)
        {
            FileAt ??= elapsed;
            last = lines;
            changedAt = elapsed;
        }

        if (mapOpen)
        {
            MapOpenedAt ??= elapsed;
        }

        var loading = last != null && last.Count > 0 && last[last.Count - 1] == rules.LoadingLine;
        if (last != null && !loading)
        {
            StartedAt ??= elapsed;
        }

        if (last != null && last.Contains(rules.OverLine))
        {
            return Stop(RunOutcome.Over);
        }

        if (!gameRuns)
        {
            return Stop(RunOutcome.GameEnded);
        }

        if (elapsed >= rules.Limit)
        {
            return Stop(RunOutcome.TimedOut);
        }

        if (last == null)
        {
            if (elapsed >= rules.LoadLimit)
            {
                return Stop(RunOutcome.NoFile);
            }

            return MapOpenedAt is { } opened && elapsed - opened >= rules.SpaceAfterOpen ? Space(elapsed) : RunAction.Wait;
        }

        if (elapsed - changedAt >= rules.Stall && (!resumedAt.HasValue || elapsed - resumedAt.Value >= rules.Stall))
        {
            if (!resumedAt.HasValue)
            {
                resumedAt = elapsed;
                return RunAction.Resume;
            }

            return Stop(loading ? RunOutcome.StuckLoading : RunOutcome.Stalled);
        }

        return loading ? Space(elapsed) : RunAction.Wait;
    }

    /// <summary>Ends the watch with an outcome found outside it (another game ran).</summary>
    public void End(RunOutcome outcome)
    {
        if (Outcome == RunOutcome.None)
        {
            Outcome = outcome;
        }
    }

    // Space, when the last was SpaceEvery ago or more.
    private RunAction Space(TimeSpan elapsed)
    {
        if (spacedAt.HasValue && elapsed - spacedAt.Value < rules.SpaceEvery)
        {
            return RunAction.Wait;
        }

        spacedAt = elapsed;
        Spaces++;
        return RunAction.PostSpace;
    }

    private RunAction Stop(RunOutcome outcome)
    {
        Outcome = outcome;
        return RunAction.Stop;
    }
}
