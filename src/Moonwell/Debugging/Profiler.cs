#if DEBUG
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>One line of the profiler: what was timed, how often, for how long, and its last ten seconds by the second.</summary>
    internal sealed class ProfileEntry
    {
        /// <summary>The seconds of the window the panel shows.</summary>
        internal const int Window = 10;

        internal ProfileEntry(string label)
        {
            Label = label;
        }

        internal string Label { get; }

        internal int Calls;

        internal float Seconds;

        /// <summary>The seconds spent in each of the last ten seconds of os.clock, at the second's place modulo ten.</summary>
        internal readonly float[] Buckets = new float[Window];

        /// <summary>The second each bucket holds.</summary>
        internal readonly int[] Stamps = new int[Window];
    }

    /// <summary>
    /// The profiler of a development build (decision 263): -profile starts and stops it. It times the map's reactions (every
    /// method of a map's class whose name starts with On, wrapped by moonwell.debug.lua while it runs), each timer by the C#
    /// line that made it, and the map's own sections (DebugMode.StartTiming). Times are os.clock's, in 1 ms steps, summed: a
    /// call reads 0 or 1 ms, and the sums come out right (game facts, 11-10). They are real time, and inclusive.
    /// </summary>
    internal static class Profiler
    {
        /// <summary>The lines the panel shows.</summary>
        internal const int PanelLines = 10;

        /// <summary>The file the stop writes, in CustomMapData.</summary>
        internal const string FilePath = "Moonwell\\profile.txt";

        private static Dictionary<string, ProfileEntry> entries = new Dictionary<string, ProfileEntry>();
        private static Dictionary<string, float> sections = new Dictionary<string, float>();
        private static float startedAt;

        /// <summary>Whether the profiler runs.</summary>
        internal static bool On { get; private set; }

        /// <summary>Stops without writing and forgets everything; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            if (On)
            {
                UnwrapReactions();
            }

            On = false;
            entries = new Dictionary<string, ProfileEntry>();
            sections = new Dictionary<string, float>();
        }

        /// <summary>-profile: starts, forgetting the last run's lines, or stops and writes every line to profile.txt.</summary>
        internal static void Toggle()
        {
            if (!On)
            {
                entries = new Dictionary<string, ProfileEntry>();
                sections = new Dictionary<string, float>();
                startedAt = DebugSession.Clock();
                On = true;
                WrapReactions();
                Game.Print("Profiler started: -profile again stops it and writes CustomMapData\\" + FilePath + ".");
                return;
            }

            On = false;
            UnwrapReactions();
            var lines = FileLines();
            DebugSession.WriteFile(FilePath, lines);
            Game.Print("Profiler stopped: " + (lines.Count - 2) + " lines written to CustomMapData\\" + FilePath + ".");
        }

        /// <summary>The entry of a label, made the first time; moonwell.debug.lua's wrappers keep theirs.</summary>
        internal static ProfileEntry EntryOf(string label)
        {
            if (!entries.TryGetValue(label, out var entry))
            {
                entry = new ProfileEntry(label);
                entries[label] = entry;
            }

            return entry;
        }

        /// <summary>Adds one call of so many seconds to an entry.</summary>
        internal static void Record(ProfileEntry entry, float seconds)
        {
            entry.Calls++;
            entry.Seconds += seconds;
            var second = (int)DebugSession.Clock();
            var at = second % ProfileEntry.Window;
            if (entry.Stamps[at] != second)
            {
                entry.Stamps[at] = second;
                entry.Buckets[at] = 0.0f;
            }

            entry.Buckets[at] += seconds;
        }

        /// <summary>Runs a timer's action, timed under the line that made it while the profiler runs.</summary>
        internal static void RunTimer(Timer timer)
        {
            if (!On)
            {
                timer.action();
                return;
            }

            var started = DebugSession.Clock();
            try
            {
                timer.action();
            }
            finally
            {
                if (timer.profileLabel == null)
                {
                    timer.profileLabel = LabelOf(timer);
                }

                Record(EntryOf(timer.profileLabel), DebugSession.Clock() - started);
            }
        }

        /// <summary>"Every 0.5 s at Spells/Rage.cs:12", or by its scope for a timer no line of the map made: "Moonwell: Game, every 0.25 s".</summary>
        internal static string LabelOf(Timer timer)
        {
            var kind = timer.IsPeriodic ? "Every " + DebugPanel.Tenths(timer.Interval) + " s" : "After";
            var line = LineName(timer.madeLine);
            if (line.Length > 0)
            {
                return kind + " at " + line;
            }

            var scope = timer.Parent != null ? Names.NameOf(timer.Parent) : "no scope";
            return "Moonwell: " + scope + ", " + kind.ToLower();
        }

        /// <summary>DebugMode.StartTiming: notes the time a map's section starts; a second start before its stop starts it again.</summary>
        internal static void StartTiming(string name)
        {
            if (On)
            {
                sections[name] = DebugSession.Clock();
            }
        }

        /// <summary>DebugMode.StopTiming: adds the time since the section's start, as one call; nothing without a start.</summary>
        internal static void StopTiming(string name)
        {
            if (On && sections.TryGetValue(name, out var started))
            {
                sections.Remove(name);
                Record(EntryOf(name), DebugSession.Clock() - started);
            }
        }

        /// <summary>The panel's lines: the ten that took the most over the last ten seconds, as milliseconds a second.</summary>
        internal static void AddLines(DebugLines lines)
        {
            var now = (int)DebugSession.Clock();
            var seconds = Clamp(now - startedAt, 1.0f, ProfileEntry.Window);
            lines.Add("PROFILE  " + (On ? "running" : "stopped") + " · ms a second over the last " + DebugPanel.Whole(seconds) + " s");
            var sorted = Sorted(entry => Recent(entry, now));
            for (var i = 0; i < sorted.Count && i < PanelLines; i++)
            {
                var entry = sorted[i];
                var recent = Recent(entry, now);
                if (recent <= 0.0f)
                {
                    break;
                }

                lines.Add(entry.Label + "  " + DebugPanel.Tenths(recent * 1000.0f / seconds) + " ms/s");
            }
        }

        /// <summary>The file's lines: a heading, then every entry by its whole time, the most first.</summary>
        internal static List<string> FileLines()
        {
            var seconds = Clamp(DebugSession.Clock() - startedAt, 0.001f, float.MaxValue);
            var lines = new List<string>();
            lines.Add("Moonwell profile: " + DebugPanel.Tenths(seconds) + " s of real time, times inclusive, from os.clock in 1 ms steps summed");
            lines.Add("what | calls | total ms | ms a second | average us");
            var sorted = Sorted(entry => entry.Seconds);
            for (var i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (entry.Calls == 0)
                {
                    // A wrapped reaction that never ran.
                    continue;
                }

                var average = entry.Calls > 0 ? entry.Seconds * 1000000.0f / entry.Calls : 0.0f;
                lines.Add(entry.Label + " | " + DebugPanel.Count(entry.Calls) + " | " + DebugPanel.Tenths(entry.Seconds * 1000.0f) + " | " + DebugPanel.Tenths(entry.Seconds * 1000.0f / seconds) + " | " + DebugPanel.Whole(average));
            }

            return lines;
        }

        /// <summary>Notes a class Moonwell has seen an object of, so its reactions are wrapped while the profiler runs; moonwell.debug.lua keeps them. .NET has nothing to wrap.</summary>
        /// @CSharpLua.Template = "MoonwellProfileSeen({0})"
        internal static void Seen(object obj)
        {
        }

        /// <summary>The script line of the code that called the method this is written in, 0 for none; .NET has none.</summary>
        /// @CSharpLua.Template = "MoonwellMadeLine()"
        internal static int MadeLine()
        {
            return 0;
        }

        /// <summary>"Spells/Rage.cs:12" for a script line, or empty; .NET has no lines.</summary>
        /// @CSharpLua.Template = "MoonwellLineName({0})"
        internal static string LineName(int line)
        {
            return string.Empty;
        }

        /// @CSharpLua.Template = "MoonwellProfileStart()"
        private static void WrapReactions()
        {
        }

        /// @CSharpLua.Template = "MoonwellProfileStop()"
        private static void UnwrapReactions()
        {
        }

        /// <summary>The seconds an entry took in the last ten seconds.</summary>
        private static float Recent(ProfileEntry entry, int now)
        {
            var sum = 0.0f;
            for (var i = 0; i < ProfileEntry.Window; i++)
            {
                if (now - entry.Stamps[i] < ProfileEntry.Window)
                {
                    sum += entry.Buckets[i];
                }
            }

            return sum;
        }

        /// <summary>The entries by a value, the most first, then by label.</summary>
        private static List<ProfileEntry> Sorted(System.Func<ProfileEntry, float> by)
        {
            var sorted = new List<ProfileEntry>();
            foreach (var entry in entries.Values)
            {
                var value = by(entry);
                var at = 0;
                while (at < sorted.Count && (by(sorted[at]) > value || (by(sorted[at]) == value && string.CompareOrdinal(sorted[at].Label, entry.Label) < 0)))
                {
                    at++;
                }

                sorted.Insert(at, entry);
            }

            return sorted;
        }

        private static float Clamp(float value, float low, float high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }
}
#endif
