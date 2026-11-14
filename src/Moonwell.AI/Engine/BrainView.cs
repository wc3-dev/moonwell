#if DEBUG
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// The AI package's view in the debug mode (decision 345): for a selected unit, player or game holding brains, each
    /// brain's head line, its trees node by node, its choices option by option, and the facts it can read. Nodes and options
    /// are numbered for -force as BrainTools.LinesOf numbers them. A brain whose lines don't fit the panel shows each subtree
    /// that isn't running as its head line with how many nodes it folds, and the numbers skip them. It adds no objects to
    /// click, drawings or statistics.
    /// </summary>
    internal sealed class BrainView : IDebugView
    {
        /// <summary>The view; made on first use.</summary>
        private static BrainView? instance;

        /// <summary>The view, made on first use; the package adds it to the debug mode with its game state.</summary>
        internal static BrainView Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BrainView();
                }

                return instance;
            }
        }

        /// <summary>Adds nothing: brains are selected through the objects that hold them.</summary>
        public void Candidates(float x, float y, float radius, List<Actor> into)
        {
        }

        /// <summary>Knows no place: the session finds a unit's place itself.</summary>
        public bool Place(Actor actor, out float x, out float y)
        {
            x = 0.0f;
            y = 0.0f;
            return false;
        }

        /// <summary>Each brain's lines, in add order, folded when they don't fit the lines left, the log's kept free.</summary>
        public void Lines(Actor selected, DebugLines lines)
        {
            var minds = BrainTools.MindsOf(selected);
            var numbered = BrainTools.LinesOf(minds);
            var room = DebugPanel.MostLines - lines.Count - (BrainLog.On ? BrainLog.PanelLines + 1 : 0);
            var start = 0;
            for (var i = 0; i < minds.Count; i++)
            {
                var end = start;
                while (end < numbered.Count && numbered[end].Mind == minds[i])
                {
                    end++;
                }

                var brain = new List<string>();
                AddBrain(minds[i], numbered, start, end, false, brain);
                if (brain.Count > room)
                {
                    brain.Clear();
                    AddBrain(minds[i], numbered, start, end, true, brain);
                }

                for (var k = 0; k < brain.Count; k++)
                {
                    lines.Add(brain[k]);
                }

                room -= brain.Count;
                start = end;
            }
        }

        /// <summary>Draws nothing: core draws the marker.</summary>
        public void Draw(Actor selected, DebugDraw draw)
        {
        }

        /// <summary>Adds nothing: the package keeps no statistics.</summary>
        public void Stats(DebugLines lines)
        {
        }

        /// <summary>"Guard  every 0.25 s  logged", then the brain's numbered lines, from start to before end, and its facts; folded, a subtree that isn't running is its head line ending in "+n", the nodes below it.</summary>
        private static void AddBrain(Mind mind, List<BrainLine> numbered, int start, int end, bool fold, List<string> into)
        {
            var every = mind.ThinkEvery;
            into.Add(Names.NameOf(mind.brain) + "  " + (every > 0.0f ? "every " + BrainTools.Hundredths(every) + " s" : "on wake") + (BrainLog.Follows(mind) ? "  logged" : string.Empty));
            var i = start;
            while (i < end)
            {
                var line = numbered[i];
                if (line.Option != null)
                {
                    if (line.Index == 0)
                    {
                        into.Add("choice " + line.Depth);
                    }

                    into.Add(OptionText(i + 1, line.Choice!, line.Option));
                    i++;
                    continue;
                }

                var text = NodeText(i + 1, line);
                var below = BrainTools.Below(numbered, i);
                if (fold && below > 0 && !line.Node!.running)
                {
                    into.Add(text + " +" + below);
                    i += below + 1;
                    continue;
                }

                into.Add(text);
                i++;
            }

            AddFacts(mind.Board, into);
        }

        /// <summary>"3   > When hurt true forced": the node's number, its depth, > when it runs, its kind and fact, a When's or If's last value, and forced.</summary>
        private static string NodeText(int number, BrainLine line)
        {
            var node = line.Node!;
            var indent = string.Empty;
            for (var i = 0; i < line.Depth; i++)
            {
                indent = indent + "  ";
            }

            var text = number + " " + indent + (node.running ? "> " : string.Empty) + BrainTools.Label(node);
            if (node.lastHeld != null)
            {
                text = text + (node.lastHeld == true ? " true" : " false");
            }

            var when = node as WhenNode;
            if ((line.Parent != null && line.Parent.forcedChild == line.Index) || (when != null && when.forced))
            {
                text = text + " forced";
            }

            return text;
        }

        /// <summary>"8   > flee 0.73  life 0.20, enemies near 4.00": the option's number, > on the current one, its last score, forced, and the current one's inputs with their last values.</summary>
        private static string OptionText(int number, Choice choice, Option option)
        {
            var current = option == choice.Current;
            var text = number + "   " + (current ? "> " : string.Empty) + option.Name + " " + BrainTools.Hundredths(option.lastScore);
            if (option == choice.forced)
            {
                text = text + " forced";
            }

            if (current)
            {
                var inputs = option.inputs;
                for (var k = 0; k < inputs.Count; k++)
                {
                    text = text + (k == 0 ? "  " : ", ") + inputs[k].Name + " " + BrainTools.Hundredths(inputs[k].lastValue);
                }
            }

            return text;
        }

        /// <summary>"facts", then every fact held on the brain's blackboard and up its chain, with its value and age: "  food (Player 1) 12 3.0 s".</summary>
        private static void AddFacts(Blackboard board, List<string> into)
        {
            var now = Minds.Now();
            var head = false;
            for (Blackboard? on = board; on != null; on = on.Up())
            {
                var where = on == board ? string.Empty : " (" + (on.player != null ? DebugPanel.NameOf(on.player) : "game") + ")";
                var held = on.held;
                for (var i = 0; i < held.Count; i++)
                {
                    if (!head)
                    {
                        into.Add("facts");
                        head = true;
                    }

                    var fact = held[i];
                    into.Add("  " + fact.FactName() + where + " " + fact.TextOn(on.Number) + " " + DebugPanel.Tenths(now - fact.TimeOn(on.Number)) + " s");
                }
            }
        }
    }
}
#endif
