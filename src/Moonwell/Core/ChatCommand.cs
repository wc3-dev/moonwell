using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A chat command a scope made with <c>Command("load", (player, rest) => ...)</c> (decision 189): it hears a chat line that
    /// starts with a dash and its name, in any case, on every machine, until it or its scope ends.
    /// </summary>
    public sealed class ChatCommand : Scoped
    {
        private static List<ChatCommand>? made;
        private static bool listening;

        private readonly Action<Player, string> heard;

        private ChatCommand(string name, Action<Player, string> heard)
        {
            Name = name;
            this.heard = heard;
        }

        /// <summary>The word after the dash that the command hears, in lower case.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>Makes a command in a scope, and listens for every person's chat from the first one on.</summary>
        internal static ChatCommand Make(Scope scope, string name, Action<Player, string> heard)
        {
            if (name.Length == 0 || name.IndexOf(' ') >= 0 || name[0] == '-')
            {
                Dev.Report(Names.NameOf(scope) + ".Command", "\"" + name + "\" is no command name: the word after the dash, Command(\"load\") for -load");
            }

            var command = new ChatCommand(name.ToLower(), heard);
            if (made == null)
            {
                made = new List<ChatCommand>();
            }

            made.Add(command);
            if (!listening)
            {
                listening = true;
                foreach (var person in Player.Users)
                {
                    Runtime.World.ListenChat(person.Handle);
                }
            }

            scope.Adopt(command);
            return command;
        }

        /// <summary>A person typed a chat line: each command of its word runs, the oldest first.</summary>
        internal static void Heard(Player from, string text)
        {
            if (made == null || text.Length < 2 || text[0] != '-')
            {
                return;
            }

            var space = text.IndexOf(' ');
            var word = (space < 0 ? text.Substring(1) : text.Substring(1, space - 1)).ToLower();
            var rest = space < 0 ? "" : text.Substring(space + 1).Trim();
            var live = new List<ChatCommand>();
            for (var i = 0; i < made.Count; i++)
            {
                if (!made[i].HasEnded)
                {
                    live.Add(made[i]);
                }
            }

            // A command may make or end commands, so the ones heard are those made before the line.
            made = live;
            var commands = new List<ChatCommand>(live);
            foreach (var command in commands)
            {
                if (command.Name != word || command.HasEnded)
                {
                    continue;
                }

                try
                {
                    command.heard(from, rest);
                }
                catch (Exception error)
                {
                    Errors.Report("-" + word, error);
                }
            }
        }

        /// <summary>Forgets the commands; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            made = null;
            listening = false;
        }
    }
}
