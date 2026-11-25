namespace Moonwell.BuildTime
{
    /// <summary>
    /// Runs a build step outside the build, for a test: on a map folder, or on a <see cref="MapReader"/> a test made, and gives
    /// back what it gave, warned, failed with and reported, as the build would have taken it.
    /// </summary>
    public static class BuildSteps
    {
        /// <summary>Makes a step of a class with its constructor without parameters and runs it on a World Editor map folder.</summary>
        /// <typeparam name="TStep">The step's class.</typeparam>
        /// <param name="mapFolder">The map's folder, <c>map.w3x</c> in a map project.</param>
        /// <param name="assetsFolder">The project's <c>assets</c> folder, whose files stand in for the map's, or null for none.</param>
        /// <returns>What the step left.</returns>
        public static BuildStepResult Run<TStep>(string mapFolder, string? assetsFolder = null)
            where TStep : BuildStep, new()
        {
            return Run(new TStep(), new MapReader(mapFolder, assetsFolder));
        }

        /// <summary>Makes a step of a class with its constructor without parameters and runs it on a map reader.</summary>
        /// <typeparam name="TStep">The step's class.</typeparam>
        /// <param name="map">The map, such as one with a test's own regions: <c>map.WithRegions(...)</c>.</param>
        /// <returns>What the step left.</returns>
        public static BuildStepResult Run<TStep>(MapReader map)
            where TStep : BuildStep, new()
        {
            return Run(new TStep(), map);
        }

        /// <summary>
        /// Runs a step on a map reader. A <see cref="BuildFailure"/>, which <c>Fail</c> throws, is kept in
        /// <see cref="BuildStepResult.Failure"/> with what the step gave and warned before it; any other exception is the test's,
        /// and goes on up as it was thrown.
        /// </summary>
        /// <param name="step">The step, made by the test.</param>
        /// <param name="map">The map.</param>
        /// <returns>What the step left.</returns>
        public static BuildStepResult Run(BuildStep step, MapReader map)
        {
            var run = new BuildRun(map);
            string? failure = null;
            step.Start(run);
            try
            {
                step.Run();
            }
            catch (BuildFailure stopped)
            {
                failure = stopped.Message;
            }

            return new BuildStepResult(run, failure);
        }
    }

    /// <summary>What a build step left when <see cref="BuildSteps"/> ran it.</summary>
    public sealed class BuildStepResult
    {
        private readonly Dictionary<string, byte[]> reports;

        internal BuildStepResult(BuildRun run, string? failure)
        {
            Given = new Dictionary<string, object>(run.Values, StringComparer.Ordinal);
            GivenJson = new Dictionary<string, string>(run.Gives, StringComparer.Ordinal);
            Warnings = run.Warnings.ToList();
            Failure = failure;
            reports = new Dictionary<string, byte[]>(run.Reports, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Each value the step gave, by its name, as it gave it: <c>(List&lt;District&gt;)result.Given["Districts"]</c>.</summary>
        public IReadOnlyDictionary<string, object> Given { get; }

        /// <summary>Each value the step gave, by its name, as the JSON the build writes into <c>Data</c> from.</summary>
        public IReadOnlyDictionary<string, string> GivenJson { get; }

        /// <summary>The step's warnings, in the order it warned.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>The line the build would have stopped with, from <c>Fail</c> or another <see cref="BuildFailure"/>; null when the step ran to its end.</summary>
        public string? Failure { get; }

        /// <summary>Whether the step stopped the build.</summary>
        public bool Failed => Failure != null;

        /// <summary>Each report the step wrote, by its file name, as its bytes: a text's UTF-8, a picture's PNG.</summary>
        public IReadOnlyDictionary<string, byte[]> Reports => reports;

        /// <summary>A text report the step wrote.</summary>
        /// <param name="fileName">The report's file name, such as <c>districts.txt</c>.</param>
        /// <exception cref="KeyNotFoundException">The step wrote no report of that name; the message lists those it wrote.</exception>
        public string ReportText(string fileName)
        {
            if (!reports.TryGetValue(fileName, out var bytes))
            {
                throw new KeyNotFoundException(reports.Count == 0
                    ? $"the step wrote no report {fileName}: it wrote none"
                    : $"the step wrote no report {fileName}: it wrote {string.Join(", ", reports.Keys.OrderBy(name => name, StringComparer.Ordinal))}");
            }

            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
