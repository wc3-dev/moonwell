using Moonwell.Objects;
using Moonwell.Testing;

namespace Moonwell.Tests
{
    /// <summary>The fast simulation with Moonwell.Objects, on .NET only: a standard unit plays by its object data, with no SetNumbers.</summary>
    public class SimulationTests
    {
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A map with nothing in it: Start(map) registers the standard classes this assembly names, as a built map's registry would.</summary>
        private sealed class EmptyMap : Map
        {
        }

        /// <summary>A map with a name, and slots with names: a person's, a computer's, and a computer's with none.</summary>
        private sealed class NamedMap : Map
        {
            private static readonly Team Blue = new Team("Blue");

            public NamedMap(string saveFolder = "")
            {
                Name = "My Rpg!";
                SaveFolder = saveFolder;
                Slots.Add(
                    new Slot(0, Blue, Controller.User) { Name = "Hero" },
                    new Slot(1, Blue, Controller.Computer) { Name = "MMAI" },
                    new Slot(2, Blue, Controller.Computer));
            }
        }

        /// <summary>The file a player's write made, by its folder.</summary>
        private static bool Wrote(Simulation sim, string folder)
        {
            PlayerFile.Write(Player.One, "settings.txt", "x");
            return sim.World.Files.ContainsKey(folder + "\\settings.txt");
        }

        /// <summary>Start(map) puts player files where the build would: the map's SaveFolder, else its name, else the project's, in letters and digits.</summary>
        [Fact]
        public void FilesGoInTheMapsSaveFolder()
        {
            Expect.True(Wrote(Simulation.Start(new NamedMap("Kept")), "Kept"), "the SaveFolder");
            Expect.True(Wrote(Simulation.Start(new NamedMap()), "MyRpg"), "the map's name");
            Expect.True(Wrote(Simulation.Start(new EmptyMap()), "MoonwellTests"), "the project's name");
            Expect.True(Wrote(Simulation.Start(), "Map"), "an empty game");
        }

        /// <summary>A computer's slot has its Slot's name, as in the game; a person's keeps their own, and a slot with no name is "Player n".</summary>
        [Fact]
        public void ComputerSlotsTakeTheirNames()
        {
            Simulation.Start(new NamedMap());
            Expect.Equal("MMAI", Player.Get(1).Name, "the named computer");
            Expect.Equal("Player 3", Player.Get(2).Name, "the computer with no name");
            Expect.Equal("Player 1", Player.One.Name, "the person");
        }

        /// <summary>A footman made by its class takes its life and move speed from its object data.</summary>
        [Fact]
        public void FootmanFromObjectData()
        {
            Simulation.Start(new EmptyMap());
            var footman = Unit.Create<Human.Units.Footman>(Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.Near(420.0f, footman.Life, "life");
            Expect.Near(420.0f, footman.MaxLife, "max life");
            Expect.Near(270.0f, footman.Get(Stat.MoveSpeed), "move speed");
            Expect.Near(2.0f, footman.Get(Stat.Armor), "armor");
        }

        /// <summary>Two footmen of enemy players, made by their id near each other, acquire each other and fight until one dies.</summary>
        [Fact]
        public void FootmenFightToADeath()
        {
            var sim = Simulation.Start();
            var first = Unit.Create(Footman, Player.One, 0.0f, 0.0f, Game.Instance);
            var second = Unit.Create(Footman, Player.Two, 200.0f, 0.0f, Game.Instance);
            Expect.True(sim.RunUntil(() => !first.IsAlive || !second.IsAlive, 60.0f), "one dies within 60 s");
            Expect.True(first.IsAlive != second.IsAlive, "and only one");
            var winner = first.IsAlive ? first : second;
            Expect.True(winner.Life < 420.0f, "the winner was hit too: " + winner.Life);
            Expect.Equal(1, sim.Alive(Player.One) + sim.Alive(Player.Two), "one footman left");
        }

        /// <summary>An empty game knows every standard class by its game id (J99, J135): a hero takes Storm Bolt by its class.</summary>
        [Fact]
        public void AnEmptyGameKnowsTheStandardClasses()
        {
            Simulation.Start();
            Expect.Equal(ObjectTypes.FourCC("AHtb"), ObjectTypes.IdOf<Human.Abilities.StormBolt>(), "Storm Bolt's id");
            var king = Unit.Create(ObjectTypes.FourCC("Hmkg"), Player.One, 0.0f, 0.0f, Game.Instance);
            king.AddAbility<Human.Abilities.StormBolt>();
            Expect.True(king.HasAbility(ObjectTypes.FourCC("AHtb")), "the king has it");
        }

        /// <summary>The map's Storm Bolt in place of the standard one, which a map's start uses and an empty game does not.</summary>
        [Replaces]
        private sealed class MapBolt : Human.Abilities.StormBolt
        {
        }

        /// <summary>With the map's start, its [Replaces] class is made for the standard class's id, the standard classes registered after it.</summary>
        [Fact]
        public void TheMapsReplacingClassWins()
        {
            Simulation.Start(new EmptyMap());
            var king = Unit.Create(ObjectTypes.FourCC("Hmkg"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.True(king.AddAbility<Human.Abilities.StormBolt>() is MapBolt, "the map's class");
            Simulation.Start();
            king = Unit.Create(ObjectTypes.FourCC("Hmkg"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.True(!(king.AddAbility<Human.Abilities.StormBolt>() is MapBolt), "the standard class in an empty game");
        }

        /// <summary>A unit starts with its type's abilities and mana from its object data (J133); a hero's hero abilities stay unlearned.</summary>
        [Fact]
        public void APriestStartsWithItsAbilitiesAndMana()
        {
            Simulation.Start();
            var priest = Unit.Create(ObjectTypes.FourCC("hmpr"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.True(priest.HasAbility(ObjectTypes.FourCC("Ahea")), "Heal");
            Expect.True(priest.HasAbility(ObjectTypes.FourCC("Ainf")), "Inner Fire");
            Expect.Near(75.0f, priest.Mana, "mana");
            Expect.Near(200.0f, priest.MaxMana, "max mana");
            var paladin = Unit.Create(ObjectTypes.FourCC("Hpal"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.True(!paladin.HasAbility(ObjectTypes.FourCC("AHhb")), "Holy Light unlearned");
        }
    }
}
