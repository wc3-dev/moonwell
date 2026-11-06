namespace Moonwell.Testing
{
    /// <summary>Cases that check sounds played once and held, their volume, fade and end, who hears them, and music.</summary>
    public static class SoundCases
    {
        private const string Boom = "Sound\\Boom.wav";

        /// <summary>A game component that holds sounds, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static Unit NewFootman(FakeWorld world, out unit handle)
        {
            handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 100f, 50f);
            return Unit.Of(handle)!;
        }

        /// <summary>Play with no place is 2D, at full volume, started and left to the game to free; at a place it is 3D, on the ground there.</summary>
        public static void PlayOnce(FakeWorld world)
        {
            world.Ground = (x, y) => 30f;
            Sound.Play(Boom);
            Sound.Play(Boom, 10f, 20f, volume: 60f);
            var flat = world.Sounds[0];
            Expect.Equal(Boom, flat.Path, "the file");
            Expect.Equal(false, flat.Is3D, "2D");
            Expect.Equal(false, flat.Looping, "once");
            Expect.Equal(127, flat.Volume, "full volume is the game's 127");
            Expect.Equal(true, flat.Started && flat.FreedWhenDone, "started and freed when done");
            var placed = world.Sounds[1];
            Expect.Equal(true, placed.Is3D, "3D at a place");
            Expect.Near(10f, placed.X, "the x");
            Expect.Near(30f, placed.Z, "on the ground");
            Expect.Equal(76, placed.Volume, "60 is the game's 76");
        }

        /// <summary>A sound for another player is made and started on this machine too, silent, so the handles stay in step.</summary>
        public static void HeardBy(FakeWorld world)
        {
            world.LocalSlot = 0;
            Sound.Play(Boom, heardBy: Player.Two);
            Sound.Play(Boom, heardBy: Player.One);
            var footman = NewFootman(world, out var handle);
            footman.PlaySound(Boom, heardBy: Player.Two);
            var looping = Sound.Create(Boom, Game.Instance, heardBy: Player.Two);
            looping.Volume = 50f;
            Expect.Equal(0, world.Sounds[0].Volume, "silent for another player");
            Expect.Equal(true, world.Sounds[0].Started, "but started");
            Expect.Equal(127, world.Sounds[1].Volume, "heard by this machine's player");
            Expect.Same(handle, world.Sounds[2].Unit, "on the unit");
            Expect.Equal(0, world.Sounds[2].Volume, "silent on the unit for another player");
            Expect.Equal(0, world.Sounds[3].Volume, "a held one stays silent when its volume is set");
            Expect.Equal(false, looping.IsHeardBy(Player.One), "not heard by another");
            Expect.Equal(true, looping.IsHeardBy(Player.Two), "heard by its player");
            footman.Remove();
        }

        /// <summary>A held sound loops; its volume is set; at its scope's end it fades over FadeOut on the game's timer, then stops once.</summary>
        public static void HeldFades(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var hum = Sound.Create(Boom, 10f, 20f, holder);
            var made = world.Sounds[0];
            Expect.Equal(true, made.Looping && made.Is3D && made.Started, "a 3D loop, started");
            hum.Volume = 50f;
            Expect.Equal(64, made.Volume, "50 is the game's 64");
            hum.PlaceAt(30f, 40f);
            Expect.Near(30f, made.X, "moved");
            holder.End();
            Expect.Equal(true, hum.HasEnded, "ended with its scope");
            Expect.Equal(false, made.Stopped, "fading, not stopped yet");
            world.Advance(0.25f);
            Expect.True(made.Volume > 0 && made.Volume < 64, "quieter as it fades: " + made.Volume);
            world.Advance(0.3f);
            Expect.Equal(true, made.Stopped, "stopped after FadeOut");
        }

        /// <summary>With no FadeOut a held sound stops at once; one made in a scope that has ended is never made.</summary>
        public static void StopAtOnce(FakeWorld world)
        {
            var hum = Sound.Create(Boom, Game.Instance);
            hum.FadeOut = 0f;
            hum.End();
            Expect.Equal(true, world.Sounds[0].Stopped, "stopped at once");
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var late = Sound.Create(Boom, holder);
            Expect.Equal(true, late.HasEnded, "ended at once");
            Expect.Equal(1, world.Sounds.Count, "nothing made in an ended scope");
        }

        /// <summary>A unit's sound is put on it and ends with it; on a unit that is gone nothing is made.</summary>
        public static void OnUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var hum = footman.AddSound(Boom, Game.Instance);
            Expect.Same(handle, world.Sounds[0].Unit, "on the unit");
            Expect.Same(footman, hum.Unit, "its unit");
            footman.Remove();
            Expect.Equal(true, hum.HasEnded, "ended with the unit");
            world.Advance(0.6f);
            Expect.Equal(true, world.Sounds[0].Stopped, "stopped after its fade");
            var late = footman.AddSound(Boom, Game.Instance);
            footman.PlaySound(Boom);
            Expect.Equal(true, late.HasEnded, "ended at once on a gone unit");
            Expect.Equal(1, world.Sounds.Count, "nothing made on a gone unit");
        }

        /// <summary>Music: a file played, one played once, stopped and its volume set.</summary>
        public static void PlaysMusic(FakeWorld world)
        {
            Moonwell.Music.Play("a.mp3");
            Moonwell.Music.Play("a.mp3", "b.mp3", "c.mp3");
            Moonwell.Music.PlayOnce("win.mp3");
            Moonwell.Music.Stop();
            Moonwell.Music.Stop(fadeOut: false);
            Moonwell.Music.SetVolume(60f);
            Expect.Equal("play a.mp3|play a.mp3|play win.mp3|stop fading|stop at once|volume 76", string.Join("|", world.MusicCalls), "the calls");
        }

        /// <summary>A file played once cuts in for its length while the playlist's clock goes on; the playlist then plays the file it is at, and with no playlist the music stops. A stop ends both.</summary>
        public static void MusicPlayOnce(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.MusicLengths["a"] = 2000;
            world.MusicLengths["b"] = 2000;
            world.MusicLengths["win"] = 2500;
            Moonwell.Music.Play("a", "b");
            world.Advance(1.0f);
            Moonwell.Music.PlayOnce("win");
            world.Advance(2.4f);
            Expect.Equal("play a|play win", string.Join("|", world.MusicCalls), "b is not played over win");
            world.Advance(0.2f);
            Expect.Equal("play a|play win|play b", string.Join("|", world.MusicCalls), "after win, the file the playlist is at");
            world.Advance(1.0f);
            Expect.Equal("play a|play win|play b|play a", string.Join("|", world.MusicCalls), "the playlist's clock went on");
            Moonwell.Music.Stop();
            Moonwell.Music.PlayOnce("win");
            world.Advance(3.1f);
            Expect.Equal("play a|play win|play b|play a|stop fading|play win|stop at once", string.Join("|", world.MusicCalls), "with no playlist, the music stops after it");
            Moonwell.Music.Play("a", "b");
            Moonwell.Music.PlayOnce("win");
            Moonwell.Music.Stop(fadeOut: false);
            world.Advance(5.0f);
            Expect.Equal("play a|play win|play b|play a|stop fading|play win|stop at once|play a|play win|stop at once", string.Join("|", world.MusicCalls), "a stop ends the playlist and the file played once");
        }

        /// <summary>A playlist plays each file for its length, read on the first person's machine, and starts again from the first; the clock counts from the file's start, not from when its length arrived.</summary>
        public static void MusicPlaylistLoops(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.MusicLengths["a"] = 2000;
            world.MusicLengths["b"] = 3000;
            Moonwell.Music.Play("a", "b");
            Expect.Equal("play a", string.Join("|", world.MusicCalls), "the first file at once");
            world.Advance(1.9f);
            Expect.Equal(1, world.MusicCalls.Count, "a plays its 2 s");
            world.Advance(0.2f);
            world.Advance(3.0f);
            world.Advance(2.0f);
            Expect.Equal("play a|play b|play a|play b", string.Join("|", world.MusicCalls), "b after a, then a again");
        }

        /// <summary>A new Play or a Stop ends the playlist's clock: nothing of the old one plays after it.</summary>
        public static void MusicReplacedAndStopped(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.MusicLengths["a"] = 1000;
            world.MusicLengths["b"] = 1000;
            world.MusicLengths["c"] = 1000;
            Moonwell.Music.Play("a", "b");
            world.Advance(0.5f);
            Moonwell.Music.Play("c");
            world.Advance(2.1f);
            Expect.Equal("play a|play c|play c|play c", string.Join("|", world.MusicCalls), "c alone, again and again");
            Moonwell.Music.Stop();
            world.Advance(5.0f);
            Expect.Equal("play a|play c|play c|play c|stop fading", string.Join("|", world.MusicCalls), "nothing after the stop");
        }

        /// <summary>A shuffle takes the game's random numbers on every machine, and the music changes only for the player it is for, whose playlist goes on by itself.</summary>
        public static void MusicShuffleHeardBy(FakeWorld world)
        {
            world.LocalSlot = 0;
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            world.MusicLengths["a"] = 1000;
            world.MusicLengths["b"] = 1000;
            world.MusicLengths["c"] = 1000;
            var picks = 0;
            world.Pick = (low, high) =>
            {
                picks++;
                return low;
            };
            Moonwell.Music.Play(new[] { "a", "b", "c" }, shuffle: true, heardBy: Player.Two);
            Expect.Equal(2, picks, "the random numbers are taken on a machine that does not play it");
            Expect.Equal(0, world.MusicCalls.Count, "no music for another player");
            Moonwell.Music.Play(new[] { "a", "b", "c" }, shuffle: true, heardBy: Player.One);
            world.Advance(1.05f);
            world.Advance(1.0f);
            Expect.Equal("play b|play c|play a", string.Join("|", world.MusicCalls), "mixed once, in the order the numbers give, then on");
            Moonwell.Music.Stop(heardBy: Player.Two);
            world.Advance(1.0f);
            Expect.Equal("play b|play c|play a|play b", string.Join("|", world.MusicCalls), "another player's stop leaves this one's playlist");
        }

        /// <summary>A file the game has not got is reported, and the playlist moves on after a second.</summary>
        public static void MusicMissingFile(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.MusicLengths["b"] = 1000;
            Moonwell.Music.Play("a", "b");
            world.Advance(1.05f);
            Expect.Equal("play a|play b", string.Join("|", world.MusicCalls), "on to b after a second");
#if DEBUG
            Expect.True(world.Printed.Count == 1 && world.Printed[0].Contains("no music file a"), "the missing file is reported");
#endif
        }
    }
}
