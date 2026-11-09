using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The game's music. Play plays one file or a playlist, its files in order, or mixed once with shuffle, then again from the
    /// first. PlayOnce cuts in with a file that plays once; the playlist then goes on from the file it was at, and with no
    /// playlist the music stops. Moonwell plays every file itself, one at a time, each for its length: the game's own list plays
    /// once from a place the game picks and then goes back to the map's music, and a stop does not hold while the game fades the
    /// music back in after its own cut-in (game facts, 11-06). The lengths are read on the first person's machine and shared,
    /// so the music moves on at the same moment on every machine; a file plays at once. Each file is also the map's music while
    /// it plays, which the game goes back to. The music goes on while the game is paused, and moves on when play does; it goes
    /// on after a saved game loads (seen 11-06). Given heardBy, the music changes for that player only: it makes nothing in the
    /// game, so the other machines leave theirs as it is.
    /// </summary>
    public static class Music
    {
        /// <summary>The game's loudest volume.</summary>
        private const float Loudest = 127.0f;

        /// <summary>The playlist each player hears, by slot; null for none of Moonwell's.</summary>
        private static Track?[] heard = new Track?[Player.MaxSlots];

        /// <summary>The file played once that each player hears over their playlist, by slot; null for none.</summary>
        private static Track?[] cutIns = new Track?[Player.MaxSlots];

        /// <summary>The length of each file read so far, in seconds, the same on every machine.</summary>
        private static Dictionary<string, float> lengths = new Dictionary<string, float>();

        /// <summary>The files whose length has been asked for and has not arrived.</summary>
        private static HashSet<string> asked = new HashSet<string>();

        /// <summary>Plays one file of music, again and again.</summary>
        public static void Play(string track, Player? heardBy = null)
        {
            Start(new[] { track ?? "" }, heardBy);
        }

        /// <summary>Plays a playlist: the files in order, then again from the first.</summary>
        public static void Play(params string[] playlist)
        {
            Play(playlist, false, null);
        }

        /// <summary>Plays a playlist, its files in order, or mixed once with shuffle, then again from the first. Given heardBy, only that player's music changes.</summary>
        public static void Play(string[] playlist, bool shuffle = false, Player? heardBy = null)
        {
            var order = new string[playlist.Length];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = playlist[i] ?? "";
            }

            if (shuffle)
            {
                // Run on every machine, as the random numbers are shared.
                Game.Random.Shuffle(order);
            }

            Start(order, heardBy);
        }

        /// <summary>Cuts in with a file of music that plays once; the playlist then goes on from the file it was at, and with none the music stops.</summary>
        public static void PlayOnce(string track, Player? heardBy = null)
        {
            var cutIn = new Track(new[] { track ?? "" }, true);
            Hand(cutIns, cutIn, heardBy);
            cutIn.Play();
        }

        /// <summary>Stops the music, a file played once included, fading it out unless fadeOut is false.</summary>
        public static void Stop(bool fadeOut = true, Player? heardBy = null)
        {
            Hand(heard, null, heardBy);
            Hand(cutIns, null, heardBy);
            if (Hears(heardBy))
            {
                Runtime.World.StopAllMusic(fadeOut);
            }
        }

        /// <summary>Sets how loud the music plays, 0 to 100.</summary>
        public static void SetVolume(float volume, Player? heardBy = null)
        {
            Dev.Check(volume >= 0.0f && volume <= 100.0f, "Music.SetVolume: 0 to 100");
            if (Hears(heardBy))
            {
                Runtime.World.SetMusicLoudness((int)(volume * Loudest / 100.0f + 0.5f));
            }
        }

        /// <summary>Forgets every playlist and length; the runtime's reset calls it.</summary>
        internal static void Reset()
        {
            heard = new Track?[Player.MaxSlots];
            cutIns = new Track?[Player.MaxSlots];
            lengths = new Dictionary<string, float>();
            asked = new HashSet<string>();
        }

        private static void Start(string[] files, Player? heardBy)
        {
            if (files.Length == 0)
            {
                Stop(true, heardBy);
                return;
            }

            var playlist = new Track(files, false);
            Hand(heard, playlist, heardBy);
            playlist.Play();
        }

        // Gives the player's slot, or every slot, to a track or to none; a track no slot holds any more stops.
        private static void Hand(Track?[] slots, Track? track, Player? heardBy)
        {
            for (var slot = 0; slot < slots.Length; slot++)
            {
                if (heardBy != null && heardBy.Slot != slot)
                {
                    continue;
                }

                var old = slots[slot];
                slots[slot] = track;
                if (old != null && old != track && !Held(old))
                {
                    old.End();
                }
            }
        }

        // Whether a slot holds the track.
        private static bool Held(Track track)
        {
            for (var slot = 0; slot < Player.MaxSlots; slot++)
            {
                if (heard[slot] == track || cutIns[slot] == track)
                {
                    return true;
                }
            }

            return false;
        }

        // The slot of this machine's player, or -1.
        private static int LocalSlot()
        {
            for (var slot = 0; slot < Player.MaxSlots; slot++)
            {
                if (Runtime.World.IsLocal(Player.Get(slot).Handle))
                {
                    return slot;
                }
            }

            return -1;
        }

        // Whether this machine's player hears the track now: their file played once, or else their playlist.
        private static bool HeardHere(Track track)
        {
            var slot = LocalSlot();
            return slot >= 0 && (cutIns[slot] ?? heard[slot]) == track;
        }

        // Asks the first person's machine for a file's length, once at a time; if they leave first, the next person's.
        private static void Ask(string path)
        {
            var people = Player.Users;
            if (asked.Contains(path) || people.Count == 0)
            {
                return;
            }

            asked.Add(path);
            Sharing.Request(Game.Instance, people[0], () => Runtime.World.MusicLength(path).ToString(), text =>
            {
                asked.Remove(path);
                if (text == null)
                {
                    Ask(path);
                    return;
                }

                var milliseconds = int.TryParse(text, out var read) ? read : 0;
                if (milliseconds <= 0)
                {
                    Dev.Report("Music.Play", "the game has no music file " + path);
                }

                lengths[path] = milliseconds / 1000.0f;
                var waiting = new List<Track>();
                for (var slot = 0; slot < Player.MaxSlots; slot++)
                {
                    AddOnce(waiting, heard[slot]);
                    AddOnce(waiting, cutIns[slot]);
                }

                for (var i = 0; i < waiting.Count; i++)
                {
                    waiting[i].Arrived(path);
                }
            });
        }

        private static void AddOnce(List<Track> tracks, Track? track)
        {
            if (track != null && !tracks.Contains(track))
            {
                tracks.Add(track);
            }
        }

        private static bool Hears(Player? heardBy)
        {
            return heardBy == null || Runtime.World.IsLocal(heardBy.Handle);
        }

        /// <summary>Music Moonwell plays: a playlist, one file at a time, the next when its length is up, round and round; or a file played once, after which the playlist under it goes on.</summary>
        private sealed class Track
        {
            private readonly string[] files;
            private readonly bool once;
            private int at;
            private float startedAt;
            private Timer? clock;

            internal Track(string[] files, bool once)
            {
                this.files = files;
                this.once = once;
            }

            /// <summary>The file it is at.</summary>
            internal string Current => files[at];

            /// <summary>Plays the file it is at, on the machines of the players who hear it, and starts the clock to the next once its length is known.</summary>
            internal void Play()
            {
                clock?.End();
                clock = null;
                var path = files[at];
                if (HeardHere(this))
                {
                    Runtime.World.StartMusic(path);
                }

                startedAt = Runtime.World.Now();
                if (lengths.TryGetValue(path, out var seconds))
                {
                    Wait(seconds);
                }
                else
                {
                    Ask(path);
                }
            }

            /// <summary>A file's length arrived: the clock starts if this track was waiting for it.</summary>
            internal void Arrived(string path)
            {
                if (clock == null && files[at] == path)
                {
                    Wait(lengths[path]);
                }
            }

            /// <summary>Stops the clock.</summary>
            internal void End()
            {
                clock?.End();
                clock = null;
            }

            // The clock counts from when the file started, so the wait for its length is not added.
            private void Wait(float seconds)
            {
                // A file the game has not got waits 1 s, so a playlist of them cannot spin.
                var left = (seconds > 0.0f ? seconds : 1.0f) - (Runtime.World.Now() - startedAt);
                clock = Game.Instance.OwnAfter(left > 0.0f ? left : 0.0f, Next);
            }

            private void Next()
            {
                clock = null;
                if (once)
                {
                    Finish();
                    return;
                }

                at = (at + 1) % files.Length;
                Play();
            }

            // A file played once is over: its players' playlists go on from the file they are at, and with none the music stops.
            private void Finish()
            {
                var here = HeardHere(this);
                for (var slot = 0; slot < Player.MaxSlots; slot++)
                {
                    if (cutIns[slot] == this)
                    {
                        cutIns[slot] = null;
                    }
                }

                if (!here)
                {
                    return;
                }

                var under = heard[LocalSlot()];
                if (under != null)
                {
                    Runtime.World.StartMusic(under.Current);
                }
                else
                {
                    Runtime.World.StopAllMusic(false);
                }
            }
        }
    }
}
