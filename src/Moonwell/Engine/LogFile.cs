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

        private float writtenAt = -Interval;
        private bool waiting;

        /// <summary>The lines the file holds, oldest first.</summary>
        internal List<string> Lines { get; } = new List<string>();

        /// <summary>Adds a line, in pieces when it is long; whether the file is to be written now.</summary>
        internal bool Add(string text, float now)
        {
            var start = 0;
            while (text.Length - start > PieceLength)
            {
                var end = start + PieceLength;
                while (end > start && IsInsideLetter(text[end]))
                {
                    end--;
                }

                Keep(text.Substring(start, end - start));
                start = end;
            }

            Keep(text.Substring(start));
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

        // In the game a string is its UTF-8 bytes, and a byte 10xxxxxx continues a letter.
        private static bool IsInsideLetter(char c)
        {
            return (c & 0xC0) == 0x80;
        }
    }
}
