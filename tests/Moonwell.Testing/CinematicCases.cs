using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check cinematics: the scene's steps, the watchers' screens, skipping with Esc, fades, and transmissions.</summary>
    public static class CinematicCases
    {
        private static List<string> steps = new List<string>();

        private static Unit NewFootman(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 100f, 50f);
            return Unit.Of(handle)!;
        }

        private static void TwoPeople(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            world.LocalSlot = 0;
        }

        // A scene of two lines, 2 s and 1 s, with a 1 s wait between, and an effect made on the cinematic.
        private static IEnumerable<Wait> Talk(Cinematic cine, Unit speaker)
        {
            steps.Add("start");
            yield return cine.Say(speaker, "We march at dawn.", name: "Arthas", seconds: 2f);
            steps.Add("said");
            yield return Wait.Seconds(1f);
            steps.Add("waited");
            yield return cine.Say(speaker, "As you command.", name: "Uther", seconds: 1f);
            steps.Add("end");
        }

        /// <summary>A scene starts in the instant after Play, waits for each line and wait in turn, and the cinematic ends with it; the screen is set for the cinematic and put back, and Ended runs once with false.</summary>
        public static void SceneRuns(FakeWorld world)
        {
            steps = new List<string>();
            var footman = NewFootman(world);
            var ended = new List<bool>();
            var cine = Cinematic.Play(c => Talk(c, footman), Game.Instance);
            cine.Ended += skipped => ended.Add(skipped);
            Expect.Equal("clear|letterbox on|controls off|sounds lowered", string.Join("|", world.ScreenCalls), "the screen set for the cinematic");
            Expect.Equal(0, steps.Count, "the scene has not started");
            Expect.Same(cine, Cinematic.Of(Player.One), "the cinematic player one watches");
            world.Advance(0.01f);
            Expect.Equal("start", string.Join("|", steps), "started in the instant after");
            Expect.True(world.ScreenCalls.Contains("scene Arthas: We march at dawn. 3.5/2"), "the first line, its portrait 1.5 s longer: " + string.Join("|", world.ScreenCalls));
            world.Advance(1.9f);
            Expect.Equal("start", string.Join("|", steps), "waits for the line");
            world.Advance(0.2f);
            Expect.Equal("start|said", string.Join("|", steps), "the line is over");
            world.Advance(1.0f);
            Expect.Equal("start|said|waited", string.Join("|", steps), "the wait is over");
            world.ScreenCalls.Clear();
            world.Advance(1.1f);
            Expect.Equal("start|said|waited|end", string.Join("|", steps), "the scene ran to its end");
            Expect.Equal(true, cine.HasEnded && !cine.IsSkipped, "ended, not skipped");
            Expect.Equal("False", string.Join("|", ended), "Ended ran once, with false");
            Expect.True(world.ScreenCalls.Contains("letterbox off") && world.ScreenCalls.Contains("controls on") && world.ScreenCalls.Contains("sounds back"), "the screen put back: " + string.Join("|", world.ScreenCalls));
            Expect.Same(null, Cinematic.Of(Player.One), "nothing playing");
            footman.Remove();
        }

        // A scene that waits long, making a timer on the cinematic.
        private static IEnumerable<Wait> Long(Cinematic cine)
        {
            steps.Add("start");
            cine.After(5f, () => steps.Add("timer"));
            yield return Wait.Seconds(10f);
            steps.Add("end");
        }

        /// <summary>Esc skips once every person watching has pressed it: the scene stops, what was made on the cinematic ends, and Ended runs with true.</summary>
        public static void EscSkipsWhenAllPressed(FakeWorld world)
        {
            steps = new List<string>();
            TwoPeople(world);
            var ended = new List<bool>();
            var cine = Cinematic.Play(Long, Game.Instance);
            cine.Ended += skipped => ended.Add(skipped);
            Expect.Equal("0|1", string.Join("|", world.ListenedEsc), "each person's Esc is listened for");
            world.Advance(1f);
            world.Esc(Player.One);
            Expect.Equal(false, cine.HasEnded, "one of two pressed");
            world.Esc(Player.One);
            Expect.Equal(false, cine.HasEnded, "the same one again");
            world.Esc(Player.Two);
            Expect.Equal(true, cine.HasEnded && cine.IsSkipped, "skipped when both pressed");
            Expect.Equal("True", string.Join("|", ended), "Ended ran with true");
            world.Advance(10f);
            Expect.Equal("start", string.Join("|", steps), "neither the rest of the scene nor its timer ran");
            Cinematic.Play(Long, Game.Instance);
            Expect.Equal("0|1", string.Join("|", world.ListenedEsc), "listened for once");
        }

        /// <summary>A cinematic that is not skippable ignores Esc, but Skip ends it as a skip.</summary>
        public static void NotSkippable(FakeWorld world)
        {
            steps = new List<string>();
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var cine = Cinematic.Play(Long, Game.Instance, skippable: false);
            world.Advance(1f);
            world.Esc(Player.One);
            Expect.Equal(false, cine.HasEnded, "Esc ignored");
            cine.Skip();
            Expect.Equal(true, cine.HasEnded && cine.IsSkipped, "Skip skips");
        }

        /// <summary>A cinematic for another player changes nothing on this screen, and only that player's Esc counts.</summary>
        public static void ShownTo(FakeWorld world)
        {
            steps = new List<string>();
            TwoPeople(world);
            var cine = Cinematic.Play(Long, Game.Instance, shownTo: Player.Two);
            world.Advance(1f);
            Expect.Equal(0, world.ScreenCalls.Count, "nothing on this screen");
            Expect.Same(null, Cinematic.Of(Player.One), "player one watches nothing");
            Expect.Same(cine, Cinematic.Of(Player.Two), "player two watches it");
            world.Esc(Player.One);
            Expect.Equal(false, cine.HasEnded, "another's Esc does not count");
            world.Esc(Player.Two);
            Expect.Equal(true, cine.IsSkipped, "its watcher's Esc skips it");
            Expect.Equal(0, world.ScreenCalls.Count, "still nothing on this screen");
        }

        /// <summary>Without the letterbox, the HUD stays; a second cinematic for the same player ends the first.</summary>
        public static void OneAtATime(FakeWorld world)
        {
            steps = new List<string>();
            var first = Cinematic.Play(Long, Game.Instance, letterbox: false);
            Expect.Equal(false, world.ScreenCalls.Contains("letterbox on"), "no letterbox");
            var second = Cinematic.Play(Long, Game.Instance);
            Expect.Equal(true, first.HasEnded && !first.IsSkipped, "the first ended, not skipped");
            Expect.Equal(false, second.HasEnded, "the second plays");
            Expect.Same(second, Cinematic.Of(Player.One), "the one watched");
        }

        // Fades out, waits, and ends faded out.
        private static IEnumerable<Wait> Dark(Cinematic cine)
        {
            yield return cine.FadeIn(1f);
            yield return cine.FadeOut(1f);
        }

        /// <summary>FadeIn uncovers the screen and takes the cover away after; a cinematic that ends faded out fades back in over 1 s.</summary>
        public static void Fades(FakeWorld world)
        {
            var cine = Cinematic.Play(Dark, Game.Instance);
            world.Advance(0.01f);
            Expect.True(world.ScreenCalls.Contains("filter 255 to 0"), "faded in: " + string.Join("|", world.ScreenCalls));
            world.Advance(1.0f);
            Expect.True(world.ScreenCalls.Contains("filter off"), "the cover taken away after the fade in");
            Expect.True(world.ScreenCalls.Contains("filter 0 to 255"), "faded out");
            world.ScreenCalls.Clear();
            world.Advance(1.0f);
            Expect.Equal(true, cine.HasEnded, "ended");
            Expect.True(world.ScreenCalls.Contains("filter 255 to 0"), "fades back in at the end: " + string.Join("|", world.ScreenCalls));
            Expect.Equal(false, world.ScreenCalls.Contains("filter off"), "not taken away yet");
            world.Advance(1.0f);
            Expect.True(world.ScreenCalls.Contains("filter off"), "taken away after the fade back");
        }

        private static IEnumerable<Wait> Broken(Cinematic cine)
        {
            yield return Wait.Seconds(1f);
            throw new InvalidOperationException("broken");
        }

        /// <summary>A scene that throws is reported, and the cinematic ends, the screen put back.</summary>
        public static void SceneThrows(FakeWorld world)
        {
            var cine = Cinematic.Play(Broken, Game.Instance);
            world.Advance(1.1f);
            Expect.Equal(true, cine.HasEnded, "ended");
            Expect.True(world.Logged.Count > 0 && world.Logged[0].Contains("broken"), "reported");
            Expect.True(world.ScreenCalls.Contains("controls on"), "the screen put back");
        }

        /// <summary>A transmission with a voice lasts the voice's length, read on the first person's machine; the voice plays and stops at its end; a line for another player shows nothing here and its voice is silent.</summary>
        public static void TransmissionVoice(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.MusicLengths["v.wav"] = 3000;
            var footman = NewFootman(world);
            var line = Transmission.Play(footman, "Hold the bridge!", "v.wav", name: "Uther");
            world.Advance(0.01f);
            Expect.True(world.ScreenCalls.Contains("scene Uther: Hold the bridge! 4.5/3"), "the voice's length: " + string.Join("|", world.ScreenCalls));
            Expect.True(world.ScreenCalls.Contains("mark"), "the speaker marked");
            Expect.Equal(true, world.Sounds[0].Started && world.Sounds[0].Volume == 127, "the voice plays");
            Expect.Equal(false, line.IsOver, "still on");
            world.Advance(3.0f);
            Expect.Equal(true, line.IsOver, "over after 3 s");
            Expect.Equal(true, world.Sounds[0].Stopped, "the voice stopped");
            Expect.Equal(false, world.ScreenCalls.Contains("end scene"), "the portrait stays its 1.5 s");
            world.ScreenCalls.Clear();
            Transmission.Play(footman, "Psst.", "v.wav", shownTo: Player.Two);
            Expect.Equal(0, world.ScreenCalls.Count, "nothing here for another player's line");
            Expect.Equal(0, world.Sounds[1].Volume, "its voice silent here");
            ObjectTypes.Unit(ObjectTypes.FourCC("H001"), () => new TestHero());
            Transmission.Play<TestHero>("Captain", "No voice.", color: PlayerColor.Blue);
            Expect.True(world.ScreenCalls.Contains("scene Captain: No voice. 3.95/2.45"), "2 s and 0.05 s a letter: " + string.Join("|", world.ScreenCalls));
            footman.Remove();
        }

        private static IEnumerable<Wait> Interrupted(Cinematic cine, Unit speaker)
        {
            yield return cine.Say(speaker, "A long line.", name: "Uther", seconds: 10f);
            steps.Add("after");
        }

        /// <summary>A skip ends the cinematic's line at once: its portrait goes.</summary>
        public static void SkipEndsLine(FakeWorld world)
        {
            steps = new List<string>();
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var footman = NewFootman(world);
            var cine = Cinematic.Play(c => Interrupted(c, footman), Game.Instance);
            world.Advance(1f);
            world.ScreenCalls.Clear();
            world.Esc(Player.One);
            Expect.Equal(true, cine.IsSkipped, "skipped");
            Expect.True(world.ScreenCalls.Contains("end scene"), "the portrait ended: " + string.Join("|", world.ScreenCalls));
            world.Advance(10f);
            Expect.Equal(0, steps.Count, "the scene did not go on");
            footman.Remove();
        }

        /// <summary>A press of Esc in the instant a cinematic starts, as when another Esc trigger started it with that press, does not skip it; the next press does (the 0.8 gate, I38).</summary>
        public static void PressThatStartsIt(FakeWorld world)
        {
            steps = new List<string>();
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var cine = Cinematic.Play(Long, Game.Instance);
            world.Esc(Player.One);
            Expect.Equal(false, cine.HasEnded, "the press that started it is not a skip");
            world.Advance(0.01f);
            Expect.Equal("start", string.Join("|", steps), "its scene started");
            world.Esc(Player.One);
            Expect.Equal(true, cine.IsSkipped, "the next press skips it");
        }

        private static IEnumerable<Wait> SaysOne(Cinematic cine, Unit speaker)
        {
            var line = cine.Say(speaker, "Hold.", seconds: 1f);
            yield return line;
            steps.Add("over " + line.IsOver);
        }

        /// <summary>A scene that waited for a line reads it over when it goes on.</summary>
        public static void LineOverWhenResumed(FakeWorld world)
        {
            steps = new List<string>();
            var footman = NewFootman(world);
            Cinematic.Play(c => SaysOne(c, footman), Game.Instance);
            world.Advance(1.1f);
            Expect.Equal("over True", string.Join("|", steps), "the line reads over");
            footman.Remove();
        }

        /// <summary>While a cinematic's letterbox is on this screen, the frames over the HUD hide, but those made on the cinematic; the frames beneath are the game's to hide; at the end they show again.</summary>
        public static void HidesOverFrames(FakeWorld world)
        {
            steps = new List<string>();
            var frames = FrameCases.Installed();
            var hud = Screen.AddOver(new Panel(), Game.Instance);
            var beneath = Screen.Add(new Panel(), Game.Instance);
            Cinematic.Play(Long, Game.Instance, letterbox: false);
            Expect.Equal(true, frames.FrameOf(hud.Handle!).Shown, "shown in a cinematic with no letterbox");
            var cine = Cinematic.Play(Long, Game.Instance);
            Expect.Equal(false, frames.FrameOf(hud.Handle!).Shown, "hidden in the letterbox");
            Expect.Equal(true, frames.FrameOf(beneath.Handle!).Shown, "a frame beneath the HUD left to the game");
            var hint = Screen.AddOver(new Panel(), cine);
            var late = Screen.AddOver(new Panel(), Game.Instance);
            Expect.Equal(true, frames.FrameOf(hint.Handle!).Shown, "a frame made on the cinematic shows");
            Expect.Equal(false, frames.FrameOf(late.Handle!).Shown, "one added during it is hidden too");
            hud.Visible = true;
            Expect.Equal(false, frames.FrameOf(hud.Handle!).Shown, "still hidden when set visible");
            cine.Skip();
            Expect.Equal(true, frames.FrameOf(hud.Handle!).Shown && frames.FrameOf(late.Handle!).Shown, "shown again at the end");
            Expect.Equal(true, hint.HasEnded, "the cinematic's frame ended with it");
        }
    }
}
