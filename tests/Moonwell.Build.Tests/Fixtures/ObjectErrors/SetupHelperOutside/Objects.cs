using Moonwell;
using Moonwell.Objects;

namespace SetupHelperOutside
{
    public sealed class SetupHelperOutsideMap : Map
    {
    }

    public static class Clan
    {
        // A list emptied is a change as a set is.
        public static void Free(StandardUnit unit)
        {
            unit.Requires.Clear();
            unit.GoldCost = 0;
        }
    }

    public class Guard : Moonwell.Objects.Human.Units.Footman
    {
        public Guard()
        {
            Clan.Free(this);
        }

        // Not a constructor: in the game the helper's changes would do nothing.
        public void Discount()
        {
            Clan.Free(this);
        }
    }
}
