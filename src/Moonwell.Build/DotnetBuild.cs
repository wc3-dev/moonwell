using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moonwell.Build
{
    /// <summary>Runs <c>dotnet build</c> on a map project.</summary>
    public static class DotnetBuild
    {
        // CS0234 names the missing type, then the namespace it was looked for in, each in the quotes of the compiler's language;
        // the project's path in brackets after the message is left out.
        private static readonly Regex MovedName = new Regex(@"error CS0234:[^\[]*\W(AttackType|DefenseType)\W[^\[]*\WMoonwell\.Objects\W", RegexOptions.CultureInvariant);

        // CS0246 names a type that binds to nothing, such as AttackType in a file with only using Moonwell.Objects.
        private static readonly Regex UnboundName = new Regex(@"error CS0246:[^\[]*\W(AttackType|DefenseType)\W", RegexOptions.CultureInvariant);

        /// <summary>
        /// Builds the project, streaming dotnet's output to the log, and returns the path of the built assembly; the project's
        /// <see cref="MapProject.Symbols"/> and sources become the ones the compiler had.
        /// </summary>
        /// <param name="project">The map project.</param>
        /// <param name="release">True for the Release configuration, false for Debug.</param>
        /// <param name="log">Where the output of <c>dotnet build</c> goes.</param>
        /// <returns>The full path of the map's assembly.</returns>
        /// <exception cref="BuildException">The build failed.</exception>
        public static string Run(MapProject project, bool release, TextWriter log)
        {
            var configuration = release ? "Release" : "Debug";
            var written = new List<string>();

            // One MSBuild process (-m:1), compiling on dotnet's compiler server: a map is one project, or a few that build in turn,
            // and each worker process MSBuild would start, or a compiler started cold, costs more than the build it helps with.
            var build = Dotnet(project, new[] { "build", project.ProjectFile, "-c", configuration, "-nologo", "-m:1" }, log, written.Add);
            if (build != 0)
            {
                var hints = MovedHints(written);
                throw new BuildException(hints.Count == 0 ? "dotnet build failed" : "dotnet build failed" + Environment.NewLine + string.Join(Environment.NewLine, hints));
            }

            // AddImplicitDefineConstants adds the SDK's symbols (NET, NET10_0_OR_GREATER) to DefineConstants, as the compile does.
            var captured = new StringWriter();
            // NoWarn, the two analyzer switches and the analyzer config files are what Silenced lists.
            var query = Dotnet(project, new[] { "msbuild", project.ProjectFile, "-t:AddImplicitDefineConstants", "-getProperty:TargetPath", "-getProperty:DefineConstants", "-getProperty:NoWarn", "-getProperty:RunAnalyzers", "-getProperty:RunAnalyzersDuringBuild", "-getItem:Compile", "-getItem:EditorConfigFiles", "-p:Configuration=" + configuration, "--disable-build-servers" }, captured);
            var text = captured.ToString();
            var start = text.IndexOf('{');
            if (query != 0 || start < 0)
            {
                log.Write(text);
                throw new BuildException("dotnet msbuild could not find the built assembly");
            }

            using var answer = JsonDocument.Parse(text.Substring(start, text.LastIndexOf('}') - start + 1));
            var properties = answer.RootElement.GetProperty("Properties");
            project.Symbols = (properties.GetProperty("DefineConstants").GetString() ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<string> Paths(string item) => answer.RootElement.TryGetProperty("Items", out var items) && items.TryGetProperty(item, out var found)
                ? found.EnumerateArray().Select(one => one.GetProperty("FullPath").GetString()!).ToList()
                : new List<string>();
            project.Sources = Paths("Compile");
            project.ConfigFiles = Paths("EditorConfigFiles");
            project.NoWarn = properties.GetProperty("NoWarn").GetString() ?? string.Empty;
            project.AnalyzersOff = new[] { "RunAnalyzers", "RunAnalyzersDuringBuild" }
                .FirstOrDefault(name => string.Equals(properties.GetProperty(name).GetString(), "false", StringComparison.OrdinalIgnoreCase));
            return properties.GetProperty("TargetPath").GetString() is { Length: > 0 } targetPath
                ? targetPath
                : throw new BuildException("dotnet msbuild could not find the built assembly");
        }

        /// <summary>
        /// The MW0011 text for each choice enum that left <c>Moonwell.Objects</c> and that an error CS0234
        /// (<c>Moonwell.Objects.AttackType</c>) or CS0246 (<c>AttackType</c>, which binds to nothing) of the output names. The
        /// analyzer reports it in a method body; a name in a declaration (a field's type, a <c>using static</c>) stops the compiler
        /// before analyzers run, so the build says it here.
        /// </summary>
        /// <param name="lines">The lines <c>dotnet build</c> wrote.</param>
        /// <returns>The hints, one for each enum named, in the order of the first error naming it.</returns>
        internal static IReadOnlyList<string> MovedHints(IEnumerable<string> lines)
        {
            var hints = new List<string>();
            foreach (var line in lines)
            {
                var found = MovedName.Match(line);
                var written = "Moonwell.Objects.";
                if (!found.Success)
                {
                    found = UnboundName.Match(line);
                    written = string.Empty;
                }

                if (found.Success)
                {
                    var name = found.Groups[1].Value;
                    var hint = $"{written}{name} is Moonwell.{name} since Moonwell 0.2.1, which the damage package uses too: write Moonwell.{name}, or {name} with using Moonwell";
                    if (!hints.Contains(hint))
                    {
                        hints.Add(hint);
                    }
                }
            }

            return hints;
        }

        private static int Dotnet(MapProject project, string[] arguments, TextWriter output, Action<string>? seen = null)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = project.Folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = start };
            var gate = new object();
            DataReceivedEventHandler forward = (_, line) =>
            {
                if (line.Data != null)
                {
                    lock (gate)
                    {
                        output.WriteLine(line.Data);
                        seen?.Invoke(line.Data);
                    }
                }
            };
            process.OutputDataReceived += forward;
            process.ErrorDataReceived += forward;
            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new BuildException("dotnet is not installed or not on the PATH");
            }

            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
