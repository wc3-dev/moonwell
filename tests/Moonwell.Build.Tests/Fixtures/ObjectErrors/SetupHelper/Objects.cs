using Moonwell;
using Moonwell.Objects;

namespace SetupHelper
{
    public sealed class SetupHelperMap : Map
    {
    }

    // Setup that classes with different bases share: methods that set declared fields, which only constructors call.
    public static class Clan
    {
        public static void Look(StandardUnit unit, string name, int gold)
        {
            unit.Name = "Clan " + name;
            Cost(unit, gold);
            unit.Abilities.Add<Moonwell.Objects.Human.Abilities.Defend>();
        }

        // One helper calls another.
        private static void Cost(StandardUnit unit, int gold)
        {
            unit.GoldCost = gold;
        }
    }

    public class Guard : Moonwell.Objects.Human.Units.Footman
    {
        public Guard()
        {
            Clan.Look(this, "Guard", 111);
        }
    }

    public class Gunner : Moonwell.Objects.Human.Units.Rifleman
    {
        public Gunner()
        {
            // A lambda inside a constructor counts as the constructor, for a call as for a set.
            System.Action look = () => Clan.Look(this, "Gunner", 222);
            look();
        }
    }
}
