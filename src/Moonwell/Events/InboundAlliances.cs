using System;

namespace Moonwell
{
    /// <summary>
    /// The alliance changes: the game tells only that a player's alliances changed, so Moonwell keeps what each player gave
    /// each other and runs OnAllianceChanged for each pair that differs (decision 198).
    /// </summary>
    internal static partial class Inbound
    {
        /// <summary>What each player gave each other when last looked at, by giver slot times Player.MaxSlots plus receiver slot; null until a reaction needs it.</summary>
        private static int[]? alliancesSeen;

        /// <summary>How many of Moonwell's own alliance sets are running: the game's events inside them wait, and the set looks at its pair once at its end.</summary>
        private static int alliancesHeld;

        /// <summary>Takes what every player gives every other, from which changes are seen; Events.Need calls it when OnAllianceChanged is first needed.</summary>
        internal static void WatchAlliances()
        {
            alliancesSeen = new int[Player.MaxSlots * Player.MaxSlots];
            for (var giver = 0; giver < Player.MaxSlots; giver++)
            {
                for (var receiver = 0; receiver < Player.MaxSlots; receiver++)
                {
                    if (giver != receiver)
                    {
                        alliancesSeen[(giver * Player.MaxSlots) + receiver] = Runtime.World.AllianceOf(Player.Get(giver).Handle, Player.Get(receiver).Handle);
                    }
                }
            }
        }

        /// <summary>The game says a player's alliances changed: unless Moonwell's own set is running, each pair from that player that differs runs its reactions.</summary>
        private static void HandleAllianceChanged(player giver)
        {
            var giving = Player.Of(giver);
            if (alliancesHeld > 0 || giving == null)
            {
                return;
            }

            for (var receiver = 0; receiver < Player.MaxSlots; receiver++)
            {
                if (receiver != giving.Slot)
                {
                    AllianceLooked(giving, Player.Get(receiver));
                }
            }
        }

        /// <summary>Moonwell's own set of an alliance begins: the game's events wait for its end.</summary>
        internal static void HoldAlliances()
        {
            alliancesHeld++;
        }

        /// <summary>Moonwell's own set of an alliance ends: its pair runs its reactions once if it changed.</summary>
        internal static void ReleaseAlliances(Player giving, Player receiving)
        {
            alliancesHeld--;
            if (alliancesHeld == 0 && giving != receiving)
            {
                AllianceLooked(giving, receiving);
            }
        }

        /// <summary>Forgets what was seen; Runtime.Reset calls it.</summary>
        internal static void ResetAlliances()
        {
            alliancesSeen = null;
            alliancesHeld = 0;
        }

        // Runs the reactions of a pair whose alliance differs from what was seen, and keeps the new one.
        private static void AllianceLooked(Player giving, Player receiving)
        {
            if (alliancesSeen == null)
            {
                return;
            }

            var index = (giving.Slot * Player.MaxSlots) + receiving.Slot;
            var now = Runtime.World.AllianceOf(giving.Handle, receiving.Handle);
            var before = alliancesSeen[index];
            if (now == before)
            {
                return;
            }

            alliancesSeen[index] = now;
            MapAlliance(Game.Instance, giving, receiving, (Alliance)before);
            MapAlliance(giving, giving, receiving, (Alliance)before);
            MapAlliance(receiving, giving, receiving, (Alliance)before);
        }

        /// <summary>Runs OnAllianceChanged on the map components of an actor, in add order.</summary>
        private static void MapAlliance(Actor actor, Player giving, Player receiving, Alliance before)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnAllianceChanged) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnAllianceChanged(giving, receiving, before);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnAllianceChanged", error);
                }
            }

            actor.EndDispatch();
        }
    }
}
