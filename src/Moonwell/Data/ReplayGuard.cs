using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Keeps a replay from writing files: watching a replay runs the map again and its Preload files would be written over the
    /// player's newer ones (game facts, 11-03). At the start each person's machine shares a value only it knows; in a replay
    /// the recorded value arrives instead, so a machine whose own value comes back changed is watching one. Writes wait until
    /// the value is back, and are dropped in a replay.
    /// </summary>
    internal static class ReplayGuard
    {
        private static bool started;
        private static bool known;
        private static string sent = "";
        private static List<Action> waiting = new List<Action>();

        /// <summary>Whether this machine is watching a replay.</summary>
        internal static bool IsReplay { get; private set; }

        /// <summary>Whether a file may be written now: always before the guard starts, and after it only in a game, not a replay.</summary>
        internal static bool MayWrite => !started || (known && !IsReplay);

        /// <summary>Forgets the guard; the runtime's reset calls it.</summary>
        internal static void Reset()
        {
            started = false;
            known = false;
            sent = "";
            waiting = new List<Action>();
            IsReplay = false;
        }

        /// <summary>
        /// Asks every person for a value only their machine knows, in slot order on every machine, so the sharing requests
        /// stay numbered alike; each machine looks at its own person's answer only.
        /// </summary>
        internal static void Start()
        {
            started = true;
            var people = Player.Users;
            var mine = false;
            for (var i = 0; i < people.Count; i++)
            {
                var person = people[i];
                var local = Runtime.World.IsLocal(person.Handle);
                mine |= local;
                Sharing.Request(Game.Instance, person, () =>
                {
                    sent = Runtime.World.ClockSeconds().ToString();
                    return sent;
                }, text =>
                {
                    if (local)
                    {
                        Answered(text);
                    }
                });
            }

            if (!mine)
            {
                Answered(sent);
            }
        }

        /// <summary>Runs a write now, or once the guard knows this is no replay; in a replay it is dropped.</summary>
        internal static void WhenWritable(Action write)
        {
            if (MayWrite)
            {
                write();
            }
            else if (!known)
            {
                waiting.Add(write);
            }
        }

        private static void Answered(string? text)
        {
            known = true;
            IsReplay = text != sent;
            var writes = waiting;
            waiting = new List<Action>();
            if (IsReplay)
            {
                return;
            }

            for (var i = 0; i < writes.Count; i++)
            {
                writes[i]();
            }
        }
    }
}
