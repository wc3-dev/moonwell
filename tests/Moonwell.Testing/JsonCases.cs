using System;
using System.Text;
using Moonwell.Json;

namespace Moonwell.Testing
{
    /// <summary>Cases for Moonwell.Json: a tree with System.Text.Json.Nodes' names, its numbers ints and floats (decision 178).</summary>
    public static class JsonCases
    {
        /// <summary>A tree writes as compact JSON, its keys in the order added and its letters outside ASCII as they are.</summary>
        public static void Write(FakeWorld world)
        {
            Expect.Equal("{\"gold\":25,\"name\":\"Åsa \\\"A\\\"\",\"ok\":true,\"list\":[1,2.5]}", Sample().ToJsonString(), "the sample");
            Expect.Equal("[-7,-0.5,false,null,\"\",{},[]]", new JsonArray { -7, -0.5f, false, null, "", new JsonObject(), new JsonArray() }.ToJsonString(), "the other values");
            Expect.Equal("\"a\\\\b\\n\\t\\r\\b\\f\\u0001\\u001F/\"", JsonValue.Create("a\\b\n\t\r\b\f\u0001\u001f/")!.ToJsonString(), "a string's escapes");
            Expect.Equal("3", JsonValue.Create(3.0f).ToJsonString(), "a whole float, as .NET writes it");
        }

        /// <summary>What a tree writes parses back to the same values.</summary>
        public static void ParseBack(FakeWorld world)
        {
            var node = JsonNode.Parse(Sample().ToJsonString())!;
            Expect.Equal(25, (int)node["gold"], "gold");
            Expect.Equal(25, node["gold"]!.GetValue<int>(), "gold by GetValue");
            Expect.Equal("Åsa \"A\"", (string?)node["name"], "name");
            Expect.Equal(true, (bool)node["ok"], "ok");
            Expect.Equal(2, node["list"]!.AsArray().Count, "the list's count");
            Expect.True((float)node["list"]![1] == 2.5f, "the list's float");
            Expect.True(node["missing"] == null, "a missing key");
            Expect.Equal(Sample().ToJsonString(), node.ToJsonString(), "written again");
        }

        /// <summary>A whole number within int is an int; past it, or with a fraction or an exponent, a float.</summary>
        public static void Numbers(FakeWorld world)
        {
            Expect.True(IsInt("2147483647") && (int)JsonNode.Parse("2147483647") == int.MaxValue, "int.MaxValue");
            Expect.True(IsInt("-2147483648") && (int)JsonNode.Parse("-2147483648") == int.MinValue, "int.MinValue");
            Expect.True(!IsInt("2147483648") && (float)JsonNode.Parse("2147483648") == 2147483648.0f, "past int");
            Expect.True(!IsInt("-2147483649"), "below int");
            Expect.True(!IsInt("1e3") && (float)JsonNode.Parse("1e3") == 1000.0f, "1e3");
            Expect.True(IsInt("-0") && (int)JsonNode.Parse("-0") == 0, "-0");
            Expect.True((float)JsonNode.Parse("-2.5E-1") == -0.25f, "an exponent");
            Expect.Equal(3, (int)JsonNode.Parse("3.0"), "a whole float as an int");
            Expect.True((float)JsonNode.Parse("7") == 7.0f, "an int as a float");
            Expect.Throws<FormatException>(() => Use((int)JsonNode.Parse("2.5")), "2.5 as an int");
            Expect.Throws<InvalidOperationException>(() => Use((int)JsonNode.Parse("\"7\"")), "a string as an int");
            Expect.Throws<JsonException>(() => JsonNode.Parse("1e39"), "past a float");
            if (GameNumbers.HasNaN)
            {
                // The game's Lua has no infinity: float.PositiveInfinity is 2^127 there, a float like any other (game facts).
                Expect.Throws<ArgumentException>(() => JsonValue.Create(float.PositiveInfinity), "infinity");
            }
        }

        /// <summary>A string's escapes and its letters outside ASCII read back as the letters they stand for.</summary>
        public static void Escapes(FakeWorld world)
        {
            Expect.Equal("é\n\t\\/\"\b\f\r", (string?)JsonNode.Parse("\"\\u00e9\\n\\t\\\\\\/\\\"\\b\\f\\r\""), "the escapes");
            Expect.Equal("€ 中 😀", (string?)JsonNode.Parse("\"\\u20AC \\u4e2d \\ud83d\\ude00\""), "\\u escapes past ASCII, a surrogate pair among them");
            Expect.Equal("Åsa ö é € 中 😀", (string?)JsonNode.Parse("\"Åsa ö é € 中 😀\""), "letters as they are");
            Expect.Equal("a\uFFFDb", (string?)JsonNode.Parse("\"a\\ud800b\""), "a lone surrogate");
        }

        /// <summary>An object's keys loop in the order they were added or parsed, and a replaced value keeps its place.</summary>
        public static void Order(FakeWorld world)
        {
            Expect.Equal("b,a,c", Keys(JsonNode.Parse("{\"b\":1,\"a\":2,\"c\":3}")!.AsObject()), "parsed");
            var made = new JsonObject { ["z"] = 1, ["y"] = 2 };
            made["x"] = 3;
            made.Remove("z");
            made["z"] = 4;
            made["y"] = 5;
            Expect.Equal("y,x,z", Keys(made), "added, removed, added again and replaced");
            Expect.Equal("{\"y\":5,\"x\":3,\"z\":4}", made.ToJsonString(), "written");
        }

        /// <summary>Text that is no JSON throws a JsonException that says where; null parses to null.</summary>
        public static void Bad(FakeWorld world)
        {
            var bad = new[] { "{\"a\":}", "[1,", "", " ", "{\"a\" 1}", "[1 2]", "tru", "01", "1.", "-", ".5", "\"a", "\"\\x\"", "\"\\u12\"", "{\"a\":1,}", "[1,]", "1 2", "\"a\u0001b\"", "{1:2}", "nul" };
            for (var i = 0; i < bad.Length; i++)
            {
                var text = bad[i];
                Expect.Throws<JsonException>(() => JsonNode.Parse(text), "\"" + text + "\"");
            }

            Expect.True(JsonNode.Parse("null") == null, "null");
            Expect.Equal("[]", JsonNode.Parse(" \n\t[ ]\r\n ")!.ToJsonString(), "white space around");
            var position = -1;
            try
            {
                JsonNode.Parse("[1,x]");
            }
            catch (JsonException error)
            {
                position = error.Position;
            }

            Expect.Equal(3, position, "where it stopped");
        }

        /// <summary>Nesting 64 deep parses, 65 throws, so a hostile file cannot run the game's Lua out of stack.</summary>
        public static void Depth(FakeWorld world)
        {
            Expect.True(JsonNode.Parse(Nested(64)) != null, "64 deep");
            Expect.Throws<JsonException>(() => JsonNode.Parse(Nested(65)), "65 deep");
        }

        /// <summary>A node has one parent, as in .NET: putting it in a second place throws, so a tree can never hold itself.</summary>
        public static void Parents(FakeWorld world)
        {
            var child = new JsonObject();
            var parent = new JsonObject { ["a"] = child };
            Expect.Same(parent, child.Parent, "the parent");
            Expect.Throws<InvalidOperationException>(() => new JsonArray { child }, "in a second place");
            Expect.Throws<InvalidOperationException>(() => child["self"] = parent, "in its own child");
            parent.Remove("a");
            Expect.True(child.Parent == null, "removed");
            var list = new JsonArray { child, 2 };
            Expect.Same(list, child.Parent, "in a list");
            list.RemoveAt(0);
            Expect.True(child.Parent == null && list.Count == 1 && (int)list[0] == 2, "removed from the list");
            parent.Add("b", 1);
            Expect.Throws<ArgumentException>(() => parent.Add("b", 2), "a key added twice");
        }

        /// <summary>Each kind of node answers only as itself; TryGetValue says whether a value converts.</summary>
        public static void Kinds(FakeWorld world)
        {
            var node = Sample();
            Expect.Throws<InvalidOperationException>(() => node.AsArray(), "an object as an array");
            Expect.Throws<InvalidOperationException>(() => node["list"]!.AsValue(), "an array as a value");
            Expect.Throws<InvalidOperationException>(() => Use(node["gold"]!["x"]), "a value as an object");
            Expect.True(node["gold"]!.AsValue().TryGetValue<float>(out var gold) && gold == 25.0f, "an int as a float");
            Expect.True(!node["gold"]!.AsValue().TryGetValue<string>(out _), "an int as a string");
            Expect.True(JsonValue.Create((string?)null) == null && (JsonNode?)(string?)null == null, "a null string");
            var sum = 0;
            foreach (var item in new JsonArray { 1, 2, 3 })
            {
                sum += (int)item;
            }

            Expect.Equal(6, sum, "a list's items looped");
        }

        /// <summary>JSON kept in a player's file reads back as the same tree, its quotes through the file's escapes.</summary>
        public static void ThroughAFile(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var json = Sample().ToJsonString();
            PlayerFile.Write(Player.One, "settings.txt", json);
            string? read = null;
            Local.For(Player.One, () => { read = PlayerFile.Read("settings.txt"); });
            Expect.Equal(json, JsonNode.Parse(read!)!.ToJsonString(), "the tree read back");
        }

        private static JsonObject Sample()
        {
            return new JsonObject { ["gold"] = 25, ["name"] = "Åsa \"A\"", ["ok"] = true, ["list"] = new JsonArray { 1, 2.5f } };
        }

        private static bool IsInt(string text)
        {
            return JsonNode.Parse(text)!.AsValue().IsInt;
        }

        private static string Keys(JsonObject json)
        {
            var keys = new StringBuilder();
            foreach (var pair in json)
            {
                keys.Append(keys.Length == 0 ? "" : ",").Append(pair.Key);
            }

            return keys.ToString();
        }

        private static string Nested(int depth)
        {
            var text = new StringBuilder();
            for (var i = 0; i < depth; i++)
            {
                text.Append('[');
            }

            for (var i = 0; i < depth; i++)
            {
                text.Append(']');
            }

            return text.ToString();
        }

        private static void Use(object? value)
        {
        }
    }
}
