using Moonwell.Objects;
using Moonwell.Testing;

// The hero bonus a built map's HeroBonus.g.cs gives its assembly: the game's standard 25 life a point of strength and 15 mana a point of intelligence.
[assembly: System.Reflection.AssemblyMetadata("Moonwell.LifePerStrength", "25")]
[assembly: System.Reflection.AssemblyMetadata("Moonwell.ManaPerIntelligence", "15")]

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

        /// <summary>A Mountain King of the map's own that keeps the life his strength gives while it takes his strength down to 1.</summary>
        [ObjectId("H0K1")]
        private sealed class SteadyKing : Human.Units.MountainKing
        {
            public SteadyKing()
            {
                var life = MaxLife;
                Strength = 1;
                MaxLife = life;
            }
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

        /// <summary>
        /// Start(map) reads a hero's life with its strength counted, by the bonus the map's build used, so a class's constructor
        /// reads MaxLife as the build does: 100 and 24 strength at 25 a point make the Mountain King's 700.
        /// </summary>
        [Fact]
        public void HeroLifeCountsStrengthAsTheBuildDoes()
        {
            Simulation.Start(new EmptyMap());
            var steady = Unit.Create<SteadyKing>(Player.One, 0.0f, 0.0f, Game.Instance);
            var standard = Unit.Create(ObjectTypes.FourCC("Hmkg"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.Near(700.0f, steady.MaxLife, "the class that keeps the total");
            Expect.Near(700.0f, steady.Life, "and starts with it");
            Expect.Near(700.0f, standard.MaxLife, "the standard Mountain King");
        }

        /// <summary>A map whose game component calls a raw native: at its start, or 0.1 s after, or never.</summary>
        private sealed class RawNativeMap : Map
        {
            public RawNativeMap(bool atStart, bool later)
            {
                Components.Add(new RawNativeStart { AtStart = atStart, Later = later });
            }
        }

        /// <summary>Calls GetWorldBounds, the game's own, where its map says; Moonwell catches and reports what a reaction throws.</summary>
        private sealed class RawNativeStart : GameComponent
        {
            public bool AtStart;
            public bool Later;

            public override void OnGameStart()
            {
                if (AtStart)
                {
                    Natives.GetWorldBounds();
                }

                After(0.1f, () =>
                {
                    if (Later)
                    {
                        Natives.GetWorldBounds();
                    }
                });
            }
        }

        /// <summary>The error a simulation throws for a raw native: named, with the reaction it failed in and the way round it.</summary>
        private static void ExpectExplained(Exception error, string where)
        {
            var explained = Assert.IsType<InvalidOperationException>(error);
            Assert.Contains("Natives.GetWorldBounds runs only in the game", explained.Message);
            Assert.Contains("#if MOONWELL_LUA", explained.Message);
            Assert.Contains("#else", explained.Message);
            if (where.Length > 0)
            {
                Assert.Contains("(in " + where + ")", explained.Message);
            }

            Assert.IsType<BadImageFormatException>(explained.InnerException);
        }

        /// <summary>A raw native in a reaction at the start, which Moonwell reports and goes on from, makes Start throw with its name and the way round it.</summary>
        [Fact]
        public void ARawNativeAtTheStartIsNamed()
        {
            ExpectExplained(Record.Exception(() => Simulation.Start(new RawNativeMap(atStart: true, later: false)))!, "RawNativeStart.OnGameStart");
        }

        /// <summary>A raw native in a timer makes Run throw at that step, and one in a RunUntil's check makes RunUntil throw; a native on a path never taken throws nothing.</summary>
        [Fact]
        public void ARawNativeInARunIsNamed()
        {
            var sim = Simulation.Start(new RawNativeMap(atStart: false, later: true));
            sim.Run(0.08f);
            ExpectExplained(Record.Exception(() => sim.Run(0.04f))!, "");
            Expect.Near(0.1f, sim.Time, "at the timer's step");

            sim = Simulation.Start(new RawNativeMap(atStart: false, later: false));
            sim.Run(1.0f);
            ExpectExplained(Record.Exception(() => sim.RunUntil(() => Natives.GetWorldBounds() == null, 1.0f))!, "");
        }

        /// <summary>Start() has no map and so no build's bonus: a hero reads its base field, after a Start(map) too.</summary>
        [Fact]
        public void HeroLifeWithNoMapIsItsBaseField()
        {
            Simulation.Start(new EmptyMap());
            Simulation.Start();
            var king = Unit.Create(ObjectTypes.FourCC("Hmkg"), Player.One, 0.0f, 0.0f, Game.Instance);
            Expect.Near(100.0f, king.MaxLife, "the base field");
        }

        /// <summary>A unit's kinds come from its type's object data: a Peasant is a worker, a Town Hall a structure and a town hall, a Barracks a mechanical structure, a Ghoul undead, a Footman none of them.</summary>
        [Fact]
        public void KindsComeFromTheObjectData()
        {
            Simulation.Start();
            var peasant = Unit.Create(ObjectTypes.FourCC("hpea"), Player.One, 0.0f, 0.0f, Game.Instance);
            var hall = Unit.Create(ObjectTypes.FourCC("htow"), Player.One, 500.0f, 0.0f, Game.Instance);
            var barracks = Unit.Create(ObjectTypes.FourCC("hbar"), Player.One, 0.0f, 500.0f, Game.Instance);
            var ghoul = Unit.Create(ObjectTypes.FourCC("ugho"), Player.Two, 900.0f, 900.0f, Game.Instance);
            var footman = Unit.Create(Footman, Player.One, 100.0f, 100.0f, Game.Instance);
            Expect.True(peasant.IsWorker && !peasant.IsStructure, "the Peasant is a worker");
            Expect.True(hall.IsStructure && hall.IsTownHall && !hall.IsWorker, "the Town Hall");
            Expect.True(barracks.IsStructure && barracks.IsMechanical && !barracks.IsTownHall, "the Barracks");
            Expect.True(ghoul.IsUndead && !ghoul.IsStructure, "the Ghoul");
            Expect.True(!footman.IsStructure && !footman.IsWorker && !footman.IsSummoned && !footman.IsMechanical, "the Footman");
        }

        /// <summary>A unit's reads come from its type's object data: a Rifleman's food, attack cooldown, acquisition range and ranged attack, a Footman's melee one, a Gold Mine's gold; a player's units are those the simulation counts alive.</summary>
        [Fact]
        public void ReadsComeFromTheObjectData()
        {
            var sim = Simulation.Start();
            var rifleman = Unit.Create(ObjectTypes.FourCC("hrif"), Player.One, 0.0f, 0.0f, Game.Instance);
            var footman = Unit.Create(Footman, Player.One, 100.0f, 100.0f, Game.Instance);
            var tower = Unit.Create(ObjectTypes.FourCC("hgtw"), Player.One, 600.0f, 0.0f, Game.Instance);
            var mine = Unit.Create(ObjectTypes.FourCC("ngol"), Player.NeutralPassive, 1500.0f, 1500.0f, Game.Instance);
            Expect.Equal(3, rifleman.FoodUsed, "the Rifleman's food");
            Expect.Near(1.4f, rifleman.LiveAttackCooldown, "its attack cooldown");
            Expect.Near(600.0f, rifleman.AcquireRange, "its acquisition range");
            Expect.True(rifleman.IsRanged, "an instant weapon is a ranged one");
            Expect.True(!footman.IsRanged && footman.FoodUsed == 2, "the Footman is melee and uses 2 food");
            Expect.True(tower.IsRanged && tower.FoodUsed == 0, "a Guard Tower's missile is ranged, and it uses no food");
            Expect.Equal(12500, mine.Resources, "the Gold Mine starts with its ability's Max Gold");
            mine.Resources = 300;
            Expect.Equal(300, mine.Resources, "and is set");
            Expect.Equal(0, rifleman.Resources, "a unit with no Gold Mine ability holds none");
            Expect.Equal(sim.Alive(Player.One), Player.One.Units.Count, "a player's units are those the simulation counts");
            Expect.True(Player.One.Units.Contains(tower) && !Player.One.Units.Contains(mine), "its own only");
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

        /// <summary>The stages play on units made from object data: a rule that halves each hit its footmen take after armor makes a fight of two footmen last at least 1.6 times as long as the same fight with no rule.</summary>
        [Fact]
        public void AHalvingRuleLengthensAFootmenFight()
        {
            var sim = Simulation.Start();
            var plainFirst = Unit.Create(Footman, Player.One, 0.0f, 0.0f, Game.Instance);
            var plainSecond = Unit.Create(Footman, Player.Two, 200.0f, 0.0f, Game.Instance);
            var halvedFirst = Unit.Create(Footman, Player.One, 0.0f, 5000.0f, Game.Instance);
            var halvedSecond = Unit.Create(Footman, Player.Two, 200.0f, 5000.0f, Game.Instance);
            halvedFirst.Add(new FightRules { Keep = 0.5f });
            halvedSecond.Add(new FightRules { Keep = 0.5f });
            Expect.True(sim.RunUntil(() => !plainFirst.IsAlive || !plainSecond.IsAlive, 200.0f), "a plain footman dies");
            var plain = sim.Time;
            Expect.True(sim.RunUntil(() => !halvedFirst.IsAlive || !halvedSecond.IsAlive, 200.0f), "a halved footman dies");
            Expect.True(sim.Time > 1.6f * plain, "the halved fight lasts " + sim.Time + " s against " + plain + " s");
        }

        /// <summary>A unit's attack range is a number of its own that a later change sets: a unit of range 100 made to reach 300 stops its chase 300 from its target.</summary>
        [Fact]
        public void AnAttackRangeSetLaterIsWhereTheUnitStops()
        {
            var sim = Simulation.Start();
            var type = ObjectTypes.FourCC("s0rg");
            sim.SetNumbers(type, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 10 });
            var attacker = Unit.Create(type, Player.One, 0.0f, 0.0f, Game.Instance);
            var target = Unit.Create(type, Player.Two, 400.0f, 0.0f, Game.Instance);
            sim.World.SetAttackRange(attacker.Handle, 300.0f);
            attacker.OrderOn("attack", target);
            sim.Run(2.0f);
            Expect.Near(100.0f, attacker.X, "it stopped 300 from the target");
            Expect.True(target.Life < 100.0f, "and swung from there");
        }
    }
}
