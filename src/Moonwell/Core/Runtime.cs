using System;
using System.Collections.Generic;
using System.ComponentModel;
using Moonwell.Engine;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>The runtime's static state: the world it talks to, and the state each task adds.</summary>
    public static class Runtime
    {
        /// <summary>The world the runtime talks to; set by Reset.</summary>
        internal static IWorld World = null!;

        /// <summary>The scheduler that runs the timers; set by Reset.</summary>
        internal static Scheduler Scheduler = null!;

#if DEBUG
        /// <summary>Moonwell's clock in a development build, which the world's Now and WakeIn go through; set by Reset.</summary>
        internal static DebugClock Clock = null!;
#endif

        /// <summary>Makes a world the runtime's world and clears the state of every task.</summary>
        internal static void Reset(IWorld world)
        {
            World = world;
#if DEBUG
            Clock = new DebugClock();
#endif
            Errors.Reset();
            Scheduler = new Scheduler();
            Hooks.Reset();
            Events.Reset();
            Game.Instance = new Game();
            Player.Reset();
            KnownUnits.Reset();
            KnownItems.Reset();
            KnownDestructables.Reset();
            ObjectTypes.Reset();
            Dummies.Reset();
            HeroTable.Reset();
            DayConstants.Reset();
#if DEBUG
            DebugSession.Reset();
            DebugViews.Reset();
            DebugPanel.Reset();
            DebugStats.Reset();
#endif
            Game.Instance.Every(0.25f, Sweep.Run);
        }

        /// <summary>Called when a wake request is due; runs the timers that are due.</summary>
        internal static void Wake()
        {
            Scheduler.Wake();
        }

        /// <summary>Starts the runtime on the game; the map's Main calls it once, with the setup that registers its types and components.</summary>
        internal static void Start(Action setup)
        {
            Reset(new NativeWorld());
#if DEBUG
            DebugSession.Install(new NativeDebugWorld((NativeWorld)World));
#endif
            NativeWorld.Later(() => Begin(setup));
        }

        /// <summary>Gives heroes the map's experience table, from level 1's total (0) to the top level's; the map's Main calls it, before anything registers, when the map's gameplay constants change the standard table.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void InstallHeroExperience(int[] experience)
        {
            HeroTable.Install(experience);
        }

        /// <summary>Listens for every unit's orders from the start, so that LastOrder and Reissue have them: the map's Main calls it, in the register step, when the map's code reads them.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ListenForOrders()
        {
            Events.Need(Hook.OnUnitOrdered);
        }

        /// <summary>Gives the day the map's dawn, dusk and hours; the map's Main calls it, before anything registers, when the map's gameplay constants change the standard ones.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void InstallDay(float dawn, float dusk, float hours)
        {
            DayConstants.Install(dawn, dusk, hours);
        }

        /// <summary>Starts the runtime on the game with a map: the map's Main calls it once, and the map's components are added before the game's start.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Start(Map map)
        {
            Start(map, () => { });
        }

        /// <summary>Starts the runtime on the game with a map and a register step: the register step runs first, before the map's components are added.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Start(Map map, Action register)
        {
            Start(() => Install(map, register));
        }

        /// <summary>Runs the register step, with every maker registered before any class's constructor runs (ObjectTypes.Registering), then adds the map's starting components to the game, in list order, and its player components to each player in the game; Start runs it as the setup.</summary>
        internal static void Install(Map map, Action register)
        {
            ObjectTypes.Registering(register);
            var components = map.StartComponents;
            for (var i = 0; i < components.Count; i++)
            {
                Game.Instance.Add(components[i]);
            }

            if (map.StartPlayerComponents.Count > 0)
            {
                AddPlayerComponents(map.StartPlayerComponents);
            }
        }

        /// <summary>Gives each player in the game, in slot order, its own object from each maker, in list order; a maker that throws is reported and the others run.</summary>
        private static void AddPlayerComponents(List<Func<PlayerComponent>> makers)
        {
            var players = Player.Playing;
            for (var p = 0; p < players.Count; p++)
            {
                for (var m = 0; m < makers.Count; m++)
                {
                    try
                    {
                        players[p].Add(makers[m]());
                    }
                    catch (Exception error)
                    {
                        Errors.Report("Moonwell.Setup", error);
                    }
                }
            }
        }

        /// <summary>Listens for pick-ups and drops, which is how an item's carrier is kept (Item.Carrier). Begin does it before the setup, where every machine runs the same code, and not when the first item is taken in, which Local.For code may do. Tests that make a world without Begin call it themselves.</summary>
        internal static void ListenForCarriers()
        {
            Events.Need(Hook.OnPickedUp | Hook.OnDropped);
        }

        /// <summary>Runs the setup, then takes in what is already in the world and starts the game's components; internal for the cases.</summary>
        internal static void Begin(Action setup)
        {
            ListenForCarriers();
            try
            {
                setup();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Setup", error);
            }

            foreach (var handle in World.UnitsInWorld())
            {
                try
                {
                    KnownUnits.TakeIn(handle, null, true);
                }
                catch (Exception error)
                {
                    Errors.Report("Moonwell.Start", error);
                }
            }

            foreach (var handle in World.ItemsInWorld())
            {
                if (ObjectTypes.ItemMaker(World.ItemTypeId(handle)) != null)
                {
                    try
                    {
                        KnownItems.TakeIn(handle, null);
                    }
                    catch (Exception error)
                    {
                        Errors.Report("Moonwell.Start", error);
                    }
                }
            }

            foreach (var handle in World.DestructablesInWorld())
            {
                if (ObjectTypes.DestructableMaker(World.DestructableTypeId(handle)) != null)
                {
                    try
                    {
                        KnownDestructables.TakeIn(handle, null);
                    }
                    catch (Exception error)
                    {
                        Errors.Report("Moonwell.Start", error);
                    }
                }
            }

            SpellBuffs.Begin();
            try
            {
                Inbound.GameStarted();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Start", error);
            }
        }
    }
}
