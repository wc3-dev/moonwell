using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// Code of a map's own that runs on .NET while the map builds, before its game code is compiled. Every class in the
    /// project's <c>buildtime</c> folder that inherits this and is not abstract is made with its constructor without
    /// parameters and run once a build, in the order of the classes' full names. It reads the map's files through
    /// <see cref="Map"/> and hands what it works out to the game with <see cref="Give"/>.
    /// </summary>
    public abstract class BuildStep
    {
        private static readonly JsonSerializerOptions Written = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            Converters = { new DecimalSingle(), new DecimalDouble(), new JsonStringEnumConverter(), new TypedLists() },
        };

        private BuildRun? run;

        /// <summary>The map's files, as World Editor saved them in <c>map\</c>, with the files in <c>assets\</c> over them.</summary>
        protected MapReader Map => Current.Map;

        private BuildRun Current => run ?? throw new InvalidOperationException("a build step reads the map and gives its results in Run, which the build calls; its constructor runs before the build has given it the map");

        /// <summary>What the step does; the build calls it once.</summary>
        public abstract void Run();

        /// <summary>
        /// Hands a value to the game's code by name: the build writes it into <c>Data</c> before the game code compiles, as it
        /// does a file in <c>data\</c>, so <c>Give("Trees", counts)</c> is read as <c>Data.Trees</c>. An object's properties and a
        /// dictionary's keys become members, a list a list; a number keeps the type it is given as, whole or decimal, and a list
        /// its items' type when it is empty (but a list of <c>object</c>, which has none to keep).
        /// </summary>
        /// <param name="name">The name of the class in <c>Data</c>: letters, digits and <c>_</c>, starting with a letter.</param>
        /// <param name="value">What to give: anything <c>System.Text.Json</c> writes.</param>
        /// <exception cref="BuildFailure">The name cannot be a class's, is given twice, or the value cannot be written.</exception>
        protected void Give(string name, object value)
        {
            if (name == null || !Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$"))
            {
                throw new BuildFailure($"Give(\"{name}\"): the name becomes a class in Data, so it is letters, digits and _, starting with a letter");
            }

            if (value == null)
            {
                throw new BuildFailure($"Give(\"{name}\"): there is no value to give");
            }

            if (Current.Gives.ContainsKey(name))
            {
                throw new BuildFailure($"Give(\"{name}\") is called twice; give each name once");
            }

            try
            {
                Current.Gives.Add(name, JsonSerializer.Serialize(value, value.GetType(), Written));
            }
            catch (Exception failure) when (failure is NotSupportedException or JsonException or ArgumentException)
            {
                throw new BuildFailure($"Give(\"{name}\"): the value cannot be written: {failure.Message}");
            }
        }

        /// <summary>Prints a warning in the build's output; the build goes on.</summary>
        /// <param name="text">The warning, a line that points at the map.</param>
        protected void Warn(string text)
        {
            Current.Warnings.Add(text);
        }

        /// <summary>Stops the build with a message of the map maker's own.</summary>
        /// <param name="text">The error, a line that points at the map: "region Harbor has no buildable ground".</param>
        /// <exception cref="BuildFailure">Always.</exception>
        [DoesNotReturn]
        protected void Fail(string text)
        {
            throw new BuildFailure(text);
        }

        /// <summary>Writes a text file for the map maker to read into <c>build\reports\</c>; nothing of it reaches the map.</summary>
        /// <param name="fileName">The file's name, such as <c>districts.txt</c>.</param>
        /// <param name="text">The file's text.</param>
        protected void Report(string fileName, string text)
        {
            Report(fileName, System.Text.Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Writes a picture of the map's cells for the map maker to look at into <c>build\reports\</c>, as a PNG.</summary>
        /// <param name="fileName">The file's name, such as <c>water.png</c>.</param>
        /// <param name="picture">The picture.</param>
        protected void Report(string fileName, Picture picture)
        {
            Report(fileName, picture.ToPng());
        }

        /// <summary>Writes a file for the map maker to look at into <c>build\reports\</c>; nothing of it reaches the map.</summary>
        /// <param name="fileName">The file's name.</param>
        /// <param name="bytes">The file's bytes.</param>
        /// <exception cref="BuildFailure">The name is no plain file name, or is written twice.</exception>
        protected void Report(string fileName, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new BuildFailure($"Report(\"{fileName}\"): a report is named by a plain file name, with no folder");
            }

            if (!Current.Reports.TryAdd(fileName, bytes))
            {
                throw new BuildFailure($"Report(\"{fileName}\") is called twice; name each report once");
            }
        }

        internal void Start(BuildRun given)
        {
            run = given;
        }

        // A float given as 3 stays a decimal in Data: the plain writer would write 3, which reads as a whole number.
        private static string Decimal(string text)
        {
            return text.Contains('.') || text.Contains('E') || text.Contains('e') ? text : text + ".0";
        }

        // The type of a list's items: an array's, or that of the IEnumerable<T> a collection is; null for text and dictionaries.
        private static Type? ItemType(Type type)
        {
            if (type == typeof(string) || IsDictionary(type))
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.GetInterfaces().Append(type)
                .FirstOrDefault(one => one.IsGenericType && one.GetGenericTypeDefinition() == typeof(IEnumerable<>))?
                .GetGenericArguments()[0];
        }

        private static bool IsDictionary(Type type)
        {
            return typeof(IDictionary).IsAssignableFrom(type) || type.GetInterfaces().Append(type).Any(one => one.IsGenericType
                && (one.GetGenericTypeDefinition() == typeof(IDictionary<,>) || one.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
        }

        // A value of a type with nothing in it, written so that Data's writer reads the type from it: 0, 0.0, false, "", an
        // object of its members' samples, an empty list of its items' sample.
        private static void Sample(Utf8JsonWriter writer, Type type, int depth)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (depth > 8 || type == typeof(object))
            {
                writer.WriteNullValue();
            }
            else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                writer.WriteRawValue("0.0");
            }
            else if (type == typeof(bool))
            {
                writer.WriteBooleanValue(false);
            }
            else if (type.IsPrimitive && type != typeof(char))
            {
                writer.WriteNumberValue(0);
            }
            else if (type == typeof(string) || type == typeof(char) || type.IsEnum)
            {
                writer.WriteStringValue(string.Empty);
            }
            else if (ItemType(type) is { } item)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(EmptyList);
                Sample(writer, item, depth + 1);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStartObject();
                if (!IsDictionary(type))
                {
                    foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(one => one.CanRead && one.GetIndexParameters().Length == 0))
                    {
                        writer.WritePropertyName(property.Name);
                        Sample(writer, property.PropertyType, depth + 1);
                    }

                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    {
                        writer.WritePropertyName(field.Name);
                        Sample(writer, field.FieldType, depth + 1);
                    }
                }

                writer.WriteEndObject();
            }
        }

        /// <summary>The key an empty list is written under, with a sample of its items' type: <c>{ "$empty": 0 }</c>.</summary>
        internal const string EmptyList = "$empty";

        // A list with items is written as the plain writer writes it; an empty one has no item to read a type from, so it is
        // written with a sample of its items' type.
        private sealed class TypedLists : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) => ItemType(typeToConvert) != null;

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                return (JsonConverter)Activator.CreateInstance(typeof(TypedList<>).MakeGenericType(typeToConvert))!;
            }
        }

        private sealed class TypedList<T> : JsonConverter<T>
            where T : notnull
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                var items = ((IEnumerable)value).Cast<object?>().ToList();
                if (items.Count == 0)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(EmptyList);
                    Sample(writer, ItemType(typeof(T))!, 0);
                    writer.WriteEndObject();
                    return;
                }

                writer.WriteStartArray();
                foreach (var item in items)
                {
                    if (item == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, item, item.GetType(), options);
                    }
                }

                writer.WriteEndArray();
            }
        }

        private sealed class DecimalSingle : JsonConverter<float>
        {
            public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetSingle();

            public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) => writer.WriteRawValue(Decimal(value.ToString("R", CultureInfo.InvariantCulture)));
        }

        private sealed class DecimalDouble : JsonConverter<double>
        {
            public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDouble();

            public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteRawValue(Decimal(value.ToString("R", CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// An error of the map maker's own that stops the build: its message is the whole line the build prints.
    /// <see cref="BuildStep.Fail"/> throws it, and build code outside a step can.
    /// </summary>
    public sealed class BuildFailure : Exception
    {
        /// <summary>Creates the error with the line the build prints.</summary>
        /// <param name="message">The line, which points at the map.</param>
        public BuildFailure(string message)
            : base(message)
        {
        }
    }

    // What one build's steps read and leave: the build makes it, runs the steps with it and writes out what they left.
    internal sealed class BuildRun
    {
        public BuildRun(MapReader map)
        {
            Map = map;
        }

        public MapReader Map { get; }

        // Each give's JSON by its name, in the order given.
        public Dictionary<string, string> Gives { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<string> Warnings { get; } = new List<string>();

        public Dictionary<string, byte[]> Reports { get; } = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    }
}
