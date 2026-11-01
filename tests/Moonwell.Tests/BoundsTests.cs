namespace Moonwell.Tests;

public class BoundsTests
{
    // Godot's Rect2 takes a position and a size, and two corners would fit the same two vectors, so a call from that habit
    // has to fail to compile instead of making another box.
    [Fact]
    public void BoundsHasNoPublicConstructor()
    {
        Assert.Empty(typeof(Bounds).GetConstructors());
    }
}
