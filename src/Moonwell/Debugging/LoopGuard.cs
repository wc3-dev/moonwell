#if DEBUG
namespace Moonwell
{
    /// <summary>
    /// The loop guard of a development build; its counting is in moonwell.debug.lua. The build writes a call at the start of
    /// every loop body of the map's own code (LineMarks in Moonwell.Build), which counts the turns run while the game's time
    /// stands still: at 10,000,000 the loop is stopped by an error raised at its line, where nothing else ends a Lua call and
    /// the game would stay frozen. Moonwell's own packages are not counted.
    /// </summary>
    internal static class LoopGuard
    {
        /// <summary>Stops the loop whose turn the count ended on: the error names its line, and is reported where the reaction, timer or other call it runs in is guarded.</summary>
        internal static void Stop()
        {
            throw new MoonwellException("more than 10000000 loop turns in one instant; the loop was stopped");
        }

        /// <summary>Keeps the count as it is before a Local.For action, whose turns only one machine runs. .NET counts none.</summary>
        /// @CSharpLua.Template = "MoonwellLoopHold()"
        internal static void Hold()
        {
        }

        /// <summary>Puts the count back as Hold kept it, so every machine stops a loop at the same turn.</summary>
        /// @CSharpLua.Template = "MoonwellLoopRelease()"
        internal static void Release()
        {
        }

        /// <summary>Starts the count again; Runtime.Reset calls it.</summary>
        /// @CSharpLua.Template = "MoonwellLoopReset()"
        internal static void Reset()
        {
        }
    }
}
#endif
