#if DEBUG
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Leak tracking in a development build (decision 262): a scope that keeps growing, and ended objects still held. The raw
    /// handle counts are moonwell.lua's (MoonwellHandle*), as only Lua sees the natives.
    /// </summary>
    internal static class Leaks
    {
        /// <summary>The children a scope holds when it is first reported; it is reported again at each doubling.</summary>
        internal const int ScopeReportAt = 500;

        /// <summary>The ended objects still held when they are first reported; again at each doubling.</summary>
        internal const int HeldReportAt = 100;

        /// <summary>The seconds since its end before an object still held counts.</summary>
        internal const float HeldAge = 60.0f;

        /// <summary>The sweeps between two checks of the ended objects: the sweep runs every 0.25 s, so every 10 s.</summary>
        internal const int CheckEverySweeps = 40;

        private static int heldReportAt = HeldReportAt;
        private static int sweeps;

        /// <summary>The ended objects still held at the last check.</summary>
        internal static int Held { get; private set; }

        /// <summary>Forgets every count; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            heldReportAt = HeldReportAt;
            sweeps = 0;
            Held = 0;
            HeldObjects.Reset();
        }

        /// <summary>A scope's children reached the count it is reported at: reported as raised at the line that made the newest, with what it holds by kind, and reported again at twice as many.</summary>
        internal static void ScopeGrew(Scope scope)
        {
            scope.leakAt = scope.count * 2;
            Dev.Report(Names.NameOf(scope) + ".Leak", "holds " + DebugPanel.Count(scope.count) + " things it has not ended (" + KindsOf(scope) + "); something made in it is never ended");
        }

        /// <summary>An actor or component ended: kept weakly, to see whether it is still held a minute on.</summary>
        internal static void Ended(Scoped obj)
        {
            if (obj is Actor || obj is Component)
            {
                HeldObjects.Add(obj, Runtime.World.Now());
            }
        }

        /// <summary>Runs with every sweep: every 10 s it drops a canary and counts the ended objects still held, and reports them at 100 and each doubling.</summary>
        internal static void Swept()
        {
            sweeps++;
            if (sweeps % CheckEverySweeps != 0)
            {
                return;
            }

            var now = Runtime.World.Now();
            Held = HeldObjects.Count(now, HeldAge);
            HeldObjects.Canary(now);
            if (Held >= heldReportAt)
            {
                heldReportAt = Held * 2;
                Dev.Report("Moonwell.Leak", DebugPanel.Count(Held) + " objects that ended over a minute ago are still held by code (" + HeldObjects.Summary() + "); let go of what has ended");
            }
        }

        /// <summary>Starts counting the game's handles as natives make and destroy them, before Moonwell makes any: moonwell.debug.lua wraps the natives. .NET has none.</summary>
        /// @CSharpLua.Template = "MoonwellHandlesInstall()"
        internal static void CountHandles()
        {
        }

        /// <summary>"trigger 120, effect 40, location 12": the kinds of handle alive (made less destroyed), the most first; empty in .NET.</summary>
        /// @CSharpLua.Template = "MoonwellHandlesLine()"
        internal static string HandlesLine()
        {
            return string.Empty;
        }

        /// <summary>What a scope holds, by class, the most first: "Timer 480, Effect 20".</summary>
        internal static string KindsOf(Scope scope)
        {
            var names = new List<string>();
            var counts = new List<int>();
            for (var child = scope.newest; child != null; child = child.before)
            {
                var name = Names.NameOf(child);
                var at = names.IndexOf(name);
                if (at < 0)
                {
                    names.Add(name);
                    counts.Add(1);
                }
                else
                {
                    counts[at]++;
                }
            }

            return Kinds(names, counts);
        }

        /// <summary>Names and their counts as "Timer 480, Effect 20": the most first, then by name, at most five, and how many other kinds there are.</summary>
        internal static string Kinds(List<string> names, List<int> counts)
        {
            var order = new List<int>();
            for (var i = 0; i < names.Count; i++)
            {
                var at = 0;
                while (at < order.Count && (counts[order[at]] > counts[i] || (counts[order[at]] == counts[i] && string.CompareOrdinal(names[order[at]], names[i]) < 0)))
                {
                    at++;
                }

                order.Insert(at, i);
            }

            var text = string.Empty;
            for (var i = 0; i < order.Count && i < 5; i++)
            {
                text = text + (i > 0 ? ", " : string.Empty) + names[order[i]] + " " + DebugPanel.Count(counts[order[i]]);
            }

            if (order.Count > 5)
            {
                text = text + " and " + (order.Count - 5) + " other kinds";
            }

            return text;
        }
    }
}
#endif
