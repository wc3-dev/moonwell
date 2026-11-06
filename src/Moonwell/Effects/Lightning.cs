namespace Moonwell
{
    /// <summary>
    /// A lightning the game draws between two points, or between two units, which it follows. It is in the scope it was
    /// made in and ends when that scope ends, or by End; one between units also ends when either unit ends. Every player
    /// sees it, in the fog and the black mask too (seen), and the game shows no colour set on it (seen), so it has
    /// neither shownTo nor a tint. Every read gives what was set, the same on every machine. A saved game's load drops the
    /// game's lightning (seen 11-06), so Moonwell makes each lasting one again there, with a new Handle.
    /// </summary>
    public sealed class Lightning : Scoped
    {
        /// <summary>How often a lightning between units is moved: every 1/32 s.</summary>
        private const float FollowStep = 0.03125f;

        /// <summary>How long PlayOnce leaves a lightning before removing it: every look's Duration in LightningData.slk, and the fading looks are gone well within it (seen).</summary>
        private const float PlayOnceSeconds = 2.0f;

        /// <summary>How high above the ground, or a unit's feet and flying height, an end is drawn unless set.</summary>
        private const float DefaultHeight = 60.0f;

        private readonly string kind;

        private readonly Unit? from;

        private readonly Unit? to;

        private lightning? handle;

        private Timer? follow;

        private float x1;

        private float y1;

        private float x2;

        private float y2;

        private float fromHeight = DefaultHeight;

        private float toHeight = DefaultHeight;

        private Lightning(string kind, Unit? from, Unit? to, float x1, float y1, float x2, float y2)
        {
            this.kind = kind;
            this.from = from;
            this.to = to;
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
        }

        /// <summary>The lightning's code, as LightningKind names it.</summary>
        [LocalSafe]
        public string Kind => kind;

        /// <summary>The unit it starts on; null for a lightning between points.</summary>
        [LocalSafe]
        public Unit? From => from;

        /// <summary>The unit it ends on; null for a lightning between points.</summary>
        [LocalSafe]
        public Unit? To => to;

        /// <summary>The game's lightning, for anything Moonwell does not do; null for one that ended before the game made it. A saved game's load makes a new one.</summary>
        [LocalSafe]
        public lightning? Handle => handle;

        /// <summary>Where it starts: the point, or its first unit's place.</summary>
        [LocalSafe]
        public float X1 => from != null ? from.X : x1;

        /// <summary>Where it starts: the point, or its first unit's place.</summary>
        [LocalSafe]
        public float Y1 => from != null ? from.Y : y1;

        /// <summary>Where it ends: the point, or its second unit's place.</summary>
        [LocalSafe]
        public float X2 => to != null ? to.X : x2;

        /// <summary>Where it ends: the point, or its second unit's place.</summary>
        [LocalSafe]
        public float Y2 => to != null ? to.Y : y2;

        /// <summary>How high its start is drawn above the ground, or above its unit's feet and flying height; 60 unless set.</summary>
        [LocalSafe]
        public float FromHeight
        {
            get
            {
                return fromHeight;
            }

            set
            {
                if (handle != null && !HasEnded)
                {
                    fromHeight = value;
                    Draw();
                }
            }
        }

        /// <summary>How high its end is drawn above the ground, or above its unit's feet and flying height; 60 unless set.</summary>
        [LocalSafe]
        public float ToHeight
        {
            get
            {
                return toHeight;
            }

            set
            {
                if (handle != null && !HasEnded)
                {
                    toHeight = value;
                    Draw();
                }
            }
        }

        /// <summary>Draws a lightning of a code between two points, in a scope, which ends it when it ends, and returns it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static Lightning Create(string kind, float x1, float y1, float x2, float y2, Scope scope)
        {
            var bolt = new Lightning(kind, null, null, x1, y1, x2, y2);
            scope.Adopt(bolt);
            if (!bolt.HasEnded)
            {
                bolt.Make();
                LastingLightning.Keep(bolt);
            }

            return bolt;
        }

        /// <summary>Draws a lightning of a code from one unit to another, in a scope, and returns it. It follows both units, moved every 1/32 s, and ends when the scope ends or either unit ends, whichever is first; a look that fades (LightningKind) fades all the same. On a unit that is gone, or in a scope that has ended, it ends at once and the game is not asked to make it.</summary>
        public static Lightning Between(string kind, Unit from, Unit to, Scope scope)
        {
            var bolt = new Lightning(kind, from, to, 0.0f, 0.0f, 0.0f, 0.0f);
            scope.Adopt(bolt);
            if (!bolt.HasEnded && (from.IsGone || to.IsGone))
            {
                bolt.End();
            }

            if (!bolt.HasEnded)
            {
                bolt.Make();
                LastingLightning.Keep(bolt);
                from.Attach(bolt);
                to.Attach(bolt);
                bolt.follow = Game.Instance.OwnEvery(FollowStep, bolt.Draw);
            }

            return bolt;
        }

        /// <summary>Draws a lightning of a code between two points once, as a spell's hop: the game fades the fading looks (LightningKind), and Moonwell removes it 2 s later.</summary>
        public static void PlayOnce(string kind, float x1, float y1, float x2, float y2)
        {
            var bolt = new Lightning(kind, null, null, x1, y1, x2, y2);
            bolt.Make();
            RemoveLater(bolt.handle!);
        }

        /// <summary>Draws a lightning of a code from one unit to another once, where they stand, as PlayOnce between points does; nothing when either unit is gone.</summary>
        public static void PlayOnce(string kind, Unit from, Unit to)
        {
            if (from.IsGone || to.IsGone)
            {
                return;
            }

            var bolt = new Lightning(kind, from, to, 0.0f, 0.0f, 0.0f, 0.0f);
            bolt.Make();
            RemoveLater(bolt.handle!);
        }

        /// <summary>Moves a lightning between points to two other points. Does nothing on one between units, and once it has ended.</summary>
        public void MoveTo(float x1, float y1, float x2, float y2)
        {
            if (handle == null || HasEnded || from != null)
            {
                return;
            }

            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
            Draw();
        }

        /// <summary>Removes a PlayOnce bolt after its time, unless a load dropped it first, as its handle may be another bolt's by then.</summary>
        private static void RemoveLater(lightning made)
        {
            var load = LastingLightning.Loads;
            Game.Instance.OwnAfter(PlayOnceSeconds, () =>
            {
                if (load == LastingLightning.Loads)
                {
                    Runtime.World.EndLightning(made);
                }
            });
        }

        /// <summary>Asks the game for the lightning where its ends are now.</summary>
        internal void Make()
        {
            float ax = X1, ay = Y1, bx = X2, by = Y2;
            handle = Runtime.World.MakeLightning(kind, ax, ay, HeightAt(from, ax, ay, fromHeight), bx, by, HeightAt(to, bx, by, toHeight));
        }

        /// <summary>Gives the game the lightning's ends where they are now; nothing while either unit is gone, as its end ends the lightning.</summary>
        private void Draw()
        {
            if (handle == null || HasEnded || (from != null && from.IsGone) || (to != null && to.IsGone))
            {
                return;
            }

            float ax = X1, ay = Y1, bx = X2, by = Y2;
            Runtime.World.MoveBolt(handle, ax, ay, HeightAt(from, ax, ay, fromHeight), bx, by, HeightAt(to, bx, by, toHeight));
        }

        /// <summary>The absolute height of an end: the ground's on this machine, which only places what is drawn, the unit's flying height, and the end's height.</summary>
        private static float HeightAt(Unit? unit, float x, float y, float height)
        {
            return Runtime.World.GroundZ(x, y) + (unit != null ? unit.FlyHeight : 0.0f) + height;
        }

        /// <summary>Stops following, removes the game's lightning and takes it off its units.</summary>
        internal override void Finish()
        {
            follow?.End();
            if (handle != null)
            {
                Runtime.World.EndLightning(handle);
            }

            from?.Detach(this);
            to?.Detach(this);
            LastingLightning.Forget(this);
        }
    }
}
