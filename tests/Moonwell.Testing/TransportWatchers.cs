namespace Moonwell.Testing
{
    /// <summary>A unit type that logs "cargo loaded" or "cargo unloaded", with whether its Transport reads the transport then.</summary>
    public sealed class CargoUnit : Unit
    {
        /// <summary>Logs the load.</summary>
        public override void OnLoaded(Unit carryingTransport)
        {
            TestLog.Lines.Add("cargo loaded, transport set " + (Transport == carryingTransport));
        }

        /// <summary>Logs the unload.</summary>
        public override void OnUnloaded(Unit carryingTransport)
        {
            TestLog.Lines.Add("cargo unloaded, transport set " + (Transport != null));
        }
    }

    /// <summary>A unit type that logs "transport got" or "transport lost", with how many its Cargo holds then.</summary>
    public sealed class TransportUnit : Unit
    {
        /// <summary>Logs the unit loaded.</summary>
        public override void OnCargoLoaded(Unit loadedUnit)
        {
            TestLog.Lines.Add("transport got, holding " + Cargo.Count);
        }

        /// <summary>Logs the unit that came out.</summary>
        public override void OnCargoUnloaded(Unit unloadedUnit)
        {
            TestLog.Lines.Add("transport lost, holding " + Cargo.Count);
        }
    }

    /// <summary>A unit component that logs "component loaded" or "component unloaded" on cargo, "component got" or "component lost" on a transport.</summary>
    public sealed class TransportComponent : UnitComponent
    {
        /// <summary>Logs the load.</summary>
        public override void OnLoaded(Unit carryingTransport)
        {
            TestLog.Lines.Add("component loaded");
        }

        /// <summary>Logs the unload.</summary>
        public override void OnUnloaded(Unit carryingTransport)
        {
            TestLog.Lines.Add("component unloaded");
        }

        /// <summary>Logs the unit loaded.</summary>
        public override void OnCargoLoaded(Unit loadedUnit)
        {
            TestLog.Lines.Add("component got");
        }

        /// <summary>Logs the unit that came out.</summary>
        public override void OnCargoUnloaded(Unit unloadedUnit)
        {
            TestLog.Lines.Add("component lost");
        }
    }

    /// <summary>A game component that logs "game loaded" or "game unloaded".</summary>
    public sealed class TransportGame : GameComponent
    {
        /// <summary>Logs the load.</summary>
        public override void OnUnitLoaded(Unit loadedUnit, Unit carryingTransport)
        {
            TestLog.Lines.Add("game loaded");
        }

        /// <summary>Logs the unload.</summary>
        public override void OnUnitUnloaded(Unit unloadedUnit, Unit carryingTransport)
        {
            TestLog.Lines.Add("game unloaded");
        }
    }

    /// <summary>A player component that logs "player 0 loaded" or "player 0 unloaded" with its own player's slot.</summary>
    public sealed class TransportPlayer : PlayerComponent
    {
        /// <summary>Logs the load.</summary>
        public override void OnUnitLoaded(Unit loadedUnit, Unit carryingTransport)
        {
            TestLog.Lines.Add("player " + Player.Slot + " loaded");
        }

        /// <summary>Logs the unload.</summary>
        public override void OnUnitUnloaded(Unit unloadedUnit, Unit carryingTransport)
        {
            TestLog.Lines.Add("player " + Player.Slot + " unloaded");
        }
    }
}
