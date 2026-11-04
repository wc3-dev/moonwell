using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The game as the keyboard sees it, through the game's natives; the only class of the keyboard that calls them.</summary>
    internal sealed class NativeKeyWorld : IKeyWorld
    {
        /// <summary>The code of every key, as Key numbers them: the game's OSKEY constants less the three that repeat a code and the plain Shift, Ctrl and Alt, which the game never sends (it sends the left and right ones).</summary>
        internal static readonly int[] Codes =
        {
            8, 9, 12, 13, 19, 20, 21, 23, 24, 25, 27, 28, 29, 30, 31,
            32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47,
            48, 49, 50, 51, 52, 53, 54, 55, 56, 57,
            65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
            91, 92, 93, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111,
            112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127,
            128, 129, 130, 131, 132, 133, 134, 135, 144, 145, 146, 147, 148, 149, 150,
            160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175,
            176, 177, 178, 179, 180, 181, 182, 183, 186, 187, 188, 189, 190, 191, 192,
            219, 220, 221, 222, 223, 225, 226, 227, 228, 229, 230, 231, 233, 234, 235, 236, 237, 238, 239,
            240, 241, 242, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254,
        };

        /// <summary>The prefix of the release messages.</summary>
        private const string Prefix = "mwkey";

        /// <summary>Seconds before this machine sends a release message again for the same count of downs, when the first has not arrived.</summary>
        private const float Resend = 1.0f;

        /// <summary>The code of each key's handle, filled at the first ListenKeys.</summary>
        private readonly Dictionary<oskeytype, int> codes = new Dictionary<oskeytype, int>();

        /// <summary>When this machine last sent a release message for each key; this machine's alone, and read only to send.</summary>
        private readonly float[] sentAt = new float[256];

        /// <summary>The count of downs each key's last release message from this machine was sent with.</summary>
        private readonly int[] sentDowns = new int[256];

        /// <summary>The count of downs each key had at the last check that found this machine not holding it; 0 once a check finds it held.</summary>
        private readonly int[] unheldDowns = new int[256];

        /// <summary>The key of the release message being delivered.</summary>
        private int releasedKey;

        /// <summary>The count of downs the release message being delivered was sent with.</summary>
        private int releasedDowns;

        /// <summary>What each slot's key events run; null until ListenKeys names it.</summary>
        private readonly Action?[] keyActions = new Action?[Player.MaxSlots];

        /// <summary>Whether each slot's keys are registered.</summary>
        private readonly bool[] registered = new bool[Player.MaxSlots];

        /// <summary>Registers the keys of every person playing now, by the game's own reads, so that it can run while the map loads, before Moonwell's players exist; ListenKeys names what they run.</summary>
        internal void RegisterPeople()
        {
            for (var slot = 0; slot < Player.PlayableSlots; slot++)
            {
                var p = Natives.Player(slot);
                if (Natives.GetPlayerSlotState(p) == Natives.PLAYER_SLOT_STATE_PLAYING && Natives.GetPlayerController(p) == Natives.MAP_CONTROL_USER)
                {
                    Register(slot);
                }
            }
        }

        /// <summary>Names what a player's key events run, registering the keys first unless RegisterPeople did.</summary>
        public void ListenKeys(player p, Action key)
        {
            var slot = Natives.GetPlayerId(p);
            keyActions[slot] = key;
            if (!registered[slot])
            {
                Register(slot);
            }
        }

        /// <summary>One trigger for a slot's keys: every key with every modifier (the game matches them exactly), pressed and released, 32 registrations a key, about 0.1 s in all.</summary>
        private void Register(int slot)
        {
            if (codes.Count == 0)
            {
                for (var i = 0; i < Codes.Length; i++)
                {
                    codes[Natives.ConvertOsKeyType(Codes[i])] = Codes[i];
                }
            }

            var p = Natives.Player(slot);
            var trigger = Natives.CreateTrigger();
            for (var i = 0; i < Codes.Length; i++)
            {
                var handle = Natives.ConvertOsKeyType(Codes[i]);
                for (var meta = 0; meta < 16; meta++)
                {
                    Natives.BlzTriggerRegisterPlayerKeyEvent(trigger, p, handle, meta, true);
                    Natives.BlzTriggerRegisterPlayerKeyEvent(trigger, p, handle, meta, false);
                }
            }

            Natives.TriggerAddAction(trigger, () =>
            {
                var action = keyActions[slot];
                if (action != null)
                {
                    action();
                }
            });
            registered[slot] = true;
        }

        /// <summary>The player of the event being delivered: the key event's, or the release message's sender.</summary>
        public player EventPlayer() => Natives.GetTriggerPlayer();

        /// <summary>The code of the event's key; 0 for a handle not among the keys.</summary>
        public int EventKey()
        {
            return codes.TryGetValue(Natives.BlzGetTriggerPlayerKey(), out var code) ? code : 0;
        }

        /// <summary>The event's modifiers.</summary>
        public int EventMeta() => Natives.BlzGetTriggerPlayerMetaKey();

        /// <summary>Whether the event is a press.</summary>
        public bool EventKeyDown() => Natives.BlzGetTriggerPlayerIsKeyDown();

        /// <summary>Whether this machine holds the key now.</summary>
        public bool IsKeyHeld(int key)
        {
            return key > 0 && key < 256 && Natives.BlzIsKeyPressed(Natives.ConvertOsKeyType(key));
        }

        /// <summary>One trigger for a player's leave.</summary>
        public void ListenLeave(player p, Action left)
        {
            var trigger = Natives.CreateTrigger();
            Natives.TriggerRegisterPlayerEvent(trigger, p, Natives.EVENT_PLAYER_LEAVE);
            Natives.TriggerAddAction(trigger, left);
        }

        /// <summary>One trigger for a player's release messages; the action reads the message's key and count of downs first.</summary>
        public void ListenReleases(player p, Action arrived)
        {
            var trigger = Natives.CreateTrigger();
            Natives.BlzTriggerRegisterPlayerSyncEvent(trigger, p, Prefix, false);
            Natives.TriggerAddAction(trigger, () =>
            {
                if (Read(Natives.BlzGetTriggerSyncData(), out releasedKey, out releasedDowns))
                {
                    arrived();
                }
            });
        }

        /// <summary>Reads a release message's "key:downs"; false for anything else.</summary>
        internal static bool Read(string data, out int key, out int downs)
        {
            key = 0;
            downs = 0;
            var colon = data.IndexOf(':');
            return colon > 0 && int.TryParse(data.Substring(0, colon), out key) && int.TryParse(data.Substring(colon + 1), out downs);
        }

        /// <summary>On that player's machine only, when it does not hold the key now: sends "key:downs", as Due says.</summary>
        public void SendIfLetGo(player p, int key, int downs)
        {
            if (Natives.GetLocalPlayer() != p || !Due(key, downs, IsKeyHeld(key), Runtime.World.Now()))
            {
                return;
            }

            Natives.BlzSendSyncData(Prefix, Natives.I2S(key) + ":" + Natives.I2S(downs));
        }

        /// <summary>
        /// Whether a check sends a release message, noting it: when this check and the one before found the key not held at
        /// the same count of downs (one alone is often a release whose event is still on its way), and none was sent for that
        /// count in the last second.
        /// </summary>
        internal bool Due(int key, int downs, bool held, float now)
        {
            if (held || unheldDowns[key] != downs)
            {
                unheldDowns[key] = held ? 0 : downs;
                return false;
            }

            if (sentDowns[key] == downs && now - sentAt[key] < Resend)
            {
                return false;
            }

            sentAt[key] = now;
            sentDowns[key] = downs;
            return true;
        }

        /// <summary>The key of the release message being delivered.</summary>
        public int EventReleasedKey() => releasedKey;

        /// <summary>The count of downs the release message being delivered was sent with.</summary>
        public int EventReleasedDowns() => releasedDowns;
    }
}
