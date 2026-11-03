using System.Globalization;

namespace Moonwell.Build
{
    /// <summary>The map's own gameplay constants, <c>war3mapMisc.txt</c> in the map's folder or in its assets: the hero bonuses and the experience table.</summary>
    public static class MapMisc
    {
        private const string FileName = "war3mapMisc.txt";
        private const string Section = "Misc";
        private const string StrengthKey = "StrHitPointBonus";
        private const string IntelligenceKey = "IntManaBonus";

        /// <summary>The folder whose <c>war3mapMisc.txt</c> the built map holds: the assets' when they have one, as it replaces the map's.</summary>
        /// <param name="project">The map project.</param>
        /// <returns>The assets folder or the map's folder.</returns>
        public static string FolderOf(MapProject project)
        {
            return File.Exists(Path.Combine(project.AssetsFolder, FileName)) ? project.AssetsFolder : project.MapFolder;
        }

        /// <summary>Reads the hero bonuses the map's file sets over the standard ones.</summary>
        /// <param name="mapFolder">The map's folder.</param>
        /// <param name="standard">The bonuses of the data set the map plays under, which a key the file leaves out keeps.</param>
        /// <returns>The map's bonuses; the standard ones when the map has no file.</returns>
        /// <exception cref="BuildException">A value of the file is not a number.</exception>
        public static HeroBonus Read(string mapFolder, HeroBonus standard)
        {
            var path = Path.Combine(mapFolder, FileName);
            if (!File.Exists(path))
            {
                return standard;
            }

            var values = Values(path);
            return new HeroBonus(
                Number(values, StrengthKey, standard.LifePerStrength, path),
                Number(values, IntelligenceKey, standard.ManaPerIntelligence, path));
        }

        /// <summary>Reads the experience constants the map's file sets over the standard ones.</summary>
        /// <param name="mapFolder">The map's folder.</param>
        /// <param name="standard">The constants of the data set the map plays under, which a key the file leaves out keeps.</param>
        /// <returns>The map's constants; the standard ones when the map has no file.</returns>
        /// <exception cref="BuildException">The top level is not a whole number, the list not a list of numbers, or a factor not a number.</exception>
        public static HeroExperience ReadExperience(string mapFolder, HeroExperience standard)
        {
            var path = Path.Combine(mapFolder, FileName);
            if (!File.Exists(path))
            {
                return standard;
            }

            var values = Values(path);
            return new HeroExperience(
                WholeNumber(values, "MaxHeroLevel", standard.MaxLevel, path),
                Numbers(values, "NeedHeroXP", standard.Listed, path),
                Number(values, "NeedHeroXPFormulaA", standard.PreviousFactor, path),
                Number(values, "NeedHeroXPFormulaB", standard.LevelFactor, path),
                Number(values, "NeedHeroXPFormulaC", standard.Constant, path));
        }

        /// <summary>Reads the day's hours the map's file sets over the standard ones.</summary>
        /// <param name="mapFolder">The map's folder.</param>
        /// <param name="standard">The standard hours, which a key the file leaves out keeps.</param>
        /// <returns>The map's day; the standard one when the map has no file.</returns>
        /// <exception cref="BuildException">A value is not a number, or the day has no hours.</exception>
        public static GameDay ReadDay(string mapFolder, GameDay standard)
        {
            var path = Path.Combine(mapFolder, FileName);
            if (!File.Exists(path))
            {
                return standard;
            }

            var values = Values(path);
            var day = new GameDay(
                Number(values, "Dawn", standard.Dawn, path),
                Number(values, "Dusk", standard.Dusk, path),
                Number(values, "DayHours", standard.Hours, path));
            if (day.Hours <= 0f)
            {
                throw new BuildException($"{path}: DayHours is {day.Hours.ToString(CultureInfo.InvariantCulture)}, and a day needs more than 0 hours");
            }

            return day;
        }

        // The keys of the file's [Misc] section and their values.
        private static Dictionary<string, string> Values(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inSection = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    inSection = string.Equals(line.Substring(1, line.Length - 2).Trim(), Section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                var equals = line.IndexOf('=');
                if (inSection && equals > 0)
                {
                    values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }

            return values;
        }

        private static float Number(Dictionary<string, string> values, string key, float standard, string path)
        {
            if (!values.TryGetValue(key, out var text))
            {
                return standard;
            }

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new BuildException($"{path}: {key} is '{text}', not a number");
            }

            return number;
        }

        private static int WholeNumber(Dictionary<string, string> values, string key, int standard, string path)
        {
            if (!values.TryGetValue(key, out var text))
            {
                return standard;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                throw new BuildException($"{path}: {key} is '{text}', not a whole number");
            }

            return number;
        }

        private static IReadOnlyList<float> Numbers(Dictionary<string, string> values, string key, IReadOnlyList<float> standard, string path)
        {
            if (!values.TryGetValue(key, out var text))
            {
                return standard;
            }

            var numbers = new List<float>();
            foreach (var part in text.Split(','))
            {
                if (!float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new BuildException($"{path}: {key} is '{text}', not a list of numbers");
                }

                numbers.Add(number);
            }

            return numbers;
        }
    }
}
