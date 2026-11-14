using System;

namespace Moonwell
{
    /// <summary>The selection events: a player's selection kept, and the reactions to a unit selected or deselected.</summary>
    internal static partial class Inbound
    {
        /// <summary>A player selects or deselects a unit: the player's Selection takes it in or lets it go, then the unit's OnSelected or OnDeselected, its components', the game's and the player's run, in that order (decision 203).</summary>
        private static void HandleSelection(unit handle, player who, bool selected)
        {
            var unit = Unit.Of(handle);
            var player = Player.Of(who);
            if (unit == null || player == null)
            {
                return;
            }

            player.Selected(unit, selected);
            UnitSelection(unit, player, selected);
            MapSelection(Game.Instance, unit, player, selected);
            MapSelection(player, unit, player, selected);
            SelectionChanges.Mark(player);
        }

        /// <summary>Runs OnSelected or OnDeselected on a unit, then on its components, in add order.</summary>
        private static void UnitSelection(Unit unit, Player player, bool selected)
        {
            if (unit.HasEnded)
            {
                return;
            }

            if ((unit.Mask & Hook.OnSelection) != 0)
            {
                try
                {
                    if (selected)
                    {
                        unit.OnSelected(player);
                    }
                    else
                    {
                        unit.OnDeselected(player);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(unit, (selected ? ".OnSelected" : ".OnDeselected"), error);
                }
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnSelection) == 0)
                {
                    continue;
                }

                try
                {
                    if (selected)
                    {
                        component.OnSelected(player);
                    }
                    else
                    {
                        component.OnDeselected(player);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, (selected ? ".OnSelected" : ".OnDeselected"), error);
                }
            }

            unit.EndDispatch();
        }

        /// <summary>Runs OnUnitSelected or OnUnitDeselected on the map components of an actor, in add order.</summary>
        private static void MapSelection(Actor actor, Unit unit, Player player, bool selected)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnSelection) == 0)
                {
                    continue;
                }

                try
                {
                    if (selected)
                    {
                        component.OnUnitSelected(unit, player);
                    }
                    else
                    {
                        component.OnUnitDeselected(unit, player);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, (selected ? ".OnUnitSelected" : ".OnUnitDeselected"), error);
                }
            }

            actor.EndDispatch();
        }
    }
}
