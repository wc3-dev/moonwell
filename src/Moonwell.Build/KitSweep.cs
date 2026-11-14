using System;
using System.IO;

namespace Moonwell.Build
{
    /// <summary>
    /// The UI kit's cooldown sweep (decision 338): 64 images the build writes into a map that uses the kit, MoonwellKit\Sweep1
    /// to Sweep64, each 64 pixels square, dark over the share of the square left, 1 to 64 64ths, measured clockwise from the
    /// top and ending there, so the lit part grows clockwise from the top as a cooldown runs; the edge between them soft.
    /// Moonwell.Kit's ActionButton names them (ActionButton.SweepFolder).
    /// </summary>
    internal static class KitSweep
    {
        internal const string Folder = @"MoonwellKit\Sweep";

        internal const int Steps = 64;

        private const int Side = 64;

        /// <summary>How dark the dark part is: 60% black.</summary>
        private const byte Dark = 153;

        /// <summary>Samples per pixel along each side, for the soft edge.</summary>
        private const int Samples = 4;

        /// <summary>Writes the images into the staged map.</summary>
        internal static void Write(string stagedFolder)
        {
            Directory.CreateDirectory(Path.Combine(stagedFolder, "MoonwellKit"));
            for (var step = 1; step <= Steps; step++)
            {
                Images.Write(Path.Combine(stagedFolder, Folder + step + ".blp"), Side, Side, Pixels(step));
            }
        }

        /// <summary>One image's pixels, blue, green, red and alpha: black, with the alpha of the dark share at each pixel.</summary>
        internal static byte[] Pixels(int step)
        {
            var lit = 1.0 - ((double)step / Steps);
            var pixels = new byte[Side * Side * 4];
            for (var y = 0; y < Side; y++)
            {
                for (var x = 0; x < Side; x++)
                {
                    var dark = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            var dx = x + ((sx + 0.5) / Samples) - (Side / 2.0);
                            var dy = y + ((sy + 0.5) / Samples) - (Side / 2.0);
                            // Clockwise from the top: y grows down in the picture.
                            var turn = Math.Atan2(dx, -dy) / (2 * Math.PI);
                            if (turn < 0)
                            {
                                turn += 1;
                            }

                            if (turn >= lit)
                            {
                                dark++;
                            }
                        }
                    }

                    pixels[(((y * Side) + x) * 4) + 3] = (byte)Math.Round(Dark * (double)dark / (Samples * Samples));
                }
            }

            return pixels;
        }
    }
}
