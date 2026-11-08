namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: hints, warnings and clears change only what one player sees, so they may run inside Local.For; an error makes its sound on every machine, so it may not (decision 218).</summary>
public class MessageTests
{
    [Fact]
    public void an_error_is_refused_inside_and_the_rest_allowed()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                p.Hint("a");
                p.Warning("b");
                p.ClearMessages();
                p.Team.Print("c");
                p.Team.Hint("d");
                p.Error("e");
                p.Team.Error("f");
            });
            """);

        Assert.Equal(new[] { "MW0001: p.Error(\"e\")", "MW0001: p.Team.Error(\"f\")" }, found);
    }
}
