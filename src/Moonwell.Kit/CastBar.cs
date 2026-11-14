namespace Moonwell
{
    /// <summary>
    /// A cast's bar of the UI kit's HUD (decision 339), driven by the map: Start(name, seconds, icon) shows it and fills it
    /// over the time in the theme's CastBar texture, then hides it; Interrupt() fills it in the theme's Interrupted texture
    /// with InterruptedText and fades it out over half a second. A Bar inside the theme's Hud border, a Label over it and
    /// an Icon covering the left end; each a frame a map may place, size or re-texture. Hidden until started. 0.2 by 0.028
    /// unless sized.
    /// </summary>
    public class CastBar : Panel
    {
        internal readonly Image icon;

        internal readonly ProgressBar bar;

        internal readonly Label label;

        internal readonly Theme theme;

        internal float seconds;

        internal float elapsed;

        internal bool casting;

        internal bool ticking;

        internal Tween? fading;

        /// <summary>Makes a hidden cast bar in Kit.Theme, to be added to the screen or to another frame.</summary>
        public CastBar()
        {
            theme = Kit.Theme;
            Template = theme.Hud;
            width = 0.2f;
            height = 0.028f;
            sized = true;
            Visible = false;
            // Inside the border's edge, 0.0065 deep in the game's control border; the icon covers the left end whole.
            bar = KitTicks.Place(this, new ProgressBar { Texture = theme.CastBar }, 0.0345f, 0.0065f, 0.159f, 0.015f);
            icon = KitTicks.Place(this, new Image(), 0.0f, 0.0f, 0.028f, 0.028f);
            label = Add(new Label { Alignment = FramePoint.Center, Color = theme.Text });
            label.Fill(bar);
        }

        /// <summary>The cast's icon at the left; hidden for a cast with none.</summary>
        [LocalSafe]
        public Image Icon => icon;

        /// <summary>The fill.</summary>
        [LocalSafe]
        public ProgressBar Bar => bar;

        /// <summary>The cast's name over the bar, or InterruptedText.</summary>
        [LocalSafe]
        public Label Label => label;

        /// <summary>What an interrupted cast's bar says: Interrupted unless set.</summary>
        [LocalSafe]
        public string InterruptedText { get; set; } = "Interrupted";

        /// <summary>Whether a cast is filling the bar.</summary>
        [LocalSafe]
        public bool Casting => casting;

        /// <summary>Shows a cast of the seconds and fills the bar over them, then hides it; a cast running or fading is replaced. An icon, as ReplaceableTextures\CommandButtons\BTNFireBolt.blp, shows at the left. Seconds of 0 or less show nothing.</summary>
        public void Start(string name, float castSeconds, string iconPath = "")
        {
            if (HasEnded)
            {
                return;
            }

            StopFading();
            if (!(castSeconds > 0.0f))
            {
                casting = false;
                Visible = false;
                return;
            }

            seconds = castSeconds;
            elapsed = 0.0f;
            casting = true;
            bar.Texture = theme.CastBar;
            bar.Max = seconds;
            bar.Value = 0.0f;
            label.Text = name;
            icon.Visible = iconPath.Length > 0;
            if (iconPath.Length > 0)
            {
                icon.Texture = iconPath;
            }

            Alpha = 255;
            Visible = true;
            if (!ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        /// <summary>Stops the cast: the bar fills in the theme's Interrupted texture, says InterruptedText and fades out over half a second. Does nothing when no cast runs.</summary>
        public void Interrupt()
        {
            if (HasEnded || !casting)
            {
                return;
            }

            casting = false;
            bar.Texture = theme.Interrupted;
            bar.Value = bar.Max;
            label.Text = InterruptedText;
            fading = FadeTo(0, 0.5f);
            fading.Finished += () =>
            {
                fading = null;
                Visible = false;
            };
        }

        private void StopFading()
        {
            fading?.Stop();
            fading = null;
        }

        private bool Tick()
        {
            if (HasEnded || !casting)
            {
                ticking = false;
                return false;
            }

            elapsed += KitTicks.Interval;
            if (elapsed < seconds)
            {
                bar.Value = elapsed;
                return true;
            }

            casting = false;
            ticking = false;
            Visible = false;
            return false;
        }
    }
}
