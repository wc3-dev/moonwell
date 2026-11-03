using System;
using System.IO;
using System.Linq;
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
        var copies = Copies();
        var script = LuaScript.Compile(project, release);
        var lines = LuaScript.Run(script);

        Assert.Empty(Copies().Except(copies));
        // A cast to a handle type checks nothing since the fork's 2.0.4-moonwell.15 (FormCases.HandleCasts): a check fails on
        // every handle in the game, where it is the game's userdata. The fake world's handles would pass it.
        Assert.DoesNotMatch(@"System\.cast\([\w.]*\bunit\b", script);
        var failures = lines.Where(line => line.StartsWith("FAIL", StringComparison.Ordinal)).ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        // The debug mode's cases exist only in a development build (Cases.AddDebug).
        var count = release ? Cases.All().Count(testCase => !testCase.Name.StartsWith("debug.", StringComparison.Ordinal)) : Cases.All().Count;
        Assert.Equal("DONE " + count, lines.LastOrDefault());
    }

    // The copies of the project a compile of the cases made and left in the temporary folder.
    private static string[] Copies()
    {
        return Directory.GetDirectories(Path.GetTempPath(), "moonwell-lua-*");
    }
}
