using System;

namespace Moonwell
{
    /// <summary>What a StatusBar follows of a unit (Stat is the stats' class).</summary>
    public enum Vital
    {
        /// <summary>Its life, out of its most.</summary>
        Life,

        /// <summary>Its mana, out of its most.</summary>
        Mana,
    }

    /// <summary>What a StatusBar's text shows.</summary>
    public enum BarText
    {
        /// <summary>The value out of the most, as 123 / 450.</summary>
        Values,

        /// <summary>The share, as 27%.</summary>
        Percent,

        /// <summary>No text.</summary>
        None,
    }

    /// <summary>
    /// A bar of the UI kit's HUD (decision 336): a Backing, a Bar filled over it and a Label over both, which follows a
    /// unit's life or mana by itself with Follow, read 20 times a second and redrawn only when it changed. Its pieces are
    /// frames of their own, covering it, which a map may place, size or re-texture (decision 334); the fill's texture is
    /// the theme's. 0.12 by 0.014 unless sized.
    /// </summary>
    public class StatusBar : Panel
    {
        internal readonly Image backing;

        internal readonly ProgressBar bar;

        internal readonly Label label;

        internal readonly Theme theme;

        internal Unit? unit;

        internal Vital vital;

        internal BarText text = BarText.Values;

        /// <summary>The value and most last drawn; -1 before the first.</summary>
        internal float shownValue = -1.0f, shownMax = -1.0f;

        internal bool ticking;

        /// <summary>Makes a bar that follows nothing yet, in Kit.Theme, to be added to the screen or to another frame.</summary>
        public StatusBar()
        {
            Template = "";
            width = 0.12f;
            height = 0.014f;
            sized = true;
            theme = Kit.Theme;
            backing = Add(new Image { Texture = theme.Backing, Alpha = 160 });
            backing.Fill(this);
            bar = Add(new ProgressBar { Texture = theme.LifeBar });
            bar.Fill(this);
            label = Add(new Label { Alignment = FramePoint.Center, Color = theme.Text });
            label.Fill(this);
        }

        /// <summary>The image under the fill: the theme's Backing, seen through.</summary>
        [LocalSafe]
        public Image Backing => backing;

        /// <summary>The fill: the theme's LifeBar or ManaBar, as Follow sets; a texture set after Follow is kept.</summary>
        [LocalSafe]
        public ProgressBar Bar => bar;

        /// <summary>The text over the bar, as Text says.</summary>
        [LocalSafe]
        public Label Label => label;

        /// <summary>The unit followed; null when none.</summary>
        [LocalSafe]
        public Unit? Unit => unit;

        /// <summary>What the text shows: the value out of the most (Values) unless set.</summary>
        [LocalSafe]
        public BarText Text
        {
            get { return text; }
            set
            {
                text = value;
                shownMax = -1.0f;
                Read();
            }
        }

        /// <summary>Follows a unit's life or mana from now on, giving the fill the theme's texture for it; null follows nothing and empties the bar. A unit gone or dead reads 0.</summary>
        public void Follow(Unit? followed, Vital what = Vital.Life)
        {
            if (HasEnded)
            {
                return;
            }

            if (unit == null || what != vital)
            {
                bar.Texture = what == Vital.Mana ? theme.ManaBar : theme.LifeBar;
            }

            unit = followed;
            vital = what;
            Read();
            if (followed != null && !ticking)
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

        /// <summary>Reads the unit and draws what changed.</summary>
        internal void Read()
        {
            if (HasEnded)
            {
                return;
            }

            var value = 0.0f;
            var max = 0.0f;
            if (unit != null && !unit.IsGone)
            {
                value = vital == Vital.Mana ? unit.Mana : unit.Life;
                max = vital == Vital.Mana ? unit.MaxMana : unit.MaxLife;
            }

            Draw(value, max);
        }

        /// <summary>Draws a value out of a most, when either changed.</summary>
        internal void Draw(float value, float max)
        {
            if (value == shownValue && max == shownMax)
            {
                return;
            }

            shownValue = value;
            shownMax = max;
            bar.Max = max > 0.0f ? max : 1.0f;
            bar.Value = max > 0.0f ? value : 0.0f;
            label.Text = max <= 0.0f || text == BarText.None ? "" : text == BarText.Percent ? (int)Math.Ceiling(value / max * 100.0f) + "%" : (int)Math.Ceiling(value) + " / " + (int)Math.Round(max);
        }
    }
}
