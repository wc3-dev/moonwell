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

        /// <summary>A hero's progress to its next level: its experience past its level's, out of what the next level needs. A unit that is no hero shows an empty bar.</summary>
        Experience,
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
    /// A bar of the UI kit's HUD (decision 336): a Backing, a Bar filled over it, an OverlayBar and a Label over both, which
    /// follows a unit's life, mana or experience by itself with Follow, read 20 times a second and redrawn only when it
    /// changed, or any stat out of another, redrawn when either changes. Overlay draws a second stat over it, as a barrier
    /// over life. Its pieces are frames of their own, covering it, which a map may place, size or re-texture (decision 334);
    /// the fill's texture is the theme's. 0.12 by 0.014 unless sized.
    /// </summary>
    public class StatusBar : Panel
    {
        internal readonly Image backing;

        internal readonly ProgressBar bar;

        internal readonly ProgressBar overlay;

        internal readonly Label label;

        internal Theme theme;

        internal Unit? unit;

        internal Vital vital;

        /// <summary>The stat followed, and what it is out of: another stat, or outOfNumber; null while a vital is followed.</summary>
        internal Stat? stat, outOfStat;

        internal float outOfNumber;

        /// <summary>The stat drawn as the second layer; null for none.</summary>
        internal Stat? overlayStat;

        /// <summary>The texture the bar last gave the fill itself: while the fill still has it, Follow and Theme give the fill the theme's; once the map set another, the map's is kept (J64).</summary>
        internal string look = "";

        /// <summary>The theme textures the bar last gave its backing and its second layer, kept on the same terms as look.</summary>
        internal string backingLook = "", overlayLook = "";

        /// <summary>Whether StatWatch tells the bar of the stats' changes.</summary>
        internal bool watching;

        internal BarText text = BarText.Values;

        /// <summary>The value and most last drawn; -1 before the first.</summary>
        internal float shownValue = -1.0f, shownMax = -1.0f;

        /// <summary>The overlay's value and the most it was drawn out of; -1 before the first.</summary>
        internal float shownOverlay = -1.0f, shownOverlayMax = -1.0f;

        internal bool ticking;

        /// <summary>Makes a bar that follows nothing yet, in Kit.Theme, to be added to the screen or to another frame.</summary>
        public StatusBar()
        {
            Template = "";
            width = 0.12f;
            height = 0.014f;
            sized = true;
            theme = Kit.Theme;
            backing = Add(new Image { Alpha = 160 });
            backing.Fill(this);
            bar = Add(new ProgressBar());
            bar.Fill(this);
            overlay = Add(new ProgressBar { Visible = false });
            overlay.Fill(this);
            label = Add(new Label { Alignment = FramePoint.Center });
            label.Fill(this);
            Dress();
        }

        /// <summary>The bar's look, Kit.Theme as it was made unless set: the backing's, the fill's and the second layer's textures (a texture the map set on one is kept), and the text's colour, size and font, which are read when the label is made, so set it before the bar is added.</summary>
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

        /// <summary>Gives the pieces the theme's looks, each texture only while the piece still has the one the bar gave it.</summary>
        private void Dress()
        {
            if (backing.Texture == backingLook)
            {
                backingLook = theme.Backing;
                backing.Texture = backingLook;
            }

            Fill(Wanted());
            if (overlay.Texture == overlayLook)
            {
                overlayLook = theme.OverlayBar;
                overlay.Texture = overlayLook;
                overlay.Alpha = theme.OverlayAlpha;
            }

            label.Color = theme.Text;
            theme.Style(label, theme.BarTextSize);
        }

        /// <summary>The theme's texture for what the bar follows.</summary>
        private string Wanted()
        {
            return stat != null ? theme.StatBar : vital == Vital.Mana ? theme.ManaBar : vital == Vital.Experience ? theme.ExperienceBar : theme.LifeBar;
        }

        /// <summary>Gives the fill a texture of the theme's, unless the map gave it one of its own.</summary>
        private void Fill(string wanted)
        {
            if (bar.Texture == look)
            {
                look = wanted;
                bar.Texture = wanted;
            }
        }

        /// <summary>The image under the fill: the theme's Backing, seen through.</summary>
        [LocalSafe]
        public Image Backing => backing;

        /// <summary>The fill: the theme's LifeBar, ManaBar, ExperienceBar or StatBar, as Follow sets; a texture the map sets, before or after Follow, is kept.</summary>
        [LocalSafe]
        public ProgressBar Bar => bar;

        /// <summary>The second layer's fill, over Bar and under the text: the theme's OverlayBar at its OverlayAlpha, shown while Overlay has a stat. A map may restyle it.</summary>
        [LocalSafe]
        public ProgressBar OverlayBar => overlay;

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

        /// <summary>Follows a unit's life, mana or experience from now on, giving the fill the theme's texture for it unless the map gave it its own; null follows nothing and empties the bar. Read 20 times a second. A unit gone or dead reads 0.</summary>
        public void Follow(Unit? followed, Vital what = Vital.Life)
        {
            Take(followed, what, null, null, 0.0f);
        }

        /// <summary>Follows a unit's stat out of another of its stats from now on, as an energy bar does: Follow(hero, Energy, MaxEnergy). The fill takes the theme's StatBar texture, and the text shows the stat out of the other as it does for life. It is redrawn when either stat changes on the unit, not by the 20-a-second poll. Null follows nothing and empties the bar; a unit gone reads 0.</summary>
        public void Follow(Unit? followed, Stat followedStat, Stat outOf)
        {
            Take(followed, Vital.Life, followedStat, outOf, 0.0f);
        }

        /// <summary>Follows a unit's stat out of a number from now on, as Follow(unit, stat, outOf) does with a stat.</summary>
        public void Follow(Unit? followed, Stat followedStat, float outOf)
        {
            Take(followed, Vital.Life, followedStat, null, outOf);
        }

        private void Take(Unit? followed, Vital what, Stat? followedStat, Stat? outOf, float outOfValue)
        {
            if (HasEnded)
            {
                return;
            }

            unit = followed;
            vital = what;
            stat = followedStat;
            Fill(Wanted());
            outOfStat = outOf;
            outOfNumber = outOfValue;
            Read();
            ReadOverlay();
            if (followed == null)
            {
                return;
            }

            if (followedStat != null || overlayStat != null)
            {
                Watch();
            }

            if (followedStat == null && !ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        /// <summary>Draws a second layer over the bar for another stat of the followed unit, as a share of the bar's own most (a barrier of 200 on a life bar of 800 fills a quarter), held to the bar's width and redrawn when that stat changes; null takes it off. It works on a life bar and on a stat bar.</summary>
        public void Overlay(Stat? overlayWith)
        {
            if (HasEnded)
            {
                return;
            }

            overlayStat = overlayWith;
            overlay.Visible = overlayWith != null;
            ReadOverlay();
            if (overlayWith != null && unit != null)
            {
                Watch();
            }
        }

        /// <summary>Has StatWatch tell the bar of the stats' changes, once.</summary>
        private void Watch()
        {
            if (!watching)
            {
                watching = true;
                StatWatch.Join(Changed);
            }
        }

        /// <summary>Draws again what a change of a stat of a unit touches; false once the bar follows and overlays no stat, or has ended.</summary>
        private bool Changed(Unit changed, Stat which)
        {
            if (HasEnded || (stat == null && overlayStat == null))
            {
                watching = false;
                return false;
            }

            if (changed == unit)
            {
                if (which == stat || which == outOfStat)
                {
                    Read();
                }

                if (which == overlayStat)
                {
                    ReadOverlay();
                }
            }

            return true;
        }

        private bool Tick()
        {
            // A stat followed is redrawn by its changes, not by the poll.
            if (HasEnded || unit == null || stat != null)
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
                if (stat != null)
                {
                    value = unit.Get(stat);
                    max = outOfStat != null ? unit.Get(outOfStat) : outOfNumber;
                }
                else if (vital == Vital.Experience)
                {
                    ExperienceOf(unit, out value, out max);
                }
                else
                {
                    value = vital == Vital.Mana ? unit.Mana : unit.Life;
                    max = vital == Vital.Mana ? unit.MaxMana : unit.MaxLife;
                }
            }

            Draw(value, max);
        }

        /// <summary>A hero's experience past its level's out of what its next level needs, which fills the bar as Hero.LevelProgress does and reads as the experience in the Values text; the last step full at the top level. Empty for a unit that is no hero.</summary>
        private static void ExperienceOf(Unit followed, out float value, out float max)
        {
            value = 0.0f;
            max = 0.0f;
            if (followed is Hero hero)
            {
                var table = HeroTable.Experience;
                var level = hero.Level;
                var top = level >= table.Length;
                var next = top ? table.Length - 1 : level;
                if (level >= 1 && next >= 1)
                {
                    max = table[next] - table[next - 1];
                    var past = top ? max : hero.Experience - table[level - 1];
                    value = past < 0.0f ? 0.0f : past > max ? max : past;
                }
            }
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
            ReadOverlay();
        }

        /// <summary>Reads the overlay's stat and draws it as a share of the bar's most.</summary>
        internal void ReadOverlay()
        {
            if (HasEnded)
            {
                return;
            }

            var value = 0.0f;
            if (overlayStat != null && unit != null && !unit.IsGone)
            {
                value = unit.Get(overlayStat);
            }

            // Held to the bar's most, so it never draws wider than the bar.
            var max = shownMax > 0.0f ? shownMax : 0.0f;
            value = max <= 0.0f || value < 0.0f ? 0.0f : value > max ? max : value;
            if (value == shownOverlay && max == shownOverlayMax)
            {
                return;
            }

            shownOverlay = value;
            shownOverlayMax = max;
            overlay.Max = max > 0.0f ? max : 1.0f;
            overlay.Value = value;
        }
    }
}
