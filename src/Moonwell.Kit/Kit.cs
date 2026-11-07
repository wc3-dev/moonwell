using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The UI kit's setting for the whole map (decision 212).</summary>
    public static class Kit
    {
        private static Theme? theme;

        /// <summary>The look of the kit's windows and tooltips made from now on, unless one is given its own; Theme.PlayersRace unless set.</summary>
        [LocalSafe]
        public static Theme Theme
        {
            get { return theme ?? Theme.PlayersRace; }
            set { theme = value; }
        }

        /// <summary>Runs each handler with two values, in the order added; one that throws is reported with the frame and the event, and the rest still run.</summary>
        internal static void Raise<TA, TB>(Frame frame, List<Action<Player, TA, TB>>? handlers, Player who, TA a, TB b, string name)
        {
            if (handlers == null || frame.HasEnded)
            {
                return;
            }

            var now = handlers.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i](who, a, b);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(frame) + "." + name, error);
                }
            }
        }
    }
}
