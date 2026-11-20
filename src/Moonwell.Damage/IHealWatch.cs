namespace Moonwell
{
    /// <summary>Map-wide reactions to heals, on the game's components (every heal) and a player's (heals where the player owns the source or the target); write only the ones needed, as public methods.</summary>
    public interface IHealWatch
    {
        /// <summary>Before the life is set, for any unit receiving a heal: the heal may change its amount.</summary>
        /// @CSharpLua.Ignore
        void OnUnitReceiving(Heal heal)
        {
        }

        /// <summary>After the life is set, for any unit that received a heal: <see cref="Heal.Done"/> and <see cref="Heal.Over"/> tell what went in.</summary>
        /// @CSharpLua.Ignore
        void OnUnitReceived(Heal heal)
        {
        }
    }
}
