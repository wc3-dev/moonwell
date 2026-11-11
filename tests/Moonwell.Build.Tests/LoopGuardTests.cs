using System.Text.RegularExpressions;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// The loop guard in a map built as a development build and run in Lua: a loop that never ends is stopped by a reported
    /// error that names its line, and what comes after it runs.
    /// </summary>
    [Collection("Sample")]
    public sealed class LoopGuardTests : IClassFixture<LoopGuardTests.LoopsRun>
    {
        private const string Red = "|cffff4040";

        private const string Stopped = "more than 10000000 loop turns in one instant; the loop was stopped";

        // The comment on a line names the class whose loop is stopped there. Every OnGameStart runs in one instant.
        private const string Loops = """
            using Moonwell;

            namespace Fails
            {
                public sealed class Spins : GameComponent
                {
                    public override void OnGameStart()
                    {
                        var turns = 0;
                        while (turns >= 0) // stopped: Spins
                        {
                            turns++;
                        }

                        Game.Print("after the loop", 5f);
                    }
                }

                public sealed class Next : GameComponent
                {
                    public override void OnGameStart()
                    {
                        Game.Print("the next reaction ran", 5f);
                    }
                }

                public sealed class LocalTurns : GameComponent
                {
                    public override void OnGameStart()
                    {
                        // With the turns only one machine runs counted, the loop after them would be stopped.
                        Local.For(Player.One, () =>
                        {
                            for (var i = 0; i < 9900000; i++)
                            {
                            }
                        });
                        for (var i = 0; i < 500000; i++)
                        {
                        }

                        Game.Print("one machine's turns were left out", 5f);
                    }
                }

                public sealed class SpinsInATimer : GameComponent
                {
                    public override void OnGameStart()
                    {
                        After(0f, () =>
                        {
                            for (;;) // stopped: SpinsInATimer
                            {
                            }
                        });
                    }
                }

                public sealed class Works : GameComponent
                {
                    private int runs;

                    public override void OnGameStart()
                    {
                        Every(10f, () =>
                        {
                            for (var i = 0; i < 2000000; i++)
                            {
                            }

                            runs++;
                            Game.Print("worked " + runs, 5f);
                        });
                    }
                }
            }
            """;

        private readonly LoopsRun run;

        public LoopGuardTests(LoopsRun run)
        {
            this.run = run;
        }

        [Fact]
        public void a_loop_that_never_ends_is_a_reported_error_at_its_line_and_the_next_reaction_runs()
        {
            var at = Array.IndexOf(run.Screen.ToArray(), Red + "Moonwell: Spins.OnGameStart failed:|r " + Stopped + " (Loops.cs:" + LineOf("Spins") + ")");

            Assert.True(at >= 0, string.Join("\n", run.Screen));
            Assert.Equal("the next reaction ran", run.Screen[at + 1]);
            Assert.DoesNotContain("after the loop", run.Screen);
            Assert.Contains("Moonwell: Spins.OnGameStart failed: " + Stopped + " (Loops.cs:" + LineOf("Spins") + ")", run.Log);
        }

        [Fact]
        public void a_loop_with_an_empty_body_in_a_timer_is_stopped_too()
        {
            Assert.Contains(Red + "Moonwell: SpinsInATimer.Timer failed:|r " + Stopped + " (Loops.cs:" + LineOf("SpinsInATimer") + ")", run.Screen);
        }

        [Fact]
        public void turns_inside_local_for_are_not_counted_after_it()
        {
            Assert.Contains("one machine's turns were left out", run.Screen);
            Assert.DoesNotContain(run.Screen, line => line.Contains("LocalTurns", StringComparison.Ordinal));
        }

        [Fact]
        public void loops_that_run_while_game_time_moves_are_never_stopped()
        {
            // The harness moves the game's time between its rounds, and the timer runs once a round: more turns than the limit
            // in all, never in one instant.
            var worked = run.Screen.Count(line => line.StartsWith("worked ", StringComparison.Ordinal));

            Assert.True(worked * 2000000L > 10100000L, "worked " + worked + " times");
            Assert.DoesNotContain(run.Screen, line => line.Contains("Works", StringComparison.Ordinal));
        }

        private static int LineOf(string stopped)
        {
            var lines = Loops.ReplaceLineEndings("\n").Split('\n');
            return Array.FindIndex(lines, line => Regex.IsMatch(line, "// stopped: " + stopped + "$")) + 1;
        }

        /// <summary>The Fails fixture with the loops' classes for its own, built as a development build and run once.</summary>
        public sealed class LoopsRun : IDisposable
        {
            private readonly TestFolders folders = new TestFolders();

            public LoopsRun()
            {
                var folder = folders.TemporaryCopyOf("Fails");
                File.Delete(Path.Combine(folder, "Failing.cs"));
                File.WriteAllText(Path.Combine(folder, "Loops.cs"), Loops);
                File.WriteAllText(
                    Path.Combine(folder, "Map.cs"),
                    "namespace Fails\n{\n    public sealed class FailsMap : Moonwell.Map\n    {\n        public FailsMap()\n        {\n            Name = \"Fails\";\n"
                    + string.Concat(new[] { "Spins", "Next", "LocalTurns", "SpinsInATimer", "Works" }.Select(name => "            Components.Add(new " + name + "());\n"))
                    + "        }\n    }\n}\n");

                MapBuild.Run(folder, new BuildOptions(), TextWriter.Null);

                var ran = ErrorLinesTests.FailsBuilds.Start(File.ReadAllText(Path.Combine(MapProject.Find(folder).StagedFolder, "war3map.lua")), string.Empty);
                Screen = ran.Screen;
                Log = ran.Log;
            }

            public IReadOnlyList<string> Screen { get; }

            public IReadOnlyList<string> Log { get; }

            public void Dispose()
            {
                folders.Dispose();
            }
        }
    }
}
