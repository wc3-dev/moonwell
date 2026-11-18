using Spectre.Console;

namespace Moonwell.Tool
{
    /// <summary><c>moonwell</c> with no command, in a terminal: a list of the commands to pick from, then what the picked one needs.</summary>
    internal static class CommandPicker
    {
        private static readonly (string Name, string Text, string[] Asks)[] Commands =
        {
            ("build", "Build the map in this folder", Array.Empty<string>()),
            ("run", "Build the map and start the game with it", Array.Empty<string>()),
            ("watch", "Build the map again after every change", Array.Empty<string>()),
            ("new", "Make a new map project", new[] { "Name of the map" }),
            ("import", "Make a map project from an old map", new[] { "The map to import (.w3x or .w3m)", "Name of the project" }),
            ("clean", @"Delete build\", Array.Empty<string>()),
            ("objects", "List the objects the map declares", Array.Empty<string>()),
            ("mdx", "Show the files a model uses", new[] { "The model (.mdx), a file or a game path" }),
            ("game find", "Find files in the game's storage", new[] { "Path in the game's storage" }),
            ("game extract", "Copy files out of the game's storage", new[] { "Path in the game's storage" }),
        };

        /// <summary>Asks for a command and what it needs, when the output is a terminal a person can answer in.</summary>
        /// <param name="output">Where the command writes.</param>
        /// <returns>The command's arguments, or null when there is no one to ask.</returns>
        public static string[]? Ask(TextWriter output)
        {
            var say = new Say(output);
            var console = say.Console;
            if (!console.Profile.Capabilities.Interactive || Console.IsInputRedirected)
            {
                return null;
            }

            var picked = console.Prompt(new SelectionPrompt<(string Name, string Text, string[] Asks)>()
                .Title("What should moonwell do?")
                .PageSize(Commands.Length)
                .UseConverter(command => $"[bold]{command.Name,-13}[/][dim]{command.Text.EscapeMarkup()}[/]")
                .AddChoices(Commands));
            say.Line($"[dim]moonwell[/] [bold]{picked.Name}[/]");
            return picked.Name.Split(' ')
                .Concat(picked.Asks.Select(ask => console.Prompt(new TextPrompt<string>(ask.EscapeMarkup() + ":"))))
                .ToArray();
        }
    }
}
