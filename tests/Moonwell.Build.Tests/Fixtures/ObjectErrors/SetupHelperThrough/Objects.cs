using Moonwell;
using Moonwell.Objects;

namespace SetupHelperThrough
{
    public sealed class SetupHelperThroughMap : Map
    {
    }

    public static class Clan
    {
        public static void Look(StandardUnit unit, int gold)
        {
            Cost(unit, gold);
        }

        private static void Cost(StandardUnit unit, int gold)
        {
            unit.GoldCost = gold;
        }
    }

    public class Guard : Moonwell.Objects.Human.Units.Footman
    {
        public Guard()
        {
            Clan.Look(this, 111);
        }

        // A reaction reaches the set through the helper that calls the one that sets.
        public override void OnDeath(Moonwell.Unit? killerUnit)
        {
            Clan.Look(this, 50);
        }
    }
}
