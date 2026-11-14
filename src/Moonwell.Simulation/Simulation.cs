using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A game run flat out on the fake world, in steps of 0.02 s. Units of a type with numbers walk straight at their move
    /// speed, chase and swing at what they attack, attack the nearest enemy in reach when idle, and stand 0.15 s at each
    /// order, while Moonwell's timers and reactions run as in the game. The same seed gives the same run. Pathing,
    /// collision, missiles' flight, spells, buffs, vision and turning are left to the game.
    /// </summary>
    public sealed class Simulation
    {
        private Simulation(FakeWorld world)
        {
            World = world;
        }

        /// <summary>The fake world the game runs on. Its own Advance moves time without the play.</summary>
        public FakeWorld World { get; }

        /// <summary>The game's seconds so far.</summary>
        public float Time => World.Time;

        /// <summary>Starts an empty game on a fresh fake world, which becomes the runtime's. The seed gives the dice of the attacks' damage, and the seed after it the map's own random numbers (Game.Random and a Dice made without a seed).</summary>
        public static Simulation Start(int seed = 1)
        {
            var world = FakeWorld.Install();
            world.StartPlay(seed);
            return new Simulation(world);
        }

        /// <summary>
        /// Starts the map's game on a fresh fake world as the game would: its slots playing (slot 0 as a person when it lists
        /// none), each team's slots allied, then its register step, its starting components, its player components on each
        /// playing slot, and every OnGameStart. On .NET the register step registers the object classes the map uses, as its
        /// build would; in Lua it registers nothing. World Editor's placed units, and the damage, terrain, areas, input and physics
        /// systems, are left out.
        /// </summary>
        public static Simulation Start(Map map, int seed = 1)
        {
            var simulation = Start(seed);
            Seat(simulation.World, map.StartSlots);
#if MOONWELL_LUA
            Runtime.Begin(() => Runtime.Install(map, () => { }));
#else
            Runtime.Begin(() => Runtime.Install(map, () => Registry.Register(map)));
#endif
            return simulation;
        }

        /// <summary>Runs the seconds in whole steps: rounded to whole milliseconds, then to whole steps with a half step up, and at least one; none for 0 or less.</summary>
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

        /// <summary>Gives a unit type its numbers, for the units of the type made from now on; on .NET they replace what its object data gives.</summary>
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

        /// <summary>How many units the player owns that the fake made, alive and not removed, whether played or not.</summary>
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
