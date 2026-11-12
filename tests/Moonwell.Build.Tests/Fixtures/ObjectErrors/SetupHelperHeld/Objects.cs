using Moonwell;
using Moonwell.Objects;

namespace SetupHelperHeld
{
    public sealed class SetupHelperHeldMap : Map
    {
    }

    public static class Clan
    {
        public static void Cost(StandardUnit unit, int gold)
        {
            unit.GoldCost = gold;
        }
    }

    public class Guard : Moonwell.Objects.Human.Units.Footman
    {
        public static System.Action<StandardUnit, int>? Later;

        public Guard()
        {
            Clan.Cost(this, 111);

            // Held as a delegate, the helper can be called at any time in the game.
            Later = Clan.Cost;
        }
    }
}
