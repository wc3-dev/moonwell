using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A line a unit says: its talking portrait, name and text where the HUD's portrait is, or in the letterbox in a cinematic,
    /// and its voice, as World Editor's transmissions do (decision 208). It lasts its voice's length, read on the first
    /// person's machine and shared, as music's (decision 204); with no voice, 2 s and 0.05 s a letter; given seconds, those.
    /// The portrait stays 1.5 s longer, as the game's does. In a cinematic's scene, <c>yield return</c> waits for it.
    /// </summary>
    public sealed class Transmission : Wait
    {
        /// <summary>The seconds the portrait stays after the line, as Blizzard.j's.</summary>
        private const float HangTime = 1.5f;

        /// <summary>The transmission whose portrait shows now: an early end takes it away only if it is still this one's.</summary>
        private static Transmission? latest;

        private readonly Line line;

        private readonly Unit? speaker;

        private readonly int typeId;

        private readonly int color;

        private readonly string? name;

        private readonly string text;

        private readonly string? voice;

        private readonly Player? shownTo;

        private readonly Scope scope;

        private float length = -1.0f;

        private sound? playing;

        private bool runOut;

        // Set as the line ends, before what waited for it goes on: line.HasEnded is set only after that.
        private bool over;

        private List<Action>? waiting;

        private Transmission(Scope scope, Unit? speaker, int typeId, int color, string? name, string text, string? voice, Player? shownTo)
        {
            this.scope = scope;
            this.speaker = speaker;
            this.typeId = typeId;
            this.color = color;
            this.name = name;
            this.text = text ?? "";
            this.voice = string.IsNullOrEmpty(voice) ? null : voice;
            this.shownTo = shownTo;
            line = new Line(this);
        }

        /// <summary>The text.</summary>
        [LocalSafe]
        public string Text => text;

        /// <summary>Whether it is over: its time is up, or what held it ended.</summary>
        [LocalSafe]
        public bool IsOver => over;

        /// <summary>
        /// A unit says a line, with its portrait in its owner's colour, and its voice if given. The name shown is the given one,
        /// or the unit's (a hero's proper name), in each player's language. Given shownTo, only that player sees and hears it.
        /// It flashes the unit red and pings the minimap at it, as the game's transmissions do.
        /// </summary>
        public static Transmission Play(Unit speaker, string text, string? voice = null, Player? shownTo = null, string? name = null, float seconds = 0.0f)
        {
            return Say(Game.Instance, speaker, text, voice, shownTo, name, seconds);
        }

        /// <summary>A line said by no unit in the game: the portrait of a unit class, in a colour (red unless given), with a name.</summary>
        public static Transmission Play<T>(string name, string text, string? voice = null, Player? shownTo = null, PlayerColor color = PlayerColor.Red, float seconds = 0.0f)
            where T : Unit
        {
            var made = new Transmission(Game.Instance, null, ObjectTypes.IdOf<T>(), (int)color, name ?? "", text, voice, shownTo);
            made.Begin(seconds);
            return made;
        }

        /// <summary>A unit's line held by a scope, which ends it when it ends: a cinematic's Say.</summary>
        internal static Transmission Say(Scope scope, Unit speaker, string text, string? voice, Player? shownTo, string? name, float seconds)
        {
            var typeId = speaker.IsGone ? 0 : Runtime.World.UnitTypeId(speaker.Handle);
            var made = new Transmission(scope, speaker, typeId, (int)speaker.Owner.Color, name, text, voice, shownTo);
            made.Begin(seconds);
            return made;
        }

        internal override void Await(Scope waiter, Action resume)
        {
            if (over)
            {
                waiter.OwnAfter(0.0f, resume);
                return;
            }

            if (waiting == null)
            {
                waiting = new List<Action>();
            }

            waiting.Add(resume);
        }

        internal static void Reset()
        {
            latest = null;
        }

        private void Begin(float seconds)
        {
            scope.Adopt(line);
            if (line.HasEnded)
            {
                return;
            }

            if (seconds > 0.0f)
            {
                Start(seconds);
            }
            else if (voice == null)
            {
                Start(2.0f + text.Length * 0.05f);
            }
            else
            {
                FileLengths.Get(voice, read => Start(read > 0.0f ? read : 2.0f + text.Length * 0.05f));
            }
        }

        private void Start(float seconds, bool waited = false)
        {
            if (line.HasEnded || length >= 0.0f)
            {
                return;
            }

            var early = waited || !(scope is Cinematic cine) ? 0.0f : cine.UntilLetterbox();
            if (early > 0.0f)
            {
                scope.OwnAfter(early, () => Start(seconds, true));
                return;
            }

            length = seconds;
            latest = this;
            if (ShownHere())
            {
                Runtime.World.ShowScene(typeId, color, NameHere(), text, seconds + HangTime, seconds);
                if (speaker != null && !speaker.IsGone && !Runtime.World.IsHidden(speaker.Handle))
                {
                    Runtime.World.MarkSpeaker(speaker.Handle, speaker.X, speaker.Y);
                }
            }

            if (voice != null)
            {
                // Made and played on every machine, silent where it is not heard, so the game's handles stay in step.
                playing = Runtime.World.MakeSound(voice, false, false);
                Runtime.World.SetLoudness(playing, ShownHere() ? 127 : 0);
                Runtime.World.StartPlaying(playing);
            }

            scope.OwnAfter(seconds, () =>
            {
                runOut = true;
                line.End();
            });
        }

        // The given name, or the unit's in this machine's language: shown only, never kept.
        private string NameHere()
        {
            if (name != null || speaker == null)
            {
                return name ?? "";
            }

            return speaker is Hero hero ? hero.ProperName : speaker.Name;
        }

        private bool ShownHere()
        {
            return shownTo == null || Runtime.World.IsLocal(shownTo.Handle);
        }

        /// <summary>The line ended: its voice stops, its portrait goes at once unless its time ran out (it then stays its 1.5 s), and what waits for it goes on.</summary>
        private void Ended()
        {
            over = true;
            if (playing != null)
            {
                Runtime.World.StopPlaying(playing);
                playing = null;
            }

            if (!runOut && latest == this && length >= 0.0f && ShownHere())
            {
                Runtime.World.EndScene();
            }

            if (latest == this)
            {
                latest = null;
            }

            var resumes = waiting;
            waiting = null;
            if (resumes != null)
            {
                for (var i = 0; i < resumes.Count; i++)
                {
                    resumes[i]();
                }
            }
        }

        /// <summary>The transmission's place in the tree of scopes.</summary>
        private sealed class Line : Scoped
        {
            private readonly Transmission owner;

            internal Line(Transmission owner)
            {
                this.owner = owner;
            }

            internal override void Finish()
            {
                owner.Ended();
            }
        }
    }

    /// <summary>The lengths of sound files, read on the first person's machine and shared, as music's are, as a voice's length differs by language.</summary>
    internal static class FileLengths
    {
        private static Dictionary<string, float> known = new Dictionary<string, float>();

        private static Dictionary<string, List<Action<float>>> waiting = new Dictionary<string, List<Action<float>>>();

        /// <summary>Hands a file's length in seconds to arrived, at once when known, or when it arrives; 0 when the game has no such file.</summary>
        internal static void Get(string path, Action<float> arrived)
        {
            if (known.TryGetValue(path, out var seconds))
            {
                arrived(seconds);
                return;
            }

            if (waiting.TryGetValue(path, out var list))
            {
                list.Add(arrived);
                return;
            }

            waiting[path] = new List<Action<float>> { arrived };
            Ask(path);
        }

        internal static void Reset()
        {
            known = new Dictionary<string, float>();
            waiting = new Dictionary<string, List<Action<float>>>();
        }

        // Asks the first person's machine; if they leave first, the next person's. With no person, the length is 0.
        private static void Ask(string path)
        {
            var people = Player.Users;
            if (people.Count == 0)
            {
                Arrived(path, 0.0f);
                return;
            }

            Sharing.Request(Game.Instance, people[0], () => Runtime.World.MusicLength(path).ToString(), text =>
            {
                if (text == null)
                {
                    Ask(path);
                    return;
                }

                var milliseconds = int.TryParse(text, out var read) ? read : 0;
                if (milliseconds <= 0)
                {
                    Dev.Report("Transmission", "the game has no sound file " + path);
                }

                Arrived(path, milliseconds / 1000.0f);
            });
        }

        private static void Arrived(string path, float seconds)
        {
            known[path] = seconds;
            var list = waiting[path];
            waiting.Remove(path);
            for (var i = 0; i < list.Count; i++)
            {
                list[i](seconds);
            }
        }
    }
}
