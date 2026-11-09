using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Research (decision 213): an upgrade class's OnResearched for each level a player gains, and a building's research reactions.</summary>
    internal static class Researching
    {
        /// <summary>The kinds of the game's research events.</summary>
        internal const int Started = 0, Cancelled = 1, Finished = 2;

        /// <summary>While levels run, the last level run of each player's upgrade, by slot and id, so that a level set inside OnResearched runs each level once, in order; null between runs.</summary>
        private static Dictionary<string, int>? running;

        /// <summary>How many runs of Gained are open, one inside another.</summary>
        private static int depth;

        /// <summary>A building starts, cancels or finishes a research. A finish first runs the upgrade's OnResearched for the level the game now counts; then the building's reactions and its components' run.</summary>
        internal static void FromGame(unit building, player owner, int upgradeId, int kind)
        {
            var upgrade = ObjectTypes.UpgradeOf(upgradeId);
            if (kind == Finished)
            {
                var player = Player.Of(owner);
                if (player != null)
                {
                    var reached = Runtime.World.UpgradeLevel(owner, upgradeId);
                    Gained(player, upgrade, reached - 1, reached);
                }
            }

            var unit = KnownUnits.Find(building);
            if (unit != null && !unit.HasEnded)
            {
                Building(unit, upgrade, kind);
            }
        }

        /// <summary>Runs the upgrade's OnResearched for each level from before + 1 to reached, while the player still has it: a level a reaction lowers away runs nothing.</summary>
        internal static void Gained(Player player, Upgrade upgrade, int before, int reached)
        {
            if (reached <= before || (upgrade.Mask2 & Hook2.OnResearchOrSale) == 0)
            {
                return;
            }

            var key = player.Slot + ":" + upgrade.TypeId;
            running ??= new Dictionary<string, int>();
            if (!running.TryGetValue(key, out var done) || done > before)
            {
                done = before;
            }

            depth++;
            try
            {
                while (done < reached && Runtime.World.UpgradeLevel(player.Handle, upgrade.TypeId) > done)
                {
                    done++;
                    running[key] = done;
                    try
                    {
                        upgrade.OnResearched(player, done);
                    }
                    catch (Exception error)
                    {
                        Errors.Report(Names.NameOf(upgrade) + ".OnResearched", error);
                    }

                    // A level set inside the reaction ran its own levels: go on after them.
                    done = running[key];
                }
            }
            finally
            {
                depth--;
                if (depth == 0)
                {
                    running = null;
                }
            }
        }

        /// <summary>Runs a research reaction on a building, then on its components, in add order.</summary>
        private static void Building(Unit unit, Upgrade upgrade, int kind)
        {
            if ((unit.Mask2 & Hook2.OnResearchOrSale) != 0)
            {
                try
                {
                    if (kind == Started)
                    {
                        unit.OnResearchStarted(upgrade);
                    }
                    else if (kind == Cancelled)
                    {
                        unit.OnResearchCancelled(upgrade);
                    }
                    else
                    {
                        unit.OnResearchFinished(upgrade);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(unit) + "." + NameOf(kind), error);
                }
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnResearchOrSale) == 0)
                {
                    continue;
                }

                try
                {
                    if (kind == Started)
                    {
                        component.OnResearchStarted(upgrade);
                    }
                    else if (kind == Cancelled)
                    {
                        component.OnResearchCancelled(upgrade);
                    }
                    else
                    {
                        component.OnResearchFinished(upgrade);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + "." + NameOf(kind), error);
                }
            }

            unit.EndDispatch();
        }

        private static string NameOf(int kind)
        {
            return kind == Started ? "OnResearchStarted" : kind == Cancelled ? "OnResearchCancelled" : "OnResearchFinished";
        }
    }
}
