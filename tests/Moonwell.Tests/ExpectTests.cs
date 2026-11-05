using Moonwell.Testing;

namespace Moonwell.Tests;

public class ExpectTests
{
    [Fact]
    public void near_fails_on_not_a_number()
    {
        Assert.Throws<ExpectationFailed>(() => Expect.Near(10f, float.NaN, "height"));
    }
}
