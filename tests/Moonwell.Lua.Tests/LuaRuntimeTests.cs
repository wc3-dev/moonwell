using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moonwell.Testing;

namespace Moonwell.Lua.Tests;

/// <summary>
/// Every case compiled as a map's development build and as its release build, each run in Lua: a development build keeps the
/// runtime's Dev.Check calls, the development error path and the cases' #if DEBUG checks, which a release build leaves out.
/// </summary>
public class LuaRuntimeTests
{
    [Fact]
    public void Every_case_passes_in_Lua_in_a_development_build()
    {
        EveryCasePasses(release: false);
    }

    [Fact]
    public void Every_case_passes_in_Lua_in_a_release_build()
    {
        EveryCasePasses(release: true);
    }

    private static void EveryCasePasses(bool release)
    {
        var project = Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Testing", "Moonwell.Testing.csproj");
        // A folder of this test's own, so another run's copy in the temporary folder is never taken for one this compile left.
        var temp = Directory.CreateTempSubdirectory("moonwell-cases-");
        try
        {
            var script = LuaScript.Compile(project, release, temp.FullName);
            var lines = LuaScript.Run(script);

            Assert.Empty(temp.EnumerateFileSystemInfos());
            // A cast to a handle type checks nothing since the fork's 2.0.4-moonwell.15 (FormCases.HandleCasts): a check fails on
            // every handle in the game, where it is the game's userdata. The fake world's handles would pass it.
            Assert.DoesNotMatch(@"System\.cast\([\w.]*\bunit\b", script);
            // The game's Lua has 32-bit integers, so it reads the literal -2147483648 as a float, and a bitwise operator on it stops
            // the script (game facts, 11-04). Desktop Lua reads it as an integer. The fork writes -2^31 as (-2147483647 - 1) since
            // 2.0.4-moonwell.20. The compiled code starts at the first namespace; CoreSystem before it only compares with the literal.
            // Strings ("-2147483648" for JSON to parse), comments and floats (2147483648.0) are no integer literal, so they are left out.
            var compiled = Regex.Replace(script.Substring(script.IndexOf("System.namespace(\"", StringComparison.Ordinal)), @"""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)*'", "\"\"");
            compiled = Regex.Replace(compiled, @"--\[\[.*?\]\]|--[^\n]*", "", RegexOptions.Singleline);
            var intMinimum = Regex.Match(compiled, @"(?<![\d.])2147483648(?![\d.])");
            Assert.False(intMinimum.Success, intMinimum.Success ? compiled.Substring(Math.Max(0, intMinimum.Index - 300), 400) : "");
            var failures = lines.Where(line => line.StartsWith("FAIL", StringComparison.Ordinal)).ToList();
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
            // The debug mode's cases exist only in a development build (Cases.AddDebug).
            var count = release ? Cases.All().Count(testCase => !testCase.Name.StartsWith("debug.", StringComparison.Ordinal)) : Cases.All().Count;
            Assert.Equal("DONE " + count, lines.LastOrDefault());
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
