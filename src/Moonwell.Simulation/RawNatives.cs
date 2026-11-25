#if !MOONWELL_LUA
using System;
using System.Diagnostics;

namespace Moonwell
{
    /// <summary>
    /// A raw native in the map's code, on .NET: Natives.X is the game's own, with no body outside the game, so a call throws
    /// BadImageFormatException there. A Simulation throws again in its place with the native's name and the way round it.
    /// </summary>
    internal static class RawNatives
    {
        /// <summary>The name of the native whose call failed in the error or an error inside it; null when no native's did.</summary>
        internal static string? NameOf(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                if (!(current is BadImageFormatException))
                {
                    continue;
                }

                if (current.TargetSite != null && current.TargetSite.DeclaringType == typeof(Natives))
                {
                    return current.TargetSite.Name;
                }

                var frames = new StackTrace(current, false).GetFrames();
                for (var i = 0; i < frames.Length; i++)
                {
                    var method = frames[i].GetMethod();
                    if (method != null && method.DeclaringType == typeof(Natives))
                    {
                        return method.Name;
                    }
                }
            }

            return null;
        }

        /// <summary>The error a Simulation throws for a native's failed call, the failure inside it; where names the reaction it failed in, empty for none.</summary>
        internal static InvalidOperationException Explained(string name, string where, Exception error)
        {
            var at = where.Length == 0 ? string.Empty : " (in " + where + ")";
            return new InvalidOperationException(
                "Natives." + name + " runs only in the game, so a simulation cannot call it" + at + ". Keep the call inside " +
                "#if MOONWELL_LUA, with what the simulation should do in #else; Moonwell's guide shows the way under Fast simulation.",
                error);
        }
    }
}
#endif
