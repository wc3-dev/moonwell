using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A frame's value moving over time, eased: made by a frame's MoveTo, SizeTo, ScaleTo, FadeTo or Tween, or a label's
    /// ColorTo. It steps 32 times a second on every machine, as a timer in the frame's scope, so it ends with the frame.
    /// </summary>
    public sealed class Tween
    {
        /// <summary>The seconds between steps.</summary>
        internal const float StepEvery = 1.0f / 32.0f;

        internal readonly Timer timer;

        internal readonly float start;

        internal readonly float seconds;

        internal readonly Func<float, float> ease;

        internal readonly Action<float> step;

        internal List<Action>? finished;

        internal Tween(Frame frame, float seconds, Func<float, float>? ease, Action<float> step)
        {
            start = Game.Time;
            this.seconds = seconds;
            this.ease = ease ?? Ease.Linear;
            this.step = step;
            timer = frame.Every(StepEvery, Step);
        }

        /// <summary>Whether it is still moving: false once it has finished, been stopped, or its frame has ended.</summary>
        [LocalSafe]
        public bool IsRunning
        {
            get { return !timer.HasEnded; }
        }

        /// <summary>Runs once the value has reached its end, not when the tween is stopped or its frame ends first.</summary>
        public event Action? Finished
        {
            add
            {
                if (value != null)
                {
                    (finished ??= new List<Action>()).Add(value);
                }
            }

            remove
            {
                if (value != null && finished != null)
                {
                    finished.Remove(value);
                }
            }
        }

        /// <summary>Stops it where it is; Finished does not run.</summary>
        public void Stop()
        {
            timer.End();
        }

        private void Step()
        {
            // Not more than 0, so NaN finishes at once too.
            var t = seconds > 0.0f ? (Game.Time - start) / seconds : 1.0f;
            var done = !(t < 1.0f);
            step(ease(done ? 1.0f : t));
            if (!done)
            {
                return;
            }

            timer.End();
            if (finished == null)
            {
                return;
            }

            var now = finished.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i]();
                }
                catch (Exception error)
                {
                    Errors.Report("Tween.Finished", error);
                }
            }
        }
    }
}
