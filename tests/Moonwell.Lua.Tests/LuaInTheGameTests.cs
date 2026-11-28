using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moonwell.GameRun;
using Xunit.Abstractions;

namespace Moonwell.Lua.Tests;

/// <summary>A theory that starts the game: skipped unless MOONWELL_LAUNCH=1 and the game is installed (GameRunOptions.WhyNoLaunch).</summary>
public sealed class LaunchTheoryAttribute : TheoryAttribute
{
    public LaunchTheoryAttribute()
    {
        if (GameRunOptions.WhyNoLaunch() is { } why)
        {
            Skip = why;
        }
    }
}

/// <summary>
/// Every case of <see cref="LuaRuntimeTests"/>, compiled the same way, played in the game's own Lua with nobody at the keyboard
/// (J187): Lua 5.3 with 32-bit integers and single floats, which the tests' desktop Lua cannot show. The script is packed with
/// the test map into a map whose main runs the cases once the loading screen is past and writes each failure, the count of
/// passes, the cases' last line (DONE n) and the over line to a run file; Moonwell.GameRun plays it. Each build is played once,
/// the first time one of its tests asks.
/// </summary>
[Collection("LuaScript")]
public class LuaInTheGameTests
{
    private const string RunFileName = "MoonwellTests\\Cases.txt";

    // The cases that read Lua's memory through the tests' host (LuaHostCollectGarbage, LuaScript.Run): the game's Lua has no
    // collectgarbage, so they cannot pass there.
    private static readonly HashSet<string> HostOnly = new(StringComparer.Ordinal)
    {
        "maths.lua.miss-makes-nothing",
        "terrain.raycast.miss-makes-nothing",
        "terrain.pathing.full-grid-memory",
        "debug.leaks.held",
        "debug.leaks.ended-pages-let-go",
        "debug.leaks.ended-pages-let-go-while-type-reads",
        "debug.leaks.held-names-holder",
    };

    private static readonly Dictionary<string, Lazy<GameRunResult>> Runs = new()
    {
        ["development"] = new Lazy<GameRunResult>(() => Play(release: false)),
        ["release"] = new Lazy<GameRunResult>(() => Play(release: true)),
    };

    private readonly ITestOutputHelper output;

    public LuaInTheGameTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [LaunchTheory]
    [InlineData("development")]
    [InlineData("release")]
    public void Every_case_runs_in_the_games_Lua(string build)
    {
        var result = Runs[build].Value;
        foreach (var line in result.Lines)
        {
            output.WriteLine(line);
        }

        output.WriteLine(result.Verdict);
        output.WriteLine(result.Detail);
        Assert.True(result.Outcome == RunOutcome.Over, result.Verdict);
        Assert.DoesNotContain(result.Lines, line => line.StartsWith("ERROR", StringComparison.Ordinal));
        Assert.Contains("DONE " + LuaRuntimeTests.CaseNames(build == "release").Count(), result.Lines);
    }

    [LaunchTheory]
    [InlineData("development")]
    [InlineData("release")]
    public void Each_case_passes_in_the_games_Lua(string build)
    {
        var result = Runs[build].Value;
        Assert.True(result.Outcome == RunOutcome.Over, result.Verdict);

        // my call: one row per build that names every failing case, not a row per case, which would add 4,400 skipped rows to
        // every run without MOONWELL_LAUNCH.
        var failing = result.Lines
            .Where(line => line.StartsWith("FAIL ", StringComparison.Ordinal))
            .Where(line => !HostOnly.Contains(line.Substring(5).Split(": ")[0]))
            .ToList();
        Assert.True(failing.Count == 0, failing.Count + " case(s) fail in the game's Lua:\n" + string.Join("\n", failing));
    }

    private static GameRunResult Play(bool release)
    {
        var project = Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Testing", "Moonwell.Testing.csproj");
        var temp = Directory.CreateTempSubdirectory("moonwell-cases-game-");
        // The game loads no map whose path is long, and none named by a short (8.3) path's form: a folder of the runner's own.
        var maps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moonwell", "runs");
        var archive = Path.Combine(maps, "cases-" + (release ? "release" : "development") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".w3x");
        try
        {
            var script = LuaScript.Compile(project, release, temp.FullName) + RunInTheGame;
            var staged = Path.Combine(temp.FullName, "staged");
            Directory.CreateDirectory(staged);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Lua.Tests", "map")))
            {
                File.Copy(file, Path.Combine(staged, Path.GetFileName(file)));
            }

            File.WriteAllText(Path.Combine(staged, "war3map.lua"), script);
            Directory.CreateDirectory(maps);
            Moonwell.Build.Archive.Pack(staged, archive);

            return GameRunner.Run(new GameRunOptions
            {
                Map = archive,
                File = RunFileName,
                Rules = new RunRules { Limit = TimeSpan.FromMinutes(8), Stall = TimeSpan.FromMinutes(4) },
            });
        }
        finally
        {
            temp.Delete(true);
            File.Delete(archive);
        }
    }

    // Appended to the cases' script: main runs the cases once the loading screen is past (a zero-second timer runs only then),
    // and writes the run file: each FAIL line, the count of PASS lines, an ERROR line when the cases stopped, the last line they
    // printed (DONE n) and the over line. A line keeps to one Preload call: no line break or double quote, at most 240 characters.
    private const string RunInTheGame = """


        do
          local casesMain = main
          local function clean(text)
            text = string.gsub(tostring(text), "[\r\n]+", " | ")
            text = string.gsub(text, '"', "'")
            if #text > 240 then
              text = string.sub(text, 1, 240) .. "..."
            end
            return text
          end
          local function write(lines)
            PreloadGenClear()
            PreloadGenStart()
            for i = 1, #lines do
              Preload("\")\n" .. lines[i] .. "\n//")
            end
            PreloadGenEnd("MoonwellTests\\Cases.txt")
          end
          function main()
            TimerStart(CreateTimer(), 0.0, false, function()
              local passed, failing, last = 0, {}, ""
              local gamePrint = print
              print = function(line)
                line = tostring(line)
                last = line
                if string.sub(line, 1, 5) == "PASS " then
                  passed = passed + 1
                elseif string.sub(line, 1, 5) == "FAIL " then
                  failing[#failing + 1] = clean(line)
                end
              end
              -- As the tests' host has it (LuaScript.Run): the cases compare logged errors with .NET's, which have no C# location.
              MoonwellLines = { files = {}, map = "" }
              local ok, problem = pcall(casesMain)
              print = gamePrint
              local out = {}
              for i = 1, #failing do
                out[#out + 1] = failing[i]
              end
              out[#out + 1] = "passed " .. passed
              if not ok then
                out[#out + 1] = "ERROR the cases stopped: " .. clean(problem)
              end
              out[#out + 1] = clean(last)
              out[#out + 1] = "-- run over"
              write(out)
            end)
          end
        end

        """;
}
