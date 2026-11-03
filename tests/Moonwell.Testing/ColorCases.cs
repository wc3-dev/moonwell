using System;

namespace Moonwell.Testing
{
    /// <summary>Cases for Color: its parts, its text form, and the colours made from others.</summary>
    public static class ColorCases
    {
        /// <summary>A colour is four whole numbers from 0 to 255, alpha 255 unless given.</summary>
        public static void Parts(FakeWorld world)
        {
            var gold = new Color(255, 204, 0);
            Expect.Equal("255 204 0 255", Parts(gold), "a colour without alpha");
            Expect.Equal("10 20 30 40", Parts(new Color(10, 20, 30, 40)), "a colour with alpha");
        }

        /// <summary>A part outside 0 to 255 is kept within them.</summary>
        public static void PartsKeptWithin(FakeWorld world)
        {
            Expect.Equal("255 0 128 255", Parts(new Color(300, -5, 128, 999)), "parts kept within 0 to 255");
        }

        /// <summary>Two colours are equal when their four parts are.</summary>
        public static void Equality(FakeWorld world)
        {
            Expect.Equal(true, new Color(1, 2, 3) == new Color(1, 2, 3, 255), "the same parts");
            Expect.Equal(true, new Color(1, 2, 3) != new Color(1, 2, 4), "another blue");
            Expect.Equal(true, new Color(1, 2, 3).Equals(new Color(1, 2, 3)), "Equals");
        }

        /// <summary>FromHtml reads six or eight hex digits, red first and alpha last as Godot's, with or without '#'; ToHtml writes them.</summary>
        public static void Html(FakeWorld world)
        {
            Expect.Equal("255 204 0 255", Parts(Color.FromHtml("#ffcc00")), "#ffcc00");
            Expect.Equal("255 204 0 128", Parts(Color.FromHtml("FFCC0080")), "FFCC0080");
            Expect.Equal("ffcc0080", new Color(255, 204, 0, 128).ToHtml(), "with alpha");
            Expect.Equal("ffcc00", new Color(255, 204, 0, 128).ToHtml(false), "without alpha");
            Expect.Throws<ArgumentException>(() => Color.FromHtml("#ffcc0"), "five digits");
            Expect.Throws<ArgumentException>(() => Color.FromHtml("#ffcc0g"), "a letter that is no hex digit");
        }

        /// <summary>Lerp mixes towards another colour by a weight from 0 to 1, rounding each part.</summary>
        public static void Lerp(FakeWorld world)
        {
            Expect.Equal("128 0 128 255", Parts(Color.Red.Lerp(Color.Blue, 0.5f)), "half way from red to blue");
            Expect.Equal("255 0 0 255", Parts(Color.Red.Lerp(Color.Blue, 0.0f)), "no way");
            Expect.Equal("0 0 255 255", Parts(Color.Red.Lerp(Color.Blue, 1.0f)), "all the way");
        }

        /// <summary>FromHsv takes a hue, saturation and value from 0 to 1, as Godot's.</summary>
        public static void FromHsv(FakeWorld world)
        {
            Expect.Equal("255 0 0 255", Parts(Color.FromHsv(0.0f, 1.0f, 1.0f)), "hue 0");
            Expect.Equal("0 255 0 255", Parts(Color.FromHsv(1.0f / 3.0f, 1.0f, 1.0f)), "hue a third");
            Expect.Equal("0 0 255 255", Parts(Color.FromHsv(2.0f / 3.0f, 1.0f, 1.0f)), "hue two thirds");
            Expect.Equal("128 128 128 255", Parts(Color.FromHsv(0.5f, 0.0f, 0.5f)), "no saturation");
            Expect.Equal("255 255 0 100", Parts(Color.FromHsv(1.0f / 6.0f, 1.0f, 1.0f, 100)), "with alpha");
        }

        /// <summary>Darkened, Lightened and Inverted, as Godot's; alpha stays.</summary>
        public static void Shades(FakeWorld world)
        {
            Expect.Equal("128 128 128 255", Parts(Color.White.Darkened(0.5f)), "white darkened by half");
            Expect.Equal("128 128 128 255", Parts(Color.Black.Lightened(0.5f)), "black lightened by half");
            Expect.Equal("0 51 255 40", Parts(new Color(255, 204, 0, 40).Inverted()), "gold inverted");
        }

        /// <summary>The named colours: Godot's plain ones, and the game's tooltip gold.</summary>
        public static void Named(FakeWorld world)
        {
            Expect.Equal("ffffff", Color.White.ToHtml(false), "White");
            Expect.Equal("000000", Color.Black.ToHtml(false), "Black");
            Expect.Equal("bebebe", Color.Gray.ToHtml(false), "Gray");
            Expect.Equal("c0c0c0", Color.Silver.ToHtml(false), "Silver");
            Expect.Equal("ff0000", Color.Red.ToHtml(false), "Red");
            Expect.Equal("ffa500", Color.Orange.ToHtml(false), "Orange");
            Expect.Equal("ffff00", Color.Yellow.ToHtml(false), "Yellow");
            Expect.Equal("ffd700", Color.Gold.ToHtml(false), "Gold");
            Expect.Equal("00ff00", Color.Green.ToHtml(false), "Green");
            Expect.Equal("00ffff", Color.Cyan.ToHtml(false), "Cyan");
            Expect.Equal("0000ff", Color.Blue.ToHtml(false), "Blue");
            Expect.Equal("a020f0", Color.Purple.ToHtml(false), "Purple");
            Expect.Equal("ff00ff", Color.Magenta.ToHtml(false), "Magenta");
            Expect.Equal("ffc0cb", Color.Pink.ToHtml(false), "Pink");
            Expect.Equal("a52a2a", Color.Brown.ToHtml(false), "Brown");
            Expect.Equal("ffcc00", Color.TooltipGold.ToHtml(false), "TooltipGold");
        }

        /// <summary>Color.Of gives a player colour's colour, as the lobby shows it.</summary>
        public static void PlayerColors(FakeWorld world)
        {
            Expect.Equal("ff0303", Color.Of(PlayerColor.Red).ToHtml(false), "red");
            Expect.Equal("0042ff", Color.Of(PlayerColor.Blue).ToHtml(false), "blue");
            Expect.Equal("1ce6b9", Color.Of(PlayerColor.Cyan).ToHtml(false), "teal");
            Expect.Equal("a46f33", Color.Of(PlayerColor.Peanut).ToHtml(false), "peanut");
        }

        /// <summary>A player's name in the player's colour.</summary>
        public static void ColoredName(FakeWorld world)
        {
            Player.One.Name = "Thrall";
            Player.One.Color = PlayerColor.Blue;
            Expect.Equal("|cff0042ffThrall|r", Player.One.ColoredName, "the coloured name");
        }

        /// <summary>A unit takes a Color for its tint, alpha and all.</summary>
        public static void UnitTint(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            Unit.Of(handle)!.Tint(new Color(255, 128, 0, 64));
            Expect.Equal("255 128 0 64", world.Tints[handle][0] + " " + world.Tints[handle][1] + " " + world.Tints[handle][2] + " " + world.Tints[handle][3], "the tint");
        }

        private static string Parts(Color color)
        {
            return color.R + " " + color.G + " " + color.B + " " + color.A;
        }
    }
}
