using System;

namespace Moonwell.Testing
{
    /// <summary>Cases that check frames' tweens: a value moved over time, eased, as a timer in the frame's scope.</summary>
    public static class TweenCases
    {
        private static void Within(float expected, float actual, float tolerance, string what)
        {
            Expect.True(Math.Abs(expected - actual) <= tolerance, what + ": " + expected + " expected, " + actual + " read");
        }

        /// <summary>MoveTo moves the offset over the seconds, in steps, and ends on it exactly; then it has stopped and Finished has run once.</summary>
        public static void Moves(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var tween = panel.MoveTo(new Vector2(0.1f, 0.2f), 1.0f);
            var finished = 0;
            tween.Finished += () => finished++;
            Expect.Equal(true, tween.IsRunning, "running once made");
            world.Advance(0.5f);
            Within(0.05f, panel.Offset.X, 0.005f, "halfway along x");
            Within(0.1f, panel.Offset.Y, 0.01f, "halfway along y");
            Within(0.05f, frames.FrameOf(panel.Handle!).Points[0].X, 0.005f, "the game's frame moved");
            Expect.Equal(0, finished, "not finished halfway");
            world.Advance(0.6f);
            Expect.Near(0.1f, panel.Offset.X, "at the end");
            Expect.Near(0.2f, frames.FrameOf(panel.Handle!).Points[0].Y, "the game's frame at the end");
            Expect.Equal(false, tween.IsRunning, "stopped at the end");
            Expect.Equal(1, finished, "Finished once");
            world.Advance(1.0f);
            Expect.Equal(1, finished, "not again");
        }

        /// <summary>The ease shapes the way: InQuad is a quarter of the way at half the time.</summary>
        public static void Eased(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            panel.FadeTo(0, 1.0f, Ease.InQuad);
            world.Advance(0.5f);
            Within(191.0f, panel.Alpha, 4.0f, "a quarter faded");
            world.Advance(0.6f);
            Expect.Equal(0, panel.Alpha, "faded out");
        }

        /// <summary>A new tween of a kind stops the frame's last one of that kind, from where it had got to; one of another kind goes on, and so does the general one.</summary>
        public static void NewStopsOld(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var first = panel.MoveTo(new Vector2(1.0f, 0.0f), 1.0f);
            var fade = panel.FadeTo(0, 1.0f);
            var steps = 0;
            var general = panel.Tween(1.0f, null, t => steps++);
            world.Advance(0.5f);
            var second = panel.MoveTo(new Vector2(0.0f, 0.0f), 0.5f);
            Expect.Equal(false, first.IsRunning, "the first move stopped");
            Expect.Equal(true, fade.IsRunning, "the fade goes on");
            Expect.Equal(true, general.IsRunning, "the general one goes on");
            world.Advance(0.25f);
            Within(0.25f, panel.Offset.X, 0.02f, "back from where the first had got to");
            world.Advance(1.0f);
            Expect.Near(0.0f, panel.Offset.X, "the second's end");
            Expect.Equal(false, second.IsRunning, "the second finished");
            Expect.True(steps >= 30, "the general one stepped about 32 times a second: " + steps);
        }

        /// <summary>Stop leaves the value where it is, and Finished does not run.</summary>
        public static void Stop(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var tween = panel.ScaleTo(2.0f, 1.0f);
            var finished = false;
            tween.Finished += () => finished = true;
            world.Advance(0.5f);
            tween.Stop();
            var scale = panel.Scale;
            world.Advance(1.0f);
            Expect.Near(scale, panel.Scale, "stayed where it stopped");
            Expect.Equal(false, finished, "no Finished");
        }

        /// <summary>A tween ends with its frame, with no error and no Finished.</summary>
        public static void EndsWithFrame(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var tween = panel.FadeTo(0, 1.0f);
            var finished = false;
            tween.Finished += () => finished = true;
            var printed = world.Printed.Count;
            world.Advance(0.5f);
            panel.End();
            world.Advance(1.0f);
            Expect.Equal(false, tween.IsRunning, "ended with the frame");
            Expect.Equal(false, finished, "no Finished");
            Expect.Equal(printed, world.Printed.Count, "nothing reported");
            var late = panel.FadeTo(255, 1.0f);
            Expect.Equal(false, late.IsRunning, "one made on an ended frame never runs");
        }

        /// <summary>No time, or less, finishes at the first step, so a Finished added after the call still runs.</summary>
        public static void NoTime(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var finished = 0;
            panel.FadeTo(0, 0.0f).Finished += () => finished++;
            panel.ScaleTo(3.0f, -1.0f);
            world.Advance(0.1f);
            Expect.Equal(0, panel.Alpha, "faded at once");
            Expect.Near(3.0f, panel.Scale, "scaled at once");
            Expect.Equal(1, finished, "Finished ran");
        }

        /// <summary>SizeTo and a label's ColorTo end on the values given; the general one ends with 1.</summary>
        public static void SizeColourAndGeneral(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel { Width = 0.1f, Height = 0.1f }, Game.Instance);
            var label = Screen.Add(new Label { Text = "hi" }, Game.Instance);
            panel.SizeTo(0.3f, 0.2f, 0.5f);
            label.ColorTo(new Color(255, 0, 0, 128), 0.5f);
            var last = -1.0f;
            panel.Tween(0.5f, Ease.OutQuad, t => last = t);
            world.Advance(0.25f);
            Within(0.2f, panel.Width, 0.02f, "half grown");
            world.Advance(0.5f);
            Expect.Near(0.3f, panel.Width, "its width");
            Expect.Near(0.2f, frames.FrameOf(panel.Handle!).Height, "the game's frame's height");
            Expect.Equal(0, label.Color.G, "the label's green");
            Expect.Equal(128, frames.FrameOf(label.Handle!).TextColor.A, "the game's text alpha");
            Expect.Near(1.0f, last, "the general one's last step");
        }

        /// <summary>A Finished handler that throws is reported with the tween, and the next still runs.</summary>
        public static void FinishedThrows(FakeWorld world)
        {
            FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var tween = panel.FadeTo(0, 0.1f);
            var second = false;
            tween.Finished += () => { throw new InvalidOperationException("boom"); };
            tween.Finished += () => second = true;
            var printed = world.Printed.Count;
            world.Advance(0.5f);
            Expect.True(world.Printed.Count > printed && world.Printed[printed].Contains("Tween.Finished"), "reported with the tween");
            Expect.Equal(true, second, "the next handler ran");
        }

        /// <summary>A tween goes on after a saved game loads, moving the frame made again.</summary>
        public static void AfterLoad(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            panel.MoveTo(new Vector2(0.2f, 0.0f), 1.0f);
            world.Advance(0.5f);
            var before = panel.Handle;
            frames.Load();
            Inbound.GameLoaded();
            world.Advance(1.0f);
            Expect.True(panel.Handle != before, "made again");
            Expect.Near(0.2f, frames.FrameOf(panel.Handle!).Points[0].X, "the frame made again moved to the end");
        }
    }
}
