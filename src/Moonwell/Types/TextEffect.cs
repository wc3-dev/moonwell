using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A colour effect that moves by itself, for a label's or a floating text's Effect: a rainbow turning or a sweep
    /// passing. Moonwell redraws every moving text 32 times a second on one timer, counting from when the effect was set,
    /// until the Effect is set to null or the text ends. The text's words stay as set, and its Color is not drawn.
    /// </summary>
    [LocalSafe]
    public sealed class TextEffect
    {
        /// <summary>The seconds between two redraws.</summary>
        internal const float RedrawEvery = 1.0f / 32.0f;

        private static readonly List<Func<bool>> redrawn = new List<Func<bool>>();

        private static Timer? redrawer;

        private readonly float seconds;

        private readonly float speed;

        private readonly Color main;

        private readonly Color band;

        private readonly SweepShape shape;

        private TextEffect(float seconds, float speed, Color main, Color band, SweepShape shape)
        {
            this.seconds = seconds;
            this.speed = speed;
            this.main = main;
            this.band = band;
            this.shape = shape;
        }

        /// <summary>The text in the colours of the rainbow, turning: the colours flow along the text, once round the colour wheel in the seconds.</summary>
        /// <exception cref="ArgumentException">Seconds not above 0.</exception>
        public static TextEffect Rainbow(float seconds = 2.0f)
        {
            if (!(seconds > 0.0f))
            {
                throw new ArgumentException("a rainbow turns in more than 0 seconds");
            }

            return new TextEffect(seconds, 0.0f, Color.White, Color.White, SweepShape.Narrow);
        }

        /// <summary>A band of the sweep colour passing through text of the main colour, again and again, at a speed in letters a second, as Text.Sweep draws it.</summary>
        /// <exception cref="ArgumentException">A speed not above 0.</exception>
        public static TextEffect Sweep(Color main, Color sweep, SweepShape shape = SweepShape.Narrow, float speed = 20.0f)
        {
            if (!(speed > 0.0f))
            {
                throw new ArgumentException("a sweep moves at more than 0 letters a second");
            }

            return new TextEffect(0.0f, speed, main, sweep, shape);
        }

        /// <summary>The text as the effect draws it the seconds after it was set.</summary>
        internal string Paint(string text, float elapsed)
        {
            // A rainbow's shift falls, so its colours flow towards the end of the text, the way it is read.
            return speed > 0.0f ? Text.Sweep(text, main, band, elapsed * speed / Text.SweepSpeed, shape) : Text.Rainbow(text, -elapsed / seconds);
        }

        /// <summary>Adds a redraw to the timer, which runs it every step until it answers false, and starts the timer when it is the first.</summary>
        internal static void Join(Func<bool> redraw)
        {
            redrawn.Add(redraw);
            redrawer ??= Game.Instance.OwnEvery(RedrawEvery, Step);
        }

        /// <summary>Forgets the redraws of the game before.</summary>
        internal static void Reset()
        {
            redrawn.Clear();
            redrawer = null;
        }

        /// <summary>Runs each redraw in the order they joined, drops those done, and ends the timer when none is left.</summary>
        private static void Step()
        {
            var kept = 0;
            var count = redrawn.Count;
            for (var i = 0; i < count; i++)
            {
                if (redrawn[i]())
                {
                    redrawn[kept] = redrawn[i];
                    kept++;
                }
            }

            // A redraw that joined during the loop is after count.
            for (var i = count; i < redrawn.Count; i++)
            {
                redrawn[kept] = redrawn[i];
                kept++;
            }

            redrawn.RemoveRange(kept, redrawn.Count - kept);
            if (redrawn.Count == 0 && redrawer != null)
            {
                redrawer.End();
                redrawer = null;
            }
        }
    }
}
