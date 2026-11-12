using Moonwell;

namespace SetFromOutside
{
    public sealed class SetFromOutsideMap : Map
    {
    }

    public class Guard : Moonwell.Objects.Human.Units.Footman
    {
    }

    // A class that declares nothing: its constructor runs in the game, where the set does nothing.
    public sealed class Tax
    {
        public Tax(Guard guard)
        {
            guard.GoldCost = 5;
        }
    }
}
