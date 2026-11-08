namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: a unit's Transport, Cargo and CargoCapacity are read inside Local.For; loading, unloading and setting the capacity are changes (decision 219).</summary>
public class TransportTests
{
    [Fact]
    public void reads_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var w = Unit.Of(u)!;
            Local.For(p, () =>
            {
                var carrier = w.Transport;
                var aboard = w.Cargo.Count;
                var room = w.CargoCapacity;
                w.CargoCapacity = 2;
                w.Load(w);
                w.Unload(w);
                w.UnloadAll(0f, 0f);
            });
            """);

        Assert.Equal(4, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }
}
