namespace Moonwell
{
    /// <summary>
    /// A special effect: a model the game draws at a point, or on a unit, which it follows. It is in the scope it was made
    /// in and ends when that scope ends, or by End, with its death animation unless PlaysDeath is false; one on a unit
    /// also ends when the unit ends. Every read gives what was set, the same on every machine.
    /// </summary>
    public sealed class SpecialEffect : Scoped
    {
        private const float RadiansPerDegree = 0.017453292f;

        private readonly string model;

        private readonly Unit? carrier;

        private readonly string point = "";

        private readonly Player? shownTo;

        private effect? handle;

        private float x;

        private float y;

        private float height;

        private float facing;

        private float pitch;

        private float roll;

        private float scale = 1.0f;

        private float speed = 1.0f;

        private Color tint = Color.White;

        private Player? teamColor;

        private bool playsDeath = true;

        private SpecialEffect(string model, float x, float y, Player? shownTo)
        {
            this.model = model;
            this.x = x;
            this.y = y;
            this.shownTo = shownTo;
        }

        private SpecialEffect(string model, Unit carrier, string point, Player? shownTo)
        {
            this.model = model;
            this.carrier = carrier;
            this.point = point;
            this.shownTo = shownTo;
        }

        /// <summary>The unit the effect is on; null for one at a point.</summary>
        [LocalSafe]
        public Unit? Unit => carrier;

        /// <summary>The attachment point the effect is on, as it was given; empty for one at a point.</summary>
        [LocalSafe]
        public string AttachPoint => point;

        /// <summary>The model's path, as it was made.</summary>
        [LocalSafe]
        public string Model => model;

        /// <summary>The game's effect, for anything Moonwell does not do; null for an effect that ended before the game made it. The game's effect is gone once this one has ended.</summary>
        [LocalSafe]
        public effect? Handle => handle;

        /// <summary>The effect's left-to-right place. Setting it moves the effect, keeping its height above the ground there. On a unit, the unit's, and setting does nothing.</summary>
        [LocalSafe]
        public float X
        {
            get { return carrier != null ? carrier.X : x; }
            set { PlaceAt(value, y); }
        }

        /// <summary>The effect's bottom-to-top place. Setting it moves the effect, keeping its height above the ground there. On a unit, the unit's, and setting does nothing.</summary>
        [LocalSafe]
        public float Y
        {
            get { return carrier != null ? carrier.Y : y; }
            set { PlaceAt(x, value); }
        }

        /// <summary>How far above the ground the effect is drawn; 0 unless set. It is kept as the effect moves: the ground is read under each new place, on each machine, which only places the model, so this reads what was set. On a unit, 0, and setting does nothing.</summary>
        [LocalSafe]
        public float Height
        {
            get { return height; }
            set
            {
                if (handle == null || HasEnded || carrier != null)
                {
                    return;
                }

                height = value;
                Move();
            }
        }

        /// <summary>Which way the effect faces, in degrees as a unit's Facing: 0 east, 90 north, read from 0 to 360 (400 reads 40); 0 unless set.</summary>
        [LocalSafe]
        public float Facing
        {
            get { return facing; }
            set { Turn(value, pitch, roll); }
        }

        /// <summary>How far the effect's nose is turned down, in degrees, read as set; 0 unless set.</summary>
        [LocalSafe]
        public float Pitch
        {
            get { return pitch; }
            set { Turn(facing, value, roll); }
        }

        /// <summary>How far the effect is rolled about its length, in degrees, read as set; 0 unless set.</summary>
        [LocalSafe]
        public float Roll
        {
            get { return roll; }
            set { Turn(facing, pitch, value); }
        }

        /// <summary>The size of the effect's model as a factor of its own; 1 unless set.</summary>
        [LocalSafe]
        public float Scale
        {
            get { return scale; }
            set
            {
                if (handle == null || HasEnded)
                {
                    return;
                }

                scale = value;
                Runtime.World.ScaleEffect(handle, value);
            }
        }

        /// <summary>The colour the effect's model is tinted with, its alpha the model's opacity; white and opaque unless set.</summary>
        [LocalSafe]
        public Color Tint
        {
            get { return tint; }
            set
            {
                if (handle == null || HasEnded)
                {
                    return;
                }

                tint = value;
                Runtime.World.TintEffect(handle, value.R, value.G, value.B, value.A);
            }
        }

        /// <summary>The player whose colour the model's team-coloured parts are drawn in (BlzSetSpecialEffectColorByPlayer); null unless set. Setting null does nothing, as the game has no way back.</summary>
        [LocalSafe]
        public Player? TeamColor
        {
            get { return teamColor; }
            set
            {
                if (handle == null || HasEnded || value == null)
                {
                    return;
                }

                teamColor = value;
                Runtime.World.SetEffectTeamColor(handle, value.Handle);
            }
        }

        /// <summary>The speed of the effect's animations as a factor of the model's own; 1 unless set.</summary>
        [LocalSafe]
        public float AnimationSpeed
        {
            get { return speed; }
            set
            {
                if (handle == null || HasEnded)
                {
                    return;
                }

                speed = value;
                Runtime.World.SetEffectSpeed(handle, value);
            }
        }

        /// <summary>Whether the effect plays its death animation when it ends, by End or by its scope (DestroyEffect); true unless set. False makes it vanish at once (BlzRemoveEffect), as a marker that is moved around should; a model with no death animation stands about 4 to 5 s before it goes unless this is false.</summary>
        [LocalSafe]
        public bool PlaysDeath
        {
            get
            {
                return playsDeath;
            }

            set
            {
                if (!HasEnded)
                {
                    playsDeath = value;
                }
            }
        }

        /// <summary>Makes an effect of a model at a place, on the ground there, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it: the other machines make it with an empty model, so the game's handles stay in step. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static SpecialEffect Create(string model, float x, float y, Scope scope, Player? shownTo = null)
        {
            var fx = new SpecialEffect(model, x, y, shownTo);
            scope.Adopt(fx);
            if (!fx.HasEnded)
            {
                fx.handle = Runtime.World.MakeEffect(ShownModel(model, shownTo), x, y);
            }

            return fx;
        }

        /// <summary>Plays an effect of a model once at a place, on the ground there: it is made and ended at once, so its death animation plays. Given shownTo, only that player sees it.</summary>
        public static void PlayOnce(string model, float x, float y, Player? shownTo = null)
        {
            Runtime.World.EndEffect(Runtime.World.MakeEffect(ShownModel(model, shownTo), x, y), true);
        }

        /// <summary>Whether a player sees the effect: the one it was shown to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Moves the effect to a place in one call, with one read of the ground there. Does nothing on a unit, and once it has ended.</summary>
        public void PlaceAt(float x, float y)
        {
            if (handle == null || HasEnded || carrier != null)
            {
                return;
            }

            this.x = x;
            this.y = y;
            Move();
        }

        /// <summary>Plays an animation of the model by name, as Animation names them or by any name the model has ("attack slam"). Does nothing once it has ended.</summary>
        public void PlayAnimation(string name)
        {
            if (handle != null && !HasEnded)
            {
                Runtime.World.AnimateEffect(handle, name, false);
            }
        }

        /// <summary>Queues an animation of the model by name, to play after the one playing. Does nothing once it has ended.</summary>
        public void QueueAnimation(string name)
        {
            if (handle != null && !HasEnded)
            {
                Runtime.World.AnimateEffect(handle, name, true);
            }
        }

        /// <summary>Gives the game the effect's orientation, in radians.</summary>
        private void Turn(float facing, float pitch, float roll)
        {
            if (handle == null || HasEnded)
            {
                return;
            }

            var wrapped = facing % 360.0f;
            if (wrapped < 0.0f)
            {
                wrapped += 360.0f;
            }

            this.facing = wrapped;
            this.pitch = pitch;
            this.roll = roll;
            Runtime.World.TurnEffect(handle, wrapped * RadiansPerDegree, pitch * RadiansPerDegree, roll * RadiansPerDegree);
        }

        /// <summary>Gives the game the effect's place, at its height above the ground there.</summary>
        private void Move()
        {
            Runtime.World.PlaceEffect(handle!, x, y, Runtime.World.GroundZ(x, y) + height);
        }

        /// <summary>Makes an effect of a model on a unit, at an attachment point, in a scope, and lists it on the unit, which ends it if the unit ends first. On a unit that is gone, or in a scope that has ended, it ends at once and the game is not asked to make it.</summary>
        internal static SpecialEffect On(Unit unit, string model, string point, Scope scope, Player? shownTo)
        {
            var fx = new SpecialEffect(model, unit, point, shownTo);
            scope.Adopt(fx);
            if (!fx.HasEnded && unit.IsGone)
            {
                fx.End();
            }

            if (!fx.HasEnded)
            {
                fx.handle = Runtime.World.MakeEffectOn(ShownModel(model, shownTo), unit.Handle, point);
                unit.Attach(fx);
            }

            return fx;
        }

        /// <summary>Plays an effect of a model once on a unit, at an attachment point; nothing on a unit that is gone.</summary>
        internal static void PlayOnceOn(Unit unit, string model, string point, Player? shownTo)
        {
            if (!unit.IsGone)
            {
                Runtime.World.EndEffect(Runtime.World.MakeEffectOn(ShownModel(model, shownTo), unit.Handle, point), true);
            }
        }

        /// <summary>The model this machine makes: the model, or an empty one where the player it is shown to is not at this machine.</summary>
        private static string ShownModel(string model, Player? shownTo)
        {
            return shownTo == null || Runtime.World.IsLocal(shownTo.Handle) ? model : "";
        }

        /// <summary>Ends the game's effect, with its death or at once, and takes it off its unit's list.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndEffect(handle, playsDeath);
            }

            if (carrier != null)
            {
                carrier.Detach(this);
            }
        }
    }
}
