using System;

namespace Moonwell
{
    /// <summary>
    /// An action's button of the UI kit's HUD (decision 337): an icon button with a cooldown sweep, its hotkey's text in the
    /// top right corner and a count in the bottom right. Cooldown(seconds) darkens it whole and uncovers it clockwise from
    /// the top until the time is up, drawn from 64 dark wedge images the build writes for a map that uses the kit, as the
    /// game's own sweep model draws at a size of its own in a frame (decision 338, probe 11-14). Hotkey only shows the key:
    /// a key press is the map's. 0.04 square unless sized.
    /// </summary>
    public class ActionButton : IconButton
    {
        /// <summary>The sweep's images: the dark share left, in 64ths, from 1 to 64, as the build writes them (KitSweep).</summary>
        internal const string SweepFolder = @"MoonwellKit\Sweep";

        internal const int SweepSteps = 64;

        internal readonly Image sweep;

        internal readonly Label hotkeyLabel;

        internal readonly Label countLabel;

        internal float cooldown;

        internal float left;

        /// <summary>The 64ths of dark drawn; -1 before the first.</summary>
        internal int step = -1;

        internal int? countValue;

        internal bool ticking;

        /// <summary>Makes an action button with no icon, to be added to the screen or to another frame.</summary>
        public ActionButton()
        {
            width = 0.04f;
            height = 0.04f;
            sized = true;
            var theme = Kit.Theme;
            sweep = Add(new Image { Texture = Image.Blank });
            sweep.Fill(this);
            hotkeyLabel = Add(new Label { Alignment = FramePoint.TopRight, Color = theme.Text });
            hotkeyLabel.Fill(this);
            countLabel = Add(new Label { Alignment = FramePoint.BottomRight, Color = theme.Text });
            countLabel.Fill(this);
        }

        /// <summary>The key's text in the top right corner, as Q; empty unless set. It only shows the key.</summary>
        [LocalSafe]
        public string Hotkey
        {
            get { return hotkeyLabel.Text; }
            set { hotkeyLabel.Text = value; }
        }

        /// <summary>The number in the bottom right corner, as charges or a stack; null, the default, shows none.</summary>
        [LocalSafe]
        public int? Count
        {
            get { return countValue; }
            set
            {
                countValue = value;
                countLabel.Text = value == null ? "" : value.Value.ToString();
            }
        }

        /// <summary>The seconds of cooldown left; 0 when none runs.</summary>
        [LocalSafe]
        public float CooldownLeft => left;

        /// <summary>The image the sweep draws in, covering the button.</summary>
        [LocalSafe]
        public Image Sweep => sweep;

        /// <summary>The hotkey's text.</summary>
        [LocalSafe]
        public Label HotkeyLabel => hotkeyLabel;

        /// <summary>The count's text.</summary>
        [LocalSafe]
        public Label CountLabel => countLabel;

        /// <summary>Starts a cooldown of the seconds, the button dark at first and uncovered clockwise from the top until it is up, replacing one running; 0 ends one. The button still takes clicks meanwhile: CooldownLeft says whether it is cooling.</summary>
        public void Cooldown(float seconds)
        {
            if (HasEnded)
            {
                return;
            }

            cooldown = seconds > 0.0f ? seconds : 0.0f;
            left = cooldown;
            Paint();
            if (left > 0.0f && !ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        private bool Tick()
        {
            if (HasEnded || left <= 0.0f)
            {
                ticking = false;
                return false;
            }

            left = left > KitTicks.Interval ? left - KitTicks.Interval : 0.0f;
            Paint();
            if (left > 0.0f)
            {
                return true;
            }

            ticking = false;
            return false;
        }

        /// <summary>Draws the sweep's image for the share left, only when its 64th changes.</summary>
        private void Paint()
        {
            var now = cooldown > 0.0f ? (int)Math.Ceiling(left / cooldown * SweepSteps) : 0;
            if (now == step)
            {
                return;
            }

            step = now;
            sweep.Texture = now == 0 ? Image.Blank : SweepFolder + now + ".blp";
        }
    }
}
