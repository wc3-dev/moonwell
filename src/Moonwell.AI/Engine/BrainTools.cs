#if DEBUG
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// The AI package's tools in the debug mode of a development build (decisions 345 to 347): the brain view, the decision
    /// log, and the chat commands -brain, -brainlog, -force, -fact and -wake. They read and change only what is the same on
    /// every machine: every change comes from a chat command or from the brains.
    /// </summary>
    internal static class BrainTools
    {
        /// <summary>Every fact made, in the order made, for -fact; kept for the whole run, as facts are static fields.</summary>
        private static readonly List<IHeldFact> facts = new List<IHeldFact>();

        /// <summary>Adds the view, the log's lines and the commands to the debug mode, and forgets the log; a new game state calls it, as Runtime.Reset clears them.</summary>
        internal static void Install()
        {
            BrainLog.Reset();
            DebugViews.Add(BrainView.Instance);
            DebugViews.Always.Add(BrainLog.AddLines);
            DebugTools.AddCommand("brain", "-brain <class>: count its brains, select the next", (player, rest) => SelectBrain(rest));
            DebugTools.AddCommand("brainlog", "-brainlog [class]: log the selected's brains or a class's", (player, rest) => ToggleLog(rest));
            DebugTools.AddCommand("force", "-force <n>: force line n of the brain view; alone, let go", (player, rest) => Force(rest));
            DebugTools.AddCommand("fact", "-fact <name> <value>  -wake: on the selected's brains", (player, rest) => SetFact(rest));
            DebugTools.AddCommand("wake", string.Empty, (player, rest) => Wake());
        }

        /// <summary>A fact is made: -fact can find it by its name.</summary>
        internal static void Made(IHeldFact fact)
        {
            facts.Add(fact);
        }

        /// <summary>The brains on an actor, in add order, none that has ended.</summary>
        internal static List<Mind> MindsOf(Actor actor)
        {
            var found = new List<Mind>();
            var components = actor.components;
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null || component.HasEnded)
                {
                    continue;
                }

                var unitBrain = component as UnitBrain;
                var playerBrain = component as PlayerBrain;
                var gameBrain = component as GameBrain;
                if (unitBrain != null)
                {
                    found.Add(unitBrain.mind);
                }
                else if (playerBrain != null)
                {
                    found.Add(playerBrain.mind);
                }
                else if (gameBrain != null)
                {
                    found.Add(gameBrain.mind);
                }
            }

            return found;
        }

        /// <summary>The numbered lines of some brains, line n at n - 1: each brain's trees depth-first in the order made, then its choices' options, brain after brain.</summary>
        internal static List<BrainLine> LinesOf(List<Mind> minds)
        {
            var lines = new List<BrainLine>();
            for (var m = 0; m < minds.Count; m++)
            {
                var mind = minds[m];
                var trees = mind.Board.trees;
                for (var t = 0; trees != null && t < trees.Count; t++)
                {
                    AddLines(mind, trees[t].Root, null, 0, 0, lines);
                }

                var choices = mind.Board.choices;
                for (var c = 0; choices != null && c < choices.Count; c++)
                {
                    for (var o = 0; o < choices[c].options.Count; o++)
                    {
                        lines.Add(new BrainLine(mind, choices[c], c + 1, o));
                    }
                }
            }

            return lines;
        }

        /// <summary>How many nodes below a node's line follow it: the lines of its subtree.</summary>
        internal static int Below(List<BrainLine> lines, int at)
        {
            var count = 0;
            while (at + count + 1 < lines.Count && lines[at + count + 1].Node != null && lines[at + count + 1].Depth > lines[at].Depth)
            {
                count++;
            }

            return count;
        }

        /// <summary>The place of the child of a node that runs; -1 for none.</summary>
        internal static int RunningChild(Branch branch)
        {
            for (var i = 0; i < branch.children.Length; i++)
            {
                if (branch.children[i].running)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>A node as the view and the log name it: its kind ("Select", "MoveTo", a map's class), and the fact it was made with: "When hurt".</summary>
        internal static string Label(Node node)
        {
            var kind = Names.NameOf(node);
            if (kind.Length > 4 && kind.Substring(kind.Length - 4) == "Node")
            {
                kind = kind.Substring(0, kind.Length - 4);
            }

            return node.factName != null ? kind + " " + node.factName : kind;
        }

        /// <summary>A number rounded to hundredths, halves away from 0, as the panel rounds tenths: "0.31", "-1.50", "1,204.00"; past a million either way, or not a number, "out of range".</summary>
        internal static string Hundredths(float value)
        {
            if (!(value > -1000000.0f && value < 1000000.0f))
            {
                return "out of range";
            }

            var hundredths = value < 0.0f ? (int)(-value * 100.0f + 0.5f) : (int)(value * 100.0f + 0.5f);
            var rest = hundredths % 100;
            return (value < 0.0f && hundredths > 0 ? "-" : "") + DebugPanel.Count(hundredths / 100) + "." + (rest < 10 ? "0" : "") + rest;
        }

        /// <summary>A value of a fact that is no int, float, bool or enum, as the view and the log show it: "none" for null, a Moonwell object by the panel's name for it, anything else as its text, which Lua writes for a number or a boolean too.</summary>
        internal static string Text(object? value)
        {
            if (value == null)
            {
                return "none";
            }

            var scope = value as Scope;
            return scope != null ? DebugPanel.NameOf(scope) : "" + value;
        }

        /// <summary>A number of chat text as DebugSession.Number reads it, with a minus in front for one below 0.</summary>
        internal static bool Number(string text, out float value)
        {
            if (text.Length > 1 && text[0] == '-')
            {
                var read = DebugSession.Number(text.Substring(1), out var size);
                value = -size;
                return read;
            }

            return DebugSession.Number(text, out value);
        }

        /// <summary>Adds the line of a node and those of the nodes below it.</summary>
        private static void AddLines(Mind mind, Node node, Branch? parent, int index, int depth, List<BrainLine> lines)
        {
            lines.Add(new BrainLine(mind, node, parent, index, depth));
            var branch = node as Branch;
            for (var i = 0; branch != null && i < branch.children.Length; i++)
            {
                AddLines(mind, branch.children[i], branch, i, depth + 1, lines);
            }
        }

        /// <summary>"1 brain", "3 brains".</summary>
        private static string Brains(int count)
        {
            return count == 1 ? "1 brain" : DebugPanel.Count(count) + " brains";
        }

        /// <summary>The brains of the selected object; null, saying so, when nothing holding one is selected.</summary>
        private static List<Mind>? SelectedMinds()
        {
            var selected = DebugSession.Selected;
            var minds = selected != null && !selected.HasEnded ? MindsOf(selected) : null;
            if (minds == null || minds.Count == 0)
            {
                Game.Print("Select an object with a brain first: Ctrl + click, or -brain <class>.");
                return null;
            }

            return minds;
        }

        /// <summary>-brain Guard: how many brains of a class there are, and selects the next object after the selected one holding one, as -find does: the units, then the players, then the game.</summary>
        private static void SelectBrain(string name)
        {
            if (name.Length == 0)
            {
                Game.Print("-brain takes a brain's class: -brain Guard.");
                return;
            }

            var wanted = name.ToLower();
            var found = new List<Actor>();
            var count = 0;
            for (var i = 0; i < KnownUnits.Live.Count; i++)
            {
                var unit = KnownUnits.Live[i];
                if (!unit.HasEnded)
                {
                    count += Holding(unit, wanted, found);
                }
            }

            var players = Player.Playing;
            for (var i = 0; i < players.Count; i++)
            {
                count += Holding(players[i], wanted, found);
            }

            count += Holding(Game.Instance, wanted, found);
            if (count == 0)
            {
                Game.Print("No brains of the class " + name + ".");
                return;
            }

            var selected = DebugSession.Selected;
            var at = selected != null ? found.IndexOf(selected) : -1;
            var next = found[(at + 1) % found.Count];
            DebugSession.Select(next);
            Game.Print(name + ": " + Brains(count) + "; selected " + DebugPanel.NameOf(next) + ".");
        }

        /// <summary>How many brains of a class, in lower case, an actor holds; one that holds any is added to the list.</summary>
        private static int Holding(Actor actor, string wanted, List<Actor> found)
        {
            var minds = MindsOf(actor);
            var count = 0;
            for (var i = 0; i < minds.Count; i++)
            {
                if (minds[i].ClassName() == wanted)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                found.Add(actor);
            }

            return count;
        }

        /// <summary>-brainlog: stops the log when it runs; otherwise starts it for a class, or for the selected object's brains.</summary>
        private static void ToggleLog(string name)
        {
            if (BrainLog.On)
            {
                BrainLog.Stop();
                return;
            }

            if (name.Length > 0)
            {
                BrainLog.Start(null, name);
                return;
            }

            var minds = SelectedMinds();
            if (minds != null)
            {
                BrainLog.Start(DebugSession.Selected, string.Empty);
            }
        }

        /// <summary>-force 3: forces what line 3 of the brain view shows; -force alone lets go of every force on the selected object's brains.</summary>
        private static void Force(string rest)
        {
            var minds = SelectedMinds();
            if (minds == null)
            {
                return;
            }

            var name = DebugPanel.NameOf(DebugSession.Selected!);
            var lines = LinesOf(minds);
            if (rest.Length == 0)
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    LetGo(lines[i]);
                }

                Game.Print("Every force let go on " + name + ".");
                return;
            }

            if (!DebugSession.Number(rest, out var value) || value < 1.0f || value > 100000.0f || value != (int)value)
            {
                Game.Print("-force takes a line of the brain view: -force 3.");
                return;
            }

            var number = (int)value;
            if (number > lines.Count || !ForceLine(lines[number - 1]))
            {
                Game.Print("Line " + number + " of " + name + " has nothing to force: a Select's or Pick's child, a When or a choice's option.");
                return;
            }

            Game.Print("Line " + number + " of " + name + " forced: -force alone lets go.");
        }

        /// <summary>Forces a line: a Select's or Pick's child has the parent run only it, a When has its condition read true, and an option wins; false for a line with nothing to force.</summary>
        private static bool ForceLine(BrainLine line)
        {
            if (line.Option != null)
            {
                line.Choice!.Force(line.Option);
                return true;
            }

            var forced = false;
            if (line.Parent is SelectNode || line.Parent is PickNode)
            {
                line.Parent!.forcedChild = line.Index;
                forced = true;
            }

            var when = line.Node as WhenNode;
            if (when != null)
            {
                when.forced = true;
                forced = true;
            }

            return forced;
        }

        /// <summary>Lets go of every force on a line.</summary>
        private static void LetGo(BrainLine line)
        {
            if (line.Choice != null)
            {
                line.Choice.Force(null);
                return;
            }

            var branch = line.Node as Branch;
            if (branch != null)
            {
                branch.forcedChild = -1;
            }

            var when = line.Node as WhenNode;
            if (when != null)
            {
                when.forced = false;
            }
        }

        /// <summary>-fact hurt true: sets a written fact, found by its name in any case, on the board of each of the selected object's brains. The value is the last word, and the name all before it.</summary>
        private static void SetFact(string rest)
        {
            var minds = SelectedMinds();
            if (minds == null)
            {
                return;
            }

            // LastIndexOf of an empty string throws in Lua.
            var space = rest.Length > 0 ? rest.LastIndexOf(' ') : -1;
            if (space <= 0)
            {
                Game.Print("-fact takes a fact's name and a value: -fact hurt true.");
                return;
            }

            var name = rest.Substring(0, space).Trim();
            var wanted = name.ToLower();
            IHeldFact? fact = null;
            var named = 0;
            for (var i = 0; i < facts.Count; i++)
            {
                if (facts[i].FactName().ToLower() == wanted)
                {
                    fact = facts[i];
                    named++;
                }
            }

            if (fact == null)
            {
                Game.Print("No fact is named " + name + ".");
                return;
            }

            if (named > 1)
            {
                Game.Print(DebugPanel.Count(named) + " facts are named " + name + ", so -fact can't tell which: give them names of their own.");
                return;
            }

            var text = rest.Substring(space + 1);
            for (var i = 0; i < minds.Count; i++)
            {
                var refused = fact.SetFromText(minds[i].Board, text);
                if (refused.Length > 0)
                {
                    Game.Print("-fact: " + refused + ".");
                    return;
                }
            }

            Game.Print(fact.FactName() + " is " + fact.TextOn(minds[0].Board.Number) + " on " + DebugPanel.NameOf(DebugSession.Selected!) + ".");
        }

        /// <summary>-wake: wakes the selected object's brains.</summary>
        private static void Wake()
        {
            var minds = SelectedMinds();
            if (minds == null)
            {
                return;
            }

            for (var i = 0; i < minds.Count; i++)
            {
                minds[i].Wake();
            }

            Game.Print("Woke " + Brains(minds.Count) + " of " + DebugPanel.NameOf(DebugSession.Selected!) + ".");
        }
    }
}
#endif
