using System;
using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// Text for the screen: coloured text, colour effects letter by letter, and time as text. The effects give each letter
    /// its own colour code, 10 characters, and end the text with |r, which goes back to white, not to an outer colour
    /// (game facts). A screen line shows 1,023 characters: Rainbow and Gradient share codes on a long text to keep
    /// within them, and Alternate and Confetti fit about 90 letters. They work at build time too, in a tooltip a class
    /// declares.
    /// </summary>
    [LocalSafe]
    public static class Text
    {
        // The share of the colour wheel a rainbow spans: red to magenta, so its ends differ.
        private const float RainbowSpan = 5.0f / 6.0f;

        // The characters a screen line shows (game facts): a smooth effect shares codes to keep within them.
        private const int ScreenLine = 1023;

        // Letters a second a sweep moves, and so how far apart Repeating's bands are.
        private const float SweepSpeed = 20.0f;

        // Letters before the first a band comes in from, and after the last it goes out to, so a comet's trail leaves too.
        private const float SweepLead = 8.0f;
        private const float SweepTrail = 24.0f;

        /// <summary>The text in a colour: "Gold".Colored(Color.TooltipGold). Its alpha is left out, as the game shows none.</summary>
        public static string Colored(this string text, Color color)
        {
            return "|cff" + color.ToHtml(false) + text + "|r";
        }

        /// <summary>The text in the colours of the rainbow, from red to magenta letter by letter; a shift from 0 to 1 starts it further round, so text redrawn with a growing shift cycles.</summary>
        public static string Rainbow(this string text, float shift = 0.0f)
        {
            return Paint(text, true, (index, count) => Color.FromHsv(shift + (count < 2 ? 0.0f : index * RainbowSpan / (count - 1)), 1.0f, 1.0f));
        }

        /// <summary>The text going from one colour to the next, letter by letter, through two colours or more spread evenly.</summary>
        /// <exception cref="ArgumentException">Fewer than two colours.</exception>
        public static string Gradient(this string text, params Color[] colors)
        {
            if (colors.Length < 2)
            {
                throw new ArgumentException("a gradient needs two colours or more");
            }

            return Paint(text, true, (index, count) =>
            {
                var at = count < 2 ? 0.0f : index * (colors.Length - 1) / (float)(count - 1);
                var stop = Math.Min((int)at, colors.Length - 2);
                return colors[stop].Lerp(colors[stop + 1], at - stop);
            });
        }

        /// <summary>Each letter in the next colour in turn: "DANGER".Alternate(Color.Red, Color.Yellow). A code for each letter, so a screen line fits about 90.</summary>
        /// <exception cref="ArgumentException">No colour.</exception>
        public static string Alternate(this string text, params Color[] colors)
        {
            if (colors.Length == 0)
            {
                throw new ArgumentException("Alternate needs a colour");
            }

            return Paint(text, false, (index, count) => colors[index % colors.Length]);
        }

        /// <summary>
        /// A band of the sweep colour moving through text of the main colour at 20 letters a second, as it stands at the
        /// time in seconds (Game.Time): each band comes in before the first letter and goes out after the last. It moves only
        /// in text that is redrawn, such as floating text set every 0.05 s.
        /// </summary>
        public static string Sweep(this string text, Color main, Color sweep, float time, SweepShape shape = SweepShape.Narrow)
        {
            return Paint(text, false, (index, count) =>
            {
                float distance;
                if (shape == SweepShape.Repeating)
                {
                    distance = (index + SweepLead - time * SweepSpeed % SweepSpeed) % SweepSpeed;
                    distance = distance < 0.0f ? distance + SweepSpeed : distance;
                    distance = distance > SweepSpeed / 2.0f ? distance - SweepSpeed : distance;
                }
                else
                {
                    distance = index - (time * SweepSpeed % (count + SweepLead + SweepTrail) - SweepLead);
                }

                var share = SweepShare(shape, distance);
                return share <= 0.0f ? main : main.Lerp(sweep, share > 1.0f ? 1.0f : share);
            });
        }

        /// <summary>A bright colour for each letter, worked out from the text, so the same text always gets the same colours, on every machine. A code for each letter, so a screen line fits about 90.</summary>
        public static string Confetti(this string text)
        {
            var seed = 0;
            for (var i = 0; i < text.Length; i++)
            {
                seed = (seed * 31 + text[i]) % 9973;
            }

            var start = seed / 9973.0f;
            return Paint(text, false, (index, count) => Color.FromHsv(start + index * 0.618034f, 0.75f, 1.0f));
        }

        /// <summary>The text with each place a word appears in a colour.</summary>
        public static string Highlight(this string text, string word, Color color)
        {
            if (word.Length == 0)
            {
                return text;
            }

            var result = new StringBuilder();
            var from = 0;
            var at = text.IndexOf(word, StringComparison.Ordinal);
            while (at >= 0)
            {
                result.Append(text.Substring(from, at - from));
                result.Append(word.Colored(color));
                from = at + word.Length;
                at = text.IndexOf(word, from, StringComparison.Ordinal);
            }

            result.Append(text.Substring(from));
            return result.ToString();
        }

        /// <summary>Seconds as a clock: "2:05", or "1:02:05" past an hour, rounded down, as a stopwatch shows it; below 0 is "0:00".</summary>
        public static string Clock(float seconds)
        {
            var total = seconds < 0.0f ? 0 : (int)seconds;
            var hours = total / 3600;
            var minutes = total % 3600 / 60;
            var rest = (total % 60).ToString("D2");
            return hours > 0 ? hours + ":" + minutes.ToString("D2") + ":" + rest : minutes + ":" + rest;
        }

        /// <summary>A time of day in hours as a 24-hour clock: 6.5 is "06:30", the minutes rounded down.</summary>
        public static string TimeOfDay(float hours)
        {
            // A thousandth of a minute takes in the float the game keeps: 6.2 is 371.99998 minutes.
            var total = (int)(hours * 60.0f + 0.001f);
            return (total / 60).ToString("D2") + ":" + (total % 60).ToString("D2");
        }

        // Gives each letter the colour colorOf names for it from its index and the count of letters, sharing a code between
        // letters of one colour. A space gets no code, a line break keeps its place and the next letter its code, and colour
        // codes in the text give way. A smooth effect that would pass a screen line gives letters next to each other one
        // colour, from the first colour to the last.
        private static string Paint(string text, bool smooth, Func<int, int, Color> colorOf)
        {
            var tokens = Tokens(text);
            var count = 0;
            var plain = 2;
            var breaks = 0;
            foreach (var token in tokens)
            {
                plain += token.Length;
                if (IsLetter(token))
                {
                    count++;
                }
                else if (token == "|n")
                {
                    breaks++;
                }
            }

            if (count == 0)
            {
                return text;
            }

            var group = 1;
            var groups = count;
            var codes = (ScreenLine - plain) / 10 - breaks;
            if (smooth && count > codes)
            {
                codes = codes < 1 ? 1 : codes;
                group = (count + codes - 1) / codes;
                groups = (count + group - 1) / group;
            }

            var result = new StringBuilder();
            var last = "";
            var index = 0;
            foreach (var token in tokens)
            {
                if (!IsLetter(token))
                {
                    result.Append(token);
                    if (token == "|n")
                    {
                        last = "";
                    }

                    continue;
                }

                var shade = group == 1 ? index : groups < 2 ? 0 : index / group * (count - 1) / (groups - 1);
                var code = colorOf(shade, count).ToHtml(false);
                index++;
                if (code != last)
                {
                    result.Append("|cff");
                    result.Append(code);
                    last = code;
                }

                result.Append(token);
            }

            result.Append("|r");
            return result.ToString();
        }

        // The text's letters, spaces and line breaks; a letter keeps all its bytes (in the game a string is its UTF-8 bytes),
        // "||" is the letter |, and colour codes are left out.
        private static List<string> Tokens(string text)
        {
            var tokens = new List<string>();
            var i = 0;
            while (i < text.Length)
            {
                if (text[i] == '|' && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    if (next == 'n' || next == 'N')
                    {
                        tokens.Add("|n");
                        i += 2;
                        continue;
                    }

                    if (next == 'r' || next == 'R')
                    {
                        i += 2;
                        continue;
                    }

                    if ((next == 'c' || next == 'C') && i + 10 <= text.Length)
                    {
                        i += 10;
                        continue;
                    }

                    if (next == '|')
                    {
                        tokens.Add("||");
                        i += 2;
                        continue;
                    }
                }

                var end = i + 1;
                while (end < text.Length && Utf8.IsInsideLetter(text[end]))
                {
                    end++;
                }

                tokens.Add(text.Substring(i, end - i));
                i = end;
            }

            return tokens;
        }

        // How much of the sweep's colour a letter takes at its distance from the band's centre, in letters (behind it below 0).
        private static float SweepShare(SweepShape shape, float distance)
        {
            var away = Math.Abs(distance);
            switch (shape)
            {
                case SweepShape.Wide:
                    return 1.0f - away / 6.0f;
                case SweepShape.Sharp:
                    return away <= 0.5f ? 1.0f : away <= 1.5f ? 0.5f : 0.0f;
                case SweepShape.Comet:
                    return distance >= 0.0f ? 1.0f - distance / 1.5f : 1.0f + distance / 20.0f;
                default:
                    return 1.0f - away / 2.0f;
            }
        }

        private static bool IsLetter(string token)
        {
            return token != " " && token != "|n";
        }
    }
}
