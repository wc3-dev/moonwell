using System.Collections.Generic;
using Moonwell;

namespace InTheGame
{
    /// <summary>Writes the run's file as the runner reads it once the game has started: what it measured, then the over line.</summary>
    public sealed class Report : GameComponent
    {
        /// <summary>The run's file in CustomMapData.</summary>
        public const string File = "MoonwellTests\\InTheGame.txt";

        private static readonly List<string> Lines = new List<string>();

        public override void OnGameStart()
        {
            Keep("started");
            // What only the game's Lua gives: its integers are 32-bit (game facts), where the tests' desktop Lua has 64.
            var max = int.MaxValue;
            Keep("integer max + 1 = " + (max + 1));
            Keep("-- run over");
        }

        private static void Keep(string line)
        {
            Lines.Add(line);
            Natives.PreloadGenClear();
            Natives.PreloadGenStart();
            for (var i = 0; i < Lines.Count; i++)
            {
                Natives.Preload("\")\n" + Lines[i] + "\n//");
            }

            Natives.PreloadGenEnd(File);
        }
    }
}
