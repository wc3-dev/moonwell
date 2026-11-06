namespace Moonwell
{
    /// <summary>
    /// A sound of a file. Play and a unit's PlaySound play one once, 2D for the whole screen or 3D at a place or on a unit.
    /// Create and a unit's AddSound make a looping one in a scope, which ends it when it ends, as End does: it fades out
    /// over FadeOut on a timer of the game's, so the fade outlives the scope, as the physics package's LoopingSound does.
    /// Given heardBy, only that player hears it: every machine makes and plays the sound, the others at volume 0, so the
    /// game's handles stay in step. Every read gives what was set, the same on every machine.
    /// </summary>
    public sealed class Sound : Scoped
    {
        /// <summary>The seconds between two steps of a fade, as LoopingSound's.</summary>
        private const float FadeStep = 0.05f;

        /// <summary>The game's loudest volume.</summary>
        private const float Loudest = 127.0f;

        private readonly string path;

        private readonly Unit? carrier;

        private readonly Player? heardBy;

        /// <summary>Whether it is a 3D sound at a place, which PlaceAt moves.</summary>
        private readonly bool atPlace;

        private float volume = 100.0f;

        private float fadeOut = 0.5f;

        private sound? playing;

        private Timer? fader;

        private float faded;

        private int fadeFrom;

        private Sound(string path, bool atPlace, Unit? carrier, Player? heardBy)
        {
            this.path = path ?? "";
            this.atPlace = atPlace;
            this.carrier = carrier;
            this.heardBy = heardBy;
        }

        /// <summary>The sound file.</summary>
        [LocalSafe]
        public string Path => path;

        /// <summary>The unit the sound is on; null for one at a place or on the whole screen.</summary>
        [LocalSafe]
        public Unit? Unit => carrier;

        /// <summary>How loud it plays, 0 to 100; 100 unless set.</summary>
        [LocalSafe]
        public float Volume
        {
            get
            {
                return volume;
            }

            set
            {
                Dev.Check(value >= 0.0f && value <= 100.0f, "Sound.Volume: 0 to 100");
                if (!HasEnded)
                {
                    volume = value;
                    if (playing != null)
                    {
                        Runtime.World.SetLoudness(playing, Level(value, heardBy));
                    }
                }
            }
        }

        /// <summary>The seconds it fades out over when it ends; 0.5 unless set, as LoopingSound's, and 0 to stop at once.</summary>
        [LocalSafe]
        public float FadeOut
        {
            get
            {
                return fadeOut;
            }

            set
            {
                Dev.Check(value >= 0.0f, "Sound.FadeOut: seconds must be 0 or more");
                fadeOut = value;
            }
        }

        /// <summary>Whether a player hears the sound: the one it was given to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsHeardBy(Player player)
        {
            return heardBy == null || heardBy == player;
        }

        /// <summary>Plays a sound once, 2D, as loud on the whole screen; volume is 0 to 100. Given heardBy, only that player hears it.</summary>
        public static void Play(string path, Player? heardBy = null, float volume = 100.0f)
        {
            Once(Runtime.World.MakeSound(path ?? "", false, false), heardBy, volume);
        }

        /// <summary>Plays a 3D sound once at a place, on the ground there; volume is 0 to 100. Given heardBy, only that player hears it.</summary>
        public static void Play(string path, float x, float y, Player? heardBy = null, float volume = 100.0f)
        {
            var made = Runtime.World.MakeSound(path ?? "", false, true);
            Runtime.World.PlaceSound(made, x, y, Runtime.World.GroundZ(x, y));
            Once(made, heardBy, volume);
        }

        /// <summary>Makes a looping 2D sound in a scope, which ends it when it ends, and returns it. Given heardBy, only that player hears it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static Sound Create(string path, Scope scope, Player? heardBy = null)
        {
            var made = new Sound(path, false, null, heardBy);
            scope.Adopt(made);
            if (!made.HasEnded)
            {
                made.Start(Runtime.World.MakeSound(made.path, true, false));
            }

            return made;
        }

        /// <summary>Makes a looping 3D sound at a place, on the ground there, in a scope, which ends it when it ends, and returns it. Given heardBy, only that player hears it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static Sound Create(string path, float x, float y, Scope scope, Player? heardBy = null)
        {
            var made = new Sound(path, true, null, heardBy);
            scope.Adopt(made);
            if (!made.HasEnded)
            {
                var looping = Runtime.World.MakeSound(made.path, true, true);
                Runtime.World.PlaceSound(looping, x, y, Runtime.World.GroundZ(x, y));
                made.Start(looping);
            }

            return made;
        }

        /// <summary>Moves a 3D sound at a place to another, on the ground there. Does nothing on a unit or the whole screen, and once it has ended.</summary>
        public void PlaceAt(float x, float y)
        {
            if (playing != null && !HasEnded && atPlace)
            {
                Runtime.World.PlaceSound(playing, x, y, Runtime.World.GroundZ(x, y));
            }
        }

        /// <summary>Makes a looping 3D sound on a unit, in a scope, and lists it on the unit; on a unit that is gone it ends at once.</summary>
        internal static Sound On(Unit unit, string path, Scope scope, Player? heardBy)
        {
            var made = new Sound(path, false, unit, heardBy);
            scope.Adopt(made);
            if (!made.HasEnded && unit.IsGone)
            {
                made.End();
            }

            if (!made.HasEnded)
            {
                var looping = Runtime.World.MakeSound(made.path, true, true);
                Runtime.World.PutSoundOn(looping, unit.Handle);
                made.Start(looping);
                unit.Attach(made);
            }

            return made;
        }

        /// <summary>Plays a 3D sound once on a unit, which it follows; nothing on a unit that is gone.</summary>
        internal static void PlayOn(Unit unit, string path, Player? heardBy, float volume)
        {
            if (!unit.IsGone)
            {
                var made = Runtime.World.MakeSound(path ?? "", false, true);
                Runtime.World.PutSoundOn(made, unit.Handle);
                Once(made, heardBy, volume);
            }
        }

        /// <summary>The game's volume, 0 to 127, of a volume 0 to 100, rounded; 0 on a machine whose player is not to hear it.</summary>
        private static int Level(float volume, Player? heardBy)
        {
            if (heardBy != null && !Runtime.World.IsLocal(heardBy.Handle))
            {
                return 0;
            }

            return (int)(volume * Loudest / 100.0f + 0.5f);
        }

        /// <summary>Plays a sound once and lets the game free it when it has played.</summary>
        private static void Once(sound made, Player? heardBy, float volume)
        {
            Dev.Check(volume >= 0.0f && volume <= 100.0f, "Sound.Play: a volume of 0 to 100");
            Runtime.World.SetLoudness(made, Level(volume, heardBy));
            Runtime.World.StartPlaying(made);
            Runtime.World.FreeWhenDone(made);
        }

        private void Start(sound looping)
        {
            playing = looping;
            Runtime.World.SetLoudness(looping, Level(volume, heardBy));
            Runtime.World.StartPlaying(looping);
        }

        /// <summary>Fades the sound out on a timer of the game's, or stops it at once with no FadeOut, and takes it off its unit.</summary>
        internal override void Finish()
        {
            if (carrier != null)
            {
                carrier.Detach(this);
            }

            if (playing == null)
            {
                return;
            }

            if (fadeOut <= 0.0f)
            {
                Stop();
                return;
            }

            faded = 0.0f;
            fadeFrom = Level(volume, heardBy);
            fader = Game.Instance.OwnEvery(FadeStep, Fade);
        }

        /// <summary>A step of the fade: quieter in proportion to the time run, and stopped, with the timer ended, once FadeOut has run.</summary>
        private void Fade()
        {
            faded += FadeStep;
            if (playing == null || faded >= fadeOut - FadeStep * 0.001f)
            {
                Stop();
                return;
            }

            Runtime.World.SetLoudness(playing, (int)(fadeFrom * (1.0f - faded / fadeOut)));
        }

        private void Stop()
        {
            var timer = fader;
            fader = null;
            if (timer != null)
            {
                timer.End();
            }

            var stopped = playing;
            playing = null;
            if (stopped != null)
            {
                Runtime.World.StopPlaying(stopped);
            }
        }
    }
}
