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

        /// <summary>The steps in a second; a ThinkEvery is multiplied by it, which is exact for a quarter second where a division by 0.02 is not.</summary>
        private const float StepsPerSecond = 50f;

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

        /// <summary>The steps a ThinkEvery is: rounded to whole steps, a half up, and at least 1; 0 for none.</summary>
        internal static int StepsOf(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            // shortcut: past 1,000,000 s a tick is as good as none, and the steps then still fit an int in the game.
            if (seconds > 1000000f)
            {
                seconds = 1000000f;
            }

            var steps = (int)(seconds * StepsPerSecond + 0.5f);
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

        /// <summary>The step of a first tick: the next step, plus the count of first ticks placed so far modulo the tick, so brains started together spread over it.</summary>
        internal int FirstTick(int every)
        {
            var at = Step + 1 + spread % every;
            spread++;
            return at;
        }

        /// <summary>One step: reads the time once, then each brain due thinks, in the order it became due. What becomes due while it runs goes to a later step.</summary>
        private void Run()
        {
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
