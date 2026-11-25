namespace Moonwell
{
    /// <summary>
    /// A unit's frame of the UI kit's HUD (decision 334): a Portrait (the unit's icon), its name, its level, a life and a
    /// mana bar and a row of its buffs below and, below those, a Stats list the map fills, laid out in the theme's Hud border, with an Art image over the bars for a
    /// map's own art. Show(unit) fills it, and it follows the unit from then on. Every piece is a frame of its own, which a
    /// map may place, size or re-texture, frame.Add(frame.Life, x, y, width, height) placing one again. Hidden until it
    /// shows a unit. 0.2 by 0.064 unless sized.
    /// </summary>
    public class UnitFrame : Panel
    {
        internal readonly Image portrait;

        internal readonly StatusBar life;

        internal readonly StatusBar mana;

        internal readonly BuffRow buffs;

        internal readonly StatList stats;

        internal readonly Image art;

        internal readonly Label nameLabel;

        internal readonly Label levelLabel;

        internal Theme theme;

        internal Unit? unit;

        /// <summary>The level last drawn; -1 before the first.</summary>
        internal int shownLevel = -1;

        internal bool ticking;

        /// <summary>Makes a frame in Kit.Theme that shows no unit yet, to be added to the screen or to another frame.</summary>
        public UnitFrame()
        {
            theme = Kit.Theme;
            width = 0.2f;
            height = 0.064f;
            sized = true;
            Visible = false;
            portrait = KitTicks.Place(this, new Image(), 0.007f, 0.007f, 0.05f, 0.05f);
            life = KitTicks.Place(this, new StatusBar(), 0.062f, 0.022f, 0.131f, 0.017f);
            mana = KitTicks.Place(this, new StatusBar(), 0.062f, 0.041f, 0.131f, 0.016f);
            buffs = KitTicks.Place(this, new BuffRow(), 0.007f, 0.066f, 0.186f, 0.032f);
            stats = KitTicks.Place(this, new StatList(), 0.007f, 0.1f, StatList.RowWidth, StatList.RowHeight);
            art = Add(new Image());
            art.Fill(this);
            nameLabel = KitTicks.Place(this, new Label { Alignment = FramePoint.Left }, 0.062f, 0.006f, 0.098f, 0.014f);
            levelLabel = KitTicks.Place(this, new Label { Alignment = FramePoint.Right }, 0.16f, 0.006f, 0.033f, 0.014f);
            Dress();
        }

        /// <summary>The frame's look, Kit.Theme as it was made unless set, passed down to its pieces: the Hud border, the texts' colours, sizes and font, and the bars', buffs' and stats' own Theme (new UnitFrame { Theme = wow }). Set it before the frame is added, as the sizes and font are read when the labels are made.</summary>
        [LocalSafe]
        public Theme Theme
        {
            get { return theme; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                theme = value;
                Dress();
            }
        }

        private void Dress()
        {
            Template = theme.Hud;
            nameLabel.Color = theme.Title;
            levelLabel.Color = theme.Text;
            theme.Style(nameLabel, theme.TextSize);
            theme.Style(levelLabel, theme.TextSize);
            life.Theme = theme;
            mana.Theme = theme;
            buffs.Theme = theme;
            stats.Theme = theme;
        }

        /// <summary>The unit's icon; each machine draws its own, as a unit type's icon can differ by graphics mode, and its Texture reads empty.</summary>
        [LocalSafe]
        public Image Portrait => portrait;

        /// <summary>The unit's name, each machine's own (a hero's proper name), as ShowNameOf shows it, in the theme's Title colour.</summary>
        [LocalSafe]
        public Label NameLabel => nameLabel;

        /// <summary>The unit's level, in the theme's Text colour.</summary>
        [LocalSafe]
        public Label LevelLabel => levelLabel;

        /// <summary>The unit's life.</summary>
        [LocalSafe]
        public StatusBar Life => life;

        /// <summary>The unit's mana; empty with no text for a unit with none.</summary>
        [LocalSafe]
        public StatusBar Mana => mana;

        /// <summary>The unit's buffs that have an icon, below the frame.</summary>
        [LocalSafe]
        public BuffRow Buffs => buffs;

        /// <summary>The stats the map lists for the unit, in rows below the buffs: empty, and drawing nothing, until the map calls Stats.Show(unit, stat, ...). Showing another unit with the frame empties it.</summary>
        [LocalSafe]
        public StatList Stats => stats;

        /// <summary>An image over the portrait and bars and under the texts, covering the frame, for a map's own art; no texture unless set.</summary>
        [LocalSafe]
        public Image Art => art;

        /// <summary>The unit shown; null when none.</summary>
        [LocalSafe]
        public Unit? Unit => unit;

        /// <summary>Shows a unit from now on, and the frame with it: its icon, name, level, life, mana and buffs, followed until another is shown; null hides the frame and follows nothing.</summary>
        public void Show(Unit? shown)
        {
            if (HasEnded)
            {
                return;
            }

            unit = shown;
            Visible = shown != null;
            life.Follow(shown, Vital.Life);
            mana.Follow(shown, Vital.Mana);
            buffs.Show(shown);
            if (stats.unit != shown)
            {
                stats.Show(null);
            }

            shownLevel = -1;
            if (shown == null)
            {
                return;
            }

            portrait.ShowLocal("", Runtime.World.IconOf(shown.TypeId));
            nameLabel.ShowNameOf(shown);
            Read();
            if (!ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        private bool Tick()
        {
            if (HasEnded || unit == null)
            {
                ticking = false;
                return false;
            }

            Read();
            return true;
        }

        /// <summary>Draws the level when it changed; the bars and buffs read themselves.</summary>
        private void Read()
        {
            var now = unit!.IsGone ? shownLevel : unit.Level;
            if (now != shownLevel)
            {
                shownLevel = now;
                levelLabel.Text = now.ToString();
            }
        }

        /// <summary>Lays the frame out small, as a SelectionList's own entries are: 0.14 by 0.036, the icon, the name and two thin bars with no text; the level, buffs and stats hidden. A map's own entry may start from it and add a backing or textures of its own.</summary>
        public void Compact()
        {
            if (HasEnded)
            {
                return;
            }

            Width = 0.14f;
            Height = 0.036f;
            KitTicks.Place(this, portrait, 0.005f, 0.005f, 0.026f, 0.026f);
            KitTicks.Place(this, nameLabel, 0.035f, 0.004f, 0.1f, 0.01f);
            KitTicks.Place(this, life, 0.035f, 0.016f, 0.1f, 0.007f);
            KitTicks.Place(this, mana, 0.035f, 0.025f, 0.1f, 0.006f);
            life.Text = BarText.None;
            mana.Text = BarText.None;
            levelLabel.Visible = false;
            buffs.Visible = false;
            stats.Visible = false;
        }
    }
}
