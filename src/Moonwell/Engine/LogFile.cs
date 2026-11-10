using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The lines of the log file and when it is due to be written. Rewriting the file costs 6.5 to 23 ms (game facts), so it
    /// is written at once only when it was last written a second ago or more; a line within the second waits for it.
    /// </summary>
    internal sealed class LogFile
    {
        /// <summary>How many lines the file keeps; it holds the last ones.</summary>
        internal const int MaxLines = 200;

        /// <summary>The longest piece of a line one Preload call keeps whole: 259 characters, less the 6 that wrap it.</summary>
        internal const int PieceLength = 253;

        /// <summary>The fewest seconds between two writes of the file.</summary>
        internal const float Interval = 1.0f;

        /// <summary>How many past games' logs the ring keeps (decision 264).</summary>
        internal const int RingSize = 10;

        /// <summary>The index file: one JASS line that stores the ring's next slot in a game cache, read at the start.</summary>
        internal const string IndexPath = "Moonwell\\logs\\next.txt";

        /// <summary>The game cache the index file stores the next slot in.</summary>
        internal const string CacheName = "Moonwell.w3v";

        private float writtenAt = -Interval;
        private bool waiting;
        private bool indexWritten;

        /// <summary>The lines the file holds, oldest first.</summary>
        internal List<string> Lines { get; } = new List<string>();

        /// <summary>The pieces of the line that opens the file (the map and the date), before the lines; empty until the ring is opened.</summary>
        internal List<string> Header { get; private set; } = new List<string>();

        /// <summary>This game's slot in the ring, 1 to 10; 0 until the ring is opened.</summary>
        internal int Slot { get; private set; }

        /// <summary>This game's file in the ring.</summary>
        internal string RingPath => "Moonwell\\logs\\" + Slot + ".txt";

        /// <summary>Whether the index is still to be written: once, with the first write of the log, so a game that logs nothing keeps its slot for the next.</summary>
        internal bool IndexDue => Slot > 0 && !indexWritten;

        /// <summary>The slot after this game's, which the index stores.</summary>
        internal int NextSlot => Slot % RingSize + 1;

        /// <summary>Opens the ring: this game takes the slot the index stored (the first when none, or one out of range) and the file opens with the header.</summary>
        internal void Open(int stored, string header)
        {
            Slot = stored >= 1 && stored <= RingSize ? stored : 1;
            Header = Utf8.Pieces(header, PieceLength);
        }

        /// <summary>Notes that the index was written.</summary>
        internal void IndexWritten()
        {
            indexWritten = true;
        }

        /// <summary>The Preload text of the index file: it closes Preload's own call and stores the next slot in the cache, so Preloader of the file sets it.</summary>
        internal string IndexLine()
        {
            return "\")\ncall StoreInteger(InitGameCache(\"" + CacheName + "\"), \"log\", \"next\", " + NextSlot + ")\n//";
        }

        /// <summary>Adds a line, in pieces when it is long; whether the file is to be written now.</summary>
        internal bool Add(string text, float now)
        {
            var pieces = Utf8.Pieces(text, PieceLength);
            for (var i = 0; i < pieces.Count; i++)
            {
                Keep(pieces[i]);
            }

            if (now - writtenAt < Interval || !ReplayGuard.MayWrite)
            {
                waiting = true;
                return false;
            }

            return true;
        }

        /// <summary>Whether lines wait, the second since the last write is up, and the replay guard lets the file be written: watching a replay never writes it.</summary>
        internal bool Due(float now)
        {
            return waiting && now - writtenAt >= Interval && ReplayGuard.MayWrite;
        }

        /// <summary>Notes that the file was written.</summary>
        internal void Written(float now)
        {
            writtenAt = now;
            waiting = false;
        }

        private void Keep(string piece)
        {
            Lines.Add(piece);
            if (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
            }
        }
    }
}
