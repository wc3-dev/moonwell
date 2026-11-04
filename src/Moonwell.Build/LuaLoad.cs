using System.Globalization;
using System.Text.RegularExpressions;
using KeraLua;

namespace Moonwell.Build
{
    /// <summary>
    /// Loads the compiled script in Lua without running it, as the game does first, so a script the game could not load fails the
    /// build. KeraLua's Lua is 5.4 and the game's 5.3; both allow 200 locals in a function (the workspace's game facts, 11-04).
    /// </summary>
    public static class LuaLoad
    {
        private static readonly Regex Marker = new Regex(@"^--@mw (?:(\d+):(\d+)|-)$", RegexOptions.CultureInvariant);

        private static readonly Regex Error = new Regex(@"^\[string ""[^""]*""\]:(\d+): (.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private static readonly Regex Break = new Regex(@"^break outside loop at line (\d+)", RegexOptions.CultureInvariant);

        /// <summary>Loads the script, which still has its <see cref="LineMarks"/> markers, and names the C# line of the marker above the failing line.</summary>
        /// <param name="script">The compiled script with its markers, before <see cref="LineTable.Apply"/>.</param>
        /// <param name="files">The marked files, relative to the project folder: the first is file 1.</param>
        /// <exception cref="BuildException">Lua does not load the script.</exception>
        public static void Check(string script, IReadOnlyList<string> files)
        {
            string message;
            using (var lua = new Lua())
            {
                if (lua.LoadString(script, "war3map.lua") == LuaStatus.OK)
                {
                    return;
                }

                message = lua.ToString(-1);
            }

            var error = Error.Match(message);
            if (!error.Success)
            {
                throw new BuildException("the game's Lua could not load the script CSharp.lua wrote (" + message + ")");
            }

            // Lua reports a break outside a loop at the end of its function, and names the break's own line in the message.
            var cause = error.Groups[2].Value;
            var stray = Break.Match(cause);
            var line = stray.Success ? stray.Groups[1].Value : error.Groups[1].Value;
            var where = Where(script, int.Parse(line, CultureInfo.InvariantCulture), files);
            throw new BuildException(cause.StartsWith("too many local variables", StringComparison.Ordinal)
                ? where + ": Lua allows 200 locals in a function, and the method around this line has more: each parameter, local variable and object or collection initializer is one, and so are some values the compiler keeps. Split the method"
                : where + ": the game's Lua could not load the script CSharp.lua wrote for this line (" + cause + ")");
        }

        // The file and C# line of the nearest marker at or above the line, or else the line in the script without its markers.
        private static string Where(string script, int failing, IReadOnlyList<string> files)
        {
            var lines = script.Split('\n');
            var markers = 0;
            (int File, int Line)? found = null;
            for (var index = 0; index < failing && index < lines.Length; index++)
            {
                var marker = Marker.Match(lines[index].Trim());
                if (marker.Success)
                {
                    markers++;
                    found = marker.Groups[1].Success
                        ? (int.Parse(marker.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(marker.Groups[2].Value, CultureInfo.InvariantCulture))
                        : null;
                }
            }

            return found is { } at && at.File >= 1 && at.File <= files.Count
                ? files[at.File - 1] + ":" + at.Line.ToString(CultureInfo.InvariantCulture)
                : "war3map.lua:" + (failing - markers).ToString(CultureInfo.InvariantCulture);
        }
    }
}
