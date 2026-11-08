namespace Moonwell
{
    /// <summary>An upgrade type: data, since the game gives no handle to an upgrade, so it is not an actor. Each upgrade type has one object, its class's for a registered class, on which OnResearched runs for every player (decision 213).</summary>
    public class Upgrade
    {
        /// <summary>The table behind UpgradeHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? upgradeHooks;

        /// <summary>The reactions of upgrades; declared here, so that an override in a subclass is found.</summary>
        internal static HookTable UpgradeHooks
        {
            get
            {
                if (upgradeHooks == null)
                {
                    upgradeHooks = new HookTable(typeof(Upgrade), new string[] { "OnResearched" }, new int[] { Hook.OnResearchOrSale });
                }

                return upgradeHooks;
            }
        }

        /// <summary>The bit mask of the reactions this class overrides, worked out when the object is made.</summary>
        internal int Mask;

        /// <summary>The upgrade's object id.</summary>
        [LocalSafe]
        public int TypeId { get; internal set; }

        /// <summary>
        /// Runs on every machine for each level a player gains of the upgrade, one level at a time, in order: inside the game's
        /// research finish (the game already counts the level, seen 11-08), before the building's OnResearchFinished, and inside
        /// Player.SetLevel and AddLevel. A level lowered runs nothing; nor does a level a native sets, which Moonwell does not see.
        /// </summary>
        /// <param name="player">The player that gained the level.</param>
        /// <param name="level">The level gained.</param>
        public virtual void OnResearched(Player player, int level)
        {
        }
    }
}
