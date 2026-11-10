using Moonwell;

namespace DescribeOutsideCtor
{
    public sealed class DescribeOutsideCtorMap : Map
    {
    }

    public class Bolt : Moonwell.Objects.Human.Abilities.StormBolt
    {
        public void Retell()
        {
            Describe("Bolt", tip => "Hits.");
        }
    }
}
