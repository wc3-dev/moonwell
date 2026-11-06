namespace Moonwell
{
    /// <summary>A component on the game, which reacts to events of the map as a whole.</summary>
    public abstract class GameComponent : MapComponent
    {
        /// <summary>The table behind GameHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? gameHooks;

        /// <summary>The reactions of game components, below those of map components.</summary>
        internal static HookTable GameHooks
        {
            get
            {
                if (gameHooks == null)
                {
                    gameHooks = new HookTable(typeof(GameComponent), new[] { "OnDawn", "OnDusk", "OnGameLoaded" }, new[] { Hook.OnDawn, Hook.OnDusk, Hook.OnGameLoaded }, MapHooks);
                }

                return gameHooks;
            }
        }

        /// <summary>The game this component is on; null before the component is added, though its type does not say so (read it from OnAdded on).</summary>
        [LocalSafe]
        public Game Game => (Game)Actor;

        /// <summary>Runs when night turns to day, as Game.IsDay turns true: within 0.25 s after the time passes dawn (6:00, or the map's own), or dusk when the day runs backward, and at once when Game.TimeOfDay is set from night to day. A time set within the day, or across a whole night, runs nothing.</summary>
        public virtual void OnDawn()
        {
        }

        /// <summary>Runs when day turns to night, as Game.IsDay turns false: within 0.25 s after the time passes dusk (18:00, or the map's own), or dawn when the day runs backward, and at once when Game.TimeOfDay is set from day to night. A time set within the night, or across a whole day, runs nothing.</summary>
        public virtual void OnDusk()
        {
        }

        /// <summary>Runs once when a game saved with the game's own Save Game is loaded, after Moonwell has made its frames again (the load takes every frame a map made, game facts 11-06): redo here what the map did with the game's natives itself, as on a frame's Handle, which is a new one now. Lua's state is as at the save.</summary>
        public virtual void OnGameLoaded()
        {
        }

        /// <summary>The reactions this kind of component declares.</summary>
        internal override HookTable Table => GameHooks;
    }
}
