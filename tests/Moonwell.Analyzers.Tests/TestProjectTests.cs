namespace Moonwell.Analyzers.Tests;

/// <summary>A test project's code runs only on .NET, so the game rules leave it alone (J96), as they leave a project without the game library (#278).</summary>
public class TestProjectTests
{
    // What a simulation's test reads: the files the fake wrote, a dictionary looped, and a double.
    private const string TestCode = """
        var files = new Dictionary<string, string>();
        foreach (var pair in files) { }
        var names = string.Join(",", files.Keys);
        double half = 0.5;
        """;

    [Fact]
    public void a_test_project_is_not_checked_by_the_game_rules()
    {
        Assert.Empty(MapCode.CheckWithTestProject("true", TestCode));
        Assert.Empty(MapCode.CheckWithTestProject("True", TestCode));
    }

    [Fact]
    public void a_project_that_is_no_test_project_is_checked_as_before()
    {
        var expected = MapCode.Check(TestCode);
        Assert.Contains("MW0004: files", expected);
        Assert.Equal(expected, MapCode.CheckWithTestProject("false", TestCode));
        Assert.Equal(expected, MapCode.CheckWithTestProject("", TestCode));
    }
}
