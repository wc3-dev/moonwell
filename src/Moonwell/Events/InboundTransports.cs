using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The transport events: a unit loaded into a transport, and a unit that comes out, which the game gives no event for (decision 219).</summary>
    internal static partial class Inbound
    {
        /// <summary>A unit is loaded: it joins the transport's Cargo, then the cargo's OnLoaded, its components', the transport's OnCargoLoaded, its components', the game's and the cargo's player's OnUnitLoaded run, in that order.</summary>
        private static void HandleLoaded(unit cargoHandle, unit transportHandle)
        {
            var cargo = Unit.Of(cargoHandle);
            var transport = Unit.Of(transportHandle);
            if (cargo == null || transport == null)
            {
                return;
            }

            if (cargo.transport != null)
            {
                Unloaded(cargo);
            }

            cargo.transport = transport;
            if (transport.cargo == null)
            {
                transport.cargo = new List<Unit>();
            }

            transport.cargo.Add(cargo);
            Transported(cargo, transport, true);
        }

        /// <summary>A unit is given an order: one Moonwell holds as cargo that the game no longer has loaded came out, as the game gives the cargo an order on every way out (unload, unload all, the transport killed or removed; seen 11-08).</summary>
        internal static void CheckUnloaded(unit handle)
        {
            var cargo = KnownUnits.Find(handle);
            if (cargo != null && cargo.transport != null && !Runtime.World.IsLoaded(handle))
            {
                Unloaded(cargo);
            }
        }

        /// <summary>A transport Moonwell removes lets its cargo out first, with the reactions, before its OnLeave and its components end (decision 219): the game puts the cargo down where the transport was only after RemoveUnit returns (seen 11-09), too late for them. A unit that died inside comes out with none.</summary>
        internal static void DropCargo(Unit transport)
        {
            transport.droppedCargo = true;
            var held = new List<Unit>(transport.cargo!);
            for (var i = 0; i < held.Count; i++)
            {
                var cargo = held[i];
                if (cargo.transport != transport)
                {
                    continue;
                }

                if (cargo.Handle != null && Runtime.World.IsAlive(cargo.Handle))
                {
                    Unloaded(cargo);
                }
                else
                {
                    cargo.transport = null;
                    transport.cargo?.Remove(cargo);
                }
            }
        }

        /// <summary>A unit leaving the game leaves its transport's Cargo with no reaction; a transport leaving lets out the cargo the game no longer has loaded, with its reactions, and forgets the rest.</summary>
        internal static void TransportLeft(Unit unit)
        {
            if (unit.transport != null)
            {
                unit.transport.cargo?.Remove(unit);
                unit.transport = null;
            }

            if (unit.cargo == null)
            {
                return;
            }

            var held = new List<Unit>(unit.cargo);
            for (var i = 0; i < held.Count; i++)
            {
                if (held[i].transport != unit)
                {
                    continue;
                }

                if (held[i].Handle != null && !Runtime.World.IsLoaded(held[i].Handle))
                {
                    Unloaded(held[i]);
                }
                else
                {
                    held[i].transport = null;
                }
            }

            unit.cargo = null;
        }

        /// <summary>The cargo leaves its transport's Cargo, then the reactions run.</summary>
        private static void Unloaded(Unit cargo)
        {
            var transport = cargo.transport!;
            cargo.transport = null;
            transport.cargo?.Remove(cargo);
            Transported(cargo, transport, false);
        }

        private static void Transported(Unit cargo, Unit transport, bool loaded)
        {
            CargoReactions(cargo, transport, loaded);
            TransportReactions(transport, cargo, loaded);
            MapTransport(Game.Instance, cargo, transport, loaded);
            MapTransport(cargo.Owner, cargo, transport, loaded);
        }

        /// <summary>Runs OnLoaded or OnUnloaded on the cargo, then on its components, in add order.</summary>
        private static void CargoReactions(Unit cargo, Unit transport, bool loaded)
        {
            if (cargo.HasEnded)
            {
                return;
            }

            var name = loaded ? ".OnLoaded" : ".OnUnloaded";
            if ((cargo.Mask & Hook.OnTransport) != 0)
            {
                try
                {
                    if (loaded)
                    {
                        cargo.OnLoaded(transport);
                    }
                    else
                    {
                        cargo.OnUnloaded(transport);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(cargo, name, error);
                }
            }

            var count = cargo.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = cargo.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnTransport) == 0)
                {
                    continue;
                }

                try
                {
                    if (loaded)
                    {
                        component.OnLoaded(transport);
                    }
                    else
                    {
                        component.OnUnloaded(transport);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, name, error);
                }
            }

            cargo.EndDispatch();
        }

        /// <summary>Runs OnCargoLoaded or OnCargoUnloaded on the transport, then on its components, in add order.</summary>
        private static void TransportReactions(Unit transport, Unit cargo, bool loaded)
        {
            if (transport.HasEnded)
            {
                return;
            }

            var name = loaded ? ".OnCargoLoaded" : ".OnCargoUnloaded";
            if ((transport.Mask & Hook.OnTransport) != 0)
            {
                try
                {
                    if (loaded)
                    {
                        transport.OnCargoLoaded(cargo);
                    }
                    else
                    {
                        transport.OnCargoUnloaded(cargo);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(transport, name, error);
                }
            }

            var count = transport.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = transport.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnTransport) == 0)
                {
                    continue;
                }

                try
                {
                    if (loaded)
                    {
                        component.OnCargoLoaded(cargo);
                    }
                    else
                    {
                        component.OnCargoUnloaded(cargo);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, name, error);
                }
            }

            transport.EndDispatch();
        }

        /// <summary>Runs OnUnitLoaded or OnUnitUnloaded on the map components of an actor, in add order.</summary>
        private static void MapTransport(Actor actor, Unit cargo, Unit transport, bool loaded)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnTransport) == 0)
                {
                    continue;
                }

                try
                {
                    if (loaded)
                    {
                        component.OnUnitLoaded(cargo, transport);
                    }
                    else
                    {
                        component.OnUnitUnloaded(cargo, transport);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, (loaded ? ".OnUnitLoaded" : ".OnUnitUnloaded"), error);
                }
            }

            actor.EndDispatch();
        }
    }
}
