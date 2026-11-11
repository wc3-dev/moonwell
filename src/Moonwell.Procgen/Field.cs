using System;

namespace Moonwell.Procgen
{
    /// <summary>A grid of numbers, a column and a row each, for heights, densities and masks. Column 0 is the left and row 0 the bottom.</summary>
    public sealed class Field
    {
        /// <summary>The cells, row by row from the bottom left.</summary>
        internal readonly float[] Cells;

        /// <summary>A field of zeros. Fewer than 1 column or row throws ArgumentException.</summary>
        public Field(int columns, int rows)
        {
            if (columns < 1 || rows < 1)
            {
                throw new ArgumentException("A field is 1 by 1 or more, not " + columns + " by " + rows);
            }

            Columns = columns;
            Rows = rows;
            Cells = new float[columns * rows];
        }

        /// <summary>
        /// A field of <c>Noise.Layered</c>, from 0 to 1, its bumps about <paramref name="featureSize"/> cells across. The
        /// same seed and size give the same field on every machine. A feature size of 0 or less throws ArgumentException.
        /// </summary>
        [LocalSafe]
        public static Field FromNoise(int seed, int columns, int rows, float featureSize, int layers = 4, float roughness = 0.5f)
        {
            if (!(featureSize > 0f))
            {
                throw new ArgumentException("Field.FromNoise takes a feature size above 0, not " + featureSize);
            }

            var field = new Field(columns, rows);
            var cells = field.Cells;

            // shortcut: every cell hashes its four noise corners again for each layer; if the game shows a large field slow, keep a layer's corners while the cells stay between them.
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    cells[row * columns + column] = Noise.Layered(seed, column / featureSize, row / featureSize, layers, roughness);
                }
            }

            return field;
        }

        /// <summary>The cells across.</summary>
        [LocalSafe]
        public int Columns { get; }

        /// <summary>The cells up.</summary>
        [LocalSafe]
        public int Rows { get; }

        /// <summary>The number in a cell. A cell outside the field throws ArgumentOutOfRangeException.</summary>
        [LocalSafe]
        public float this[int column, int row]
        {
            get
            {
                return Cells[At(column, row)];
            }

            set
            {
                Cells[At(column, row)] = value;
            }
        }

        /// <summary>The lowest number in the field.</summary>
        [LocalSafe]
        public float Lowest()
        {
            var lowest = Cells[0];
            for (var k = 1; k < Cells.Length; k++)
            {
                if (Cells[k] < lowest)
                {
                    lowest = Cells[k];
                }
            }

            return lowest;
        }

        /// <summary>The highest number in the field.</summary>
        [LocalSafe]
        public float Highest()
        {
            var highest = Cells[0];
            for (var k = 1; k < Cells.Length; k++)
            {
                if (Cells[k] > highest)
                {
                    highest = Cells[k];
                }
            }

            return highest;
        }

        /// <summary>Stretches the field so its lowest number is 0 and its highest 1. A field of one number becomes all 0.</summary>
        public void Normalize()
        {
            var lowest = Lowest();
            var span = Highest() - lowest;
            for (var k = 0; k < Cells.Length; k++)
            {
                Cells[k] = span > 0f ? (Cells[k] - lowest) / span : 0f;
            }
        }

        /// <summary>
        /// Fades the field to 0 at its edges: each cell is multiplied by how far in it is, 0 on the edge and 1 from
        /// <paramref name="cells"/> in, smooth between. 0 or less does nothing.
        /// </summary>
        public void FadeEdges(float cells)
        {
            if (!(cells > 0f))
            {
                return;
            }

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var edge = Math.Min(Math.Min(column, Columns - 1 - column), Math.Min(row, Rows - 1 - row));
                    if (edge < cells)
                    {
                        var t = edge / cells;
                        Cells[row * Columns + column] *= t * t * (3f - 2f * t);
                    }
                }
            }
        }

        /// <summary>Smooths the field once: each cell becomes the mean of itself and the cells that touch it.</summary>
        public void Smooth()
        {
            var before = new float[Cells.Length];
            for (var k = 0; k < Cells.Length; k++)
            {
                before[k] = Cells[k];
            }

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var sum = 0f;
                    var count = 0;
                    for (var r = Math.Max(row - 1, 0); r <= Math.Min(row + 1, Rows - 1); r++)
                    {
                        for (var c = Math.Max(column - 1, 0); c <= Math.Min(column + 1, Columns - 1); c++)
                        {
                            sum += before[r * Columns + c];
                            count++;
                        }
                    }

                    Cells[row * Columns + column] = sum / count;
                }
            }
        }

        /// <summary>The place of a cell in Cells.</summary>
        private int At(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException("column", "A field of " + Columns + " by " + Rows + " has no cell " + column + ", " + row);
            }

            return row * Columns + column;
        }
    }
}
