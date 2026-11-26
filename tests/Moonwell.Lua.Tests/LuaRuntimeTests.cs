using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moonwell.Testing;

namespace Moonwell.Lua.Tests;

/// <summary>
/// Every case compiled as a map's development build and as its release build, each run in Lua: a development build keeps the
/// runtime's Dev.Check calls, the development error path and the cases' #if DEBUG checks, which a release build leaves out.
/// Each build is compiled and run once, the first time one of its rows asks, and each case is a row of its own per build, so a
/// failure names the case and the build (J186).
/// </summary>
// One at a time with PathSpeedTests: LuaScript keeps a run's printed lines, and the compiled script's copy, in one place.
[Collection("LuaScript")]
public class LuaRuntimeTests
{
    private const string Development = "development";

    private const string Release = "release";

    // The build's script and what its run printed, made once per build and shared by its rows.
    private static readonly Dictionary<string, Lazy<(string Script, List<string> Lines, bool TempEmpty)>> Runs = new()
    {
        [Development] = new Lazy<(string, List<string>, bool)>(() => CompileAndRun(release: false)),
        [Release] = new Lazy<(string, List<string>, bool)>(() => CompileAndRun(release: true)),
    };

    /// <summary>Each build's case names: a release build's leave out the debug mode's cases.</summary>
    public static TheoryData<string, string> EveryCase()
    {
        var data = new TheoryData<string, string>();
        foreach (var build in new[] { Development, Release })
        {
            foreach (var name in CaseNames(build == Release))
            {
                data.Add(build, name);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCase))]
    public void Each_case_passes_in_Lua(string build, string name)
    {
        var lines = Runs[build].Value.Lines;
        var line = lines.FirstOrDefault(one => one == "PASS " + name || one.StartsWith("FAIL " + name + ": ", StringComparison.Ordinal));

        Assert.True(line != null, $"{name} did not run in the {build} build's Lua: {lines.LastOrDefault()}");
        Assert.True(line.StartsWith("PASS ", StringComparison.Ordinal), line);
    }

    [Theory]
    [InlineData(Development)]
    [InlineData(Release)]
    public void The_script_runs_every_case_and_holds_nothing_the_game_cannot_run(string build)
    {
        var (script, lines, tempEmpty) = Runs[build].Value;

        Assert.True(tempEmpty, "the compile left files in its temporary folder");
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
        // Every case the script ran is a row of this build, so none runs without one.
        var rows = CaseNames(build == Release).ToHashSet(StringComparer.Ordinal);
        var unlisted = lines.Where(line => line.StartsWith("PASS ", StringComparison.Ordinal) || line.StartsWith("FAIL ", StringComparison.Ordinal))
            .Select(line => line.Substring(5).Split(": ")[0])
            .Where(name => !rows.Contains(name))
            .ToList();
        Assert.True(unlisted.Count == 0, "cases with no row: " + string.Join(", ", unlisted));
        Assert.Equal("DONE " + rows.Count, lines.LastOrDefault());
    }

    // The cases of a build. The debug mode's cases exist only in a development build (Cases.AddDebug), whatever this test was
    // built as, so they are read from the source the script is compiled from.
    private static IEnumerable<string> CaseNames(bool release)
    {
        var names = Cases.All().Select(testCase => testCase.Name).Where(name => !name.StartsWith("debug.", StringComparison.Ordinal));
        if (release)
        {
            return names;
        }

        var source = File.ReadAllText(Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Testing", "Cases.cs"));
        return names.Concat(Regex.Matches(source, @"Name = ""(debug\.[^""]+)""").Select(match => match.Groups[1].Value));
    }

    private static (string Script, List<string> Lines, bool TempEmpty) CompileAndRun(bool release)
    {
        var project = Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Testing", "Moonwell.Testing.csproj");
        // A folder of this build's own, so another run's copy in the temporary folder is never taken for one this compile left.
        var temp = Directory.CreateTempSubdirectory("moonwell-cases-");
        try
        {
            var script = LuaScript.Compile(project, release, temp.FullName);
            var lines = LuaScript.Run(script);
            return (script, lines, !temp.EnumerateFileSystemInfos().Any());
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
