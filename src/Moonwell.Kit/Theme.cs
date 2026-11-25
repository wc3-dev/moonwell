namespace Moonwell
{
    /// <summary>
    /// The UI kit's look (decision 212): the template its windows and rich tooltips are made from, its text colours, sizes and font (read when a part's labels are made, decision 305), a window's close button, and its HUD parts' border and bar textures (decision 334). Every part takes Kit.Theme when it is made unless given its own Theme, which it passes down to its pieces. The
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

        /// <summary>A bar following a stat other than life or mana: the game's orange team colour unless set.</summary>
        [LocalSafe]
        public string StatBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor05.blp";

        /// <summary>A hero's experience bar's fill: the game's purple team colour unless set.</summary>
        [LocalSafe]
        public string ExperienceBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor03.blp";

        /// <summary>A bar's second layer, as a barrier over life: the game's snow white team colour unless set, drawn at OverlayAlpha so the bar shows through.</summary>
        [LocalSafe]
        public string OverlayBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor21.blp";

        /// <summary>How opaque a bar's second layer is, 0 to 255; 150 unless set.</summary>
        [LocalSafe]
        public int OverlayAlpha { get; set; } = 150;

        /// <summary>A cast bar's fill: the game's yellow team colour unless set.</summary>
        [LocalSafe]
        public string CastBar { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor04.blp";

        /// <summary>A cast bar's fill once interrupted: the game's red team colour unless set.</summary>
        [LocalSafe]
        public string Interrupted { get; set; } = @"ReplaceableTextures\TeamColor\TeamColor00.blp";

        /// <summary>The path in the map of a .ttf font in the map's assets for the parts' text (Label.Font); empty for the game's font unless set. Read when a part's labels are made.</summary>
        [LocalSafe]
        public string Font { get; set; } = "";

        /// <summary>The height of titles (a window's, a rich tooltip's) in Label.Size's units; 0, the game's, unless set. Read when a part's labels are made.</summary>
        [LocalSafe]
        public float TitleSize { get; set; }

        /// <summary>The height of other text (a unit's name and level, a tooltip's text and cost, a buff's time, a button's hotkey and count, a stat's row) in Label.Size's units; 0, the game's, unless set. Read when a part's labels are made.</summary>
        [LocalSafe]
        public float TextSize { get; set; }

        /// <summary>The height of the text over a bar (a StatusBar's, a CastBar's) in Label.Size's units; 0, the game's, unless set. Read when a part's labels are made.</summary>
        [LocalSafe]
        public float BarTextSize { get; set; }

        /// <summary>A window's close button: the texture of an icon button, as WoW's red cross; empty, unless set, for the game's dialog button with an X fitted inside it.</summary>
        [LocalSafe]
        public string Close { get; set; } = "";

        /// <summary>The close button's side; 0.024 unless set.</summary>
        [LocalSafe]
        public float CloseSize { get; set; } = 0.024f;

        /// <summary>How far the close button sits in from the window's top right corner, across and down; 0.014 unless set.</summary>
        [LocalSafe]
        public float CloseInset { get; set; } = 0.014f;

        /// <summary>Gives a label the theme's font and a height, while it is not made yet; a label made already keeps its own, as the game takes them only when it is made.</summary>
        internal void Style(Label label, float size)
        {
            if (label.handle == null && !label.HasEnded)
            {
                label.Size = size;
                label.Font = Font;
            }
        }
    }
}
