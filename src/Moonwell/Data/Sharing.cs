using System;
using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// What one machine knows, sent to every machine: a request's read runs on its player's machine only, its answer goes out
    /// as sync messages, and the callback runs on every machine once the last piece is in (decisions 173 and 179). Every
    /// machine makes the same requests in the same order, so a request's number is the same everywhere, and each message
    /// carries it: <c>number:index:count:piece</c>.
    /// </summary>
    internal static class Sharing
    {
        /// <summary>The prefix of the sharing messages.</summary>
        internal const string Prefix = "mwshare";

        /// <summary>The most one sync message carries, in bytes; the game cuts the rest without a word (game facts, 11-03).</summary>
        internal const int MessageLength = 255;

        // A zero byte ends a sync message (game facts, 11-03): it goes as Escape and '0', and Escape itself doubled.
        private const char Escape = '\u0001';

        private static int made;
        private static OrderedMap<int, Pending> waiting = new OrderedMap<int, Pending>();
        private static bool[] listened = new bool[Player.MaxSlots];

        /// <summary>Forgets every request; the runtime's reset calls it.</summary>
        internal static void Reset()
        {
            made = 0;
            waiting = new OrderedMap<int, Pending>();
            listened = new bool[Player.MaxSlots];
        }

        /// <summary>
        /// Asks the player's machine for what <paramref name="read"/> gives and hands it to <paramref name="arrived"/> on every
        /// machine, as a child of the scope: nothing runs if the scope ends first. A player who leaves first gives null, and so
        /// does a player who is not a person playing, in the next instant.
        /// </summary>
        internal static void Request(Scope scope, Player player, Func<string?> read, Action<string?> arrived)
        {
            made++;
            var request = new Pending(made, player, arrived);
            scope.Adopt(request);
            if (request.HasEnded)
            {
                return;
            }

            if (player.SlotState != SlotState.Playing || player.Controller != Controller.User)
            {
                scope.OwnAfter(0.0f, () => request.Answer(null));
                return;
            }

            waiting[request.Number] = request;
            Listen(player);
            Events.Need(Hook.OnLeave);
            if (!Runtime.World.IsLocal(player.Handle))
            {
                return;
            }

            string? text;
            Local.Depth++;
            try
            {
                text = read();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Share", error);
                text = null;
            }
            finally
            {
                Local.Depth--;
            }

            var messages = Messages(request.Number, text);
            for (var i = 0; i < messages.Count; i++)
            {
                Runtime.World.SendSync(messages[i]);
            }
        }

        /// <summary>The messages that carry an answer; null goes as a count of 0, and each fits a sync message.</summary>
        internal static List<string> Messages(int number, string? text)
        {
            var messages = new List<string>();
            if (text == null)
            {
                messages.Add(number + ":0:0:");
                return messages;
            }

            var escaped = Escaped(text);
            // The index and the count take 5 digits at most: 99,999 pieces is far past any answer.
            var room = MessageLength - (number.ToString().Length + 13);
            var count = escaped.Length == 0 ? 1 : (escaped.Length + room - 1) / room;
            for (var i = 0; i < count; i++)
            {
                var start = i * room;
                messages.Add(number + ":" + i + ":" + count + ":" + escaped.Substring(start, Math.Min(room, escaped.Length - start)));
            }

            return messages;
        }

        /// <summary>A sharing message arrived from a player; the world's sync event calls it.</summary>
        internal static void Arrived(Player from, string data)
        {
            var first = data.IndexOf(':');
            var second = first < 0 ? -1 : data.IndexOf(':', first + 1);
            var third = second < 0 ? -1 : data.IndexOf(':', second + 1);
            if (third < 0)
            {
                return;
            }

            var number = Number(data, 0, first);
            var index = Number(data, first + 1, second);
            var count = Number(data, second + 1, third);
            if (number < 0 || index < 0 || count < 0 || !waiting.TryGetValue(number, out var request) || request.Player != from)
            {
                return;
            }

            if (!request.Take(index, count, data.Substring(third + 1)))
            {
                return;
            }

            waiting.Remove(number);
            request.Answer(request.Joined());
        }

        /// <summary>A player left: each of their requests still waiting ends with null, oldest first.</summary>
        internal static void Left(Player leaving)
        {
            var gone = new List<Pending>();
            for (var i = 0; i < waiting.Count; i++)
            {
                if (waiting.ValueAt(i).Player == leaving)
                {
                    gone.Add(waiting.ValueAt(i));
                }
            }

            for (var i = 0; i < gone.Count; i++)
            {
                waiting.Remove(gone[i].Number);
                gone[i].Answer(null);
            }
        }

        private static void Listen(Player player)
        {
            if (!listened[player.Slot])
            {
                listened[player.Slot] = true;
                Runtime.World.ListenSync(player.Handle);
            }
        }

        private static string Escaped(string text)
        {
            var escaped = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\0')
                {
                    escaped.Append(Escape).Append('0');
                }
                else if (c == Escape)
                {
                    escaped.Append(Escape).Append(Escape);
                }
                else
                {
                    escaped.Append(c);
                }
            }

            return escaped.ToString();
        }

        private static string Unescaped(string text)
        {
            var plain = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == Escape && i + 1 < text.Length)
                {
                    i++;
                    plain.Append(text[i] == '0' ? '\0' : text[i]);
                }
                else
                {
                    plain.Append(c);
                }
            }

            return plain.ToString();
        }

        // The whole number between two places of a text; -1 when it is not one.
        private static int Number(string text, int start, int end)
        {
            if (end <= start || end - start > 9)
            {
                return -1;
            }

            var value = 0;
            for (var i = start; i < end; i++)
            {
                var digit = text[i] - '0';
                if (digit < 0 || digit > 9)
                {
                    return -1;
                }

                value = value * 10 + digit;
            }

            return value;
        }

        /// <summary>A request waiting for its answer: a child of the scope it was made in.</summary>
        private sealed class Pending : Scoped
        {
            private readonly Action<string?> arrived;
            private string?[] pieces = new string?[0];
            private int got;
            private bool none;

            internal Pending(int number, Player player, Action<string?> arrived)
            {
                Number = number;
                Player = player;
                this.arrived = arrived;
            }

            internal int Number { get; }

            internal Player Player { get; }

            /// <summary>Keeps a piece; whether the answer is now whole.</summary>
            internal bool Take(int index, int count, string piece)
            {
                if (count == 0)
                {
                    none = true;
                    return true;
                }

                if (pieces.Length != count)
                {
                    pieces = new string?[count];
                    got = 0;
                }

                if (index >= count || pieces[index] != null)
                {
                    return false;
                }

                pieces[index] = piece;
                got++;
                return got == count;
            }

            /// <summary>The answer, its pieces joined: null when the player's machine sent none.</summary>
            internal string? Joined()
            {
                if (none)
                {
                    return null;
                }

                var text = new StringBuilder();
                for (var i = 0; i < pieces.Length; i++)
                {
                    text.Append(pieces[i]);
                }

                return Unescaped(text.ToString());
            }

            /// <summary>Hands the answer over, unless the scope ended first, and ends the request.</summary>
            internal void Answer(string? text)
            {
                if (HasEnded)
                {
                    return;
                }

                try
                {
                    arrived(text);
                }
                catch (Exception error)
                {
                    Errors.Report("Moonwell.Share", error);
                }

                End();
            }
        }
    }
}
