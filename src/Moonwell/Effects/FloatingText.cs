namespace Moonwell
{
    /// <summary>
    /// Floating text: words the game draws at a place or over a unit, as damage numbers, gold and labels. Pop and a unit's
    /// PopText play one once: it rises, fades and goes by itself. Create and a unit's AddText make one in a scope, which
    /// ends it when it ends, as End and its Duration do. Moonwell times and fades every text itself, on the timers' clock,
    /// and never lets the game free one: the game gives a freed text's id to the next one made (seen 11-06). Text on a unit
    /// shows only to the players who see the unit. An Effect moves its colours by itself. Given shownTo, a text is made on
    /// that player's machine only, as a text takes no handle id (seen 11-06). Every read gives what was set, the same on
    /// every machine.
    /// </summary>
    public sealed class FloatingText : Scoped
    {
        /// <summary>Blizzard.j's TextTagSize2Height: World Editor's font size to the game's height.</summary>
        private const float SizeToHeight = 0.023f / 10.0f;

        /// <summary>Blizzard.j's TextTagSpeed2Velocity: a speed a second to the game's velocity (seen 11-06 to match).</summary>
        private const float SpeedToVelocity = 0.071f / 128.0f;

        private const float RadiansPerDegree = 0.017453292f;

        private readonly Unit? carrier;

        private readonly bool follows;

        private readonly Player? shownTo;

        private readonly float made;

        private texttag? handle;

        private string words;

        private float x;

        private float y;

        private float height;

        private float size = 10.0f;

        private float speed;

        private float angle = 90.0f;

        private float duration;

        private float fadeOut;

        private Color color = Color.White;

        private TextEffect? effect;

        /// <summary>When the Effect was set, which it moves from.</summary>
        private float effectSince;

        /// <summary>Whether it is in TextEffect's redraws.</summary>
        private bool redrawing;

        /// <summary>The alpha last given to the game, which the fade lowers; -1 until it is first coloured.</summary>
        private int shownAlpha = -1;

        /// <summary>Whether the game was last told to show it, on a unit; it shows unless the unit is out of sight.</summary>
        private bool visible = true;

        /// <summary>Whether it is in the list of texts that step.</summary>
        internal bool stepping;

        private FloatingText(string words, float x, float y, Unit? carrier, bool follows, Player? shownTo)
        {
            this.words = words ?? "";
            this.x = x;
            this.y = y;
            this.carrier = carrier;
            this.follows = follows;
            this.shownTo = shownTo;
            made = Runtime.World.Now();
        }

        /// <summary>The words shown.</summary>
        [LocalSafe]
        public string Text
        {
            get
            {
                return words;
            }

            set
            {
                if (!HasEnded)
                {
                    words = value ?? "";
                    Write();
                }
            }
        }

        /// <summary>The size of the letters, as World Editor's font size; 10 unless set.</summary>
        [LocalSafe]
        public float Size
        {
            get
            {
                return size;
            }

            set
            {
                if (!HasEnded)
                {
                    size = value;
                    Write();
                }
            }
        }

        /// <summary>The colour of the letters; its alpha is their opacity, which a fade lowers from. White unless set.</summary>
        [LocalSafe]
        public Color Color
        {
            get
            {
                return color;
            }

            set
            {
                if (!HasEnded)
                {
                    color = value;
                    Paint(true);
                }
            }
        }

        /// <summary>A colour effect Moonwell moves by itself, a rainbow turning or a sweep passing, from when it is set; null, the default, for none. Text reads the words as set, and Color's red, green and blue are not drawn while it runs; its alpha is.</summary>
        [LocalSafe]
        public TextEffect? Effect
        {
            get
            {
                return effect;
            }

            set
            {
                if (HasEnded)
                {
                    return;
                }

                effect = value;
                effectSince = Runtime.World.Now();
                Write();
                Paint(true);
                if (effect != null && !redrawing)
                {
                    redrawing = true;
                    TextEffect.Join(Redraw);
                }
            }
        }

        /// <summary>How high the text is drawn: above the ground at a place, above the health bar on a unit; 0 unless set. A text that moves keeps it.</summary>
        [LocalSafe]
        public float Height
        {
            get
            {
                return height;
            }

            set
            {
                if (!HasEnded)
                {
                    height = value;
                    Place();
                }
            }
        }

        /// <summary>How fast the text moves by itself, a second; 0 unless set (a Pop's 64). A text that follows a unit does not move from it.</summary>
        [LocalSafe]
        public float Speed
        {
            get
            {
                return speed;
            }

            set
            {
                if (!HasEnded)
                {
                    speed = value;
                    Drift();
                }
            }
        }

        /// <summary>Which way the text moves, in degrees as a unit's Facing: 90, the default, is up the screen.</summary>
        [LocalSafe]
        public float Angle
        {
            get
            {
                return angle;
            }

            set
            {
                if (!HasEnded)
                {
                    angle = value;
                    Drift();
                }
            }
        }

        /// <summary>The seconds from when the text was made to when it ends by itself; 0, the default, lasts until it is ended (a Pop's 2). One set past its age ends it at the next step.</summary>
        [LocalSafe]
        public float Duration
        {
            get
            {
                return duration;
            }

            set
            {
                if (!HasEnded)
                {
                    duration = value < 0.0f ? 0.0f : value;
                    Join();
                }
            }
        }

        /// <summary>The seconds before its Duration's end over which the text fades out; 0 unless set (a Pop's 1). Nothing fades with no Duration.</summary>
        [LocalSafe]
        public float FadeOut
        {
            get
            {
                return fadeOut;
            }

            set
            {
                if (!HasEnded)
                {
                    fadeOut = value < 0.0f ? 0.0f : value;
                    Join();
                }
            }
        }

        /// <summary>The unit the text is on; null for one at a place.</summary>
        [LocalSafe]
        public Unit? Unit => carrier;

        /// <summary>Whether a player is meant to see the text: the one it was shown to, or everyone. The same on every machine; a text on a unit is also hidden while the player does not see the unit.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Makes a text at a place, its height above the ground there, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static FloatingText Create(string text, float x, float y, Scope scope, Player? shownTo = null)
        {
            return Make(new FloatingText(text, x, y, null, false, shownTo), scope);
        }

        /// <summary>Plays a text once at a place: it rises 64 a second, lasts 2 s and fades over the last 1. Given shownTo, only that player sees it.</summary>
        public static void Pop(string text, float x, float y, Color? color = null, Player? shownTo = null)
        {
            Popped(Make(new FloatingText(text, x, y, null, false, shownTo), Game.Instance), color);
        }

        /// <summary>Moves the text to a place, keeping its height above the ground there. Does nothing on a unit, and once it has ended.</summary>
        public void PlaceAt(float x, float y)
        {
            if (HasEnded || carrier != null)
            {
                return;
            }

            this.x = x;
            this.y = y;
            Place();
        }

        /// <summary>Makes a text on a unit, held in a scope, which follows the unit; on a unit that is gone it ends at once.</summary>
        internal static FloatingText On(Unit unit, string text, Scope scope, Player? shownTo)
        {
            var label = new FloatingText(text, 0.0f, 0.0f, unit, true, shownTo);
            if (unit.IsGone)
            {
                scope.Adopt(label);
                label.End();
                return label;
            }

            Make(label, scope);
            if (!label.HasEnded)
            {
                unit.Attach(label);
            }

            return label;
        }

        /// <summary>Plays a text once over a unit, where it starts and rises from; nothing on a unit that is gone.</summary>
        internal static void PopOn(Unit unit, string text, Color? color, Player? shownTo)
        {
            if (!unit.IsGone)
            {
                Popped(Make(new FloatingText(text, 0.0f, 0.0f, unit, false, shownTo), Game.Instance), color);
            }
        }

        /// <summary>A step: ends the text at its Duration, fades it, and keeps one that follows a unit over it, shown as the unit is seen. Answers whether it still needs steps.</summary>
        internal bool Step()
        {
            if (HasEnded)
            {
                return false;
            }

            var age = Runtime.World.Now() - made;
            if (duration > 0.0f && age >= duration)
            {
                End();
                return false;
            }

            if (handle != null)
            {
                Paint(false);
                if (follows)
                {
                    Place();
                }
            }

            return follows || duration > 0.0f;
        }

        private static FloatingText Make(FloatingText text, Scope scope)
        {
            scope.Adopt(text);
            if (text.HasEnded)
            {
                return text;
            }

            if (text.shownTo == null || Runtime.World.IsLocal(text.shownTo.Handle))
            {
                text.handle = Runtime.World.MakeText();
                text.Write();
                text.Paint(true);
                text.Place();
            }

            text.Join();
            return text;
        }

        private static void Popped(FloatingText text, Color? color)
        {
            if (color != null)
            {
                text.Color = color.Value;
            }

            text.Speed = 64.0f;
            text.FadeOut = 1.0f;
            text.Duration = 2.0f;
        }

        /// <summary>Joins the texts that step, when it follows a unit or has a Duration.</summary>
        private void Join()
        {
            if (!stepping && !HasEnded && (follows || duration > 0.0f))
            {
                FloatingTexts.Join(this);
            }
        }

        private void Write()
        {
            if (handle != null)
            {
                Runtime.World.WriteText(handle, effect == null ? words : effect.Paint(words, Runtime.World.Now() - effectSince), size * SizeToHeight);
            }
        }

        /// <summary>A redraw of the Effect; answers false, and leaves the redraws, once it has ended or has none.</summary>
        private bool Redraw()
        {
            if (HasEnded || effect == null)
            {
                redrawing = false;
                return false;
            }

            Write();
            return true;
        }

        /// <summary>Gives the game the colour, its alpha lowered by the fade; only a change of alpha unless forced.</summary>
        private void Paint(bool force)
        {
            if (handle == null)
            {
                return;
            }

            var alpha = color.A;
            if (duration > 0.0f && fadeOut > 0.0f)
            {
                var left = duration - (Runtime.World.Now() - made);
                if (left < fadeOut)
                {
                    alpha = (int)(color.A * (left > 0.0f ? left : 0.0f) / fadeOut);
                }
            }

            if (force || alpha != shownAlpha)
            {
                shownAlpha = alpha;
                var shown = effect == null ? color : Color.White;
                Runtime.World.ColorText(handle, shown.R, shown.G, shown.B, alpha);
            }
        }

        /// <summary>Gives the game the place, over the unit or at the place, and on a unit shows it only where the unit is seen.</summary>
        private void Place()
        {
            if (handle == null)
            {
                return;
            }

            if (carrier == null)
            {
                Runtime.World.PlaceText(handle, x, y, height);
                return;
            }

            if (!carrier.IsGone)
            {
                Runtime.World.PlaceTextOver(handle, carrier.Handle, height);
                var sees = Runtime.World.LocalSees(carrier.Handle);
                if (sees != visible)
                {
                    visible = sees;
                    Runtime.World.ShowText(handle, sees);
                }
            }
        }

        private void Drift()
        {
            if (handle != null && !follows)
            {
                var velocity = speed * SpeedToVelocity;
                Runtime.World.DriftText(handle, velocity * System.MathF.Cos(angle * RadiansPerDegree), velocity * System.MathF.Sin(angle * RadiansPerDegree));
            }
        }

        /// <summary>Ends the game's text and takes it off its unit.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndText(handle);
                handle = null;
            }

            if (carrier != null)
            {
                carrier.Detach(this);
            }
        }
    }
}
