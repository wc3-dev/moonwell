using System.Text.RegularExpressions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The order of System.init's types once the used classes of Moonwell.Objects are put in: a class that inherits one of them, such as a spell template on Channel, is set up after it.</summary>
    public sealed class ObjectLuaInitTests
    {
        private static UsedClass Class(string name, string baseName, bool inObjects)
        {
            return new UsedClass(name, baseName, "ability", null, inObjects, Array.Empty<UsedField>(), Array.Empty<UsedMethod>(), "", Array.Empty<UsedField>());
        }

        private static List<string> Order(string script)
        {
            return Regex.Matches(script, "\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToList();
        }

        [Fact]
        public void a_class_on_a_stub_listed_before_it_moves_after_it_and_one_after_it_stays()
        {
            var script = "System.init({\n    types = {\n      \"Moonwell.Spells.SpellTemplate\",\n      \"Moonwell.Spells.Wave\",\n      \"Moonwell.Ability\",\n      \"Moonwell.Spells.Bolt\",\n      \"Moonwell.Spells.Summon_1\",\n      \"Map.Firebolt\"\n    }\n  })";
            var used = new[]
            {
                Class("Moonwell.Objects.StandardAbility", "Moonwell.Ability", true),
                Class("Moonwell.Objects.Creeps.Abilities.Channel", "Moonwell.Objects.StandardAbility", true),
                Class("Moonwell.Spells.SpellTemplate", "Moonwell.Objects.Creeps.Abilities.Channel", false),
                Class("Moonwell.Spells.Wave", "Moonwell.Spells.SpellTemplate", false),
                Class("Moonwell.Spells.Bolt", "Moonwell.Spells.SpellTemplate", false),
                Class("Moonwell.Spells.Summon`1", "Moonwell.Spells.SpellTemplate", false),
                Class("Map.Firebolt", "Moonwell.Spells.Bolt", false),
            };

            var order = Order(ObjectLua.AddToInit(script, used));

            Assert.Equal(
                new[]
                {
                    "Moonwell.Ability",
                    "Moonwell.Objects.StandardAbility",
                    "Moonwell.Objects.Creeps.Abilities.Channel",
                    "Moonwell.Spells.SpellTemplate",
                    "Moonwell.Spells.Wave",
                    "Moonwell.Spells.Bolt",
                    "Moonwell.Spells.Summon_1",
                    "Map.Firebolt",
                },
                order);
        }

        [Fact]
        public void classes_after_the_stubs_keep_their_place()
        {
            var script = "System.init({\n    types = {\n      \"Moonwell.Ability\",\n      \"Map.Other\",\n      \"Map.Firebolt\"\n    }\n  })";
            var used = new[]
            {
                Class("Moonwell.Objects.StandardAbility", "Moonwell.Ability", true),
                Class("Map.Firebolt", "Moonwell.Objects.StandardAbility", false),
            };

            Assert.Equal(new[] { "Moonwell.Ability", "Moonwell.Objects.StandardAbility", "Map.Other", "Map.Firebolt" }, Order(ObjectLua.AddToInit(script, used)));
        }
    }
}
