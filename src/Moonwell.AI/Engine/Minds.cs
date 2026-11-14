using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// The package's state for one game: the loop every brain thinks on, and the blackboards of the players and the game.
    /// The loop is one timer of Moonwell's own, every 0.02 s step on the game, running from the first brain started to the
    /// last one ended. Steps are counted by the loop, and the brains due at a step wait in its list, in the order they became
    /// due; an entry that went stale (a brain that ended, a tick that changed) is skipped when the step meets it.
    /// </summary>
    internal sealed class Minds
    {
        /// <summary>The seconds of a step.</summary>
        internal const float StepSeconds = 0.02f;

        /// <summary>The milliseconds of a step.</summary>
        private const int StepMilliseconds = 20;

        /// <summary>The state of the game now; made on first use after an install or a runtime reset.</summary>
        private static Minds? current;

        /// <summary>The number the last blackboard made was given; never reused, so a value kept for an old blackboard is never another's.</summary>
        private static int boardsMade;

        /// <summary>The game this state was made for; a runtime reset makes another, and this one is stale.</summary>
        private readonly Game game;

        /// <summary>The brains due at each step, by step number, in the order they became due; a lookup only.</summary>
        private readonly Dictionary<int, List<Mind>> due = new Dictionary<int, List<Mind>>();

        /// <summary>Each player's blackboard, by slot, made on first use; a lookup only.</summary>
        private readonly Dictionary<int, Blackboard> players = new Dictionary<int, Blackboard>();

        /// <summary>The game's blackboard; null until first used.</summary>
        private Blackboard? gameBoard;

        /// <summary>The loop's timer; null while no brain is started.</summary>
        private Timer? timer;

        /// <summary>The number of the last step run, or the one running.</summary>
        internal int Step;

        /// <summary>How many brains are started and have not ended.</summary>
        private int brains;

        /// <summary>How many first ticks have been placed: each goes that many steps, modulo its tick, after the next step.</summary>
        private int spread;

        /// <summary>Whether a step is running its brains, so that reads use its time.</summary>
        private bool thinking;

        /// <summary>The time the running step read once, at its start.</summary>
        private float now;

        private Minds(Game game)
        {
            this.game = game;
        }

        /// <summary>The state of this game, made when there is none: a new install or a runtime reset starts a fresh one.</summary>
        internal static Minds Current()
        {
            if (current == null || current.game != Game.Instance)
            {
                current = new Minds(Game.Instance);
#if DEBUG
                BrainTools.Install();
#endif
            }

            return current;
        }

        /// <summary>The time facts are written and aged by: the step's, inside a step, and the game's otherwise.</summary>
        internal static float Now()
        {
            var state = current;
            if (state != null && state.thinking && state.game == Game.Instance)
            {
                return state.now;
            }

            return Game.Time;
        }

        /// <summary>A number for a new blackboard.</summary>
        internal static int NextBoard()
        {
            boardsMade++;
            return boardsMade;
        }

        /// <summary>The steps a ThinkEvery is: rounded to whole milliseconds, as the timers are, then to whole steps, a half step up, and at least 1; 0 for none. Through whole milliseconds a half step is the same in the game's floats and in Lua's doubles: 0.03 s is 2 steps in both.</summary>
        internal static int StepsOf(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            // shortcut: past 1,000,000 s a tick is as good as none, and its 10^9 milliseconds still fit an int in the game.
            if (seconds > 1000000f)
            {
                seconds = 1000000f;
            }

            var milliseconds = (int)(seconds * 1000f + 0.5f);
            var steps = (milliseconds + StepMilliseconds / 2) / StepMilliseconds;
            return steps < 1 ? 1 : steps;
        }

        /// <summary>The game's blackboard, made on first use.</summary>
        internal Blackboard GameBoard()
        {
            if (gameBoard == null)
            {
                gameBoard = new Blackboard(Blackboard.GameKind, null);
            }

            return gameBoard;
        }

        /// <summary>A player's blackboard, made on first use.</summary>
        internal Blackboard BoardOf(Player player)
        {
            if (players.TryGetValue(player.Slot, out var board))
            {
                return board;
            }

            board = new Blackboard(Blackboard.PlayerKind, player);
            players[player.Slot] = board;
            return board;
        }

        /// <summary>A brain starts: the first starts the loop.</summary>
        internal void Started()
        {
            brains++;
            if (timer == null)
            {
                timer = game.OwnEvery(StepSeconds, Run);
            }
        }

        /// <summary>A started brain ends: the last ends the loop, and what waited is dropped.</summary>
        internal void Ended()
        {
            brains--;
            if (brains == 0 && timer != null)
            {
                var ending = timer;
                timer = null;
                due.Clear();
                ending.End();
            }
        }

        /// <summary>Puts a brain in the list of a step.</summary>
        internal void Due(Mind mind, int at)
        {
            if (!due.TryGetValue(at, out var list))
            {
                list = new List<Mind>();
                due[at] = list;
            }

            list.Add(mind);
        }

        /// <summary>Takes a brain's entry out of a step's list, leaving the list.</summary>
        internal void Undue(Mind mind, int at)
        {
            if (due.TryGetValue(at, out var list))
            {
                list.Remove(mind);
            }
        }

        /// <summary>How many entries wait in a step's list; for the cases.</summary>
        internal int DueAt(int at)
        {
            return due.TryGetValue(at, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// The step of a first tick: the first from the next step whose number, modulo the tick, is the count of first ticks
        /// placed so far. The phase is the step's own, not one counted from now, so brains started together spread over the
        /// tick and so do brains started at a steady rate, which a phase from now would put on one step.
        /// </summary>
        internal int FirstTick(int every)
        {
            var next = Step + 1;
            var at = next + ((spread % every) - next % every + every) % every;
            spread++;
            return at;
        }

        /// <summary>One step: reads the time once, then each brain due thinks, in the order it became due. What becomes due while it runs goes to a later step.</summary>
        private void Run()
        {
            Control.Answer();
            Step++;
            if (!due.TryGetValue(Step, out var list))
            {
                return;
            }

            due.Remove(Step);
            now = Game.Time;
            thinking = true;
            for (var i = 0; i < list.Count; i++)
            {
                list[i].ThinkAt(Step);
            }

            thinking = false;
        }
    }
}
