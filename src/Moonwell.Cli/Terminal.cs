using System.Text;
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

    /// <summary>
    /// The one way the tool writes a line (decision 368): a result, a note, warning or error, or a line of markup, styled in a
    /// terminal and the same words plain anywhere else (decision 363).
    /// </summary>
    internal sealed class Say
    {
        private readonly TextWriter writer;

        public Say(TextWriter writer)
        {
            this.writer = writer;
            Console = Terminal.Over(writer, out var styled);
            Styled = styled;
        }

        /// <summary>The console over the writer, for a prompt or a tree.</summary>
        public IAnsiConsole Console { get; }

        /// <summary>True when the lines are coloured and may wrap.</summary>
        public bool Styled { get; }

        /// <summary>A line of Spectre markup; plain, its markup is taken out.</summary>
        /// <param name="markup">The line.</param>
        public void Line(string markup = "")
        {
            if (Styled)
            {
                Console.MarkupLine(markup);
            }
            else
            {
                writer.WriteLine(markup.RemoveMarkup());
            }
        }

        /// <summary>What a command did, after a green tick.</summary>
        /// <param name="text">The sentence, not markup.</param>
        public void Result(string text) => Line($"[green]✓[/] {text.EscapeMarkup()}");

        /// <summary>
        /// A note, warning or error: its word, then the text. In a terminal the text wraps at its own indent with each item on a
        /// line under it; elsewhere it is one line, for tools to read.
        /// </summary>
        /// <param name="kind">note, warning or error.</param>
        /// <param name="text">The text, not markup.</param>
        /// <param name="indent">The spaces before the word.</param>
        /// <param name="items">A list the text ends in.</param>
        public void Message(string kind, string text, int indent = 0, IReadOnlyList<string>? items = null)
        {
            items ??= Array.Empty<string>();
            if (!Styled)
            {
                writer.WriteLine($"{new string(' ', indent)}{kind,-7}  {(items.Count == 0 ? text : text + " " + string.Join(", ", items))}");
                return;
            }

            var grid = new Grid()
                .AddColumn(new GridColumn().NoWrap().Width(7).Padding(indent, 0, 2, 0))
                .AddColumn(new GridColumn().Padding(0, 0, 0, 0));
            Console.Write(grid.AddRow(new Markup(Kind(kind)), new Markup(string.Join('\n', items.Prepend(text)).EscapeMarkup())));
        }

        /// <summary>A kind's word in its colour, as wide as the widest.</summary>
        /// <param name="kind">note, warning or error.</param>
        /// <returns>The markup.</returns>
        public static string Kind(string kind)
        {
            var colour = kind switch
            {
                "error" => "red",
                "warning" => "yellow",
                _ => "blue",
            };
            return $"[{colour}]{kind,-7}[/]";
        }

        /// <summary>A writer for the lines the build library logs (<c>moonwell: text</c>, <c>warning: text</c>), each shown as a note or a warning.</summary>
        /// <returns>The writer.</returns>
        public TextWriter Log() => new LogLines(this);

        private sealed class LogLines : TextWriter
        {
            private readonly Say say;
            private readonly StringBuilder pending = new StringBuilder();

            public LogLines(Say say)
            {
                this.say = say;
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value != '\n')
                {
                    pending.Append(value);
                    return;
                }

                var line = pending.ToString().TrimEnd('\r');
                pending.Clear();
                line = line.StartsWith("moonwell: ", StringComparison.Ordinal) ? line["moonwell: ".Length..] : line;
                var colon = line.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0 && line[..colon] is "warning" or "note")
                {
                    say.Message(line[..colon], line[(colon + 2)..]);
                }
                else if (line.Trim().Length > 0)
                {
                    say.Message("note", line.Trim());
                }
            }
        }
    }
}
