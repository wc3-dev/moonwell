namespace Moonwell
{
    /// <summary>
    /// The UI kit's look (decision 212): the template its windows and rich tooltips are made from, and its text colours. The
    /// four races' looks are the game's art; PlayersRace is the game's own window, which draws each player's race on their
    /// screen. A map's own theme names a template of its own .fdf.
    /// </summary>
    public sealed class Theme
    {
        // Made at their first use: a public type has no static initializer (StaticSetupTests).
        private static Theme? playersRace, human, orc, nightElf, undead;

        /// <summary>Each player's race on their own screen, as the game's Esc menu: EscMenuBackdrop, whose art war3skins.txt gives for each race.</summary>
        public static Theme PlayersRace => playersRace ??= new Theme();

        /// <summary>The Human window, gold and blue.</summary>
        public static Theme Human => human ??= new Theme { Window = "MoonwellKitWindowHuman" };

        /// <summary>The Orc window.</summary>
        public static Theme Orc => orc ??= new Theme { Window = "MoonwellKitWindowOrc" };

        /// <summary>The Night Elf window.</summary>
        public static Theme NightElf => nightElf ??= new Theme { Window = "MoonwellKitWindowNightElf" };

        /// <summary>The Undead window.</summary>
        public static Theme Undead => undead ??= new Theme { Window = "MoonwellKitWindowUndead" };

        /// <summary>The template windows and rich tooltips are made from; EscMenuBackdrop unless set.</summary>
        [LocalSafe]
        public string Window { get; set; } = "EscMenuBackdrop";

        /// <summary>The colour of titles and costs; the game's gold unless set.</summary>
        [LocalSafe]
        public Color Title { get; set; } = new Color(252, 211, 18);

        /// <summary>The colour of other text; white unless set.</summary>
        [LocalSafe]
        public Color Text { get; set; } = Color.White;
    }
}
