using Moonwell;

namespace ObjectsLibraryCode
{
    /// <summary>Makes a standard unit, a class of Moonwell.Objects that only this library names.</summary>
    public static class Spawner
    {
        public static Unit Footman(Player player)
        {
            return Unit.Create<Moonwell.Objects.Human.Units.Footman>(player, 0f, 0f, Game.Instance);
        }
    }
}
