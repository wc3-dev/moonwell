namespace Moonwell
{
    /// <summary>A component on a player, which reacts to the map's events that involve the player's own units (a unit's owner; for a death or a hit, the killer's or source's owner too), and to the player's leaving; a game component hears them for every unit.</summary>
    public abstract class PlayerComponent : MapComponent
    {
        /// <summary>The player this component is on; null before the component is added, though its type does not say so (read it from OnAdded on).</summary>
        [LocalSafe]
        public Player Player => (Player)Actor;
    }
}
