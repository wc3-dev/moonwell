namespace Moonwell
{
    /// <summary>
    /// The game's music. Play gives the game one file or a playlist, which it plays in order and starts again (seen 11-06);
    /// shuffle mixes the order once, with the game's random numbers, on every machine. PlayOnce cuts in with a file that plays
    /// once, and the music goes on after it. The first file of the first playlist may not be heard (a 2 s one was not, seen
    /// 11-06). Given heardBy, the music changes for that player only: it makes nothing in the game, so the other machines
    /// leave theirs as it is.
    /// </summary>
    public static class Music
    {
        /// <summary>The game's loudest volume.</summary>
        private const float Loudest = 127.0f;

        /// <summary>Plays one file of music, again and again.</summary>
        public static void Play(string track, Player? heardBy = null)
        {
            if (Hears(heardBy))
            {
                Runtime.World.StartMusic(track ?? "");
            }
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
                for (var i = order.Length - 1; i > 0; i--)
                {
                    var j = Runtime.World.RandomBetween(0, i);
                    var swapped = order[i];
                    order[i] = order[j];
                    order[j] = swapped;
                }
            }

            if (Hears(heardBy))
            {
                Runtime.World.StartMusic(string.Join(";", order));
            }
        }

        /// <summary>Cuts in with a file of music that plays once; the music goes on after it.</summary>
        public static void PlayOnce(string track, Player? heardBy = null)
        {
            if (Hears(heardBy))
            {
                Runtime.World.StartMusicOnce(track ?? "");
            }
        }

        /// <summary>Stops the music, a file played once included, fading it out unless fadeOut is false.</summary>
        public static void Stop(bool fadeOut = true, Player? heardBy = null)
        {
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

        private static bool Hears(Player? heardBy)
        {
            return heardBy == null || Runtime.World.IsLocal(heardBy.Handle);
        }
    }
}
