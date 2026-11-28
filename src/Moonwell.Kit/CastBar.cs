namespace Moonwell
{
    /// <summary>
    /// A cast's bar of the UI kit's HUD (decision 339). Follow(unit) shows the unit's casts as the game runs them: a spell
    /// with a casting time fills it, a channel drains it, and each hides it when the spell runs to its end, or interrupts it
    /// when the spell is stopped. The map can drive it too: Start(name, seconds, icon) shows it and fills it over the time
    /// in the theme's CastBar texture, then hides it; Channel(name, seconds, icon) drains it the same way; Stop() hides it
    /// at once; Interrupt() fills it in the theme's Interrupted texture with InterruptedText and fades it out over half a
    /// second. A Bar inside the theme's Hud border, a Label over it and an Icon covering the left end; each a frame a map
    /// may place, size or re-texture. Hidden until started. 0.2 by 0.028 unless sized.
    /// </summary>
    public class CastBar : Panel
    {
        internal readonly Image icon;

        internal readonly ProgressBar bar;

        internal readonly Label label;

        internal Theme theme;

        internal float seconds;

        internal float elapsed;

        internal bool casting;

        /// <summary>Whether the bar drains, for a channel.</summary>
        internal bool channeling;

        /// <summary>Whether the bar waits, full or empty, for the followed cast's end, which hides it or interrupts it, rather than hiding when its time is up.</summary>
        internal bool holding;

        internal bool ticking;

        internal Tween? fading;

        /// <summary>The followed unit's watch; null when the bar follows none.</summary>
        internal CastWatch? watch;

        /// <summary>The followed cast on the bar; null when the bar shows none.</summary>
        internal Cast? shown;

        /// <summary>Makes a hidden cast bar in Kit.Theme, to be added to the screen or to another frame.</summary>
        public CastBar()
        {
            theme = Kit.Theme;
            width = 0.2f;
            height = 0.028f;
            sized = true;
            Visible = false;
            // Laid by Dress, by the theme's border; the icon covers the left end whole.
            bar = Add(new ProgressBar());
            icon = KitTicks.Place(this, new Image(), 0.0f, 0.0f, 0.028f, 0.028f);
            label = Add(new Label { Alignment = FramePoint.Center });
            label.Fill(bar);
            Dress();
        }

        /// <summary>The bar's look, Kit.Theme as it was made unless set: the Hud border, the bar laid inside the border, or against the icon with none, the CastBar and Interrupted fills (each cast starts in the theme's), and the text's colour, size and font. Set it before the bar is added, as the size and font are read when the label is made.</summary>
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

        /// <summary>Gives the pieces the theme, and lays the bar from the icon's right edge, as the icon covers the border's left end and a gap there shows (Sven's gate runs, 11-25): inside the border's other edges, 0.0065 deep in the game's control border, or, with no border (Hud ""), across the rest.</summary>
        private void Dress()
        {
            Template = theme.Hud;
            if (theme.Hud.Length > 0)
            {
                KitTicks.Place(this, bar, 0.028f, 0.0065f, 0.1655f, 0.015f);
            }
            else
            {
                KitTicks.Place(this, bar, 0.028f, 0.0065f, 0.172f, 0.015f);
            }

            bar.Texture = casting || fading == null ? theme.CastBar : theme.Interrupted;
            label.Color = theme.Text;
            theme.Style(label, theme.BarTextSize);
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

        /// <summary>Whether a cast or a channel is on the bar.</summary>
        [LocalSafe]
        public bool Casting => casting;

        /// <summary>Whether a channel is on the bar, draining it.</summary>
        [LocalSafe]
        public bool Channeling => casting && channeling;

        /// <summary>The unit whose casts the bar follows; null when it follows none.</summary>
        [LocalSafe]
        public Unit? Followed => watch?.Unit;

        /// <summary>
        /// Shows the unit's casts from now on, as the game runs them, in place of the unit followed before; null follows none.
        /// From the spell's effect, a casting time (cast.CastSeconds, as Flame Strike's) fills the bar, and a channel's follow
        /// through time (cast.ChannelSeconds, a spell based on Channel) drains it; a spell with both shows its casting time,
        /// and one with neither shows nothing. The name and icon are the game's, each machine its own (label.ShowNameOf), as
        /// both differ by machine. The bar then waits, full or empty, for the spell's end: a spell that ran to its end hides
        /// it, one stopped early, by a stun, an order, a death or the unit's next cast, interrupts it. A followed cast on the
        /// bar is hidden when the bar follows another unit or none; Start, Channel and Stop take the bar over from it.
        /// </summary>
        public void Follow(Unit? unit)
        {
            if (HasEnded || (unit != null && unit == Followed))
            {
                return;
            }

            watch?.End();
            watch = null;
            if (shown != null)
            {
                Stop();
            }

            if (unit != null && !unit.IsGone)
            {
                watch = unit.Add(new CastWatch(this), this);
            }
        }

        /// <summary>Shows a cast of the seconds and fills the bar over them, then hides it; a cast running or fading is replaced. An icon, as ReplaceableTextures\CommandButtons\BTNFireBolt.blp, shows at the left. Seconds of 0 or less show nothing.</summary>
        public void Start(string name, float castSeconds, string iconPath = "")
        {
            Show(name, castSeconds, iconPath, false);
        }

        /// <summary>Shows a channel of the seconds: the bar starts full and drains over them, then hides; a cast running or fading is replaced. An icon shows at the left, as for Start. Seconds of 0 or less show nothing.</summary>
        public void Channel(string name, float channelSeconds, string iconPath = "")
        {
            Show(name, channelSeconds, iconPath, true);
        }

        /// <summary>Hides the bar at once, with no interrupted look: a cast or channel on it ends, and the fade of an interrupted one stops.</summary>
        public void Stop()
        {
            if (HasEnded)
            {
                return;
            }

            StopFading();
            shown = null;
            casting = false;
            holding = false;
            Visible = false;
        }

        /// <summary>Stops the cast or channel: the bar fills in the theme's Interrupted texture, says InterruptedText and fades out over half a second. Does nothing when none is on the bar.</summary>
        public void Interrupt()
        {
            if (HasEnded || !casting)
            {
                return;
            }

            shown = null;
            casting = false;
            holding = false;
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

        /// <summary>Shows a followed cast: its casting time filling, else its channel draining; nothing, and the bar as it was, for a spell with neither.</summary>
        internal void Show(Cast cast)
        {
            if (HasEnded)
            {
                return;
            }

            var castSeconds = cast.CastSeconds;
            var drains = !(castSeconds > 0.0f);
            var showSeconds = drains ? cast.ChannelSeconds : castSeconds;
            if (!(showSeconds > 0.0f))
            {
                return;
            }

            Begin(showSeconds, drains, true);
            shown = cast;
            label.ShowNameOf(cast.Ability);
            icon.Visible = true;
            icon.ShowLocal("", Runtime.World.IconOf(cast.Ability.Id));
        }

        /// <summary>A followed cast ended: when it is the one on the bar, the bar hides if it ran to its end, else it interrupts.</summary>
        internal void Ended(Cast cast)
        {
            if (HasEnded || cast != shown)
            {
                return;
            }

            shown = null;
            if (cast.RanToEnd)
            {
                Stop();
            }
            else
            {
                Interrupt();
            }
        }

        private void Show(string name, float showSeconds, string iconPath, bool drains)
        {
            if (HasEnded)
            {
                return;
            }

            shown = null;
            if (!Begin(showSeconds, drains, false))
            {
                return;
            }

            label.Text = name;
            icon.Visible = iconPath.Length > 0;
            if (iconPath.Length > 0)
            {
                icon.Texture = iconPath;
            }
        }

        /// <summary>Starts the bar over the seconds, filling or draining, and answers whether it shows: seconds of 0 or less hide it.</summary>
        private bool Begin(float showSeconds, bool drains, bool holds)
        {
            StopFading();
            if (!(showSeconds > 0.0f))
            {
                casting = false;
                holding = false;
                Visible = false;
                return false;
            }

            seconds = showSeconds;
            elapsed = 0.0f;
            casting = true;
            channeling = drains;
            holding = holds;
            bar.Texture = theme.CastBar;
            bar.Max = seconds;
            bar.Value = drains ? seconds : 0.0f;
            Alpha = 255;
            Visible = true;
            if (!ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }

            return true;
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
                bar.Value = channeling ? seconds - elapsed : elapsed;
                return true;
            }

            ticking = false;
            if (holding)
            {
                // The followed cast's end hides the bar or interrupts it.
                bar.Value = channeling ? 0.0f : seconds;
                return false;
            }

            casting = false;
            Visible = false;
            return false;
        }
    }

    /// <summary>A followed unit's component that hands each of its casts, as it takes effect, to its cast bar, and the cast's end after.</summary>
    internal sealed class CastWatch : UnitComponent
    {
        private readonly CastBar bar;

        internal CastWatch(CastBar bar)
        {
            this.bar = bar;
        }

        /// <summary>Shows the cast on the bar, and hears its end when it shows.</summary>
        public override void OnCast(Cast cast)
        {
            if (HasEnded)
            {
                return;
            }

            bar.Show(cast);
            if (bar.shown == cast)
            {
                cast.Ended += bar.Ended;
            }
        }
    }
}
