using System;

namespace Moonwell
{
    /// <summary>
    /// A colour as the game takes it: red, green, blue and alpha, each a whole number from 0 to 255. A readonly struct, as
    /// Vector2 is. Names follow Godot's Color where the idea is the same; Godot's parts run from 0 to 1, these from 0 to 255.
    /// </summary>
    public readonly struct Color : IEquatable<Color>
    {
        /// <summary>The red part, 0 to 255.</summary>
        public readonly int R;

        /// <summary>The green part, 0 to 255.</summary>
        public readonly int G;

        /// <summary>The blue part, 0 to 255.</summary>
        public readonly int B;

        /// <summary>The alpha part, 0 (clear) to 255 (solid). Text shows no alpha (game facts).</summary>
        public readonly int A;

        /// <summary>Makes a colour; a part outside 0 to 255 is kept within them.</summary>
        [LocalSafe]
        public Color(int r, int g, int b, int a = 255)
        {
            R = Within(r);
            G = Within(g);
            B = Within(b);
            A = Within(a);
        }

        /// <summary>ffffff.</summary>
        [LocalSafe]
        public static Color White => new Color(255, 255, 255);

        /// <summary>000000.</summary>
        [LocalSafe]
        public static Color Black => new Color(0, 0, 0);

        /// <summary>bebebe, Godot's gray.</summary>
        [LocalSafe]
        public static Color Gray => new Color(190, 190, 190);

        /// <summary>c0c0c0.</summary>
        [LocalSafe]
        public static Color Silver => new Color(192, 192, 192);

        /// <summary>ff0000.</summary>
        [LocalSafe]
        public static Color Red => new Color(255, 0, 0);

        /// <summary>ffa500.</summary>
        [LocalSafe]
        public static Color Orange => new Color(255, 165, 0);

        /// <summary>ffff00.</summary>
        [LocalSafe]
        public static Color Yellow => new Color(255, 255, 0);

        /// <summary>ffd700, Godot's gold; the game's own is TooltipGold.</summary>
        [LocalSafe]
        public static Color Gold => new Color(255, 215, 0);

        /// <summary>00ff00.</summary>
        [LocalSafe]
        public static Color Green => new Color(0, 255, 0);

        /// <summary>00ffff.</summary>
        [LocalSafe]
        public static Color Cyan => new Color(0, 255, 255);

        /// <summary>0000ff.</summary>
        [LocalSafe]
        public static Color Blue => new Color(0, 0, 255);

        /// <summary>a020f0, Godot's purple.</summary>
        [LocalSafe]
        public static Color Purple => new Color(160, 32, 240);

        /// <summary>ff00ff.</summary>
        [LocalSafe]
        public static Color Magenta => new Color(255, 0, 255);

        /// <summary>ffc0cb.</summary>
        [LocalSafe]
        public static Color Pink => new Color(255, 192, 203);

        /// <summary>a52a2a.</summary>
        [LocalSafe]
        public static Color Brown => new Color(165, 42, 42);

        /// <summary>ffcc00, the gold of the game's own tooltips ("Holy Light - [|cffffcc00Level 1|r]").</summary>
        [LocalSafe]
        public static Color TooltipGold => new Color(255, 204, 0);

        /// <summary>A player colour as the lobby shows it.</summary>
        [LocalSafe]
        public static Color Of(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return new Color(255, 3, 3);
                case PlayerColor.Blue: return new Color(0, 66, 255);
                case PlayerColor.Cyan: return new Color(28, 230, 185);
                case PlayerColor.Purple: return new Color(84, 0, 129);
                case PlayerColor.Yellow: return new Color(255, 252, 0);
                case PlayerColor.Orange: return new Color(254, 138, 14);
                case PlayerColor.Green: return new Color(32, 192, 0);
                case PlayerColor.Pink: return new Color(229, 91, 176);
                case PlayerColor.LightGray: return new Color(149, 150, 151);
                case PlayerColor.LightBlue: return new Color(126, 191, 241);
                case PlayerColor.Aqua: return new Color(16, 98, 70);
                case PlayerColor.Brown: return new Color(78, 42, 4);
                case PlayerColor.Maroon: return new Color(155, 0, 0);
                case PlayerColor.Navy: return new Color(0, 0, 195);
                case PlayerColor.Turquoise: return new Color(0, 234, 255);
                case PlayerColor.Violet: return new Color(190, 0, 254);
                case PlayerColor.Wheat: return new Color(235, 205, 135);
                case PlayerColor.Peach: return new Color(248, 164, 139);
                case PlayerColor.Mint: return new Color(191, 255, 128);
                case PlayerColor.Lavender: return new Color(220, 185, 235);
                case PlayerColor.Coal: return new Color(40, 40, 40);
                case PlayerColor.Snow: return new Color(235, 240, 255);
                case PlayerColor.Emerald: return new Color(0, 120, 30);
                default: return new Color(164, 111, 51);
            }
        }

        /// <summary>A colour from six or eight hex digits, red first and alpha last ("#ffcc00", "ffcc0080"), with or without '#'.</summary>
        /// <exception cref="ArgumentException">Not six or eight hex digits.</exception>
        [LocalSafe]
        public static Color FromHtml(string html)
        {
            var digits = html.StartsWith("#") ? html.Substring(1) : html;
            if (digits.Length != 6 && digits.Length != 8)
            {
                throw new ArgumentException("a colour needs six or eight hex digits, not '" + html + "'");
            }

            var parts = new int[4];
            parts[3] = 255;
            for (var i = 0; i < digits.Length / 2; i++)
            {
                parts[i] = HexDigit(digits, i * 2, html) * 16 + HexDigit(digits, i * 2 + 1, html);
            }

            return new Color(parts[0], parts[1], parts[2], parts[3]);
        }

        /// <summary>A colour from a hue, saturation and value, each from 0 to 1, as Godot's; a hue past 1 goes round again.</summary>
        [LocalSafe]
        public static Color FromHsv(float hue, float saturation, float value, int alpha = 255)
        {
            var turn = (hue - Floor(hue)) * 6.0f;
            var sector = Floor(turn);
            var f = turn - sector;
            var p = value * (1.0f - saturation);
            var q = value * (1.0f - saturation * f);
            var t = value * (1.0f - saturation * (1.0f - f));
            switch ((int)sector)
            {
                case 0: return Of(value, t, p, alpha);
                case 1: return Of(q, value, p, alpha);
                case 2: return Of(p, value, t, alpha);
                case 3: return Of(p, q, value, alpha);
                case 4: return Of(t, p, value, alpha);
                default: return Of(value, p, q, alpha);
            }
        }

        /// <summary>The colour as hex digits, red first: "ffcc00ff", or "ffcc00" without alpha.</summary>
        [LocalSafe]
        public string ToHtml(bool includeAlpha = true)
        {
            var html = R.ToString("x2") + G.ToString("x2") + B.ToString("x2");
            return includeAlpha ? html + A.ToString("x2") : html;
        }

        /// <summary>The colour mixed towards another by a weight from 0 (this one) to 1 (the other).</summary>
        [LocalSafe]
        public Color Lerp(Color to, float weight)
        {
            return new Color(
                Round(R + (to.R - R) * weight),
                Round(G + (to.G - G) * weight),
                Round(B + (to.B - B) * weight),
                Round(A + (to.A - A) * weight));
        }

        /// <summary>The colour darker by an amount from 0 to 1, as Godot's: 1 is black. Alpha stays.</summary>
        [LocalSafe]
        public Color Darkened(float amount)
        {
            return new Color(Round(R * (1.0f - amount)), Round(G * (1.0f - amount)), Round(B * (1.0f - amount)), A);
        }

        /// <summary>The colour lighter by an amount from 0 to 1, as Godot's: 1 is white. Alpha stays.</summary>
        [LocalSafe]
        public Color Lightened(float amount)
        {
            return new Color(Round(R + (255 - R) * amount), Round(G + (255 - G) * amount), Round(B + (255 - B) * amount), A);
        }

        /// <summary>The opposite colour, as Godot's: each of red, green and blue from 255. Alpha stays.</summary>
        [LocalSafe]
        public Color Inverted()
        {
            return new Color(255 - R, 255 - G, 255 - B, A);
        }

        /// <summary>Whether all four parts are equal.</summary>
        [LocalSafe]
        public static bool operator ==(Color a, Color b)
        {
            return a.Equals(b);
        }

        /// <summary>Whether a part differs.</summary>
        [LocalSafe]
        public static bool operator !=(Color a, Color b)
        {
            return !a.Equals(b);
        }

        /// <summary>Whether all four parts are equal.</summary>
        [LocalSafe]
        public bool Equals(Color other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        /// <summary>Whether the object is a Color with all four parts equal.</summary>
        [LocalSafe]
        public override bool Equals(object? obj)
        {
            return obj is Color other && Equals(other);
        }

        /// <summary>The four parts in one number.</summary>
        [LocalSafe]
        public override int GetHashCode()
        {
            return ((R * 256 + G) * 256 + B) * 128 + A / 2;
        }

        private static Color Of(float red, float green, float blue, int alpha)
        {
            return new Color(Round(red * 255.0f), Round(green * 255.0f), Round(blue * 255.0f), alpha);
        }

        private static int HexDigit(string digits, int index, string html)
        {
            var digit = "0123456789abcdef".IndexOf(digits.Substring(index, 1).ToLowerInvariant());
            if (digit < 0)
            {
                throw new ArgumentException("'" + html + "' is not a colour of hex digits");
            }

            return digit;
        }

        private static float Floor(float value)
        {
            var whole = (float)(int)value;
            return whole > value ? whole - 1.0f : whole;
        }

        private static int Round(float value)
        {
            return (int)(value + 0.5f);
        }

        private static int Within(int part)
        {
            return part < 0 ? 0 : part > 255 ? 255 : part;
        }
    }
}
