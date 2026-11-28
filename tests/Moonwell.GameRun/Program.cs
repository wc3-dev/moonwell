using System.Globalization;
using System.Text;
using Moonwell.GameRun;

// moonwell-gamerun <map.w3x> --file <run file> [options]: plays the map in the game with nobody at the keyboard, prints the
// run's lines and then one verdict line, and exits 0 when it passed, 1 when it failed, 2 for arguments it does not
// understand and 3 when it could not play (no game, no map, another game that did not end).
Console.OutputEncoding = new UTF8Encoding(false);
const string Usage = """
    usage: Moonwell.GameRun <map.w3x> --file <run file> [--over <line>] [--loading <line>] [--fail <start>]...
                            [--limit <s>] [--load-limit <s>] [--stall <s>] [--wait-free <s>] [--game <exe>] [--docs <folder>]
      --file        the run's Preload file, relative to Documents\Warcraft III\CustomMapData
      --over        the line that ends the run (default "-- run over")
      --loading     a line the map may write while it loads (default "-- loading"); space is posted while it is the last
      --fail        a start of a line that fails the run; repeatable (default ERROR and FAIL)
      --limit       seconds the whole run may take (default 300)
      --load-limit  seconds the file may take to come (default 120)
      --stall       seconds the file may stay the same before the game is resumed once (default 60)
      --wait-free   seconds to wait for another Warcraft III to end first (default 600)
    """;

string? map = null;
string? file = null;
var rules = new RunRules { Stall = TimeSpan.FromSeconds(60) };
var fail = new List<string>();
var program = GameRunOptions.DefaultProgram;
string? docs = null;
var waitFree = TimeSpan.FromMinutes(10);
for (var i = 0; i < args.Length; i++)
{
    var argument = args[i];
    string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(argument + " needs a value");
    TimeSpan SecondsValue() => double.TryParse(Value(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0
        ? TimeSpan.FromSeconds(s)
        : throw new ArgumentException(argument + " needs a number of seconds");
    try
    {
        switch (argument)
        {
            case "--file": file = Value(); break;
            case "--over": rules = rules with { OverLine = Value() }; break;
            case "--loading": rules = rules with { LoadingLine = Value() }; break;
            case "--fail": fail.Add(Value()); break;
            case "--limit": rules = rules with { Limit = SecondsValue() }; break;
            case "--load-limit": rules = rules with { LoadLimit = SecondsValue() }; break;
            case "--stall": rules = rules with { Stall = SecondsValue() }; break;
            case "--wait-free": waitFree = SecondsValue(); break;
            case "--game": program = Value(); break;
            case "--docs": docs = Value(); break;
            default:
                if (argument.StartsWith("--", StringComparison.Ordinal) || map != null)
                {
                    throw new ArgumentException("unexpected " + argument);
                }

                map = argument;
                break;
        }
    }
    catch (ArgumentException problem)
    {
        Console.Error.WriteLine(problem.Message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}

if (map == null || file == null)
{
    Console.Error.WriteLine(map == null ? "give the map" : "give the run's file with --file");
    Console.Error.WriteLine(Usage);
    return 2;
}

var options = new GameRunOptions
{
    Map = map,
    File = file,
    Program = program,
    Rules = rules,
    FailWhen = fail.Count > 0 ? fail : new[] { "ERROR", "FAIL" },
    WaitForFree = waitFree,
    Progress = line => Console.Error.WriteLine("gamerun: " + line),
};
if (docs != null)
{
    options = options with { Documents = docs };
}

GameRunResult result;
try
{
    result = GameRunner.Run(options);
}
catch (InvalidOperationException problem)
{
    Console.Error.WriteLine("gamerun: " + problem.Message);
    return 3;
}

foreach (var line in result.Lines)
{
    Console.WriteLine(line);
}

Console.WriteLine(result.Verdict);
Console.Error.WriteLine("gamerun: " + result.Detail);
return result.Passed ? 0 : 1;
