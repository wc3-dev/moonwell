namespace Moonwell.Build
{
    /// <summary>What <see cref="LineMarks"/> inserted on a file's lines, to read a column of the marked text as the file's own.</summary>
    public sealed class MarkedColumns
    {
        private readonly Dictionary<int, List<(int Column, int Length)>> inserted = new Dictionary<int, List<(int Column, int Length)>>();

        /// <summary>Records text of a length inserted before a column of a line of the file; both count from 1.</summary>
        internal void Add(int line, int column, int length)
        {
            if (!inserted.TryGetValue(line, out var inserts))
            {
                inserts = new List<(int Column, int Length)>();
                inserted[line] = inserts;
            }

            inserts.Add((column, length));
        }

        /// <summary>The column in the file of a column of the marked text; a column inside inserted text is the one it was inserted before.</summary>
        /// <param name="line">The line, from 1; marking moves no line.</param>
        /// <param name="column">The column in the marked text, from 1.</param>
        /// <returns>The column in the file, from 1.</returns>
        public int Original(int line, int column)
        {
            if (!inserted.TryGetValue(line, out var inserts))
            {
                return column;
            }

            var moved = 0;
            foreach (var insert in inserts.OrderBy(insert => insert.Column))
            {
                var start = insert.Column + moved;
                if (column < start)
                {
                    break;
                }

                if (column < start + insert.Length)
                {
                    return insert.Column;
                }

                moved += insert.Length;
            }

            return column - moved;
        }
    }
}
