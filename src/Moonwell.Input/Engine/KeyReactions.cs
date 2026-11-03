using System;
using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// Takes the game's key events into the player's Keyboard and runs the map-wide reactions: the game's components in add
    /// order, then the player's own, never another player's. A held key's repeats run nothing.
    /// </summary>
    internal static class KeyReactions
    {
        /// <summary>The reaction methods, one bit each: a class's mask holds the ones it writes.</summary>
        private const int Pressed = 1, Released = 2;

        /// <summary>The highest code a key has.</summary>
        private const int LastCode = 254;

        /// <summary>
        /// A press of a key already down that comes sooner than this after the key's last press is the game's repeat of a held
        /// key (the first comes 0.5 s after the press, then about 30 a second); a later one is a new press whose release the
        /// game never sent, as it sends none for a key let go while it takes no keyboard input.
        /// </summary>
        private const float RepeatGap = 0.7f;

        /// <summary>The mask of each class seen, keyed by the class.</summary>
        private static readonly Dictionary<object, int> masks = new Dictionary<object, int>();

        /// <summary>The game's watchers.</summary>
        private static WatcherList gameWatchers = new WatcherList();

        /// <summary>Each slot's player's watchers; null until that player's first event.</summary>
        private static WatcherList?[] playerWatchers = new WatcherList?[Player.MaxSlots];

        /// <summary>Forgets every class's mask and the watchers read; InstallKeyboard calls it.</summary>
        internal static void Reset()
        {
            masks.Clear();
            gameWatchers = new WatcherList();
            playerWatchers = new WatcherList?[Player.MaxSlots];
        }

        /// <summary>The game's key event: takes the modifiers and the key into the player's keyboard, then runs OnKeyPressed or OnKeyReleased.</summary>
        internal static void OnKey()
        {
            var world = InputSystem.KeyWorld;
            var player = Player.Of(world.EventPlayer());
            var code = world.EventKey();
            if (player == null || code < 1 || code > LastCode)
            {
                return;
            }

            var keyboard = InputSystem.KeyboardOf(player.Slot);
            keyboard.TakeModifiers(world.EventMeta());
            if (!world.EventKeyDown())
            {
                Release(keyboard, code);
                return;
            }

            var now = Runtime.World.Now();
            if (keyboard.IsDownCode(code))
            {
                var repeat = now - keyboard.LastPressOf(code) < RepeatGap;
                keyboard.TakeRepeat(code, now);
                if (repeat)
                {
                    return;
                }

                Release(keyboard, code);
            }

            keyboard.TakePress(code, now);
            Run(keyboard, Pressed, (Key)code);
        }

        /// <summary>How often, in seconds, each machine compares the keys the events say are down with the keys it holds.</summary>
        internal const float CheckPeriod = 0.25f;

        /// <summary>The check: for each player listened for and still playing, asks the world to send a release message, on that player's machine, for each key the events say is down and the machine no longer holds.</summary>
        internal static void Check()
        {
            var world = InputSystem.KeyWorld;
            var players = InputSystem.KeyPlayers;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var keyboard = InputSystem.KeyboardOf(player.Slot);
                if (keyboard.DownCount == 0 || player.SlotState != SlotState.Playing)
                {
                    continue;
                }

                for (var code = 1; code <= LastCode; code++)
                {
                    if (keyboard.IsDownCode(code))
                    {
                        world.SendIfLetGo(player.Handle, code, keyboard.DownsOf(code));
                    }
                }
            }
        }

        /// <summary>A release message arrived: the key of the player who sent it comes up and OnKeyReleased runs, when it is still down and has had no down event since the message was sent.</summary>
        internal static void OnReleaseArrived()
        {
            var world = InputSystem.KeyWorld;
            var player = Player.Of(world.EventPlayer());
            var code = world.EventReleasedKey();
            if (player == null || code < 1 || code > LastCode)
            {
                return;
            }

            var keyboard = InputSystem.KeyboardOf(player.Slot);
            if (!keyboard.IsDownCode(code) || keyboard.DownsOf(code) != world.EventReleasedDowns())
            {
                return;
            }

            keyboard.TakeRelease(code);
            keyboard.LetGoModifiers();
            Run(keyboard, Released, (Key)code);
        }

        /// <summary>A player left: their keyboard holds no key and no modifier from then on, and no reaction runs, as none runs for them afterwards.</summary>
        internal static void OnLeft()
        {
            var player = Player.Of(InputSystem.KeyWorld.EventPlayer());
            if (player != null)
            {
                InputSystem.KeyboardOf(player.Slot).LetGoAll();
            }
        }

        /// <summary>Marks a key up and runs OnKeyReleased; nothing for a key already up.</summary>
        internal static void Release(Keyboard keyboard, int code)
        {
            if (!keyboard.IsDownCode(code))
            {
                return;
            }

            keyboard.TakeRelease(code);
            Run(keyboard, Released, (Key)code);
        }

        /// <summary>Runs a reaction on the game's components, then on the keyboard's player's.</summary>
        private static void Run(Keyboard keyboard, int bit, Key key)
        {
            RunMap(Game.Instance, gameWatchers, keyboard, bit, key);
            var player = Player.Existing(keyboard.Slot);
            if (player != null)
            {
                var watchers = playerWatchers[keyboard.Slot];
                if (watchers == null)
                {
                    watchers = new WatcherList();
                    playerWatchers[keyboard.Slot] = watchers;
                }

                RunMap(player, watchers, keyboard, bit, key);
            }
        }

        /// <summary>Runs the reaction on an actor's watchers, in add order: those there when the event came (one added by a reaction runs from the next event), skipping one that has ended.</summary>
        private static void RunMap(Actor actor, WatcherList watchers, Keyboard keyboard, int bit, Key key)
        {
            watchers.Refresh(actor, MaskOf);
            var found = watchers.Found;
            var masksFound = watchers.Masks;
            for (var i = 0; i < found.Count; i++)
            {
                var component = found[i];
                if (!component.HasEnded && (masksFound[i] & bit) != 0)
                {
                    Call(component, keyboard, bit, key);
                }
            }
        }

        /// <summary>Calls one reaction; a failing reaction is reported.</summary>
        private static void Call(Scope obj, Keyboard keyboard, int bit, Key key)
        {
            try
            {
                if (bit == Pressed)
                {
                    ((IKeyWatch)obj).OnKeyPressed(keyboard, key);
                }
                else
                {
                    ((IKeyWatch)obj).OnKeyReleased(keyboard, key);
                }
            }
            catch (Exception error)
            {
                Errors.Report(Names.NameOf(obj) + "." + (bit == Pressed ? "OnKeyPressed" : "OnKeyReleased"), error);
            }
        }

        /// <summary>The bits of the reaction methods an object's class writes, worked out once per class. Only methods the class writes count: in Lua an interface's empty defaults are not there.</summary>
        private static int MaskOf(object obj)
        {
            var key = Names.ClassOf(obj);
            if (masks.TryGetValue(key, out var mask))
            {
                return mask;
            }

            mask = 0;
            if (obj is IKeyWatch)
            {
                mask = (Hooks.Overrides(obj, "OnKeyPressed", typeof(object)) ? Pressed : 0) | (Hooks.Overrides(obj, "OnKeyReleased", typeof(object)) ? Released : 0);
            }

            masks[key] = mask;
            return mask;
        }
    }
}
