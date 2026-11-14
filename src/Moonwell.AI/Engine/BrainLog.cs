#if DEBUG
using System;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// The decision log of the debug mode (decision 346): -brainlog follows the selected object's brains, or every brain of a
    /// class, those made later too, and notes a line at a think when what won changes: a choice's Current, or a tree's running
    /// branch (a Select or Pick running another child, a When cutting in or ending). Each line has the facts the brain read
    /// in that think. It keeps the newest 2,000 lines, the panel shows the last 5, and stopping writes them to brains.txt.
    /// </summary>
    internal static class BrainLog
    {
        /// <summary>The log's lines the panel shows, under its head.</summary>
        internal const int PanelLines = 5;

        /// <summary>The file the stop writes, in CustomMapData.</summary>
        internal const string FilePath = "Moonwell\\brains.txt";

        /// <summary>The lines kept, the newest replacing the oldest.</summary>
        private const int MostLines = 2000;

        /// <summary>The facts a line names at most.</summary>
        private const int MostReads = 8;

        /// <summary>Whether the log runs; a think reads it before anything else of the log.</summary>
        internal static bool On;

        /// <summary>Whether a logged brain is thinking, so the facts it reads are noted.</summary>
        internal static bool Reading;

        /// <summary>The number of the log's run, so a brain's trees and choices are noted anew at its first think in another.</summary>
        private static int run;

        /// <summary>The lines kept, from first on, around the end.</summary>
        private static string[] kept = new string[MostLines];

        /// <summary>The place of the oldest line kept.</summary>
        private static int first;

        /// <summary>How many lines are kept.</summary>
        private static int count;

        /// <summary>The object whose brains the log follows; null when it follows a class.</summary>
        private static Actor? followed;

        /// <summary>The class the log follows, in lower case; empty when it follows an object.</summary>
        private static string className = string.Empty;

        /// <summary>What the log follows, as its head says it: "Footman 3", "every Guard".</summary>
        private static string shown = string.Empty;

        /// <summary>The number the walk of a brain's trees last gave.</summary>
        private static int number;

        /// <summary>The names of the facts read in the think, the first 8.</summary>
        private static List<string> readNames = new List<string>();

        /// <summary>Each fact read, beside its name: "life 0.31 0.2s".</summary>
        private static List<string> reads = new List<string>();

        /// <summary>Stops without writing and forgets the lines; the AI tools' install calls it with each game state.</summary>
        internal static void Reset()
        {
            On = false;
            Reading = false;
            kept = new string[MostLines];
            first = 0;
            count = 0;
            followed = null;
            className = string.Empty;
            shown = string.Empty;
            readNames = new List<string>();
            reads = new List<string>();
        }

        /// <summary>Starts the log, empty, for an object's brains, or with null for every brain of a class.</summary>
        internal static void Start(Actor? actor, string name)
        {
            On = true;
            run++;
            first = 0;
            count = 0;
            followed = actor;
            className = name.ToLower();
            shown = actor != null ? DebugPanel.NameOf(actor) : "every " + name;
            Game.Print("Brain log started for " + (actor != null ? shown : shown + " brain") + ": -brainlog again stops it and writes CustomMapData\\" + FilePath + ".");
        }

        /// <summary>Stops the log and writes every line kept, oldest first, to brains.txt.</summary>
        internal static void Stop()
        {
            On = false;
            Reading = false;
            var lines = new List<string>();
            for (var i = 0; i < count; i++)
            {
                lines.Add(kept[(first + i) % MostLines]);
            }

            DebugSession.WriteFile(FilePath, lines);
            Game.Print("Brain log stopped: " + DebugPanel.Count(lines.Count) + " lines written to CustomMapData\\" + FilePath + ".");
        }

        /// <summary>Whether the log follows a brain.</summary>
        internal static bool Follows(Mind mind)
        {
            if (!On)
            {
                return false;
            }

            return followed != null ? mind.brain.actor == followed : mind.ClassName() == className;
        }

        /// <summary>The panel's lines while the log runs: "BRAIN LOG  Footman 3 · 37 lines", then the last 5.</summary>
        internal static void AddLines(DebugLines lines)
        {
            if (!On)
            {
                return;
            }

            lines.Add("BRAIN LOG  " + shown + " · " + (count == 1 ? "1 line" : DebugPanel.Count(count) + " lines"));
            for (var i = count > PanelLines ? count - PanelLines : 0; i < count; i++)
            {
                lines.Add(kept[(first + i) % MostLines]);
            }
        }

        /// <summary>A logged brain is to think: at its first think in this run, what runs and wins is noted, so only a change makes a line; then the facts it reads are noted.</summary>
        internal static void Before(Mind mind)
        {
            if (mind.logRun != run)
            {
                mind.logRun = run;
                try
                {
                    Note(mind);
                }
                catch (Exception error)
                {
                    Errors.Report("Moonwell.Debug", error);
                }
            }

            readNames.Clear();
            reads.Clear();
            Reading = true;
        }

        /// <summary>A fact a logged brain read in its think, with its value and age; one read again keeps its last value, and past 8 none is added.</summary>
        internal static void Read(string name, string value, float age)
        {
            var text = name + " " + value + (age < float.MaxValue ? " " + DebugPanel.Tenths(age) + "s" : string.Empty);
            var at = readNames.IndexOf(name);
            if (at >= 0)
            {
                reads[at] = text;
            }
            else if (readNames.Count < MostReads)
            {
                readNames.Add(name);
                reads.Add(text);
            }
        }

        /// <summary>A logged brain has thought: a line for each choice whose Current changed, and one for the changes in its trees.</summary>
        internal static void After(Mind mind)
        {
            Reading = false;
            try
            {
                Changes(mind);
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Debug", error);
            }
        }

        /// <summary>Notes what runs in a brain's trees and what its choices picked, as they are now.</summary>
        private static void Note(Mind mind)
        {
            var trees = mind.Board.trees;
            for (var i = 0; trees != null && i < trees.Count; i++)
            {
                NoteNode(trees[i].Root);
            }

            var choices = mind.Board.choices;
            for (var i = 0; choices != null && i < choices.Count; i++)
            {
                choices[i].logged = choices[i].Current;
            }
        }

        /// <summary>Notes the child that runs of each Select and Pick, and whether each When runs, from a node down.</summary>
        private static void NoteNode(Node node)
        {
            var branch = node as Branch;
            if (branch == null)
            {
                return;
            }

            branch.loggedChild = BrainTools.RunningChild(branch);
            var when = node as WhenNode;
            if (when != null)
            {
                when.loggedRunning = when.running;
            }

            for (var i = 0; i < branch.children.Length; i++)
            {
                NoteNode(branch.children[i]);
            }
        }

        /// <summary>"choice 1: flee 0.61 (was fight 0.40)" for each choice whose Current changed, then "tree: line 2 When hurt cut in" with every change in the trees.</summary>
        private static void Changes(Mind mind)
        {
            var choices = mind.Board.choices;
            for (var i = 0; choices != null && i < choices.Count; i++)
            {
                var choice = choices[i];
                if (choice.Current != choice.logged)
                {
                    Add(mind, "choice " + (i + 1) + ": " + Picked(choice.Current) + " (was " + Picked(choice.logged) + ")");
                    choice.logged = choice.Current;
                }
            }

            var trees = mind.Board.trees;
            if (trees == null)
            {
                return;
            }

            var changes = new List<string>();
            number = FirstNumber(mind);
            for (var i = 0; i < trees.Count; i++)
            {
                Walk(trees[i].Root, null, 0, changes);
            }

            if (changes.Count > 0)
            {
                var text = "tree: " + changes[0];
                for (var i = 1; i < changes.Count; i++)
                {
                    text = text + ", " + changes[i];
                }

                Add(mind, text);
            }
        }

        /// <summary>How many lines the brains on a brain's object before it number, so the log's lines are the view's.</summary>
        private static int FirstNumber(Mind mind)
        {
            var minds = BrainTools.MindsOf(mind.brain.actor!);
            var before = 0;
            for (var i = 0; i < minds.Count && minds[i] != mind; i++)
            {
                before += BrainTools.Entries(minds[i]);
            }

            return before;
        }

        /// <summary>
        /// Numbers a node and those below it as the view does, and notes the changes since the last think: a Select or Pick
        /// running another child ("line 4 Repeat runs", or for a When above the child that ran, "line 2 When hurt cut in"),
        /// and a When that ran and has ended ("line 2 When hurt ended").
        /// </summary>
        private static void Walk(Node node, Branch? parent, int index, List<string> changes)
        {
            number++;
            var at = number;
            var branch = node as Branch;
            if (branch == null)
            {
                return;
            }

            var when = node as WhenNode;
            if (when != null)
            {
                if (when.loggedRunning && !when.running)
                {
                    changes.Add("line " + at + " " + BrainTools.Label(when) + " ended");
                    if (parent != null && parent.loggedChild == index)
                    {
                        parent.loggedChild = -1;
                    }
                }

                when.loggedRunning = when.running;
            }

            if (node is SelectNode || node is PickNode)
            {
                var now = BrainTools.RunningChild(branch);
                if (now >= 0 && now != branch.loggedChild)
                {
                    var child = branch.children[now];
                    var line = at + 1;
                    for (var i = 0; i < now; i++)
                    {
                        line += BrainTools.Size(branch.children[i]);
                    }

                    changes.Add("line " + line + " " + BrainTools.Label(child) + (child is WhenNode && branch.loggedChild > now ? " cut in" : " runs"));
                    branch.loggedChild = now;
                }
            }

            for (var i = 0; i < branch.children.Length; i++)
            {
                Walk(branch.children[i], branch, i, changes);
            }
        }

        /// <summary>"flee 0.61" for an option and its last score, "none" for null.</summary>
        private static string Picked(Option? option)
        {
            return option != null ? option.Name + " " + BrainTools.Hundredths(option.lastScore) : "none";
        }

        /// <summary>Keeps a line: "12.25 Footman 3  choice 1: flee 0.61 (was fight 0.40)  life 0.31 0.2s", the brain's class after its object when the object holds more than one brain.</summary>
        private static void Add(Mind mind, string change)
        {
            var actor = mind.brain.actor!;
            var name = DebugPanel.NameOf(actor);
            if (BrainTools.MindsOf(actor).Count > 1)
            {
                name = name + " " + Names.NameOf(mind.brain);
            }

            var line = BrainTools.Hundredths(Minds.Now()) + " " + name + "  " + change;
            for (var i = 0; i < reads.Count; i++)
            {
                line = line + (i == 0 ? "  " : ", ") + reads[i];
            }

            if (count < MostLines)
            {
                kept[(first + count) % MostLines] = line;
                count++;
                return;
            }

            kept[first] = line;
            first = (first + 1) % MostLines;
        }
    }
}
#endif
