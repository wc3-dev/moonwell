namespace Moonwell
{
    /// <summary>
    /// The UI kit's look (decision 212): the template its windows and rich tooltips are made from, its text colours, and its HUD parts' border and bar textures (decision 334). The
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

        /// <summary>The template the HUD parts with a border (UnitFrame, CastBar) are drawn in: the game's thin control border, EscMenuControlBackdropTemplate, unless set; "" draws none.</summary>
        [LocalSafe]
        public string Hud { get; set; } = "EscMenuControlBackdropTemplate";

        /// <summary>What a StatusBar draws under its fill: the game's black, seen through, unless set.</summary>
        [LocalSafe]
        public string Backing { get; set; } = @"Textures\Black32.blp";

        /// <summary>A life bar's fill: the game's green team colour unless set.</summary>
        [LocalSafe]
        public string LifeBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor06.blp";

        /// <summary>A mana bar's fill: the game's blue team colour unless set.</summary>
        [LocalSafe]
        public string ManaBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor01.blp";

        /// <summary>A cast bar's fill: the game's yellow team colour unless set.</summary>
        [LocalSafe]
        public string CastBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor04.blp";

        /// <summary>A cast bar's fill once interrupted: the game's red team colour unless set.</summary>
        [LocalSafe]
        public string Interrupted { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor00.blp";
    }
}
