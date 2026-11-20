using System;
using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// A game run flat out on the fake world, in steps of 0.02 s. Units of a type with numbers walk straight at their move
    /// speed, chase and swing at what they attack, attack the nearest enemy in reach when idle, cast, spending the cooldown
    /// and mana, and stand 0.16 s at each order, while Moonwell's timers and reactions run as in the game. Each swing and
    /// each hit from code goes through the damage package's stages over the fake's armor, and a swing reads its unit's damage and
    /// attack speed bonus as they are then. The same seed gives the same run. Pathing, collision, missiles' flight, what spells
    /// do, buffs, vision and turning are left to the game, and a hero's attributes add no damage, armor or attack speed. Its life counts its strength in a game started with a built map.
    /// </summary>
    public sealed class Simulation
    {
        private Simulation(FakeWorld world)
        {
            World = world;
        }

        /// <summary>The fake world the game runs on. Its own Advance moves time without the play: units neither walk nor fight, and the steps Run counts fall behind, so it is not to be mixed with Run.</summary>
        public FakeWorld World { get; }

        /// <summary>The game's seconds so far.</summary>
        public float Time => World.Time;

        /// <summary>Starts an empty game on a fresh fake world, which becomes the runtime's, with the damage package running on it and every standard class of Moonwell.Objects registered by its game id on .NET. The seed gives the dice of the attacks' damage, and the seed after it the map's own random numbers (Game.Random and a Dice made without a seed). With no map there is no build's hero bonus, so a hero's life is its base field: its attributes add nothing.</summary>
        public static Simulation Start(int seed = 1)
        {
#if !MOONWELL_LUA
            ObjectNumbers.UseHeroBonus(null);
#endif
            var simulation = Begin(seed);
#if !MOONWELL_LUA
            Registry.RegisterStandard();
#endif
            return simulation;
        }

        /// <summary>
        /// Starts the map's game on a fresh fake world as the game would: its slots playing (slot 0 as a person when it lists
        /// none), each team's slots allied, then its register step, its starting components, its player components on each
        /// playing slot, and every OnGameStart. On .NET the register step registers the object classes the map uses, as its
        /// build would, then every other standard class as an empty game does; in Lua it registers nothing. World Editor's placed units, and the terrain, areas, input and physics
        /// systems, are left out; the damage package runs, as in a map that uses it. On .NET a hero's MaxLife, MaxMana and Mana read their totals by the hero bonus the map's last
        /// build wrote into its assembly, as the build reads them, so a hero starts with the life its strength gives; a map not
        /// built yet has none, and they read their base fields. A hero's attributes add no damage or armor.
        /// </summary>
        public static Simulation Start(Map map, int seed = 1)
        {
            var simulation = Begin(seed);
#if !MOONWELL_LUA
            ObjectNumbers.UseHeroBonus(map);
#endif
            Seat(simulation.World, map.StartSlots);
            Runtime.InstallSaveFolder(FolderOf(map));
#if MOONWELL_LUA
            Runtime.Begin(() => Runtime.Install(map, () => { }));
#else
            Runtime.Begin(() => Runtime.Install(map, () =>
            {
                Registry.Register(map);
                Registry.RegisterStandard();
            }));
#endif
            return simulation;
        }

        /// <summary>A fresh fake world, made the runtime's, with the play started.</summary>
        private static Simulation Begin(int seed)
        {
            var world = FakeWorld.Install();
            world.StartPlay(seed);
            world.InstallDamage();
            return new Simulation(world);
        }

        /// <summary>Runs the seconds in whole steps: rounded to whole milliseconds, then to whole steps with a half step up, and at least one; none for 0 or less. Each step's time is its number of steps since the start, so a World.Advance between runs, which moves time without the play, is not to be mixed with it.</summary>
        public void Run(float seconds)
        {
            var steps = StepsOf(seconds);
            for (var i = 0; i < steps; i++)
            {
                World.PlayOneStep();
            }
        }

        /// <summary>Runs step by step, for at most the seconds as Run counts them, until the check holds: true when it held, before the first step too; false when it never did.</summary>
        public bool RunUntil(Func<bool> check, float seconds)
        {
            if (check())
            {
                return true;
            }

            var steps = StepsOf(seconds);
            for (var i = 0; i < steps; i++)
            {
                World.PlayOneStep();
                if (check())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gives a unit type its numbers, for the units of the type made from now on; on .NET they replace what its object data gives. A unit takes its range, cooldown, damage, dice and acquisition range when it is made, though a swing adds the unit's green damage bonus and divides the cooldown by 1 + its attack speed bonus as they are then; its move speed and armor are read as they are at each step.</summary>
        public void SetNumbers(int typeId, UnitNumbers numbers)
        {
            World.SetNumbers(typeId, numbers);
        }

        /// <summary>How many orders the unit was given, a repeat included and an order the fake does not know left out; 0 for a unit not played.</summary>
        public int OrdersOf(Unit unit)
        {
            var unitPlay = World.PlayedOf(unit.Handle);
            return unitPlay == null ? 0 : unitPlay.Orders;
        }

        /// <summary>The seconds the unit stood at its orders before carrying them out; 0 for a unit not played.</summary>
        public float StoodOf(Unit unit)
        {
            var unitPlay = World.PlayedOf(unit.Handle);
            return unitPlay == null ? 0.0f : unitPlay.Stood / 50.0f;
        }

        /// <summary>How many units the player owns that the fake made, alive and not removed, whether played or not; Moonwell's dummies and helper units are left out.</summary>
        public int Alive(Player player)
        {
            return World.AliveOf(player.Slot);
        }

        /// <summary>Sets up the map's slots as the game does from its war3map.w3i: each playing, with its controller and race, on its team, numbered in the order the slots first name them, and passive to the others on it.</summary>
        private static void Seat(FakeWorld world, List<Slot> slots)
        {
            if (slots.Count == 0)
            {
                world.SetSlot(0, SlotState.Playing, Controller.User);
                return;
            }

            var teams = new List<Team>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                world.SetSlot(slot.Number, SlotState.Playing, slot.Controller);
                // Only a computer's slot reads its war3map.w3i name in the game; a person's is their own (game-facts, probe `lobby`).
                if (slot.Controller == Controller.Computer && slot.Name.Length > 0)
                {
                    world.Rename(world.PlayerAt(slot.Number), slot.Name);
                }

                if (slot.Race.HasValue)
                {
                    world.SetRace(slot.Number, slot.Race.Value);
                }

                if (!teams.Contains(slot.Team))
                {
                    teams.Add(slot.Team);
                }

                world.MoveToTeam(world.PlayerAt(slot.Number), teams.IndexOf(slot.Team));
                for (var j = 0; j < i; j++)
                {
                    var other = slots[j];
                    if (other.Team == slot.Team && other.Number != slot.Number)
                    {
                        world.SetAllianceOf(world.PlayerAt(slot.Number), world.PlayerAt(other.Number), (int)Alliance.Passive);
                        world.SetAllianceOf(world.PlayerAt(other.Number), world.PlayerAt(slot.Number), (int)Alliance.Passive);
                    }
                }
            }
        }

        /// <summary>The map's save folder as the build picks it (FileStore.FolderOf): its SaveFolder, else its name, else its project's, in letters and digits only; "Map" with none. In Lua the project's name is not known.</summary>
        private static string FolderOf(Map map)
        {
#if MOONWELL_LUA
            var names = new[] { map.SaveFolder, map.Name };
#else
            var names = new[] { map.SaveFolder, map.Name, map.GetType().Assembly.GetName().Name ?? "" };
#endif
            for (var i = 0; i < names.Length; i++)
            {
                var folder = new StringBuilder();
                for (var j = 0; j < names[i].Length; j++)
                {
                    var c = names[i][j];
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    {
                        folder.Append(c);
                    }
                }

                if (folder.Length > 0)
                {
                    return folder.ToString();
                }
            }

            return "Map";
        }

        /// <summary>The steps of a time, as the brains count a ThinkEvery: rounded to whole milliseconds, then to whole steps with a half step up, and at least 1; 0 for none.</summary>
        internal static int StepsOf(float seconds)
        {
            if (!(seconds > 0.0f))
            {
                return 0;
            }

            // Past 1,000,000 s the milliseconds would no longer fit an int.
            if (seconds > 1000000.0f)
            {
                seconds = 1000000.0f;
            }

            var milliseconds = (int)(seconds * 1000.0f + 0.5f);
            var steps = (milliseconds + 10) / 20;
            return steps < 1 ? 1 : steps;
        }
    }
}
