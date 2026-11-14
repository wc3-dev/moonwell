#if DEBUG
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// The AI package's view in the debug mode (decision 345): for a selected unit, player or game holding brains, each
    /// brain's head line, its trees node by node, its choices option by option, and the facts it can read. Nodes and options
    /// are numbered for -force through the object's brains in add order: each brain's trees depth-first in the order made,
    /// then its choices' options. A brain whose lines don't fit the panel shows each subtree that isn't running as its head
    /// line with how many nodes it folds, and the numbers skip them. It adds no objects to click, drawings or statistics.
    /// </summary>
    internal sealed class BrainView : IDebugView
    {
        /// <summary>The view; made on first use.</summary>
        private static BrainView? instance;

        /// <summary>The number the last node or option was given, while the lines are made.</summary>
        private int number;

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
            var room = DebugPanel.MostLines - lines.Count - (BrainLog.On ? BrainLog.PanelLines + 1 : 0);
            number = 0;
            for (var i = 0; i < minds.Count; i++)
            {
                var first = number;
                var brain = new List<string>();
                AddBrain(minds[i], false, brain);
                if (brain.Count > room)
                {
                    number = first;
                    brain.Clear();
                    AddBrain(minds[i], true, brain);
                }

                for (var k = 0; k < brain.Count; k++)
                {
                    lines.Add(brain[k]);
                }

                room -= brain.Count;
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

        /// <summary>"Guard  every 0.25 s  logged", then the trees, the choices and the facts.</summary>
        private void AddBrain(Mind mind, bool fold, List<string> into)
        {
            var every = mind.ThinkEvery;
            into.Add(Names.NameOf(mind.brain) + "  " + (every > 0.0f ? "every " + BrainTools.Hundredths(every) + " s" : "on wake") + (BrainLog.Follows(mind) ? "  logged" : string.Empty));
            var board = mind.Board;
            var trees = board.trees;
            for (var i = 0; trees != null && i < trees.Count; i++)
            {
                AddNode(trees[i].Root, null, 0, string.Empty, fold, into);
            }

            var choices = board.choices;
            for (var i = 0; choices != null && i < choices.Count; i++)
            {
                AddChoice(i + 1, choices[i], into);
            }

            AddFacts(board, into);
        }

        /// <summary>"3   > When hurt true forced": the node's number, its depth, > when it runs, its kind and fact, a When's or If's last value, and forced; folded, a subtree that isn't running ends with "+n", the nodes below it.</summary>
        private void AddNode(Node node, Branch? parent, int index, string indent, bool fold, List<string> into)
        {
            number++;
            var line = number + " " + indent + (node.running ? "> " : string.Empty) + BrainTools.Label(node);
            if (node.lastHeld != null)
            {
                line = line + (node.lastHeld == true ? " true" : " false");
            }

            var when = node as WhenNode;
            if ((parent != null && parent.forcedChild == index) || (when != null && when.forced))
            {
                line = line + " forced";
            }

            var branch = node as Branch;
            if (branch == null || branch.children.Length == 0)
            {
                into.Add(line);
                return;
            }

            if (fold && !node.running)
            {
                var folded = BrainTools.Size(node) - 1;
                number += folded;
                into.Add(line + " +" + folded);
                return;
            }

            into.Add(line);
            for (var i = 0; i < branch.children.Length; i++)
            {
                AddNode(branch.children[i], branch, i, indent + "  ", fold, into);
            }
        }

        /// <summary>"choice 1", then each option: "8   > flee 0.73  life 0.20, enemies near 4.00", with the current one's inputs and their last values.</summary>
        private void AddChoice(int place, Choice choice, List<string> into)
        {
            into.Add("choice " + place);
            var options = choice.options;
            for (var i = 0; i < options.Count; i++)
            {
                number++;
                var option = options[i];
                var current = option == choice.Current;
                var line = number + "   " + (current ? "> " : string.Empty) + option.Name + " " + BrainTools.Hundredths(option.lastScore);
                if (option == choice.forced)
                {
                    line = line + " forced";
                }

                if (current)
                {
                    var inputs = option.inputs;
                    for (var k = 0; k < inputs.Count; k++)
                    {
                        line = line + (k == 0 ? "  " : ", ") + inputs[k].Name + " " + BrainTools.Hundredths(inputs[k].lastValue);
                    }
                }

                into.Add(line);
            }
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
