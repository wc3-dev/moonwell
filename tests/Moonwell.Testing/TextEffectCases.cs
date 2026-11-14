using System;

namespace Moonwell.Testing
{
    /// <summary>Cases for coloured text, its effects letter by letter, and time as text.</summary>
    public static class TextEffectCases
    {
        /// <summary>Colored puts the text in a colour code, with full alpha, as the game shows no other (game facts).</summary>
        public static void Colored(FakeWorld world)
        {
            Expect.Equal("|cffffcc00Gold|r", "Gold".Colored(Color.TooltipGold), "gold text");
            Expect.Equal("|cffff0000Red|r", "Red".Colored(new Color(255, 0, 0, 10)), "alpha left out");
        }

        /// <summary>Rainbow goes from red through to magenta over the letters. The middle letter's hue lands on a half, which .NET and the game round apart, so it is worked out as Rainbow does.</summary>
        public static void Rainbow(FakeWorld world)
        {
            var middle = Color.FromHsv(1 * (5.0f / 6.0f) / 2, 1.0f, 1.0f).ToHtml(false);
            Expect.Equal("|cffff0000a|cff" + middle + "b|cffff00ffc|r", "abc".Rainbow(), "three letters");
            Expect.Equal("|cffff0000a|cffff00ffb|r", "ab".Rainbow(), "two letters");
            Expect.Equal("|cffff0000a|r", "a".Rainbow(), "one letter");
        }

        /// <summary>A shift starts the rainbow further round the colour wheel.</summary>
        public static void RainbowShift(FakeWorld world)
        {
            Expect.Equal("|cff00ff00a|cffffff00b|r", "ab".Rainbow(1.0f / 3.0f), "a third round: green, then on round the wheel to yellow");
        }

        /// <summary>Gradient spreads two colours or more evenly over the letters.</summary>
        public static void Gradient(FakeWorld world)
        {
            Expect.Equal("|cffff0000a|cff800080b|cff0000ffc|r", "abc".Gradient(Color.Red, Color.Blue), "red to blue");
            Expect.Equal("|cffff0000a|cff00ff00b|cff0000ffc|r", "abc".Gradient(Color.Red, Color.Green, Color.Blue), "through green");
            Expect.Throws<ArgumentException>(() => "abc".Gradient(Color.Red), "one colour");
        }

        /// <summary>Spaces get no code, and letters next to each other of one colour share one.</summary>
        public static void SpacesAndSharedCodes(FakeWorld world)
        {
            Expect.Equal("|cffff0000a |cffff00ffb|r", "a b".Rainbow(), "a space between");
            Expect.Equal("|cffff0000aa|r", "aa".Gradient(Color.Red, Color.Red), "one code for two letters");
        }

        /// <summary>A line break stays one, and the next letter gets its code again; colour codes in the text give way to the effect's.</summary>
        public static void BreaksAndCodes(FakeWorld world)
        {
            Expect.Equal("|cffff0000a|n|cffff0000a|r", "a|na".Gradient(Color.Red, Color.Red), "a line break");
            Expect.Equal("|cffff0000a|cffff00ffb|r", "|cff00ff00a|rb".Rainbow(), "codes in the text dropped");
            Expect.Equal("|cffff0000||a|r", "||a".Gradient(Color.Red, Color.Red), "an escaped bar is a letter");
        }

        /// <summary>A letter of more than one byte gets one code: in the game a string is its UTF-8 bytes.</summary>
        public static void WholeLetters(FakeWorld world)
        {
            Expect.Equal("|cffff0000å|cffff00ffö|r", "åö".Rainbow(), "two Swedish letters");
        }

        /// <summary>A smooth effect over a long text gives neighbouring letters one code, so the text keeps within the 1,023 characters a screen line shows (game facts), from its first colour to its last.</summary>
        public static void LongTextSharesCodes(FakeWorld world)
        {
            var text = "";
            for (var i = 0; i < 200; i++)
            {
                text += "a";
            }

            var rainbow = text.Rainbow();
            Expect.True(rainbow.Length <= 1023, "the rainbow's length: " + rainbow.Length);
            Expect.True(rainbow.StartsWith("|cffff0000a"), "it starts red");
            Expect.True(rainbow.EndsWith("|cffff00ffa|r") || rainbow.EndsWith("|cffff00ffaa|r") || rainbow.EndsWith("|cffff00ffaaa|r"), "it ends magenta");
            Expect.Equal(text, Plain(rainbow), "every letter kept");
            Expect.True(text.Gradient(Color.Red, Color.Blue).Length <= 1023, "a gradient too");
        }

        /// <summary>Alternate and Confetti keep a colour for each letter however long the text.</summary>
        public static void LongTextLetterByLetter(FakeWorld world)
        {
            var text = "";
            for (var i = 0; i < 200; i++)
            {
                text += "a";
            }

            Expect.Equal(200 * 11 + 2, text.Alternate(Color.Red, Color.Yellow).Length, "alternate, a code for each letter");
        }

        /// <summary>Alternate gives each letter the next colour in turn.</summary>
        public static void Alternate(FakeWorld world)
        {
            Expect.Equal("|cffff0000a|cffffff00b|cffff0000c|r", "abc".Alternate(Color.Red, Color.Yellow), "red and yellow");
            Expect.Equal("|cffff0000a |cffffff00b|r", "a b".Alternate(Color.Red, Color.Yellow), "a space takes no turn");
            Expect.Equal("|cffff0000a|cffffff00©|cffff0000b|r", "a©b".Alternate(Color.Red, Color.Yellow), "© is a letter");
            Expect.Equal("|cffff0000Α|cffffff00Β|cffff0000Γ|r", "ΑΒΓ".Alternate(Color.Red, Color.Yellow), "Greek letters");
            Expect.Equal("|cffff0000中|cffffff00文|r", "中文".Alternate(Color.Red, Color.Yellow), "Chinese letters");
        }

        /// <summary>Confetti gives the same text the same colours each time, with no random numbers.</summary>
        public static void Confetti(FakeWorld world)
        {
            var once = "Happy".Confetti();
            Expect.Equal(once, "Happy".Confetti(), "the same colours again");
            Expect.True(once.StartsWith("|cff") && once.EndsWith("|r"), "coloured: " + once);
            Expect.True(once != "Happy".Rainbow(), "not the rainbow");
        }

        /// <summary>Highlight colours each place a word appears.</summary>
        public static void Highlight(FakeWorld world)
        {
            Expect.Equal("Deals |cffff000050|r, then |cffff000050|r more", "Deals 50, then 50 more".Highlight("50", Color.Red), "twice");
            Expect.Equal("|cffff0000ab|r, |cffff0000ab|r", "ab, ab".Highlight("ab", Color.Red), "at the start");
            Expect.Equal("no match", "no match".Highlight("50", Color.Red), "no match");
        }

        /// <summary>Sweep moves a band of one colour through another, 20 letters a second, narrow unless a shape is named: at 0.5 s the band is on the third letter.</summary>
        public static void Sweep(FakeWorld world)
        {
            Expect.Equal("|cffff0000a|cff800080b|cff0000ffc|cff800080d|cffff0000e|r", "abcde".Sweep(Color.Red, Color.Blue, 0.5f), "narrow on the third letter");
            Expect.Equal("abcde".Sweep(Color.Red, Color.Blue, 0.5f), "abcde".Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Narrow), "narrow is the default");
        }

        /// <summary>A sweep enters before the first letter: at 0 s no letter has its colour yet.</summary>
        public static void SweepEnters(FakeWorld world)
        {
            Expect.Equal("|cffff0000abcde|r", "abcde".Sweep(Color.Red, Color.Blue, 0.0f), "not in yet");
        }

        /// <summary>Wide reaches further than narrow, sharp has one letter in the sweep's colour, and a comet's trail lingers behind it.</summary>
        public static void SweepShapes(FakeWorld world)
        {
            var narrow = Colours("abcdefghij".Sweep(Color.Red, Color.Blue, 0.5f));
            var wide = Colours("abcdefghij".Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Wide));
            Expect.Equal("ff0000", narrow[5], "narrow leaves the sixth letter");
            Expect.True(wide[5] != "ff0000", "wide reaches the sixth letter: " + wide[5]);
            var sharp = Colours("abcde".Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Sharp));
            Expect.Equal("ff0000 800080 0000ff 800080 ff0000", string.Join(" ", sharp), "sharp");
            var comet = Colours("abcde".Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Comet));
            Expect.True(comet[0] != "ff0000" && comet[1] != "ff0000", "the trail behind: " + string.Join(" ", comet));
            Expect.Equal("ff0000", comet[4], "nothing ahead of the front");
        }

        /// <summary>Repeating starts a new narrow sweep every second, so a long text has several at once.</summary>
        public static void SweepRepeating(FakeWorld world)
        {
            var text = "";
            for (var i = 0; i < 45; i++)
            {
                text += "a";
            }

            Expect.Equal(3, Count(Colours(text.Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Repeating)), "0000ff"), "three sweeps in 45 letters");
            Expect.Equal(1, Count(Colours("abcdefghij".Sweep(Color.Red, Color.Blue, 0.5f, SweepShape.Repeating)), "0000ff"), "one in 10 letters");
        }

        /// <summary>The letters outside the band share one code, so a long text keeps within a screen line.</summary>
        public static void SweepLongText(FakeWorld world)
        {
            var text = "";
            for (var i = 0; i < 200; i++)
            {
                text += "a";
            }

            var comet = text.Sweep(Color.Red, Color.Blue, 5.0f, SweepShape.Comet);
            Expect.True(comet.Length <= 1023, "a comet's length: " + comet.Length);
        }

        /// <summary>A moving effect draws as Text's effect at its time: a rainbow a turn in its seconds, its shift falling so the colours flow along the text, a sweep at its letters a second; a still one is refused.</summary>
        public static void MovingEffect(FakeWorld world)
        {
            Expect.Equal(Text.Rainbow("abc", 0f), TextEffect.Rainbow().Paint("abc", 0f), "a rainbow at its start");
            Expect.Equal(Text.Rainbow("abc", -0.25f), TextEffect.Rainbow().Paint("abc", 0.5f), "a quarter turn in 0.5 s");
            Expect.Equal(Text.Rainbow("abc", -0.125f), TextEffect.Rainbow(4f).Paint("abc", 0.5f), "an eighth, turning in 4 s");
            Expect.Equal(Text.Sweep("abcdef", Color.Red, Color.White, 0.5f), TextEffect.Sweep(Color.Red, Color.White).Paint("abcdef", 0.5f), "20 letters a second, as Sweep");
            Expect.Equal(Text.Sweep("abcdef", Color.Red, Color.White, 0.25f, SweepShape.Comet), TextEffect.Sweep(Color.Red, Color.White, SweepShape.Comet, 10f).Paint("abcdef", 0.5f), "10 letters a second, a comet");
            Expect.Throws<ArgumentException>(() => TextEffect.Rainbow(0f), "a rainbow that never turns");
            Expect.Throws<ArgumentException>(() => TextEffect.Sweep(Color.Red, Color.White, speed: -1f), "a sweep going back");
        }

        // The colour of each letter, as hex.
        private static string[] Colours(string coloured)
        {
            var colours = new System.Collections.Generic.List<string>();
            var current = "";
            var i = 0;
            while (i < coloured.Length)
            {
                if (coloured.Substring(i).StartsWith("|c"))
                {
                    current = coloured.Substring(i + 4, 6);
                    i += 10;
                }
                else if (coloured.Substring(i).StartsWith("|r"))
                {
                    i += 2;
                }
                else
                {
                    colours.Add(current);
                    i++;
                }
            }

            return colours.ToArray();
        }

        private static int Count(string[] colours, string colour)
        {
            var count = 0;
            foreach (var each in colours)
            {
                if (each == colour)
                {
                    count++;
                }
            }

            return count;
        }

        // The text with its colour codes taken out.
        private static string Plain(string coloured)
        {
            var plain = "";
            var i = 0;
            while (i < coloured.Length)
            {
                if (coloured.Substring(i).StartsWith("|c"))
                {
                    i += 10;
                }
                else if (coloured.Substring(i).StartsWith("|r"))
                {
                    i += 2;
                }
                else
                {
                    plain += coloured.Substring(i, 1);
                    i++;
                }
            }

            return plain;
        }

        /// <summary>Text.Clock writes seconds as minutes and seconds, hours past an hour, rounding down.</summary>
        public static void Clock(FakeWorld world)
        {
            Expect.Equal("2:05", Text.Clock(125.0f), "125 s");
            Expect.Equal("0:59", Text.Clock(59.9f), "59.9 s");
            Expect.Equal("1:02:05", Text.Clock(3725.0f), "3725 s");
            Expect.Equal("0:00", Text.Clock(-3.0f), "below 0");
        }

        /// <summary>Text.TimeOfDay writes hours as a 24-hour clock.</summary>
        public static void TimeOfDay(FakeWorld world)
        {
            Expect.Equal("06:30", Text.TimeOfDay(6.5f), "6.5");
            Expect.Equal("00:00", Text.TimeOfDay(0.0f), "0");
            Expect.Equal("13:15", Text.TimeOfDay(13.25f), "13.25");
            Expect.Equal("23:59", Text.TimeOfDay(23.99f), "23.99");
            Expect.Equal("06:12", Text.TimeOfDay(6.2f), "6.2");
            Expect.Equal("12:42", Text.TimeOfDay(12.7f), "12.7");
            Expect.Equal("18:18", Text.TimeOfDay(18.3f), "18.3");
            Expect.Equal("06:06", Text.TimeOfDay(6.1f), "6.1");
            for (var minute = 0; minute < 24 * 60; minute++)
            {
                var hours = minute / 60.0f;
                var shown = Text.TimeOfDay(hours);
                Expect.Equal((minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2"), shown, "minute " + minute);
            }
        }
    }
}
