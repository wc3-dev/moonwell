using System;

namespace Moonwell
{
    /// <summary>
    /// Runs the game components' OnDawn and OnDusk when Game.IsDay changes: checked every 0.25 s, and at once when
    /// Game.TimeOfDay is set (decision 190). The game's own time-of-day event is not used: once it missed a pass, and
    /// inside it at dusk the time can read a float step under the hour, where IsDay still reads day (game facts, 11-05).
    /// </summary>
    internal static class DayChange
    {
        /// <summary>Whether it was day when last checked; null until a dawn or dusk reaction is needed.</summary>
        private static bool? wasDay;

        /// <summary>Remembers whether it is day and starts the checks, once; Events calls it as a dawn or dusk reaction is needed.</summary>
        internal static void Start()
        {
            if (wasDay != null)
            {
                return;
            }

            wasDay = Game.IsDay;
            Game.Instance.Every(0.25f, Check);
        }

        /// <summary>Runs a reaction if day turned to night or back since the last check; Game.TimeOfDay calls it after a set.</summary>
        internal static void Check()
        {
            if (wasDay == null)
            {
                return;
            }

            var isDay = Game.IsDay;
            if (isDay == wasDay)
            {
                return;
            }

            wasDay = isDay;
            var bit = isDay ? Hook.OnDawn : Hook.OnDusk;
            var game = Game.Instance;
            var count = game.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                if (wasDay != isDay)
                {
                    // A reaction set the time across the hour: the check its set ran has told every component.
                    break;
                }

                var component = game.ComponentAt(i) as GameComponent;
                if (component == null || component.HasEnded || (component.Mask & bit) == 0)
                {
                    continue;
                }

                try
                {
                    if (isDay)
                    {
                        component.OnDawn();
                    }
                    else
                    {
                        component.OnDusk();
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, (isDay ? ".OnDawn" : ".OnDusk"), error);
                }
            }

            game.EndDispatch();
        }

        /// <summary>Forgets the day; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            wasDay = null;
        }
    }
}
