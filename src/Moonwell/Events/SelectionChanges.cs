using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>OnSelectionChanged (decision 341): the players whose selection events came in this instant are checked once, on a timer of 0 s, after every event of the instant is in.</summary>
    internal static class SelectionChanges
    {
        private static readonly List<Player> marked = new List<Player>();

        /// <summary>What else hears a change after the reactions, such as the kit's selection lists; one that answers false is dropped.</summary>
        private static readonly List<Func<Player, bool>> watchers = new List<Func<Player, bool>>();

        private static Timer? check;

        /// <summary>Forgets the game before's players and watchers.</summary>
        internal static void Reset()
        {
            marked.Clear();
            watchers.Clear();
            check = null;
        }

        /// <summary>A selection event of the player's came in: the player is checked at the end of the instant.</summary>
        internal static void Mark(Player player)
        {
            if (!marked.Contains(player))
            {
                marked.Add(player);
            }

            check ??= Game.Instance.OwnAfter(0.0f, Check);
        }

        /// <summary>Adds a watcher, run with the player after each change's reactions until it answers false, and listens for the selection events.</summary>
        internal static void Watch(Func<Player, bool> watcher)
        {
            watchers.Add(watcher);
            Events.Need(Hook.OnSelection);
        }

        private static void Check()
        {
            check = null;
            var players = marked.ToArray();
            marked.Clear();
            foreach (var player in players)
            {
                if (!player.SelectionMoved())
                {
                    continue;
                }

                Raise(Game.Instance, player);
                Raise(player, player);
                var now = watchers.ToArray();
                for (var i = 0; i < now.Length; i++)
                {
                    try
                    {
                        if (!now[i](player))
                        {
                            watchers.Remove(now[i]);
                        }
                    }
                    catch (Exception error)
                    {
                        Errors.Report(player, ".OnSelectionChanged", error);
                    }
                }
            }
        }

        /// <summary>Runs OnSelectionChanged on the map components of an actor, in add order.</summary>
        private static void Raise(Actor actor, Player player)
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
                    component.OnSelectionChanged(player);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnSelectionChanged", error);
                }
            }

            actor.EndDispatch();
        }
    }
}
