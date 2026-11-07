using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A cinematic: a scene, written as a method that yields what it waits for, played on its watchers' screens (decision
    /// 208). It is a scope: what is made on it (its lines, timers, effects, sounds, frames) ends with it, at the scene's end
    /// or a skip. While it plays, each watcher's screen has the letterbox (unless letterbox is false), no controls, the
    /// game's sounds lowered and the earlier messages cleared; the map's frames over the HUD hide too, but those made on the
    /// cinematic. Fog, the game's speed, the day and the random numbers are left as they are. Esc skips it once every person
    /// watching has pressed it, unless it is not skippable. One plays at a time for a player: a new one ends the one before.
    /// </summary>
    public sealed class Cinematic : Scope
    {
        /// <summary>The seconds the letterbox takes to come and go, as Blizzard.j's.</summary>
        private const float LetterboxFade = 0.5f;

        /// <summary>The seconds a cinematic that ends faded out takes to fade back in.</summary>
        private const float FadeBack = 1.0f;

        // No initializer: a public type with static setup can stop the script while it loads (StaticSetupTests).
        private static List<Cinematic>? playing;

        private static bool listening;

        private static List<Cinematic> Playing => playing ??= new List<Cinematic>();

        /// <summary>Counts the fades, so a fade's later end does not take away a newer fade's colour.</summary>
        private static int fades;

        private readonly Func<Cinematic, IEnumerable<Wait>> scene;

        private readonly Player? shownTo;

        private readonly bool letterbox;

        private readonly bool skippable;

        private readonly HashSet<int> pressed = new HashSet<int>();

        private IEnumerator<Wait>? steps;

        private bool skipped;

        private bool filtered;

        private bool fadedOut;

        private Color fadeColor = Color.Black;

        private Cinematic(Func<Cinematic, IEnumerable<Wait>> scene, Player? shownTo, bool letterbox, bool skippable)
        {
            this.scene = scene;
            this.shownTo = shownTo;
            this.letterbox = letterbox;
            this.skippable = skippable;
        }

        /// <summary>Runs once when the cinematic ends, after what was made on it: true when it was skipped. The place to set things up for play, whichever way it ended.</summary>
        public event Action<bool>? Ended;

        /// <summary>Whether the cinematic was skipped.</summary>
        [LocalSafe]
        public bool IsSkipped => skipped;

        /// <summary>Whether Esc can skip it.</summary>
        [LocalSafe]
        public bool IsSkippable => skippable;

        /// <summary>Whether it hides the HUD behind the letterbox.</summary>
        [LocalSafe]
        public bool HasLetterbox => letterbox;

        /// <summary>Called by the frames package when a watcher's screen changes, so it hides or shows the frames over the HUD.</summary>
        internal static Action? ScreenChanged { get; set; }

        /// <summary>Whether a player watches it: the one it was given to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>The cinematic a player watches now; null when none.</summary>
        [LocalSafe]
        public static Cinematic? Of(Player player)
        {
            for (var i = 0; i < Playing.Count; i++)
            {
                if (Playing[i].IsShownTo(player))
                {
                    return Playing[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Plays a scene, in a scope, which ends it when it ends, and returns the cinematic. The scene starts in the instant
        /// after, so Ended can be set first. Given shownTo, only that player watches it. In a scope that has ended it ends at
        /// once and nothing changes on the screen.
        /// </summary>
        public static Cinematic Play(Func<Cinematic, IEnumerable<Wait>> scene, Scope scope, Player? shownTo = null, bool letterbox = true, bool skippable = true)
        {
            var made = new Cinematic(scene, shownTo, letterbox, skippable);
            scope.Adopt(made);
            if (made.HasEnded)
            {
                return made;
            }

            for (var i = Playing.Count - 1; i >= 0; i--)
            {
                var other = Playing[i];
                if (other.shownTo == null || shownTo == null || other.shownTo == shownTo)
                {
                    other.End();
                }
            }

            Playing.Add(made);
            Listen();
            made.Enter();
            made.OwnAfter(0.0f, made.Next);
            return made;
        }

        /// <summary>A unit says a line in the cinematic, to its watchers; it ends with the cinematic. <c>yield return</c> waits for it.</summary>
        public Transmission Say(Unit speaker, string text, string? voice = null, string? name = null, float seconds = 0.0f)
        {
            return Transmission.Say(this, speaker, text, voice, shownTo, name, seconds);
        }

        /// <summary>Covers the watchers' screens with a colour, black unless given, over the seconds; it stays until FadeIn, or until the cinematic ends, which fades it back in over 1 s. <c>yield return</c> waits for it.</summary>
        public Wait FadeOut(float seconds, Color? color = null)
        {
            fadeColor = color ?? Color.Black;
            fadedOut = true;
            Filter(0, 255, seconds);
            return Wait.Seconds(seconds);
        }

        /// <summary>Uncovers the watchers' screens over the seconds, from the last fade's colour, black at the start. <c>yield return</c> waits for it.</summary>
        public Wait FadeIn(float seconds)
        {
            fadedOut = false;
            var fade = Filter(255, 0, seconds);
            OwnAfter(seconds, () => ClearFilter(fade));
            return Wait.Seconds(seconds);
        }

        /// <summary>Ends the cinematic as a skip, as Esc does, skippable or not.</summary>
        public void Skip()
        {
            if (ending || HasEnded)
            {
                return;
            }

            skipped = true;
            End();
        }

        /// <summary>A person pressed Esc: the cinematics they watch skip once every person watching has pressed it.</summary>
        internal static void Esc(Player from)
        {
            for (var i = Playing.Count - 1; i >= 0; i--)
            {
                if (i < Playing.Count && Playing[i].IsShownTo(from))
                {
                    Playing[i].Pressed(from);
                }
            }
        }

        /// <summary>The cinematic with a letterbox on this machine's screen; null when none.</summary>
        internal static Cinematic? LetterboxHere()
        {
            for (var i = 0; i < Playing.Count; i++)
            {
                if (Playing[i].letterbox && Playing[i].ShownHere())
                {
                    return Playing[i];
                }
            }

            return null;
        }

        internal static void Reset()
        {
            playing = null;
            listening = false;
            fades = 0;
            ScreenChanged = null;
        }

        /// <summary>Puts the watchers' screens back, the frames over the HUD too, and runs Ended.</summary>
        internal override void Finish()
        {
            Playing.Remove(this);
            if (steps != null)
            {
                try
                {
                    steps.Dispose();
                }
                catch (Exception error)
                {
                    Errors.Report("Cinematic.scene", error);
                }
            }

            if (filtered && fadedOut)
            {
                var fade = Filter(255, 0, FadeBack);
                Game.Instance.OwnAfter(FadeBack, () => ClearFilter(fade));
            }
            else if (filtered)
            {
                ClearFilter(fades);
            }

            if (ShownHere())
            {
                Runtime.World.EndScene();
                if (letterbox)
                {
                    Runtime.World.Letterbox(false, LetterboxFade);
                }

                Runtime.World.UserControl(true);
                Runtime.World.CinematicSounds(false);
            }

            ScreenChanged?.Invoke();
            try
            {
                Ended?.Invoke(skipped);
            }
            catch (Exception error)
            {
                Errors.Report("Cinematic.Ended", error);
            }
        }

        private static void Listen()
        {
            if (listening)
            {
                return;
            }

            listening = true;
            foreach (var person in Player.Users)
            {
                Runtime.World.ListenEsc(person.Handle);
            }
        }

        private bool ShownHere()
        {
            return shownTo == null || Runtime.World.IsLocal(shownTo.Handle);
        }

        private void Enter()
        {
            if (ShownHere())
            {
                Runtime.World.ClearMessages();
                if (letterbox)
                {
                    Runtime.World.Letterbox(true, LetterboxFade);
                }

                Runtime.World.UserControl(false);
                Runtime.World.CinematicSounds(true);
            }

            ScreenChanged?.Invoke();
        }

        /// <summary>The scene's next step: runs it up to its next yield, and waits for what it yields; at its end the cinematic ends.</summary>
        private void Next()
        {
            if (ending || HasEnded)
            {
                return;
            }

            bool more;
            try
            {
                if (steps == null)
                {
                    steps = scene(this).GetEnumerator();
                }

                more = steps.MoveNext();
            }
            catch (Exception error)
            {
                Errors.Report("Cinematic.scene", error);
                End();
                return;
            }

            if (!more)
            {
                End();
                return;
            }

            var wait = steps.Current;
            if (wait == null)
            {
                OwnAfter(0.0f, Next);
                return;
            }

            wait.Await(this, Next);
        }

        private void Pressed(Player from)
        {
            // Before the scene's first step is the instant of Play: a press then is the one that started it from another Esc trigger.
            if (!skippable || ending || HasEnded || steps == null)
            {
                return;
            }

            pressed.Add(from.Slot);
            foreach (var person in Player.Users)
            {
                if (IsShownTo(person) && !pressed.Contains(person.Slot))
                {
                    return;
                }
            }

            Skip();
        }

        // Every machine counts the fade, so the counts agree; only the watchers' screens change.
        private int Filter(int fromAlpha, int toAlpha, float seconds)
        {
            filtered = true;
            fades++;
            if (ShownHere())
            {
                Runtime.World.ScreenFilter(fadeColor.R, fadeColor.G, fadeColor.B, fromAlpha, toAlpha, seconds);
            }

            return fades;
        }

        private void ClearFilter(int fade)
        {
            if (fade == fades && ShownHere())
            {
                Runtime.World.EndScreenFilter();
            }
        }
    }
}
