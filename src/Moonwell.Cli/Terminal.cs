using Spectre.Console;

namespace Moonwell.Tool
{
    /// <summary>The tool's Spectre console over its output.</summary>
    internal static class Terminal
    {
        /// <summary>
        /// A console that styles only a terminal without NO_COLOR; anywhere else (CI, a redirected file, the tests) it writes plain
        /// text, with no colour codes and no wrapping (decision 363).
        /// </summary>
        /// <param name="output">Where the command writes.</param>
        /// <param name="styled">True when the console writes colours and may redraw.</param>
        /// <returns>The console.</returns>
        public static IAnsiConsole Over(TextWriter output, out bool styled)
        {
            var terminal = new AnsiConsoleOutput(output).IsTerminal;
            styled = terminal && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(output),
                Ansi = styled ? AnsiSupport.Detect : AnsiSupport.No,
                ColorSystem = styled ? ColorSystemSupport.Detect : ColorSystemSupport.NoColors,
                Interactive = terminal ? InteractionSupport.Detect : InteractionSupport.No,
            });
            styled = styled && console.Profile.Capabilities.Ansi;
            if (!terminal)
            {
                console.Profile.Width = 4096;
            }

            return console;
        }
    }
}
