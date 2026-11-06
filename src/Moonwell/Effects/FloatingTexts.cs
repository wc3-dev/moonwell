using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The texts that follow a unit or last a time, stepped together on one timer of the game's while there are any.</summary>
    internal static class FloatingTexts
    {
        /// <summary>The seconds between two steps: follows a walking unit and fades smoothly.</summary>
        private const float Interval = 1.0f / 32.0f;

        private static readonly List<FloatingText> stepped = new List<FloatingText>();

        private static Timer? stepper;

        /// <summary>Forgets the texts of the game before.</summary>
        internal static void Reset()
        {
            stepped.Clear();
            stepper = null;
        }

        /// <summary>Adds a text to the steps, and starts the timer when it is the first.</summary>
        internal static void Join(FloatingText text)
        {
            text.stepping = true;
            stepped.Add(text);
            if (stepper == null)
            {
                stepper = Game.Instance.OwnEvery(Interval, Step);
            }
        }

        /// <summary>Steps each text, in the order they joined, drops those that need no more, and ends the timer when none is left.</summary>
        private static void Step()
        {
            var kept = 0;
            var count = stepped.Count;
            for (var i = 0; i < count; i++)
            {
                var text = stepped[i];
                if (text.Step())
                {
                    stepped[kept] = text;
                    kept++;
                }
                else
                {
                    text.stepping = false;
                }
            }

            // A text that joined during the loop, from a reaction to an End, is after count.
            for (var i = count; i < stepped.Count; i++)
            {
                stepped[kept] = stepped[i];
                kept++;
            }

            stepped.RemoveRange(kept, stepped.Count - kept);
            if (stepped.Count == 0 && stepper != null)
            {
                stepper.End();
                stepper = null;
            }
        }
    }
}
