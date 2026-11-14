using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Moonwell.Build;
using Spectre.Console;

namespace Moonwell.Tool
{
    /// <summary>
    /// Shows a build as a checklist of its steps (decision 361). It reads the build's log and steps and writes each step with its
    /// time, Moonwell's notes under the step that made them, and the errors and warnings of dotnet build and CSharp.lua in one form,
    /// once each. In a terminal the running step has a spinner; anywhere else, or with NO_COLOR, the same lines are plain text,
    /// each step's line once it ends (decision 363). Verbose adds the whole log as it comes.
    /// </summary>
    internal sealed class BuildView : TextWriter
    {
        // path(line,column): error CS1061: text [project], or MSBuild's and a project's "origin : error NU1101: text".
        private static readonly Regex Diagnostic = new Regex(@"^(?<file>.*?)(?:\((?<line>\d+),(?<column>\d+)(?:,\d+,\d+)?\))?\s*:\s*(?<kind>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<text>.*?)(?:\s+\[[^\]]*\])?$", RegexOptions.CultureInvariant);

        // CSharp.lua's stop, as LuaCompiler words it: path(line,column): text.
        private static readonly Regex Located = new Regex(@"^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s*(?<text>.*)$", RegexOptions.CultureInvariant);

        private static readonly Regex Header = new Regex(@"^moonwell: (?<name>.+) \((?<kind>development|release) build\)$", RegexOptions.CultureInvariant);

        private const string Built = "moonwell: built ";

        private readonly TextWriter output;
        private readonly TextWriter error;
        private readonly IAnsiConsole? errorConsole;
        private readonly IAnsiConsole? console;
        private readonly bool verbose;
        private readonly bool live;
        private readonly object gate = new object();
        private readonly StringBuilder pending = new StringBuilder();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly List<Line> before = new List<Line>();
        private readonly List<Step> steps = new List<Step>();
        private readonly List<Line> problems = new List<Line>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private bool spinning;
        private string? archive;
        private bool inSilencedList;

        private BuildView(TextWriter output, TextWriter error, bool verbose)
        {
            this.output = output;
            this.error = error;
            var errorTerminal = Terminal.Over(error, out var errorStyled);
            errorConsole = errorStyled ? errorTerminal : null;
            this.verbose = verbose;
            var terminal = Terminal.Over(output, out var styled);
            if (styled)
            {
                console = terminal;
                live = !verbose && terminal.Profile.Capabilities.Interactive;
            }
        }

        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>Runs a build and shows it.</summary>
        /// <param name="output">Where the checklist goes.</param>
        /// <param name="error">Where a failed build's errors and warnings go.</param>
        /// <param name="verbose">True to show the whole log too.</param>
        /// <param name="build">The build, given the log and the step callback; gives the archive's path.</param>
        /// <returns>The archive's path, or null when the build failed and the view showed why.</returns>
        public static string? Show(TextWriter output, TextWriter error, bool verbose, Func<TextWriter, Action<string>, string> build)
        {
            return new BuildView(output, error, verbose).Run(build);
        }

        public override void Write(char value)
        {
            lock (gate)
            {
                if (value == '\n')
                {
                    Take(pending.ToString().TrimEnd('\r'));
                    pending.Clear();
                }
                else
                {
                    pending.Append(value);
                }
            }
        }

        public override void Write(string? value)
        {
            foreach (var letter in value ?? string.Empty)
            {
                Write(letter);
            }
        }

        private string? Run(Func<TextWriter, Action<string>, string> build)
        {
            Exception? failure = null;
            string? built = null;
            void Work()
            {
                try
                {
                    built = build(this, Start);
                }
                catch (Exception error)
                {
                    failure = error;
                }
            }

            if (live)
            {
                // Only the running step's line is drawn again, from its start: moving the cursor up miscounts in some
                // terminals (the Moonwell Editor's), so nothing above it is ever redrawn.
                output.Write("\u001b[?25l");
                var work = Task.Run(Work);
                while (!work.Wait(80))
                {
                    lock (gate)
                    {
                        Spin();
                    }
                }

                output.Write("\u001b[?25h");
            }
            else
            {
                Work();
            }

            lock (gate)
            {
                TakeRest();
                End(failure != null);
            }

            if (failure != null && failure is not BuildException)
            {
                ExceptionDispatchInfo.Throw(failure);
            }

            if (failure is BuildException stop)
            {
                Failed(stop.Message);
            }

            if (problems.Count > 0)
            {
                Print(string.Empty, failure != null);
            }

            for (var i = 0; i < problems.Count; i++)
            {
                // In a terminal a problem takes several lines, so a blank one keeps each apart.
                if (i > 0 && (failure != null ? errorConsole : console) != null)
                {
                    Print(string.Empty, failure != null);
                }

                Print(problems[i], 2, failure != null);
            }

            var errors = problems.Count(problem => problem.Kind == "error");
            var warnings = problems.Count(problem => problem.Kind == "warning") + steps.Sum(step => step.Notes.Count(note => note.Kind == "warning")) + before.Count(note => note.Kind == "warning");
            Print(string.Empty, failure != null);
            if (failure != null)
            {
                Print($"[red]✗[/] Build failed: {Count(errors, "error")}{(warnings > 0 ? ", " + Count(warnings, "warning") : string.Empty)}", true);
                return null;
            }

            Print($"[green]✓[/] Built {(archive ?? built).EscapeMarkup()} in {Seconds(clock.Elapsed)}{(warnings > 0 ? $" [yellow]({Count(warnings, "warning")})[/]" : string.Empty)}");
            return built;
        }

        // A step starts: the one before it ends.
        private void Start(string name)
        {
            lock (gate)
            {
                End(false);
                steps.Add(new Step(name));
            }
        }

        private void End(bool failed)
        {
            if (steps.Count == 0 || steps[^1].Took != null)
            {
                return;
            }

            var step = steps[^1];
            step.Took = step.Clock.Elapsed;
            step.Failed = failed;
            Print(StepLine(step));
            foreach (var note in step.Notes)
            {
                Print(note, 6);
            }
        }

        private void TakeRest()
        {
            if (pending.Length > 0)
            {
                Take(pending.ToString());
                pending.Clear();
            }
        }

        // One line of the build's log: shown at once when verbose, then sorted into the header, a note or a problem.
        private void Take(string line)
        {
            if (verbose)
            {
                Print($"[dim]{line.EscapeMarkup()}[/]");
            }

            if (inSilencedList && line.StartsWith("  ", StringComparison.Ordinal))
            {
                return;
            }

            inSilencedList = false;
            if (Header.Match(line) is { Success: true } found)
            {
                Print($"[bold]{found.Groups["name"].Value.EscapeMarkup()}[/] [dim]·[/] {found.Groups["kind"].Value} build");
                Print(string.Empty);
            }
            else if (line.StartsWith(Built, StringComparison.Ordinal))
            {
                archive = line[Built.Length..];
            }
            else if (line.StartsWith("moonwell: ", StringComparison.Ordinal))
            {
                var text = line["moonwell: ".Length..];

                // The places behind a rule turned off are listed in build\silenced.txt and shown when verbose.
                inSilencedList = text.EndsWith("):", StringComparison.Ordinal);
                Note(new Line("note", string.Empty, string.Empty, inSilencedList ? text[..^1] : text));
            }
            else if (line.StartsWith("note: ", StringComparison.Ordinal) || line.StartsWith("warning: ", StringComparison.Ordinal))
            {
                var colon = line.IndexOf(':');
                Note(new Line(line[..colon], string.Empty, string.Empty, line[(colon + 2)..]));
            }
            else if (Diagnostic.Match(line) is { Success: true } diagnostic)
            {
                Problem(FromDiagnostic(diagnostic));
            }
        }

        // The build's error: the lines of a Lua step's errors in the compiler's form, or Moonwell's own message.
        private void Failed(string message)
        {
            var lines = message.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();

            // The errors of a failed dotnet build were shown from its output; what follows are hints.
            var dotnet = lines.Count > 0 && lines[0] == "dotnet build failed";
            if (dotnet)
            {
                lines.RemoveAt(0);
                if (!problems.Any(problem => problem.Kind == "error"))
                {
                    Problem(new Line("error", string.Empty, string.Empty, "dotnet build failed; moonwell build --verbose shows its output"));
                }
            }

            var located = lines.Any(line => Diagnostic.IsMatch(line) || Located.IsMatch(line));
            foreach (var line in lines)
            {
                if (Diagnostic.Match(line) is { Success: true } diagnostic)
                {
                    Problem(FromDiagnostic(diagnostic));
                }
                else if (Located.Match(line) is { Success: true } at)
                {
                    Problem(new Line("error", Where(at.Groups["file"].Value, at.Groups["line"].Value, at.Groups["column"].Value), string.Empty, at.Groups["text"].Value));
                }
                else
                {
                    Problem(new Line(dotnet || located ? "note" : "error", string.Empty, string.Empty, line));
                }
            }
        }

        private void Note(Line note)
        {
            if (steps.Count == 0)
            {
                before.Add(note);
                Print(note, 2);
            }
            else
            {
                steps[^1].Notes.Add(note);
            }
        }

        private void Problem(Line problem)
        {
            if (seen.Add($"{problem.Kind}|{problem.Where}|{problem.Code}|{problem.Text}"))
            {
                problems.Add(problem);
            }
        }

        private static Line FromDiagnostic(Match found)
        {
            return new Line(found.Groups["kind"].Value, Where(found.Groups["file"].Value, found.Groups["line"].Value, found.Groups["column"].Value), found.Groups["code"].Value, found.Groups["text"].Value);
        }

        // The file relative to the folder the command runs in when it is inside it, as editors open it from there, with its line and column.
        private static string Where(string file, string line, string column)
        {
            file = file.Trim();
            if (Path.IsPathFullyQualified(file))
            {
                var relative = Path.GetRelativePath(Environment.CurrentDirectory, file);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relative))
                {
                    file = relative;
                }
            }

            return line.Length > 0 ? $"{file}({line},{column})" : file;
        }

        // The running step's line, drawn over itself.
        private void Spin()
        {
            if (steps.Count > 0 && steps[^1].Took == null)
            {
                output.Write("\r\u001b[2K");
                console!.Markup(StepLine(steps[^1]));
                spinning = true;
            }
        }

        private string StepLine(Step step)
        {
            var mark = step.Took == null ? $"[yellow]{Frame()}[/]"
                : step.Failed ? "[red]✗[/]"
                : "[green]✓[/]";
            return $"  {mark} {step.Name.EscapeMarkup(),-16}[dim]{Seconds(step.Took ?? step.Clock.Elapsed)}[/]";
        }

        private string Frame()
        {
            var spinner = Spinner.Known.Dots;
            return spinner.Frames[(int)(clock.ElapsedMilliseconds / (long)spinner.Interval.TotalMilliseconds) % spinner.Frames.Count];
        }

        private static string Markup(Line line)
        {
            var where = line.Where.Length > 0 ? line.Where.EscapeMarkup() + "  " : string.Empty;
            var code = line.Code.Length > 0 ? $"[dim]{line.Code.EscapeMarkup()}[/]  " : string.Empty;
            return $"{Kind(line)}  {where}{code}{line.Text.EscapeMarkup()}";
        }

        private static string Kind(Line line)
        {
            var colour = line.Kind switch
            {
                "error" => "red",
                "warning" => "yellow",
                _ => "blue",
            };
            return $"[{colour}]{line.Kind,-7}[/]";
        }

        // A problem or a note. In a terminal (decision 366) a problem's place and code take a line and its text goes under it,
        // and any text wraps at its own indent, never back at the left edge; elsewhere each is one line, for tools to read.
        private void Print(Line line, int indent, bool failed = false)
        {
            if ((failed ? errorConsole : console) is not { } styled)
            {
                Print(new string(' ', indent) + Markup(line), failed);
                return;
            }

            StopSpinning();
            var text = new Markup(line.Text.EscapeMarkup());
            if (line.Where.Length > 0)
            {
                var code = line.Code.Length > 0 ? $"  [dim]{line.Code.EscapeMarkup()}[/]" : string.Empty;
                styled.MarkupLine($"{new string(' ', indent)}{Kind(line)}  {line.Where.EscapeMarkup()}{code}");
                styled.Write(new Padder(text, new Padding(indent + 9, 0, 0, 0)));
                return;
            }

            var grid = new Grid()
                .AddColumn(new GridColumn().NoWrap().Width(7).Padding(indent, 0, 2, 0))
                .AddColumn(new GridColumn().Padding(0, 0, 0, 0));
            styled.Write(grid.AddRow(new Markup(Kind(line)), text));
        }

        private void StopSpinning()
        {
            if (spinning)
            {
                output.Write("\r\u001b[2K");
                spinning = false;
            }
        }

        private void Print(string markup, bool failed = false)
        {
            StopSpinning();

            if ((failed ? errorConsole : console) is { } styled)
            {
                styled.MarkupLine(markup);
            }
            else
            {
                (failed ? error : output).WriteLine(markup.RemoveMarkup());
            }
        }

        private static string Seconds(TimeSpan time) => $"{time.TotalSeconds:0.0}s";

        private static string Count(int count, string what) => $"{count} {what}{(count == 1 ? string.Empty : "s")}";

        private sealed record Line(string Kind, string Where, string Code, string Text);

        private sealed class Step
        {
            public Step(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Stopwatch Clock { get; } = Stopwatch.StartNew();

            public TimeSpan? Took { get; set; }

            public bool Failed { get; set; }

            public List<Line> Notes { get; } = new List<Line>();
        }
    }
}
