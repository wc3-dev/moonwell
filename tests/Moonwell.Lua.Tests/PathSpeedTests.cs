using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Moonwell.Testing;
using Xunit.Abstractions;

namespace Moonwell.Lua.Tests;

/// <summary>
/// Times the path search on PathMeasure's long routes (backlog J173), in Lua as a development build runs it and in .NET,
/// and checks both give the routes the search gave before it was made faster. Each route is found once on cells not yet
/// asked (cold, as a map's first search) and then Warm times more (warm, as every later one); the times are written to the
/// test's output (dotnet test --logger "console;verbosity=detailed"). NLua's Lua 5.4 on a desktop is not the game's Lua,
/// so the times compare one search with another, not with the game's 84 ms.
/// </summary>
public class PathSpeedTests
{
    private const int Warm = 5;

    /// <summary>The routes the search gave before J173, which a faster search keeps.</summary>
    private static readonly string[] Expected =
    {
        "reaches -2500,-1000 2500,-1000",
        "reaches -2500,-2500 2500,500",
        "reaches -2500,0 48,-528 2500,0",
        "reaches -2500,-2600 2500,-2600",
    };

    private readonly ITestOutputHelper output;

    public PathSpeedTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Long_routes_are_timed_in_Lua_and_in_NET_and_keep_their_points()
    {
        var count = PathMeasure.Count;
        var net = new List<string>();
        PathMeasure.Setup();
        for (var i = 0; i < count; i++)
        {
            var watch = Stopwatch.StartNew();
            var route = PathMeasure.Route(i);
            var cold = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            for (var k = 0; k < Warm; k++)
            {
                PathMeasure.Route(i);
            }

            var warm = watch.Elapsed.TotalMilliseconds / Warm;
            net.Add(route);
            output.WriteLine($".NET {i} ({PathMeasure.Name(i)}): cold {cold:0.0} ms, warm {warm:0.0} ms");
        }

        var project = Path.Combine(LuaScript.RepoRoot, "tests", "Moonwell.Testing", "Moonwell.Testing.csproj");
        var temp = Directory.CreateTempSubdirectory("moonwell-pathspeed-");
        List<string> lines;
        try
        {
            var script = LuaScript.Compile(project, release: false, temp.FullName);
            // The script's entry runs the measure instead of every case.
            const string entry = "Moonwell.Testing.Cases.Main()";
            Assert.Contains(entry, script);
            var measure = $$"""
                local M = Moonwell.Testing.PathMeasure
                M.Setup()
                for i = 0, {{count - 1}} do
                  local t = os.clock()
                  local route = M.Route(i)
                  local cold = os.clock() - t
                  t = os.clock()
                  for k = 1, {{Warm}} do M.Route(i) end
                  print(string.format("TIME %d %.1f %.1f", i, cold * 1000, (os.clock() - t) * 1000 / {{Warm}}))
                  print("ROUTE " .. route)
                end
                """;
            lines = LuaScript.Run(script.Replace(entry, measure, StringComparison.Ordinal));
        }
        finally
        {
            temp.Delete(true);
        }

        var lua = lines.Where(line => line.StartsWith("ROUTE ", StringComparison.Ordinal)).Select(line => line.Substring(6)).ToList();
        foreach (var line in lines.Where(line => line.StartsWith("TIME ", StringComparison.Ordinal)))
        {
            var parts = line.Split(' ');
            output.WriteLine($"Lua {parts[1]} ({PathMeasure.Name(int.Parse(parts[1]))}): cold {parts[2]} ms, warm {parts[3]} ms");
        }

        for (var i = 0; i < count; i++)
        {
            output.WriteLine($"route {i}: {net[i]}");
        }

        Assert.Equal(net, lua);
        Assert.Equal(Expected, net);
    }
}
