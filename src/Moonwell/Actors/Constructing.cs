using System;

namespace Moonwell
{
    /// <summary>Construction and training (J55): a building's construction starting, being cancelled or finishing, and a unit it trained, run on the building, its components and the map's components.</summary>
    internal static class Constructing
    {
        /// <summary>The kinds of the game's construction events.</summary>
        internal const int Started = 0, Cancelled = 1, Finished = 2;

        /// <summary>A building's construction starts, is cancelled or finishes: the building is taken in when Moonwell does not hold it yet (at the start it is coming into the game), then its reactions run, its components', the game's components' and its owner's.</summary>
        internal static void FromGame(unit? handle, int kind)
        {
            if (handle == null)
            {
                return;
            }

            var building = KnownUnits.Find(handle) ?? KnownUnits.TakeIn(handle, null, kind == Started);
            if (building == null || building.HasEnded)
            {
                return;
            }

            var slot = building.Owner.Slot;
            if ((building.Mask2 & Hook2.OnConstruction) != 0)
            {
                try
                {
                    Run(building, kind);
                }
                catch (Exception error)
                {
                    Errors.Report(building, "." + NameOf(kind), error);
                }
            }

            var count = building.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = building.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnConstruction) == 0)
                {
                    continue;
                }

                try
                {
                    Run(component, kind);
                }
                catch (Exception error)
                {
                    Errors.Report(component, "." + NameOf(kind), error);
                }
            }

            building.EndDispatch();
            Map(Game.Instance, building, kind);
            var player = Player.Existing(slot);
            if (player != null)
            {
                Map(player, building, kind);
            }
        }

        /// <summary>A building finished training a unit: the trained unit is taken in first, as it is coming into the game (its OnCreated and the map's OnUnitCreated run before), then the building's OnTrained runs, then its components'.</summary>
        internal static void Trained(unit? trainer, unit? trainedHandle)
        {
            if (trainer == null || trainedHandle == null)
            {
                return;
            }

            var trained = KnownUnits.Find(trainedHandle) ?? KnownUnits.TakeIn(trainedHandle, null, true);
            var building = KnownUnits.Find(trainer);
            if (trained == null || trained.HasEnded || building == null || building.HasEnded)
            {
                return;
            }

            if ((building.Mask2 & Hook2.OnTrained) != 0)
            {
                try
                {
                    building.OnTrained(trained);
                }
                catch (Exception error)
                {
                    Errors.Report(building, ".OnTrained", error);
                }
            }

            var count = building.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = building.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnTrained) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnTrained(trained);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnTrained", error);
                }
            }

            building.EndDispatch();
        }

        private static void Run(Unit building, int kind)
        {
            if (kind == Started)
            {
                building.OnConstructionStarted();
            }
            else if (kind == Cancelled)
            {
                building.OnConstructionCancelled();
            }
            else
            {
                building.OnConstructionFinished();
            }
        }

        private static void Run(UnitComponent component, int kind)
        {
            if (kind == Started)
            {
                component.OnConstructionStarted();
            }
            else if (kind == Cancelled)
            {
                component.OnConstructionCancelled();
            }
            else
            {
                component.OnConstructionFinished();
            }
        }

        /// <summary>Runs a construction reaction on the map components of an actor, in add order.</summary>
        private static void Map(Actor actor, Unit building, int kind)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnConstruction) == 0)
                {
                    continue;
                }

                try
                {
                    if (kind == Started)
                    {
                        component.OnConstructionStarted(building);
                    }
                    else if (kind == Cancelled)
                    {
                        component.OnConstructionCancelled(building);
                    }
                    else
                    {
                        component.OnConstructionFinished(building);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, "." + NameOf(kind), error);
                }
            }

            actor.EndDispatch();
        }

        private static string NameOf(int kind)
        {
            return kind == Started ? "OnConstructionStarted" : kind == Cancelled ? "OnConstructionCancelled" : "OnConstructionFinished";
        }
    }
}
