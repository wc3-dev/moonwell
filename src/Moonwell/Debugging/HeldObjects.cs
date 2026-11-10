#if DEBUG
using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Ended objects kept weakly, to count those still held (decision 262). The collector runs on what is made, not on time: a
    /// quiet game collected nothing in 90 s (game facts, 11-10). So each check also drops a canary, and an object counts only
    /// once a canary dropped after its end has been collected: a full collection ran since, and the object outlived it. In Lua
    /// the tables are weak tables in moonwell.lua (MoonwellHeld*); .NET keeps weak references, so a test can collect.
    /// </summary>
    internal static class HeldObjects
    {
        private static List<WeakReference> objects = new List<WeakReference>();
        private static List<float> endedAt = new List<float>();
        private static List<WeakReference> canaries = new List<WeakReference>();
        private static List<float> canaryAt = new List<float>();
        private static float collectedAt = float.MinValue;
        private static string summary = string.Empty;

        /// <summary>Forgets everything; Leaks.Reset calls it.</summary>
        /// @CSharpLua.Template = "MoonwellHeldReset()"
        internal static void Reset()
        {
            objects = new List<WeakReference>();
            endedAt = new List<float>();
            canaries = new List<WeakReference>();
            canaryAt = new List<float>();
            collectedAt = float.MinValue;
            summary = string.Empty;
        }

        /// <summary>Keeps an object that ended, weakly, with the time it ended.</summary>
        /// @CSharpLua.Template = "MoonwellHeldAdd({0}, {1})"
        internal static void Add(object obj, float now)
        {
            objects.Add(new WeakReference(obj));
            endedAt.Add(now);
        }

        /// <summary>Drops a canary, held by nothing, at the time.</summary>
        /// @CSharpLua.Template = "MoonwellHeldCanary({0})"
        internal static void Canary(float now)
        {
            canaries.Add(new WeakReference(new object()));
            canaryAt.Add(now);
        }

        /// <summary>How many objects are still held that ended at least age seconds ago and before a canary that has been collected; Summary then names them by class.</summary>
        /// @CSharpLua.Template = "MoonwellHeldCount({0}, {1})"
        internal static int Count(float now, float age)
        {
            for (var i = canaries.Count - 1; i >= 0; i--)
            {
                if (!canaries[i].IsAlive)
                {
                    collectedAt = Math.Max(collectedAt, canaryAt[i]);
                    canaries.RemoveAt(i);
                    canaryAt.RemoveAt(i);
                }
            }

            var names = new List<string>();
            var counts = new List<int>();
            var total = 0;
            for (var i = objects.Count - 1; i >= 0; i--)
            {
                var obj = objects[i].Target;
                if (obj == null)
                {
                    objects.RemoveAt(i);
                    endedAt.RemoveAt(i);
                    continue;
                }

                if (endedAt[i] < collectedAt && now - endedAt[i] >= age)
                {
                    total++;
                    var name = Names.NameOf(obj);
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
            }

            summary = Leaks.Kinds(names, counts);
            return total;
        }

        /// <summary>The classes the last Count found, the most first: "Footman 120, Rage 30".</summary>
        /// @CSharpLua.Template = "MoonwellHeldSummary()"
        internal static string Summary()
        {
            return summary;
        }
    }
}
#endif
