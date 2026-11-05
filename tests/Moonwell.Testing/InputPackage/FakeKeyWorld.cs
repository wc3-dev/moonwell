using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>The fake game the keyboard's cases run against: key events and release messages a case sends by hand, the keys a case holds on this machine, and a record of the release messages sent.</summary>
    public sealed class FakeKeyWorld : IKeyWorld
    {
        /// <summary>What the keyboard gave for one player.</summary>
        private sealed class Listened
        {
            /// <summary>The player listened for.</summary>
            public player Player = new player();

            /// <summary>What runs on a key event; null until ListenKeys.</summary>
            public Action? Key;

            /// <summary>What runs when a release message arrives; null until ListenReleases.</summary>
            public Action? Arrived;

            /// <summary>What runs when the player leaves; null until ListenLeave.</summary>
            public Action? Left;

            /// <summary>When the player's machine sends a release message, by the game side's own rule.</summary>
            public Moonwell.Engine.NativeKeyWorld Sends = new Moonwell.Engine.NativeKeyWorld();
        }

        /// <summary>A release message sent and not yet delivered.</summary>
        private sealed class Message
        {
            /// <summary>The player who sent it.</summary>
            public player From = new player();

            /// <summary>The key's code.</summary>
            public int Key;

            /// <summary>The key's count of downs when it was sent.</summary>
            public int Downs;
        }

        /// <summary>Each player listened for, in the order ListenKeys or ListenReleases first named them.</summary>
        private readonly List<Listened> listens = new List<Listened>();

        /// <summary>The messages sent and not delivered, in order.</summary>
        private readonly List<Message> queued = new List<Message>();

        /// <summary>The slots ListenKeys was called for, in order.</summary>
        public List<int> ListenedSlots = new List<int>();

        /// <summary>The keys (by code, 0 to 255) held on this machine now.</summary>
        public bool[] Held = new bool[256];

        /// <summary>The release messages sent, as "slot code downs", in order.</summary>
        public List<string> Sent = new List<string>();

        /// <summary>The player of the event being delivered.</summary>
        private player? eventPlayer;

        /// <summary>The code of the key event being delivered.</summary>
        private int eventKey;

        /// <summary>The modifiers of the key event being delivered.</summary>
        private int eventMeta;

        /// <summary>Whether the key event being delivered is a press.</summary>
        private bool eventDown;

        /// <summary>The key of the release message being delivered.</summary>
        private int eventReleasedKey;

        /// <summary>The count of downs the release message being delivered was sent with.</summary>
        private int eventReleasedDowns;

        /// <summary>Sends a press of a key, with the modifiers (1 Shift, 2 Ctrl, 4 Alt).</summary>
        public void Press(Player player, Key key, int meta = 0)
        {
            Send(player, (int)key, meta, true);
        }

        /// <summary>Sends a release of a key, with the modifiers held as it comes up.</summary>
        public void Release(Player player, Key key, int meta = 0)
        {
            Send(player, (int)key, meta, false);
        }

        /// <summary>Sends a key event of any code; the player must have been listened for.</summary>
        public void Send(Player player, int code, int meta, bool down)
        {
            var found = Find(player.Handle);
            if (found == null || found.Key == null)
            {
                throw new ExpectationFailed("The keyboard did not listen for slot " + player.Slot + ".");
            }

            var beforePlayer = eventPlayer;
            var beforeKey = eventKey;
            var beforeMeta = eventMeta;
            var beforeDown = eventDown;
            eventPlayer = player.Handle;
            eventKey = code;
            eventMeta = meta;
            eventDown = down;
            try
            {
                found.Key();
            }
            finally
            {
                eventPlayer = beforePlayer;
                eventKey = beforeKey;
                eventMeta = beforeMeta;
                eventDown = beforeDown;
            }
        }

        /// <summary>Delivers a release message as if a player had sent it, whatever was sent.</summary>
        public void Arrive(Player player, int key, int downs)
        {
            var found = Find(player.Handle);
            if (found == null || found.Arrived == null)
            {
                throw new ExpectationFailed("The keyboard did not listen for slot " + player.Slot + "'s messages.");
            }

            var beforePlayer = eventPlayer;
            var beforeKey = eventReleasedKey;
            var beforeDowns = eventReleasedDowns;
            eventPlayer = player.Handle;
            eventReleasedKey = key;
            eventReleasedDowns = downs;
            try
            {
                found.Arrived();
            }
            finally
            {
                eventPlayer = beforePlayer;
                eventReleasedKey = beforeKey;
                eventReleasedDowns = beforeDowns;
            }
        }

        /// <summary>Delivers a player's leave, as the game's leave event does.</summary>
        public void Leave(Player player)
        {
            var found = Find(player.Handle);
            if (found == null || found.Left == null)
            {
                throw new ExpectationFailed("The keyboard did not listen for slot " + player.Slot + "'s leave.");
            }

            var beforePlayer = eventPlayer;
            eventPlayer = player.Handle;
            try
            {
                found.Left();
            }
            finally
            {
                eventPlayer = beforePlayer;
            }
        }

        /// <summary>Delivers every message sent and not yet delivered, in order, as the game would to every machine.</summary>
        public void Deliver()
        {
            var messages = new List<Message>(queued);
            queued.Clear();
            for (var i = 0; i < messages.Count; i++)
            {
                Arrive(Player.Of(messages[i].From)!, messages[i].Key, messages[i].Downs);
            }
        }

        /// <summary>The record of a player listened for; null when none.</summary>
        private Listened? Find(player p)
        {
            for (var i = 0; i < listens.Count; i++)
            {
                if (listens[i].Player == p)
                {
                    return listens[i];
                }
            }

            return null;
        }

        /// <summary>The record of a player, made when there is none.</summary>
        private Listened Of(player p)
        {
            var found = Find(p);
            if (found == null)
            {
                found = new Listened();
                found.Player = p;
                listens.Add(found);
            }

            return found;
        }

        /// <summary>Remembers what to run on a player's key events.</summary>
        public void ListenKeys(player p, Action key)
        {
            Of(p).Key = key;
            ListenedSlots.Add(Player.Of(p)!.Slot);
        }

        /// <summary>The player of the event being delivered.</summary>
        public player EventPlayer() => eventPlayer!;

        /// <summary>The code of the key event being delivered.</summary>
        public int EventKey() => eventKey;

        /// <summary>The modifiers of the key event being delivered.</summary>
        public int EventMeta() => eventMeta;

        /// <summary>Whether the key event being delivered is a press.</summary>
        public bool EventKeyDown() => eventDown;

        /// <summary>Whether a case holds the key on this machine; false for a code past 255.</summary>
        public bool IsKeyHeld(int key) => key >= 0 && key < Held.Length && Held[key];

        /// <summary>Remembers what to run when a player leaves.</summary>
        public void ListenLeave(player p, Action left)
        {
            Of(p).Left = left;
        }

        /// <summary>Remembers what to run when a player's release message arrives.</summary>
        public void ListenReleases(player p, Action arrived)
        {
            Of(p).Arrived = arrived;
        }

        /// <summary>Queues a release message as that player's machine would: by NativeKeyWorld.Due, from whether the case holds the key.</summary>
        public void SendIfLetGo(player p, int key, int downs)
        {
            if (!Of(p).Sends.Due(key, downs, IsKeyHeld(key), Runtime.World.Now()))
            {
                return;
            }

            Sent.Add(Player.Of(p)!.Slot + " " + key + " " + downs);
            var message = new Message();
            message.From = p;
            message.Key = key;
            message.Downs = downs;
            queued.Add(message);
        }

        /// <summary>The key of the release message being delivered.</summary>
        public int EventReleasedKey() => eventReleasedKey;

        /// <summary>The count of downs the release message being delivered was sent with.</summary>
        public int EventReleasedDowns() => eventReleasedDowns;
    }
}
