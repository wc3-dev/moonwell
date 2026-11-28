using Moonwell.GameRun;
using Moonwell.Tool;
using Xunit;
using Xunit.Abstractions;

namespace Moonwell.Build.Tests
{
    /// <summary>A fact that starts the game: skipped unless MOONWELL_LAUNCH=1 and the game is installed (GameRunOptions.WhyNoLaunch).</summary>
    public sealed class LaunchFactAttribute : FactAttribute
    {
        /// <summary>Skips the fact, saying why, when it cannot play here.</summary>
        public LaunchFactAttribute()
        {
            if (GameRunOptions.WhyNoLaunch() is { } why)
            {
                Skip = why;
            }
        }
    }

    /// <summary>The runner that plays a built map in the game with nobody at the keyboard: its file reading and its watch, and one run in the game.</summary>
    public sealed class GameRunTests : IDisposable
    {
        private static readonly RunRules Rules = new RunRules
        {
            Limit = TimeSpan.FromSeconds(300),
            LoadLimit = TimeSpan.FromSeconds(120),
            Stall = TimeSpan.FromSeconds(30),
            SpaceEvery = TimeSpan.FromSeconds(2),
        };

        private readonly ITestOutputHelper output;

        private readonly TestFolders folders;

        public GameRunTests(ITestOutputHelper output)
        {
            this.output = output;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

        [Fact]
        public void the_file_gives_the_text_between_each_preload_line_and_its_close()
        {
            var text = "function PreloadFiles takes nothing returns nothing\r\n\r\n\tcall PreloadStart()\r\n" +
                "\tcall Preload( \"\")\r\nstarted\r\n//\" )\r\n" +
                "\tcall Preload( \"\")\r\nERROR in measure: x\r\n//\" )\r\n" +
                "\tcall PreloadEnd( 0.0 )\r\n\r\nendfunction\r\n";

            Assert.Equal(new[] { "started", "ERROR in measure: x" }, RunFile.Parse(text));
        }

        [Fact]
        public void a_file_written_before_the_game_started_is_an_earlier_runs()
        {
            var folder = folders.TemporaryCopyOf("InTheGame");
            var path = Path.Combine(folder, "run.txt");
            File.WriteAllText(path, "\tcall Preload( \"\")\nold\n//\" )\n");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));

            Assert.Null(RunFile.ReadIfWrittenSince(path, DateTime.UtcNow.AddSeconds(-5)));
            Assert.Equal(new[] { "old" }, RunFile.ReadIfWrittenSince(path, DateTime.UtcNow.AddMinutes(-2)));
            Assert.Null(RunFile.ReadIfWrittenSince(Path.Combine(folder, "none.txt"), DateTime.MinValue));
        }

        [Fact]
        public void space_is_posted_every_two_seconds_from_two_seconds_after_the_game_opens_the_map_until_it_has_started()
        {
            var watch = new RunWatch(Rules);

            Assert.Equal(RunAction.Wait, watch.Look(At(10), null, true, false));
            Assert.Equal(RunAction.Wait, watch.Look(At(15), null, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(16), null, true, true));
            Assert.Equal(RunAction.PostSpace, watch.Look(At(17), null, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(18), null, true, true));
            Assert.Equal(RunAction.PostSpace, watch.Look(At(19), new[] { "-- loading" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(20), new[] { "-- loading" }, true, true));
            Assert.Equal(RunAction.PostSpace, watch.Look(At(21), new[] { "-- loading" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(23), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(25), new[] { "started" }, true, true));

            Assert.Equal(3, watch.Spaces);
            Assert.Equal(At(15), watch.MapOpenedAt);
            Assert.Equal(At(19), watch.FileAt);
            Assert.Equal(At(23), watch.StartedAt);
        }

        [Fact]
        public void no_space_is_posted_before_the_game_opens_the_map()
        {
            var watch = new RunWatch(Rules);
            for (var second = 1; second < 120; second++)
            {
                Assert.Equal(RunAction.Wait, watch.Look(At(second), null, true, false));
            }

            Assert.Equal(RunAction.Stop, watch.Look(At(120), null, true, false));
            Assert.Equal(RunOutcome.NoFile, watch.Outcome);
            Assert.Null(watch.MapOpenedAt);
            Assert.Equal(0, watch.Spaces);
        }

        [Fact]
        public void the_over_line_stops_the_run_even_when_the_game_has_ended()
        {
            var watch = new RunWatch(Rules);

            Assert.Equal(RunAction.Stop, watch.Look(At(30), new[] { "started", "-- run over" }, false, true));
            Assert.Equal(RunOutcome.Over, watch.Outcome);
            Assert.Equal(At(30), watch.StartedAt);
            Assert.Equal(RunAction.Stop, watch.Look(At(31), null, true, true));
        }

        [Fact]
        public void a_game_that_ends_before_the_over_line_stops_the_run()
        {
            var watch = new RunWatch(Rules);
            watch.Look(At(20), new[] { "started" }, true, true);

            Assert.Equal(RunAction.Stop, watch.Look(At(21), new[] { "started" }, false, true));
            Assert.Equal(RunOutcome.GameEnded, watch.Outcome);
        }

        [Fact]
        public void a_file_that_stops_changing_is_resumed_once_and_then_given_up()
        {
            var watch = new RunWatch(Rules);
            watch.Look(At(20), new[] { "started" }, true, true);

            Assert.Equal(RunAction.Wait, watch.Look(At(49), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Resume, watch.Look(At(50), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(79), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Stop, watch.Look(At(80), new[] { "started" }, true, true));
            Assert.Equal(RunOutcome.Stalled, watch.Outcome);
            Assert.True(watch.Resumed);
        }

        [Fact]
        public void a_file_that_changes_after_the_resume_runs_on()
        {
            var watch = new RunWatch(Rules);
            watch.Look(At(20), new[] { "-- loading" }, true, true);

            Assert.Equal(RunAction.Resume, watch.Look(At(50), new[] { "-- loading" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(55), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Wait, watch.Look(At(84), new[] { "started" }, true, true));
            Assert.Equal(RunAction.Stop, watch.Look(At(85), new[] { "started" }, true, true));
            Assert.Equal(RunOutcome.Stalled, watch.Outcome);
        }

        [Fact]
        public void a_loading_line_that_stays_is_given_up_as_stuck_loading()
        {
            var watch = new RunWatch(Rules);
            var loading = new[] { "-- loading" };
            watch.Look(At(20), loading, true, true);
            watch.Look(At(50), loading, true, true);

            Assert.Equal(RunAction.Stop, watch.Look(At(80), loading, true, true));
            Assert.Equal(RunOutcome.StuckLoading, watch.Outcome);
        }

        [Fact]
        public void the_time_limit_stops_a_run_that_keeps_writing()
        {
            var watch = new RunWatch(Rules);
            for (var second = 20; second < 300; second += 10)
            {
                Assert.Equal(RunAction.Wait, watch.Look(At(second), new[] { "line " + second }, true, true));
            }

            Assert.Equal(RunAction.Stop, watch.Look(At(300), new[] { "line 300" }, true, true));
            Assert.Equal(RunOutcome.TimedOut, watch.Outcome);
        }

        [Fact]
        public void a_run_passes_only_when_it_is_over_and_no_line_fails()
        {
            var lines = new[] { "started", "FAIL maths.one: 2", "ERROR in measure: x", "-- run over" };
            var failing = GameRunner.FailingLines(lines, new[] { "ERROR", "FAIL" });

            Assert.Equal(new[] { "FAIL maths.one: 2", "ERROR in measure: x" }, failing);
            Assert.False(new GameRunResult(RunOutcome.Over, lines, failing, "", "3.0.1.24371").Passed);
            Assert.True(new GameRunResult(RunOutcome.Over, new[] { "-- run over" }, Array.Empty<string>(), "", "3.0.1.24371").Passed);
            Assert.False(new GameRunResult(RunOutcome.TimedOut, Array.Empty<string>(), Array.Empty<string>(), "", null).Passed);
            Assert.StartsWith("FAIL: the time limit was over", new GameRunResult(RunOutcome.TimedOut, Array.Empty<string>(), Array.Empty<string>(), "", null).Verdict);
        }

        /// <summary>
        /// The fixture InTheGame, built and played in the game with nobody at the keyboard: once the runner has got the game past
        /// its loading screen, the map writes what it measured and the over line.
        /// </summary>
        [LaunchFact]
        public void a_print_only_map_plays_in_the_game_start_to_finish()
        {
            var folder = folders.TemporaryCopyOf("InTheGame");
            var build = new StringWriter();
            Assert.True(Cli.Run(new[] { "build", folder }, build, build, (_, _) => throw new InvalidOperationException("build starts nothing")) == 0, build.ToString());

            var result = GameRunner.Run(new GameRunOptions
            {
                Map = Path.Combine(folder, "build", "InTheGame.w3x"),
                File = "MoonwellTests\\InTheGame.txt",
                Rules = Rules,
                Progress = output.WriteLine,
            });

            foreach (var line in result.Lines)
            {
                output.WriteLine(line);
            }

            output.WriteLine(result.Verdict);
            output.WriteLine(result.Detail);
            Assert.True(result.Passed, result.Verdict);
            Assert.Equal(new[] { "started", "integer max + 1 = -2147483648", "-- run over" }, result.Lines);
        }
    }
}
