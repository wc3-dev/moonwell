#if DEBUG
using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the AI tools of K7 (decisions 345 to 347): the live brain view in the debug panel, the decision log in
    /// Moonwell\brains.txt, and the commands -brain, -brainlog, -force, -fact and -wake. They go through the chat, the
    /// selection, the panel's lines and the written file only, and find a line by what it holds, not by its layout, but for
    /// what the spec fixes: the numbers, > on what runs and on the current option, and "forced".
    /// </summary>
    public static class BrainToolsCases
    {
        /// <summary>The file the log is written to at its stop, in CustomMapData.</summary>
        private const string LogFile = "Moonwell\\brains.txt";

        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A unit of player 1.</summary>
        private static Unit UnitOf(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(Footman, slot: 0))!;
        }

        /// <summary>Installs the fake debug world and turns the mode on with -debug.</summary>
        private static FakeDebugWorld On(FakeWorld world)
        {
            var debug = FakeDebugWorld.Install(world);
            debug.Chat(0, "-debug");
            return debug;
        }

        /// <summary>Runs the brains' loop for a number of its 0.02 s steps, and the debug mode's refresher with it.</summary>
        private static void Steps(FakeDebugWorld debug, int count)
        {
            for (var i = 0; i < count; i++)
            {
                debug.Advance(Minds.StepSeconds);
            }
        }

        /// <summary>The panel's lines, refreshed now, as a selection refreshes them.</summary>
        private static List<string> Panel(FakeDebugWorld debug)
        {
            DebugMode.Select(DebugMode.Selected);
            return debug.PanelLines();
        }

        /// <summary>Whether a character is a digit, 0 to 9.</summary>
        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>The number a line of a brain's view starts with, past spaces, > and #; -1 for a line that starts with none, or with a game time, as a log line does.</summary>
        private static int NumberOf(string line)
        {
            var at = 0;
            while (at < line.Length && (line[at] == ' ' || line[at] == '>' || line[at] == '#'))
            {
                at++;
            }

            var start = at;
            var number = 0;
            while (at < line.Length && IsDigit(line[at]))
            {
                number = number * 10 + (line[at] - '0');
                at++;
            }

            if (at == start || (at + 1 < line.Length && line[at] == '.' && IsDigit(line[at + 1])))
            {
                return -1;
            }

            return number;
        }

        /// <summary>The line of a brain's view numbered n; null when none shows it.</summary>
        private static string? Numbered(List<string> lines, int n)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (NumberOf(lines[i]) == n)
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>Whether a line is there and holds the text.</summary>
        private static bool Holds(string? line, string text)
        {
            // IndexOf, not Contains: the fork's Contains starts its search past the start when the text holds a Lua magic character.
            return line != null && line.IndexOf(text) >= 0;
        }

        /// <summary>The first line holding the text; null when none does.</summary>
        private static string? Find(List<string> lines, string text)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (Holds(lines[i], text))
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>The first line holding both texts; null when none does.</summary>
        private static string? FindBoth(List<string> lines, string first, string second)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (Holds(lines[i], first) && Holds(lines[i], second))
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>Whether a line starts with a game time, as a log line does: past spaces, digits, a point and a digit.</summary>
        private static bool IsLogLine(string line)
        {
            var at = 0;
            while (at < line.Length && line[at] == ' ')
            {
                at++;
            }

            var start = at;
            while (at < line.Length && IsDigit(line[at]))
            {
                at++;
            }

            return at > start && at + 1 < line.Length && line[at] == '.' && IsDigit(line[at + 1]);
        }

        /// <summary>The log lines among some lines, in order.</summary>
        private static List<string> LogLines(List<string> lines)
        {
            var found = new List<string>();
            for (var i = 0; i < lines.Count; i++)
            {
                if (IsLogLine(lines[i]))
                {
                    found.Add(lines[i]);
                }
            }

            return found;
        }

        /// <summary>The panel's log lines as one text, to see whether a line was added.</summary>
        private static string LogShown(FakeDebugWorld debug)
        {
            return string.Join("\n", LogLines(Panel(debug)));
        }

        /// <summary>The game time a log line starts with: "12.25".</summary>
        private static string TimeOf(string line)
        {
            var at = 0;
            while (at < line.Length && line[at] == ' ')
            {
                at++;
            }

            var end = at;
            while (end < line.Length && line[end] != ' ')
            {
                end++;
            }

            return line.Substring(at, end - at);
        }

        /// <summary>The log lines of the written file; the case fails when it was not written.</summary>
        private static List<string> Written(FakeDebugWorld debug)
        {
            Expect.True(debug.Files.ContainsKey(LogFile), "the log is written to " + LogFile);
            return LogLines(debug.Files[LogFile]);
        }

        /// <summary>The name the panel gives an actor, read from its object line ("Unit 4"); it leaves the actor selected.</summary>
        private static string NameShown(FakeDebugWorld debug, Actor actor)
        {
            DebugMode.Select(actor);
            var line = debug.PanelLines()[1];
            return line.Substring(0, line.IndexOf(" ("));
        }

        /// <summary>Whether a line printed since a count of them holds the text.</summary>
        private static bool Printed(FakeWorld world, int from, string text)
        {
            for (var i = from; i < world.PrintedTexts.Count; i++)
            {
                if (Holds(world.PrintedTexts[i], text))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The last line printed, for a failure's message.</summary>
        private static string Last(FakeWorld world)
        {
            return world.PrintedTexts.Count > 0 ? world.PrintedTexts[world.PrintedTexts.Count - 1] : "nothing printed";
        }

        /// <summary>How many logged lines hold the text.</summary>
        private static int Logged(FakeWorld world, string text)
        {
            var found = 0;
            for (var i = 0; i < world.Logged.Count; i++)
            {
                if (Holds(world.Logged[i], text))
                {
                    found++;
                }
            }

            return found;
        }

        /// <summary>Whether a line holds a number on its own, not inside a longer number or a decimal.</summary>
        private static bool HoldsNumber(string line, int number)
        {
            var text = "" + number;
            for (var at = 0; at + text.Length <= line.Length; at++)
            {
                if (line.Substring(at, text.Length) != text)
                {
                    continue;
                }

                var end = at + text.Length;
                var before = at == 0 || !(IsDigit(line[at - 1]) || line[at - 1] == '.');
                var after = end == line.Length || !(IsDigit(line[end]) || (line[end] == '.' && end + 1 < line.Length && IsDigit(line[end + 1])));
                if (before && after)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a line lists -brain itself, not only -brainlog.</summary>
        private static bool ListsBrain(string line)
        {
            for (var at = 0; at + 6 <= line.Length; at++)
            {
                if (line.Substring(at, 6) == "-brain" && (at + 9 > line.Length || line.Substring(at + 6, 3) != "log"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A unit's tree brain: a head line with its class and its tick, its 7 nodes numbered depth-first from 1 with > on
        /// those running (the Select, the Sequence and its running child), and the When with its fact's name and last value;
        /// once the fact is true, the When has cut in and > follows it.
        /// </summary>
        public static void ViewTree(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.8f);
            brain.Blackboard.Set(ToolFacts.Hurt, false);
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 1);
            var lines = Panel(debug);
            var head = Find(lines, "ToolBrain");
            Expect.True(head != null, "a head line names the brain's class: " + debug.PanelText);
            Expect.True(Holds(head, "0.02"), "and how it thinks, every 0.02 s: " + head);
            Expect.True(Holds(Numbered(lines, 1), "Select") && Holds(Numbered(lines, 1), ">"), "1 is the root Select, running: " + debug.PanelText);
            var when = Numbered(lines, 2);
            Expect.True(Holds(when, "When") && Holds(when, "hurt") && Holds(when, "false"), "2 is the When, with its fact's name and last value: " + when);
            Expect.True(!Holds(when, ">"), "not running: " + when);
            Expect.True(Numbered(lines, 3) != null && !Holds(Numbered(lines, 3), ">"), "3, its child, not started: " + Numbered(lines, 3));
            Expect.True(Holds(Numbered(lines, 4), "Sequence") && Holds(Numbered(lines, 4), ">"), "4 is the Sequence, running: " + Numbered(lines, 4));
            Expect.True(Numbered(lines, 5) != null && !Holds(Numbered(lines, 5), ">"), "5 succeeded: " + Numbered(lines, 5));
            Expect.True(Holds(Numbered(lines, 6), ">"), "6 runs: " + Numbered(lines, 6));
            Expect.True(Numbered(lines, 7) != null && !Holds(Numbered(lines, 7), ">"), "7 is not reached: " + Numbered(lines, 7));
            Expect.True(Find(lines, "forced") == null, "nothing is forced");

            brain.Blackboard.Set(ToolFacts.Hurt, true);
            Steps(debug, 1);
            lines = Panel(debug);
            when = Numbered(lines, 2);
            Expect.True(Holds(when, ">") && Holds(when, "true"), "the When cut in, its fact now true: " + when);
            Expect.True(Holds(Numbered(lines, 3), ">"), "its child runs: " + Numbered(lines, 3));
            Expect.True(!Holds(Numbered(lines, 4), ">") && !Holds(Numbered(lines, 6), ">"), "the Sequence was stopped: " + debug.PanelText);
        }

        /// <summary>
        /// A choice's options are numbered after the tree's 7 nodes (8 fight, 9 flee, 10 rest), each with its last score to two
        /// decimals and > on the current one; the current one shows its inputs, so rest's "tired", which no board holds, shows
        /// only once rest is current.
        /// </summary>
        public static void ViewChoice(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.8f);
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 1);
            var lines = Panel(debug);
            var fight = Numbered(lines, 8);
            Expect.True(Holds(fight, "fight") && Holds(fight, "0.80") && Holds(fight, ">"), "8 is fight at 0.80, current: " + debug.PanelText);
            var flee = Numbered(lines, 9);
            Expect.True(Holds(flee, "flee") && Holds(flee, "0.30") && !Holds(flee, ">"), "9 is flee at 0.30: " + flee);
            var rest = Numbered(lines, 10);
            Expect.True(Holds(rest, "rest") && Holds(rest, "0.50") && !Holds(rest, ">"), "10 is rest at 0.50: " + rest);
            Expect.True(Find(lines, "tired") == null, "rest is not current, so its input is not shown: " + debug.PanelText);

            brain.Blackboard.Set(ToolFacts.Morale, 0.2f);
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.Equal("rest", brain.Stance, "rest at 0.50 beats fight at 0.20");
            Expect.True(Holds(Numbered(lines, 10), ">") && Holds(Numbered(lines, 10), "0.50"), "> on rest: " + Numbered(lines, 10));
            Expect.True(!Holds(Numbered(lines, 8), ">") && Holds(Numbered(lines, 8), "0.20"), "fight at 0.20, no longer current: " + Numbered(lines, 8));
            Expect.True(Find(lines, "tired") != null, "the current option's input, tired, is shown: " + debug.PanelText);
        }

        /// <summary>The facts held on the brain's board and up its chain, each with its value and its age to a tenth of a second; the owner's says Player 1, the game's says game, and the brain's own says neither.</summary>
        public static void ViewFacts(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.8f);
            Player.Get(0).Blackboard().Set(ToolFacts.Rations, 12);
            Game.Instance.Blackboard().Set(ToolFacts.Omen, true);
            var debug = On(world);
            debug.Advance(1.5f);
            DebugMode.Select(unit);
            var lines = debug.PanelLines();
            var morale = FindBoth(lines, "morale", "1.5");
            Expect.True(morale != null, "the brain's own fact with its age, 1.5 s: " + debug.PanelText);
            Expect.True(Holds(morale, "0.8") && !Holds(morale, "Player") && !Holds(morale, "game") && !Holds(morale, "Game"), "its value, and no other board named: " + morale);
            var rations = Find(lines, "rations");
            Expect.True(Holds(rations, "12") && Holds(rations, "1.5") && Holds(rations, "Player 1"), "the owner's fact, with its value and age, from Player 1: " + rations);
            var omen = Find(lines, "omen");
            Expect.True(Holds(omen, "true") && Holds(omen, "1.5") && (Holds(omen, "game") || Holds(omen, "Game")), "the game's fact, from the game: " + omen);
        }

        /// <summary>A unit without a brain shows its object line only; a player's brain shows, thinking on wake, when the player is selected, and the game's when the game is.</summary>
        public static void ViewOtherActors(FakeWorld world)
        {
            UnitOf(world).Add(new ToolBrain());
            var plain = UnitOf(world);
            Player.Get(0).Add(new ToolPlayerBrain());
            Game.Instance.Add(new ToolGameBrain());
            var debug = On(world);
            Steps(debug, 1);
            DebugMode.Select(plain);
            Expect.Equal(2, debug.PanelLines().Count, "a unit without a brain: the status and its object line: " + debug.PanelText);
            DebugMode.Select(Player.Get(0));
            var lines = debug.PanelLines();
            var head = Find(lines, "ToolPlayerBrain");
            Expect.True(head != null, "the player's brain: " + debug.PanelText);
            Expect.True(Holds(head, "wake"), "thinking on wake: " + head);
            Expect.True(Find(lines, "ToolGameBrain") == null, "not the game's");
            DebugMode.Select(Game.Instance);
            lines = debug.PanelLines();
            Expect.True(Find(lines, "ToolGameBrain") != null, "the game's brain: " + debug.PanelText);
            Expect.True(Find(lines, "ToolPlayerBrain") == null, "not the player's");
        }

        /// <summary>
        /// A tree of 30 nodes, too big for the panel, folds each subtree not running to its head line with +n, and the numbers
        /// stay: the failed Sequence at 4 with +12, the When at 2 with +1, the running Sequence at 17 with its children 18 to
        /// 29, and the root's last child at 30.
        /// </summary>
        public static void ViewFolds(FakeWorld world)
        {
            var unit = UnitOf(world);
            unit.Add(new BigTreeBrain());
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 1);
            var lines = Panel(debug);
            Expect.True(!lines[lines.Count - 1].StartsWith("and "), "the folded tree fits the panel: " + debug.PanelText);
            var running = Numbered(lines, 17);
            Expect.True(Holds(running, "Sequence") && Holds(running, ">"), "17 is the running Sequence: " + debug.PanelText);
            Expect.True(Holds(Numbered(lines, 18), ">"), "18 is its running child: " + Numbered(lines, 18));
            Expect.True(Numbered(lines, 29) != null, "29 is its last child");
            var failed = Numbered(lines, 4);
            Expect.True(Holds(failed, "Sequence") && Holds(failed, "+12"), "4 is the Sequence that failed, folded with its 12 nodes: " + failed);
            Expect.True(Numbered(lines, 5) == null && Numbered(lines, 16) == null, "whose nodes are not shown");
            Expect.True(Holds(Numbered(lines, 2), "+1") && Numbered(lines, 3) == null, "the When is folded with its child: " + Numbered(lines, 2));
            Expect.True(Numbered(lines, 30) != null, "30, the root's last child, keeps its number");
        }

        /// <summary>-brain toolbrain, in any case, selects the units holding one in turn, from the one after the selected, and says how many there are; a class no brain has says so and keeps the selection; a game brain's class selects the game.</summary>
        public static void BrainSelects(FakeWorld world)
        {
            var first = UnitOf(world);
            first.Add(new ToolBrain());
            var second = UnitOf(world);
            second.Add(new ToolBrain());
            var third = UnitOf(world);
            third.Add(new ToolBrain());
            UnitOf(world).Add(new StanceBrain());
            Game.Instance.Add(new ToolGameBrain());
            var debug = On(world);

            // Units named first take the numbers 1 to 3, so a 3 in the answer is the count.
            for (var i = 0; i < 3; i++)
            {
                NameShown(debug, UnitOf(world));
            }

            DebugMode.Select(null);
            var printed = world.PrintedTexts.Count;
            debug.Chat(0, "-brain toolbrain");
            var picked = DebugMode.Selected;
            Expect.True(picked == first || picked == second || picked == third, "-brain selects a unit holding a ToolBrain, the class in any case");
            Expect.True(world.PrintedTexts.Count > printed && HoldsNumber(Last(world), 3), "and says there are 3: " + Last(world));
            debug.Chat(0, "-brain ToolBrain");
            var next = DebugMode.Selected;
            Expect.True(next != picked && (next == first || next == second || next == third), "again, the next");
            debug.Chat(0, "-brain ToolBrain");
            var last = DebugMode.Selected;
            Expect.True(last != picked && last != next && (last == first || last == second || last == third), "and the third");
            debug.Chat(0, "-brain ToolBrain");
            Expect.Same(picked, DebugMode.Selected, "then the first again");

            printed = world.PrintedTexts.Count;
            debug.Chat(0, "-brain Nobody");
            Expect.Same(picked, DebugMode.Selected, "a class no brain has keeps the selection");
            Expect.True(Printed(world, printed, "Nobody"), "and says so: " + Last(world));
            debug.Chat(0, "-brain ToolGameBrain");
            Expect.Same(Game.Instance, DebugMode.Selected, "a game brain's class selects the game");
        }

        /// <summary>
        /// -brainlog follows the selected unit's brain, its head line saying so, and -brainlog again writes Moonwell\brains.txt:
        /// a line each time the choice's winner changes, from none too, and none at the thinks between, each naming the unit
        /// and the facts read.
        /// </summary>
        public static void LogChoice(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new StanceBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.9f);
            var debug = On(world);
            var name = NameShown(debug, unit);
            Expect.True(FindBoth(Panel(debug), "StanceBrain", "log") == null, "not logged yet: " + debug.PanelText);
            debug.Chat(0, "-brainlog");
            Expect.True(FindBoth(Panel(debug), "StanceBrain", "log") != null, "the head line says the log follows it: " + debug.PanelText);
            Steps(debug, 6);
            brain.Blackboard.Set(ToolFacts.Morale, 0.2f);
            Steps(debug, 6);
            debug.Chat(0, "-brainlog");
            var lines = Written(debug);
            Expect.Equal(2, lines.Count, "a line at each change of winner, none at the 10 thinks between: " + string.Join(" | ", lines));
            Expect.True(Holds(lines[0], name) && Holds(lines[0], "fight"), "first fight won, from none: " + lines[0]);
            Expect.True(Holds(lines[1], name) && Holds(lines[1], "rest") && Holds(lines[1], "fight"), "then rest, over fight: " + lines[1]);
            Expect.True(Holds(lines[0], "morale") && Holds(lines[1], "morale"), "each with the facts read");
            Expect.True(FindBoth(Panel(debug), "StanceBrain", "log") == null, "no longer logged: " + debug.PanelText);
        }

        /// <summary>A tree's log, followed with nothing selected: a line when the When cuts in and when it ends, each with the fact read, and none while the same branch runs on.</summary>
        public static void LogTree(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new GuardBrain());
            brain.Blackboard.Set(ToolFacts.Hurt, false);
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-brainlog");
            DebugMode.Select(null);
            Steps(debug, 2);
            var shown = LogShown(debug);
            Steps(debug, 5);
            Expect.Equal(shown, LogShown(debug), "no line while the Sequence runs on");

            brain.Blackboard.Set(ToolFacts.Hurt, true);
            Steps(debug, 1);
            var cut = LogLines(Panel(debug));
            Expect.True(cut.Count > 0 && string.Join("\n", cut) != shown, "a line when the When cuts in: " + debug.PanelText);
            Expect.True(Holds(cut[cut.Count - 1], "hurt"), "with the fact read: " + cut[cut.Count - 1]);
            shown = string.Join("\n", cut);
            Steps(debug, 5);
            Expect.Equal(shown, LogShown(debug), "none while the When's branch runs on");

            brain.Blackboard.Set(ToolFacts.Hurt, false);
            Steps(debug, 1);
            var ended = LogLines(Panel(debug));
            Expect.True(ended.Count > 0 && string.Join("\n", ended) != shown, "a line when the When ends: " + debug.PanelText);
            Expect.True(Holds(ended[ended.Count - 1], "hurt"), "with the fact read: " + ended[ended.Count - 1]);
            debug.Chat(0, "-brainlog");
            Expect.True(Written(debug).Count >= 2, "both are written");
        }

        /// <summary>-brainlog StanceBrain follows every brain of the class, one made after it started too, and no other; with nothing selected the panel shows the last 5 of its 8 lines under a head with the count.</summary>
        public static void LogClass(FakeWorld world)
        {
            var first = UnitOf(world);
            first.Add(new StanceBrain());
            var other = UnitOf(world);
            other.Add(new ToolBrain());
            var mine = Player.Get(0).Blackboard();
            mine.Set(ToolFacts.Morale, 0.9f);
            var debug = On(world);
            var firstName = NameShown(debug, first);
            var otherName = NameShown(debug, other);
            DebugMode.Select(null);
            debug.Chat(0, "-brainlog StanceBrain");
            var later = UnitOf(world);
            later.Add(new StanceBrain());
            var laterName = NameShown(debug, later);
            DebugMode.Select(null);

            // Morale flips at each think, so both brains' winner changes at each: 2 lines a think.
            for (var i = 0; i < 4; i++)
            {
                mine.Set(ToolFacts.Morale, i % 2 == 0 ? 0.9f : 0.2f);
                Steps(debug, 1);
            }

            var lines = Panel(debug);
            var shown = LogLines(lines);
            Expect.Equal(5, shown.Count, "the panel shows the last 5 of 8 lines: " + debug.PanelText);
            var at = lines.IndexOf(shown[0]);
            Expect.True(at >= 1 && HoldsNumber(lines[at - 1], 8), "under a head with the count, 8: " + debug.PanelText);

            debug.Chat(0, "-brainlog");
            var written = Written(debug);
            Expect.Equal(8, written.Count, "the 8 lines: " + string.Join(" | ", written));
            var firsts = 0;
            var laters = 0;
            var others = 0;
            for (var i = 0; i < written.Count; i++)
            {
                firsts += Holds(written[i], firstName) ? 1 : 0;
                laters += Holds(written[i], laterName) ? 1 : 0;
                others += Holds(written[i], otherName) ? 1 : 0;
            }

            Expect.Equal(4, firsts, "the brain there when the log started: " + firstName);
            Expect.Equal(4, laters, "the brain made after it started: " + laterName);
            Expect.Equal(0, others, "no brain of another class: " + otherName);
        }

        /// <summary>The log keeps the newest 2,000 lines: 10 brains changing their winner at 201 thinks make 2,010, and the 10 of the first think, the oldest, are dropped.</summary>
        public static void LogKeepsNewest(FakeWorld world)
        {
            for (var i = 0; i < 10; i++)
            {
                UnitOf(world).Add(new StanceBrain());
            }

            var mine = Player.Get(0).Blackboard();
            mine.Set(ToolFacts.Morale, 0.9f);
            var debug = On(world);
            debug.Chat(0, "-brainlog StanceBrain");
            Steps(debug, 1);
            var oldest = LogLines(Panel(debug));
            Expect.True(oldest.Count > 0, "the first think's lines show: " + debug.PanelText);
            var firstTime = TimeOf(oldest[0]);
            for (var i = 0; i < 200; i++)
            {
                mine.Set(ToolFacts.Morale, i % 2 == 0 ? 0.2f : 0.9f);
                Steps(debug, 1);
            }

            debug.Chat(0, "-brainlog");
            var lines = Written(debug);
            Expect.Equal(2000, lines.Count, "the newest 2,000 of 2,010 lines");
            Expect.True(TimeOf(lines[0]) != firstTime, "oldest first, the first think's dropped: " + lines[0]);
        }

        /// <summary>
        /// -force 7 has the Select run its child 7 and no other, -force 2 makes the When's condition read true, and -force 10
        /// makes rest win whatever the scores, each line saying forced; -force alone lets go. A number naming the root, a
        /// Sequence's child or nothing says so, and forces nothing.
        /// </summary>
        public static void Force(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.8f);
            brain.Blackboard.Set(ToolFacts.Hurt, false);
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 1);
            Expect.True(Holds(Numbered(Panel(debug), 4), ">"), "the Sequence runs at first: " + debug.PanelText);

            debug.Chat(0, "-force 7");
            Steps(debug, 1);
            var lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 7), ">") && Holds(Numbered(lines, 7), "forced"), "7 runs, forced: " + debug.PanelText);
            Expect.True(!Holds(Numbered(lines, 4), ">"), "the Sequence no longer runs: " + Numbered(lines, 4));
            debug.Chat(0, "-force");
            Steps(debug, 1);
            Expect.True(Find(Panel(debug), "forced") == null, "-force alone lets go: " + debug.PanelText);

            debug.Chat(0, "-force 2");
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 2), ">") && Holds(Numbered(lines, 2), "forced"), "the When runs though hurt is false, forced: " + debug.PanelText);
            Expect.True(Holds(Numbered(lines, 3), ">"), "and its child: " + Numbered(lines, 3));
            debug.Chat(0, "-force");
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.True(!Holds(Numbered(lines, 2), ">") && Holds(Numbered(lines, 4), ">"), "let go, the When ends and the Sequence runs: " + debug.PanelText);

            debug.Chat(0, "-force 10");
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.Equal("rest", brain.Stance, "rest won, though fight scores more");
            Expect.True(Holds(Numbered(lines, 10), ">") && Holds(Numbered(lines, 10), "forced"), "> on rest, forced: " + Numbered(lines, 10));
            Expect.True(!Holds(Numbered(lines, 8), ">"), "fight is not current: " + Numbered(lines, 8));
            debug.Chat(0, "-force");
            Steps(debug, 1);
            Expect.Equal("fight", brain.Stance, "let go, fight wins again");
            Expect.True(Find(Panel(debug), "forced") == null, "nothing forced: " + debug.PanelText);

            var refused = new string[] { "-force 1", "-force 5", "-force 99" };
            for (var i = 0; i < refused.Length; i++)
            {
                var printed = world.PrintedTexts.Count;
                debug.Chat(0, refused[i]);
                Steps(debug, 1);
                Expect.True(world.PrintedTexts.Count > printed, refused[i] + " says so");
                Expect.True(Find(Panel(debug), "forced") == null, refused[i] + " forces nothing: " + debug.PanelText);
            }
        }

        /// <summary>-fact sets an int, a float, a bool and a string fact on the selected brain's own board, the name in any case; a worked-out fact, an unknown name, two facts of one name and a fact of another type are refused, saying so.</summary>
        public static void SetFact(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-fact ammo 7");
            Expect.Equal(7, brain.Read(ToolFacts.Ammo), "an int");
            Expect.Equal(0, Player.Get(0).Blackboard().Read(ToolFacts.Ammo), "on the brain's own board, not its owner's");
            debug.Chat(0, "-fact MORALE 0.25");
            Expect.Near(0.25f, brain.Read(ToolFacts.Morale), "a float, the name in any case");
            debug.Chat(0, "-fact hurt true");
            Expect.Equal(true, brain.Read(ToolFacts.Hurt), "a bool, true");
            debug.Chat(0, "-fact hurt 0");
            Expect.Equal(false, brain.Read(ToolFacts.Hurt), "0");
            debug.Chat(0, "-fact hurt 1");
            Expect.Equal(true, brain.Read(ToolFacts.Hurt), "1");
            debug.Chat(0, "-fact hurt false");
            Expect.Equal(false, brain.Read(ToolFacts.Hurt), "false");
            debug.Chat(0, "-fact shout charge");
            Expect.Equal("charge", brain.Read(ToolFacts.Shout), "a string");
            debug.Chat(0, "-fact ammo lots");
            Expect.Equal(7, brain.Read(ToolFacts.Ammo), "a value an int cannot be leaves it");

            var printed = world.PrintedTexts.Count;
            debug.Chat(0, "-fact tally 5");
            Expect.Equal(3, brain.Read(ToolFacts.Tally), "a worked-out fact is refused");
            Expect.True(Printed(world, printed, "tally"), "saying so: " + Last(world));
            printed = world.PrintedTexts.Count;
            debug.Chat(0, "-fact nothere 1");
            Expect.True(Printed(world, printed, "nothere"), "an unknown name says so: " + Last(world));
            printed = world.PrintedTexts.Count;
            debug.Chat(0, "-fact twin 2");
            Expect.Equal(0, brain.Read(ToolFacts.TwinCount), "two facts of one name are refused");
            Expect.Near(0f, brain.Read(ToolFacts.TwinNumber), "either of them");
            Expect.True(Printed(world, printed, "twin"), "saying so: " + Last(world));
            printed = world.PrintedTexts.Count;
            debug.Chat(0, "-fact quarry 1");
            Expect.True(brain.Read(ToolFacts.Quarry) == null, "a fact of a type -fact cannot read is refused");
            Expect.True(Printed(world, printed, "quarry"), "saying so: " + Last(world));
        }

        /// <summary>-wake has the selected unit's brains, both of them, think at the next step, within 0.02 s, and once.</summary>
        public static void Wake(FakeWorld world)
        {
            var unit = UnitOf(world);
            var first = unit.Add(new StepBrain());
            var second = unit.Add(new StepBrain());
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 3);
            Expect.Equal(0, first.Thought.Count, "a brain that thinks on wake has not thought");
            debug.Chat(0, "-wake");
            Steps(debug, 1);
            Expect.Equal(1, first.Thought.Count, "-wake: it thinks within 0.02 s");
            Expect.Equal(1, second.Thought.Count, "and so does the unit's other brain");
            Steps(debug, 3);
            Expect.Equal(1, first.Thought.Count, "once");
        }

        /// <summary>While -debug is off the commands answer nothing and start no log; -help lists them; a map's own -wake is reported and wins, and the debug -wake steps aside (decision 261).</summary>
        public static void OffAndMapCommands(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var unit = UnitOf(world);
            var brain = unit.Add(new StepBrain());
            UnitOf(world).Add(new StanceBrain());
            var debug = FakeDebugWorld.Install(world);
            var printed = world.PrintedTexts.Count;
            debug.Chat(0, "-brain StepBrain");
            debug.Chat(0, "-brainlog StanceBrain");
            debug.Chat(0, "-force 1");
            debug.Chat(0, "-fact ammo 3");
            debug.Chat(0, "-wake");
            Steps(debug, 2);
            Expect.Equal(printed, world.PrintedTexts.Count, "nothing answers while -debug is off: " + Last(world));
            debug.Chat(0, "-debug");
            Expect.Same(null, DebugMode.Selected, "-brain selected nothing");
            Expect.Equal(1, debug.PanelLines().Count, "-brainlog started no log: the panel shows its status only: " + debug.PanelText);

            debug.Chat(0, "-help");
            var help = debug.PanelLines();
            Expect.True(Find(help, "-force") != null && Find(help, "-fact") != null && Find(help, "-wake") != null, "-help lists -force, -fact and -wake: " + debug.PanelText);
            Expect.True(Find(help, "-brainlog") != null, "and -brainlog");
            var brainListed = false;
            for (var i = 0; i < help.Count; i++)
            {
                brainListed = brainListed || ListsBrain(help[i]);
            }

            Expect.True(brainListed, "and -brain");
            debug.Chat(0, "-help");

            DebugMode.Select(unit);
            var heard = 0;
            Game.Instance.Command("wake", (who, rest) => heard++);
            Expect.Equal(1, Logged(world, "-wake is also a debug command"), "the clash is reported");
            debug.Chat(0, "-wake");
            Steps(debug, 2);
            Expect.Equal(0, brain.Thought.Count, "the debug -wake steps aside for the map's");
            world.Chat(Player.Get(0), "-wake");
            Expect.Equal(1, heard, "the map's -wake runs");
        }

        /// <summary>An enum fact shows by its number, as Lua keeps it, and an int? fact by its number, in the view and among the log's facts read; in Lua neither stops the view, the log or the brain's own reads.</summary>
        public static void EnumAndNullableFacts(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new MoodBrain());
            brain.Blackboard.Set(ToolFacts.Mood, ToolMood.Angry);
            brain.Blackboard.Set(ToolFacts.Spare, 5);
            brain.Blackboard.Set(ToolFacts.Morale, 0.9f);
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-brainlog");
            Steps(debug, 1);
            var lines = Panel(debug);
            // The view's facts give their age as "0.0 s", the log's as "0.0s".
            Expect.True(Find(lines, "mood 1 0.0 s") != null, "the view shows the enum by its number, Angry's 1: " + debug.PanelText);
            Expect.True(Find(lines, "spare 5 0.0 s") != null, "and the int? by its number: " + debug.PanelText);
            debug.Chat(0, "-brainlog");
            var written = Written(debug);
            Expect.True(written.Count == 1 && Holds(written[0], "mood 1") && Holds(written[0], "spare 5"), "the log's line names both as read: " + string.Join(" | ", written));
            Expect.Equal(0, Logged(world, "Moonwell.Debug"), "the tools report no error");
            Expect.Equal(0, Logged(world, "MoodBrain.Think"), "the brain's think goes on");
        }

        /// <summary>With the package installed as a map's Main installs it, -help lists the commands and -brainlog of a class starts before any brain is made, then follows the first one made.</summary>
        public static void BeforeAnyBrain(FakeWorld world)
        {
            AISystem.Install();
            var debug = On(world);
            debug.Chat(0, "-help");
            var help = debug.PanelLines();
            Expect.True(Find(help, "-brainlog") != null && Find(help, "-force") != null && Find(help, "-fact") != null, "-help lists the commands before any brain: " + debug.PanelText);
            debug.Chat(0, "-help");
            var printed = world.PrintedTexts.Count;
            debug.Chat(0, "-brainlog StanceBrain");
            Expect.True(Printed(world, printed, "StanceBrain"), "-brainlog of a class starts before any brain: " + Last(world));
            var unit = UnitOf(world);
            var brain = unit.Add(new StanceBrain());
            brain.Blackboard.Set(ToolFacts.Morale, 0.9f);
            Steps(debug, 2);
            debug.Chat(0, "-brainlog");
            var lines = Written(debug);
            Expect.True(lines.Count == 1 && Holds(lines[0], "fight"), "the brain made after it is followed: " + string.Join(" | ", lines));
        }

        /// <summary>-force on a Pick's option runs it and no other, whatever the scores, with its child; -force alone lets the best run again.</summary>
        public static void ForcePick(FakeWorld world)
        {
            var unit = UnitOf(world);
            unit.Add(new PickBrain());
            var debug = On(world);
            DebugMode.Select(unit);
            Steps(debug, 1);
            var lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 2), ">") && !Holds(Numbered(lines, 4), ">"), "2, the best option, runs: " + debug.PanelText);
            debug.Chat(0, "-force 4");
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 4), ">") && Holds(Numbered(lines, 4), "forced") && Holds(Numbered(lines, 5), ">"), "4 runs, forced, and its child: " + debug.PanelText);
            Expect.True(!Holds(Numbered(lines, 2), ">"), "2 no longer runs: " + Numbered(lines, 2));
            debug.Chat(0, "-force");
            Steps(debug, 1);
            lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 2), ">") && Find(lines, "forced") == null, "let go, the best runs again: " + debug.PanelText);
        }

        /// <summary>Two brains on one unit, both logged: each line names its brain's class, and the guard's tree lines carry the view's numbers, after the stance's two options: its When at 4 cuts in, then ends as its Sequence at 6 runs.</summary>
        public static void LogTwoBrains(FakeWorld world)
        {
            var unit = UnitOf(world);
            var stance = unit.Add(new StanceBrain());
            var guard = unit.Add(new GuardBrain());
            stance.Blackboard.Set(ToolFacts.Morale, 0.9f);
            guard.Blackboard.Set(ToolFacts.Hurt, false);
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-brainlog");
            Steps(debug, 1);
            var lines = Panel(debug);
            Expect.True(Holds(Numbered(lines, 4), "When hurt") && Holds(Numbered(lines, 6), "Sequence"), "the view numbers the When 4 and the Sequence 6: " + debug.PanelText);
            guard.Blackboard.Set(ToolFacts.Hurt, true);
            Steps(debug, 1);
            guard.Blackboard.Set(ToolFacts.Hurt, false);
            Steps(debug, 1);
            debug.Chat(0, "-brainlog");
            var written = Written(debug);
            var all = string.Join(" | ", written);
            Expect.True(FindBoth(written, "fight", "StanceBrain") != null, "the stance's line names its class: " + all);
            Expect.True(FindBoth(written, "line 6 Sequence runs", "GuardBrain") != null, "the guard's first line, numbered as the view: " + all);
            Expect.True(FindBoth(written, "line 4 When hurt cut in", "GuardBrain") != null, "the When cuts in at 4: " + all);
            var ended = FindBoth(written, "line 4 When hurt ended", "GuardBrain");
            Expect.True(ended != null && Holds(ended, "line 6 Sequence runs"), "then ends, and the Sequence runs again: " + all);
        }

        /// <summary>-fact reads a value below 0, for an int and for a float.</summary>
        public static void NegativeFact(FakeWorld world)
        {
            var unit = UnitOf(world);
            var brain = unit.Add(new ToolBrain());
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-fact ammo -3");
            Expect.Equal(-3, brain.Read(ToolFacts.Ammo), "an int below 0");
            debug.Chat(0, "-fact morale -0.5");
            Expect.Near(-0.5f, brain.Read(ToolFacts.Morale), "a float below 0");
        }

        /// <summary>
        /// A logged brain that ends in its own think, before another brain of its unit, keeps the view's numbers in its last
        /// line. Its unit removed while logged and while the other brain's option is forced, nothing is reported, -force says
        /// nothing is selected, and -brainlog still stops and writes.
        /// </summary>
        public static void EndingWhileLoggedAndForced(FakeWorld world)
        {
            var unit = UnitOf(world);
            var quitter = unit.Add(new QuitBrain());
            var stance = unit.Add(new StanceBrain());
            quitter.Blackboard.Set(ToolFacts.Hurt, false);
            stance.Blackboard.Set(ToolFacts.Morale, 0.9f);
            var debug = On(world);
            DebugMode.Select(unit);
            debug.Chat(0, "-brainlog");
            Steps(debug, 1);
            quitter.Blackboard.Set(ToolFacts.Hurt, true);
            quitter.Quit = true;
            Steps(debug, 1);
            Expect.True(quitter.HasEnded, "the brain ended in its think");
            debug.Chat(0, "-force 2");
            Steps(debug, 1);
            Expect.True(Holds(Numbered(Panel(debug), 2), "forced"), "the stance's rest, now line 2, is forced: " + debug.PanelText);
            unit.Remove();
            Steps(debug, 2);
            var printed = world.PrintedTexts.Count;
            debug.Chat(0, "-force");
            Expect.True(Printed(world, printed, "Select"), "-force with the unit gone says to select one: " + Last(world));
            debug.Chat(0, "-brainlog");
            var written = Written(debug);
            var all = string.Join(" | ", written);
            Expect.True(FindBoth(written, "line 2 When hurt cut in", "QuitBrain") != null, "the ended brain's line keeps the view's number, 2: " + all);
            Expect.True(FindBoth(written, "rest", "was fight") != null, "the forced option's line: " + all);
            Expect.Equal(0, Logged(world, "Moonwell.Debug"), "the tools report no error");
            Expect.Equal(0, Logged(world, ".Think"), "no think fails");
        }
    }
}
#endif
