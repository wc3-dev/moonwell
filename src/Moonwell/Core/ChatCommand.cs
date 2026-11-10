using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A chat command a scope made with <c>Command("load", (player, rest) => ...)</c> (decision 189): it hears a chat line that
    /// starts with a dash and its name, in any case, on every machine, until it or its scope ends. A name that is empty, holds
    /// a space or starts with the dash is refused in every build (decision 191).
    /// </summary>
    public sealed class ChatCommand : Scoped
    {
        private static List<ChatCommand>? made;
        private static bool listening;
        private static bool hearing;

        private readonly Action<Player, string> heard;

        private ChatCommand(string name, Action<Player, string> heard)
        {
            Name = name;
            this.heard = heard;
        }

        /// <summary>How many commands are kept: those not ended, and those ended while a line is heard.</summary>
        internal static int Count => made?.Count ?? 0;

        /// <summary>The word after the dash that the command hears, in lower case.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>Makes a command in a scope, and listens for every person's chat from the first one on.</summary>
        internal static ChatCommand Make(Scope scope, string name, Action<Player, string> heard)
        {
            if (name.Length == 0 || name.IndexOf(' ') >= 0 || name[0] == '-')
            {
                Errors.Refuse(Names.NameOf(scope) + ".Command", "\"" + name + "\" is no command name: the word after the dash, Command(\"load\") for -load");
                var refused = new ChatCommand(name, heard);
                refused.End();
                return refused;
            }

            var command = new ChatCommand(name.ToLower(), heard);
            if (command.Name == "debug" || command.Name == "step" || command.Name == "speed" || command.Name == "stats")
            {
                Dev.Report(Names.NameOf(scope) + ".Command", "-" + command.Name + " is also a debug command, so in a development build both run");
            }
#if DEBUG
            else if (DebugTools.IsBuiltIn(command.Name))
            {
                Dev.Report(Names.NameOf(scope) + ".Command", "-" + command.Name + " is also a debug command: in a development build this one runs and the debug one steps aside");
            }
#endif

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

        /// <summary>
        /// A person typed a chat line with a dash in it, anywhere, as the game's chat event gives it: when it starts with the
        /// dash, each command of its word runs, the oldest first.
        /// </summary>
        internal static void Heard(Player from, string text)
        {
            if (made == null || text.Length < 2 || text[0] != '-')
            {
                return;
            }

            var space = text.IndexOf(' ');
            var word = (space < 0 ? text.Substring(1) : text.Substring(1, space - 1)).ToLower();
            var rest = space < 0 ? "" : text.Substring(space + 1).Trim();

            // A command may make or end commands: those made during the line wait for the next, and those ended leave after it.
            var count = made.Count;
            hearing = true;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var command = made[i];
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
            finally
            {
                hearing = false;
            }

            var kept = 0;
            for (var i = 0; i < made.Count; i++)
            {
                if (!made[i].HasEnded)
                {
                    made[kept++] = made[i];
                }
            }

            made.RemoveRange(kept, made.Count - kept);
        }

        /// <summary>Leaves the list of commands, at once or, while a line is being heard, once it has been.</summary>
        internal override void Finish()
        {
            if (!hearing)
            {
                made?.Remove(this);
            }
        }

        /// <summary>Whether a command of that word, in lower case, is live.</summary>
        internal static bool Has(string word)
        {
            if (made == null)
            {
                return false;
            }

            for (var i = 0; i < made.Count; i++)
            {
                if (made[i].Name == word && !made[i].HasEnded)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The words of the live commands, each once, in the order first made.</summary>
        internal static List<string> LiveNames()
        {
            var names = new List<string>();
            if (made == null)
            {
                return names;
            }

            for (var i = 0; i < made.Count; i++)
            {
                if (!made[i].HasEnded && !names.Contains(made[i].Name))
                {
                    names.Add(made[i].Name);
                }
            }

            return names;
        }

        /// <summary>Forgets the commands; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            made = null;
            listening = false;
            hearing = false;
        }
    }
}
