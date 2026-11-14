using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>The fake game a case runs against: it records what the runtime asks of it and moves time by hand.</summary>
    public sealed partial class FakeWorld : IWorld
    {
        /// <summary>Game seconds since the world was made.</summary>
        public float Time;

        /// <summary>The absolute time of the pending wake request; null when there is none.</summary>
        public float? WakeAt;

        /// <summary>
        /// How many seconds after the time asked for Advance delivers each wake; 0, the default, delivers it on time. The game wakes a
        /// timer at a frame, not at the exact time asked for: timers were seen running 0.0001 s off, and the time of slow frames is
        /// made up later in bursts.
        /// </summary>
        public float WakeLate;

        /// <summary>The slot of the player at this machine.</summary>
        public int LocalSlot;

        /// <summary>Added to LocalSlot: the twin run sets it to 1 to run a case again as the next player's machine, whatever slot the case sets.</summary>
        public static int MachineShift;

        /// <summary>The slots the game gives the neutral players.</summary>
        public int NeutralAggressive = 24, NeutralVictim = 25, NeutralExtra = 26, NeutralPassive = 27;

        /// <summary>The lines printed on the screen.</summary>
        public List<string> Printed = new List<string>();

        /// <summary>The slot of each line printed to a player, -1 for a line printed to everyone.</summary>
        public List<int> PrintedSlots = new List<int>();

        /// <summary>The facing the last unit made by SpawnUnit was given.</summary>
        public float LastFacing;

        /// <summary>Each line printed to a player or to everyone.</summary>
        public List<string> PrintedTexts = new List<string>();

        /// <summary>The seconds of each line printed to a player or to everyone; below 0 for the game's own time.</summary>
        public List<float> PrintedSeconds = new List<float>();

        /// <summary>The lines appended to the log.</summary>
        public List<string> Logged = new List<string>();

        /// <summary>The damage the runtime asked the game to deal: the amount (whole), attack, ranged, and the game's attack, damage and weapon numbers.</summary>
        public List<string> Dealt = new List<string>();

        /// <summary>The damage the runtime dealt to destructables: the game's numbers, as in Dealt.</summary>
        public List<string> DealtToDestructables = new List<string>();

        /// <summary>The unit each damage to a destructable came from.</summary>
        public List<unit> DealtToDestructableSources = new List<unit>();

        /// <summary>The destructable each damage to a destructable went to.</summary>
        public List<destructable> DealtToDestructableTargets = new List<destructable>();

        /// <summary>The type ids SpawnUnit and SpawnItem make nothing of, as the game for an id it has no object for.</summary>
        public List<int> Unmakeable = new List<int>();

        /// <summary>Whether the game deals no damage the runtime asks for, as for a target that cannot take it.</summary>
        public bool RefuseDamage;

        /// <summary>The spell-immune units: a hit of a magic damage type, or of attack type magic, fires no damage event and takes no life, though the game answers true (seen 10-31).</summary>
        public HashSet<unit> SpellImmune = new HashSet<unit>();

        /// <summary>The events the runtime asked to hear, once each.</summary>
        internal List<GameEvent> Listening = new List<GameEvent>();

        /// <summary>How many level events the world fired.</summary>
        public int LevelEvents;

        /// <summary>The destructables whose deaths the runtime asked to hear, once each.</summary>
        public List<destructable> Watched = new List<destructable>();

        /// <summary>The units the runtime removed.</summary>
        public List<unit> RemovedUnits = new List<unit>();

        /// <summary>The items the runtime removed.</summary>
        public List<item> RemovedItems = new List<item>();

        /// <summary>The destructables the runtime removed.</summary>
        public List<destructable> RemovedDestructables = new List<destructable>();

        /// <summary>The ability calls, as "add 42", "hide 42" or "remove 42", with the ability id in decimal.</summary>
        public List<string> AbilityCalls = new List<string>();

        /// <summary>The units UnitsInWorld returns.</summary>
        public List<unit> PlacedUnits = new List<unit>();

        /// <summary>The items ItemsInWorld returns.</summary>
        public List<item> PlacedItems = new List<item>();

        /// <summary>The destructables DestructablesInWorld returns.</summary>
        public List<destructable> PlacedDestructables = new List<destructable>();

        /// <summary>The type id of each unit the world made.</summary>
        private readonly Dictionary<unit, int> unitTypes = new Dictionary<unit, int>();

        /// <summary>Each unit the world made, in the order made.</summary>
        private readonly List<unit> madeUnits = new List<unit>();

        /// <summary>The slot of the player that owns each unit the world made.</summary>
        private readonly Dictionary<unit, int> unitSlots = new Dictionary<unit, int>();

        /// <summary>The units whose owner reads null, as a unit the game removed a while ago does.</summary>
        private readonly HashSet<unit> ownerless = new HashSet<unit>();

        /// <summary>Whether each unit the world made is a hero.</summary>
        private readonly Dictionary<unit, bool> heroes = new Dictionary<unit, bool>();

        /// <summary>The units made illusions.</summary>
        private readonly HashSet<unit> illusions = new HashSet<unit>();

        /// <summary>The abilities code gave each unit, which a morph takes away unless they are permanent.</summary>
        private readonly Dictionary<unit, List<int>> addedAbilities = new Dictionary<unit, List<int>>();

        /// <summary>The abilities made permanent on each unit.</summary>
        private readonly Dictionary<unit, HashSet<int>> permanentAbilities = new Dictionary<unit, HashSet<int>>();

        /// <summary>The ability levels of each unit, keyed by ability id.</summary>
        private readonly Dictionary<unit, Dictionary<int, int>> abilityLevels = new Dictionary<unit, Dictionary<int, int>>();

        /// <summary>The number of levels of each ability given one; an ability not given one has no last level.</summary>
        private readonly Dictionary<int, int> levelCounts = new Dictionary<int, int>();

        /// <summary>The abilities that are hero skills.</summary>
        private readonly HashSet<int> heroAbilities = new HashSet<int>();

        /// <summary>The type id of each item the world made.</summary>
        private readonly Dictionary<item, int> itemTypes = new Dictionary<item, int>();

        /// <summary>The type id of each destructable the world made.</summary>
        private readonly Dictionary<destructable, int> destructableTypes = new Dictionary<destructable, int>();

        /// <summary>Where PlaceDestructable put each destructable; one not placed stands at (100, -50).</summary>
        private readonly Dictionary<destructable, float[]> destructablePlaces = new Dictionary<destructable, float[]>();

        /// <summary>The life of each destructable whose life has changed; one not here has DestructableLifeAtStart.</summary>
        private readonly Dictionary<destructable, float> destructableLives = new Dictionary<destructable, float>();

        /// <summary>The most life of each destructable whose most has changed; one not here has DestructableLifeAtStart.</summary>
        private readonly Dictionary<destructable, float> destructableMaxLives = new Dictionary<destructable, float>();

        /// <summary>The life and most life every destructable starts with, a Lordaeron tree's (LTlt).</summary>
        public float DestructableLifeAtStart = 50f;

        /// <summary>The destructables the runtime asked the game to make, in turn, null types included.</summary>
        public List<DestructableSpawn> DestructableSpawns = new List<DestructableSpawn>();

        /// <summary>The life each restore asked for, in turn, a living destructable's included.</summary>
        public List<float> RestoredLives = new List<float>();

        /// <summary>Whether each restore asked for the birth animation, in turn.</summary>
        public List<bool> RestoredBirths = new List<bool>();

        /// <summary>The calls that changed a unit's place or facing, as "move", "place", "turn" or "face-now".</summary>
        public List<string> UnitCalls = new List<string>();

        /// <summary>How many times a unit's life, mana, place or facing was read or set after the game had removed it, which the runtime must never do.</summary>
        public int GoneCalls;

        /// <summary>The facing each unit was last ordered to turn to; its own facing does not change by it, since turning takes time.</summary>
        public Dictionary<unit, float> TurnTargets = new Dictionary<unit, float>();

        /// <summary>How many times the runtime asked the game to kill a unit.</summary>
        public int KillCalls;

        /// <summary>The units that are paused.</summary>
        public List<unit> PausedUnits = new List<unit>();

        /// <summary>How many times a unit's scale was set.</summary>
        public int ScaleCalls;

        /// <summary>The red, green, blue and alpha each unit was last tinted with.</summary>
        public Dictionary<unit, int[]> Tints = new Dictionary<unit, int[]>();
        public Dictionary<unit, int> TeamColors = new Dictionary<unit, int>();

        /// <summary>What was asked of each unit's animations, in order: "play attack", "queue stand", "index 3", "speed 0.5", "tag defend", "untag defend".</summary>
        public Dictionary<unit, List<string>> UnitAnimations = new Dictionary<unit, List<string>>();

        /// <summary>The units hidden; a test adds one to stand for ShowUnit(false).</summary>
        public List<unit> HiddenUnits = new List<unit>();

        /// <summary>What the local machine was asked to do to its selection, in order: "select", "deselect" or "clear", with the slot asked for ("0 select"); another player's ask changes nothing here, as in the game.</summary>
        public List<string> SelectionCalls = new List<string>();

        /// <summary>The drops of units the runtime removed (RemoveUnit): the game fires them after the call (seen 10-21), so they wait for the next Advance. The items are removed with the unit.</summary>
        private readonly List<KeyValuePair<unit, item>> pendingDrops = new List<KeyValuePair<unit, item>>();

        /// <summary>The units removed by the runtime in this frame, which still read as they were until the next Advance.</summary>
        private readonly List<unit> pendingRemovals = new List<unit>();

        /// <summary>The units the game has removed but whose handles still answer: every number reads 0, as the game gives in the quarter second before the sweep.</summary>
        private readonly HashSet<unit> vanishedUnits = new HashSet<unit>();

        /// <summary>The items the game has removed but whose handles still answer: every number reads 0.</summary>
        private readonly HashSet<item> vanishedItems = new HashSet<item>();

        /// <summary>The units whose type flies (UNIT_TYPE_FLYING).</summary>
        private readonly HashSet<unit> flyers = new HashSet<unit>();

        /// <summary>The units that have been given the Amrf ability.</summary>
        private readonly HashSet<unit> crowFormed = new HashSet<unit>();

        /// <summary>The units that can show a fly height: those whose type flies and those that had Amrf added and removed.</summary>
        private readonly HashSet<unit> floaters = new HashSet<unit>();

        /// <summary>The units that have been killed.</summary>
        private readonly HashSet<unit> dead = new HashSet<unit>();

        /// <summary>The level of each unit, 1 until set.</summary>
        private readonly Dictionary<unit, int> levels = new Dictionary<unit, int>();

        /// <summary>The scale of each unit, 1 until set.</summary>
        private readonly Dictionary<unit, float> scales = new Dictionary<unit, float>();

        /// <summary>The fly height of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> flyHeights = new Dictionary<unit, float>();

        /// <summary>The orders given to units, as "immediate", "point" or "target", a space, and the order string; and the item uses, as "useitem", "useitem point" or "useitem target".</summary>
        public List<string> Orders = new List<string>();

        /// <summary>The x and y of each point order, in turn.</summary>
        public List<float> OrderPoints = new List<float>();

        /// <summary>The widget each target order was given on, in turn.</summary>
        public List<widget> OrderTargets = new List<widget>();

        /// <summary>What the game answers to an order.</summary>
        public bool OrderAnswer = true;

        /// <summary>The inventory calls that changed something, as "give" or "drop".</summary>
        public List<string> InventoryCalls = new List<string>();

        /// <summary>How many times a destructable's type id was read.</summary>
        public int DestructableTypeReads;

        /// <summary>How many times a slot of an inventory was read.</summary>
        public int ItemSlotReads;

        /// <summary>The calls that changed an item, as "charges 5", "owner 3 False" (slot and colour change) or "move".</summary>
        public List<string> ItemCalls = new List<string>();

        /// <summary>The charges of each item, 1 until set.</summary>
        private readonly Dictionary<item, int> itemCharges = new Dictionary<item, int>();

        /// <summary>The player slot of each item, 0 until set.</summary>
        private readonly Dictionary<item, int> itemSlots = new Dictionary<item, int>();

        /// <summary>The x and y of each item, 0 until placed.</summary>
        private readonly Dictionary<item, float[]> itemPlaces = new Dictionary<item, float[]>();

        /// <summary>The items in each unit's slots; null for an empty slot.</summary>
        private readonly Dictionary<unit, item[]> inventories = new Dictionary<unit, item[]>();

        /// <summary>The number of inventory slots of each unit, 6 until set.</summary>
        private readonly Dictionary<unit, int> inventorySizes = new Dictionary<unit, int>();

        /// <summary>The life of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> lives = new Dictionary<unit, float>();

        /// <summary>The most life of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> maxLives = new Dictionary<unit, float>();

        /// <summary>The mana of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> manas = new Dictionary<unit, float>();

        /// <summary>The most mana of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> maxManas = new Dictionary<unit, float>();

        /// <summary>The x of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> unitXs = new Dictionary<unit, float>();

        /// <summary>The y of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> unitYs = new Dictionary<unit, float>();

        /// <summary>The facing of each unit, 0 until set.</summary>
        private readonly Dictionary<unit, float> facings = new Dictionary<unit, float>();

        /// <summary>The player made for each slot, once it is asked for.</summary>
        private readonly Dictionary<int, player> players = new Dictionary<int, player>();

        /// <summary>The upgrade levels of each player slot, keyed by slot and then by upgrade id.</summary>
        private readonly Dictionary<int, Dictionary<int, int>> upgradeLevels = new Dictionary<int, Dictionary<int, int>>();

        /// <summary>The x of each player slot's start location, for the slots that have one set.</summary>
        private readonly Dictionary<int, float> startXs = new Dictionary<int, float>();

        /// <summary>The y of each player slot's start location, for the slots that have one set.</summary>
        private readonly Dictionary<int, float> startYs = new Dictionary<int, float>();

        /// <summary>The states set on players, by slot times 16 plus the state.</summary>
        private readonly Dictionary<int, int> states = new Dictionary<int, int>();

        /// <summary>The slot state of each slot SetSlot set.</summary>
        private readonly Dictionary<int, int> slotStates = new Dictionary<int, int>();

        /// <summary>Who controls each slot SetSlot set.</summary>
        private readonly Dictionary<int, int> controllers = new Dictionary<int, int>();

        /// <summary>The slot of each player made.</summary>
        private readonly Dictionary<player, int> slots = new Dictionary<player, int>();

        /// <summary>The names set for player slots.</summary>
        private readonly Dictionary<int, string> names = new Dictionary<int, string>();

        /// <summary>The colours set for player slots.</summary>
        private readonly Dictionary<int, int> colors = new Dictionary<int, int>();

        private float hourOfDay;
        private float dayScale = 1.0f;
        private bool dayStopped;

        /// <summary>The teams set for player slots.</summary>
        private readonly Dictionary<int, int> teams = new Dictionary<int, int>();

        /// <summary>The races set for player slots.</summary>
        private readonly Dictionary<int, int> races = new Dictionary<int, int>();

        /// <summary>The alliances set, keyed by the source slot times 32 plus the other slot.</summary>
        private readonly Dictionary<int, int> alliances = new Dictionary<int, int>();

        /// <summary>The tech limits set, keyed by slot, then by type id.</summary>
        private readonly Dictionary<int, Dictionary<int, int>> techMaxes = new Dictionary<int, Dictionary<int, int>>();

        /// <summary>The declared values of each type, keyed by type id, then by field id, as the build's table gives them.</summary>
        private readonly Dictionary<int, Dictionary<string, object>> typeValues = new Dictionary<int, Dictionary<string, object>>();

        /// <summary>The name of each unit, "" until set.</summary>
        private readonly Dictionary<unit, string> unitNames = new Dictionary<unit, string>();

        private readonly Dictionary<int, string> unitIcons = new Dictionary<int, string>();

        /// <summary>The proper name of each hero, "" until set.</summary>
        private readonly Dictionary<unit, string> properNames = new Dictionary<unit, string>();

        /// <summary>Whether a call on a unit or an item throws, for a case that checks the runtime makes none.</summary>
        public bool CallsThrow;

        /// <summary>What the runtime is given in place of a world, when set: the twin run sets it to an IWorld that writes down the runtime's calls and passes them on.</summary>
        public static Func<FakeWorld, object>? Around;

        /// <summary>Starts the runtime again on this world.</summary>
        public void Restart()
        {
            Runtime.Reset(Around != null ? (IWorld)Around(this) : this);
        }

        /// <summary>Makes a fresh world for one case and makes it the runtime's world; as the runtime does at its start, it listens for pick-ups and drops.</summary>
        public static FakeWorld Install()
        {
            var world = new FakeWorld();
            world.GameBuffs = new FakeGameBuffs(world);
            world.Dummies = new FakeDummies(world);
            world.Restart();
            RealClock.Wanted = false;
            Interactions.ResetSettings();
            Physics.ResetSettings();
            Runtime.ListenForCarriers();
            return world;
        }

        /// <summary>Makes a unit with a type id, a slot and a hero flag, not placed in the world.</summary>
        public unit NewUnit(int typeId, int slot = 0, bool hero = false)
        {
            var created = new unit();
            madeUnits.Add(created);
            unitTypes[created] = typeId;
            unitSlots[created] = slot;
            heroes[created] = hero;
            return created;
        }

        /// <summary>Makes an item with a type id, not placed in the world.</summary>
        public item NewItem(int typeId)
        {
            var created = new item();
            itemTypes[created] = typeId;
            return created;
        }

        /// <summary>Makes a destructable with a type id, not placed in the world.</summary>
        public destructable NewDestructable(int typeId)
        {
            var created = new destructable();
            destructableTypes[created] = typeId;
            return created;
        }

        /// <summary>Puts a destructable at a place; one not placed stands at (100, -50).</summary>
        public void PlaceDestructable(destructable d, float x, float y)
        {
            destructablePlaces[d] = new float[] { x, y };
        }

        /// <summary>Makes a unit vanish the way the game does: its type id and its owner read 0 and null, and nothing is told.</summary>
        public void VanishUnit(unit u)
        {
            unitTypes[u] = 0;
            ownerless.Add(u);
            vanishedUnits.Add(u);
        }

        /// <summary>Changes a unit's type id on the same handle, as a morph does when its morph time is up and a building's upgrade when it finishes: nothing is told (probe of 11-11).</summary>
        public void MorphUnit(unit u, int typeId)
        {
            unitTypes[u] = typeId;
        }

        /// <summary>Makes an item vanish the way the game does: its type id reads 0 and nothing is told.</summary>
        public void VanishItem(item i)
        {
            itemTypes[i] = 0;
            vanishedItems.Add(i);
        }

        /// <summary>Makes a destructable vanish the way the game does: its type id reads 0 and nothing is told.</summary>
        public void VanishDestructable(destructable d)
        {
            destructableTypes[d] = 0;
        }

        /// <summary>Makes a unit one whose type flies: CanFly answers true and it shows a fly height at once.</summary>
        public void MakeFlying(unit u)
        {
            flyers.Add(u);
            floaters.Add(u);
        }

        /// <summary>Whether the unit would be seen above the ground: its type flies, or Amrf was added and removed (seen 10-21: a ground unit's fly height reads back but shows only then), and a height is set.</summary>
        public bool Floats(unit u)
        {
            return (floaters.Contains(u) || AbilityLevel(u, CrowForm) > 0) && flyHeights.TryGetValue(u, out var height) && height != 0.0f;
        }

        /// <summary>Sets the level an ability has on a unit.</summary>
        public void SetAbilityLevel(unit u, int abilityId, int level)
        {
            if (!abilityLevels.TryGetValue(u, out var levels))
            {
                levels = new Dictionary<int, int>();
                abilityLevels[u] = levels;
            }

            levels[abilityId] = level;
        }

        /// <summary>Gives an ability its number of levels, which ChangeAbilityLevel keeps a level within.</summary>
        public void SetLevelCount(int abilityId, int count)
        {
            levelCounts[abilityId] = count;
        }

        /// <summary>Makes an ability a hero skill.</summary>
        public void MarkHeroAbility(int abilityId)
        {
            heroAbilities.Add(abilityId);
        }

        /// <summary>Sets the level a player slot has of an upgrade.</summary>
        public void SetUpgradeLevel(int slot, int upgradeId, int level)
        {
            if (!upgradeLevels.TryGetValue(slot, out var levels))
            {
                levels = new Dictionary<int, int>();
                upgradeLevels[slot] = levels;
            }

            levels[upgradeId] = level;
        }

        /// <summary>Sets the race of a player slot.</summary>
        public void SetRace(int slot, Race race)
        {
            PlayerAt(slot);
            races[slot] = (int)race;
        }

        /// <summary>Sets the start location of a player slot.</summary>
        public void SetStart(int slot, float x, float y)
        {
            startXs[slot] = x;
            startYs[slot] = y;
        }

        /// <summary>Sets a declared value of a type, as the build's table holds it.</summary>
        public void SetTypeValue(int typeId, string field, object value)
        {
            if (!typeValues.TryGetValue(typeId, out var values))
            {
                values = new Dictionary<string, object>();
                typeValues[typeId] = values;
            }

            values[field] = value;
        }

        /// <summary>A declared value of a type; null when none was set.</summary>
        public object? TypeValue(int typeId, string field)
        {
            if (typeValues.TryGetValue(typeId, out var values) && values.TryGetValue(field, out var value))
            {
                return value;
            }

            return null;
        }

        /// <summary>Moves time forward by the seconds, calling Runtime.Wake at each wake request on the way, WakeLate after the time it asked for; time never goes back, so a wake that came late keeps its time. The frame of the last calls ends first, and again each time the time moves: the drops that waited for it fire, and the units removed in it read type id 0. The second wake in a row that would not move the time comes at the game's next tick instead (NextTick), as the game's own clock moves on.</summary>
        public void Advance(float seconds)
        {
            var target = Time + seconds;
            DeliverPendingDrops();
            EndPendingRemovals();
            DeliverPendingSync();
            var standing = 0;
            while (WakeAt.HasValue && WakeAt.Value + WakeLate <= target + 0.001f)
            {
                var next = WakeAt.Value + WakeLate;
                standing = next <= Time ? standing + 1 : 0;
                if (standing >= 2)
                {
                    next = NextTick(Time);
                    standing = 0;
                    if (next > target + 0.001f)
                    {
                        break;
                    }
                }

                if (next > Time)
                {
                    DeliverPendingDrops();
                    EndPendingRemovals();
                }

                Time = next;
                WakeAt = null;
                GameBuffs.Step(Time);
                Runtime.Wake();
            }

            if (target > Time)
            {
                DeliverPendingDrops();
                EndPendingRemovals();
                Time = target;
            }

            GameBuffs.Step(Time);
        }

        /// <summary>
        /// The game's next tick after a time: 1/256 s later, or the smallest doubling of that which moves a time so late. In
        /// the game's Lua an addition drops what is below two float steps (game-facts, 10-27), so late in a game a short wake
        /// can ask for the time it is now again and again; the game's own clock still moves on, and a wake of 0 s comes at
        /// its next tick.
        /// </summary>
        private static float NextTick(float time)
        {
            var tick = 1.0f / 256.0f;
            while (time + tick <= time)
            {
                tick *= 2.0f;
            }

            return time + tick;
        }

        /// <summary>Makes the units removed in the frame that ends read as the game reads them from the next frame: a type id of 0, every number 0 and no owner (seen 10-17, 10-21).</summary>
        private void EndPendingRemovals()
        {
            foreach (var removed in pendingRemovals)
            {
                VanishUnit(removed);
            }

            pendingRemovals.Clear();
        }

        /// <summary>Fires the drop events that waited for the end of the call that removed their unit, when they are listened for, and then removes the items with the unit.</summary>
        private void DeliverPendingDrops()
        {
            if (pendingDrops.Count == 0)
            {
                return;
            }

            var waiting = new List<KeyValuePair<unit, item>>(pendingDrops);
            pendingDrops.Clear();
            foreach (var drop in waiting)
            {
                if (Listening.Contains(GameEvent.Drop))
                {
                    Inbound.Dropped(drop.Key, drop.Value);
                }

                VanishItem(drop.Value);
            }
        }

        /// <summary>The game buffs, spell hits and auras of this world (<see cref="FakeGameBuffs"/>); Install makes it.</summary>
        public FakeGameBuffs GameBuffs = null!;

        /// <summary>The spells dummies cast in this world, and what the runtime sets and reads of them (<see cref="FakeDummies"/>); Install makes it.</summary>
        public FakeDummies Dummies = null!;

        /// <summary>The area of each ability, for every level, by its id; 0 when not set.</summary>
        public Dictionary<int, float> AbilityAreas = new Dictionary<int, float>();

        /// <summary>The area of an ability at a unit's level of it, as set in <see cref="AbilityAreas"/>.</summary>
        public float AbilityArea(unit u, int abilityId)
        {
            return AbilityAreas.TryGetValue(abilityId, out var area) ? area : 0.0f;
        }

        /// <summary>Records the free cast in <see cref="FakeDummies.Fields"/>.</summary>
        public void FreeCasting(unit unit, int abilityId, int level)
        {
            Dummies.Freed(unit, abilityId, level);
        }

        /// <summary>The longer of the durations a case set for the spell; 0 until set.</summary>
        public float SpellLasts(unit unit, int abilityId, int level)
        {
            return Dummies.Lasts(abilityId);
        }

        /// <summary>The missile speed a case set for the spell; 0 until set.</summary>
        public int ProjectileSpeedOf(unit unit, int abilityId)
        {
            return Dummies.SpeedOf(abilityId);
        }

        /// <summary>The count a case set; 0 until set.</summary>
        public int TechCount(player player, int techId)
        {
            return Dummies.TechCount(player, techId);
        }

        /// <summary>Whether the player sees the unit, as the case hid it or not.</summary>
        public bool Sees(player player, unit unit)
        {
            return Dummies.Sees(player, unit);
        }

        /// <summary>A patch of vision, recorded "made".</summary>
        public fogmodifier SeeAround(player player, float x, float y)
        {
            return Dummies.Patch(player, x, y);
        }

        /// <summary>The patch ended, recorded "stopped".</summary>
        public void StopSeeing(fogmodifier patch)
        {
            Dummies.Unpatch(patch);
        }

        /// <summary>The units in the world within a radius of a place, centre to centre, alive or dead, in the order made; a removed unit is not seen, in the instant of its removal too (seen 10-02), and its place is not read.</summary>
        public List<unit> UnitsInRange(float x, float y, float radius)
        {
            var found = new List<unit>();
            foreach (var made in madeUnits)
            {
                if (unitTypes[made] == 0 || RemovedUnits.Contains(made))
                {
                    continue;
                }

                var dx = UnitX(made) - x;
                var dy = UnitY(made) - y;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    found.Add(made);
                }
            }

            return found;
        }
        /// <summary>The game seconds since the world was made; in a development build, Moonwell's time on its clock (Runtime.Clock), as NativeWorld's.</summary>
        public float Now()
        {
#if DEBUG
            return Runtime.Clock.Now(Time);
#else
            return Time;
#endif
        }

        /// <summary>Requests a wake the seconds from now, or now for seconds below 0, as NativeWorld starts the game's timer at 0 for them; replaces any earlier request. In a development build the seconds are Moonwell's, and a request while its clock is halted drops the wake until the clock re-arms it, as NativeWorld's.</summary>
        public void WakeIn(float seconds)
        {
#if DEBUG
            if (Runtime.Clock.Arm(seconds, out var gameSeconds))
            {
                WakeAt = Time + (gameSeconds < 0.0f ? 0.0f : gameSeconds);
            }
            else
            {
                WakeAt = null;
            }
#else
            WakeAt = Time + (seconds < 0.0f ? 0.0f : seconds);
#endif
        }

        /// <summary>Records that the runtime asked to hear an event, once.</summary>
        void IWorld.Listen(GameEvent gameEvent)
        {
            if (!Listening.Contains(gameEvent))
            {
                Listening.Add(gameEvent);
            }
        }

        /// <summary>Records the damage; unless refused, delivers it to Inbound as the game does, inside the call, when the runtime listens for damage. The game's event says no attack even for one dealt as an attack. Then the life drops by what the reactions left, before the call returns, and 0.405 or less kills with the source as the killer; a unit whose life was never set keeps its 0. A dead unit gets no event and takes nothing, though the game answers true (seen 10-31).</summary>
        public bool DealDamage(unit source, unit target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType)
        {
            Dealt.Add((int)amount + " attack:" + attack + " ranged:" + ranged + " " + attackType + " " + damageType + " " + weaponType);
            if (RefuseDamage)
            {
                return false;
            }

            if (dead.Contains(target) || (SpellImmune.Contains(target) && (attackType == 4 || IsMagicDamage(damageType))))
            {
                return true;
            }

            var left = amount;
            if (Listening.Contains(GameEvent.Damage))
            {
                left = Inbound.Damaged(target, source, amount, false);
            }

            if (lives.TryGetValue(target, out var life))
            {
                if (life - left <= 0.405f)
                {
                    Die(target, source);
                }
                else
                {
                    lives[target] = life - left;
                }
            }

            return true;
        }

        /// <summary>Whether spell immunity stops a damage type with any attack type: fire, cold, lightning, divine, magic, sonic, force, death, mind, plant, defensive, spirit link and shadow strike (seen 10-31).</summary>
        private static bool IsMagicDamage(int damageType)
        {
            switch (damageType)
            {
                case 8:
                case 9:
                case 10:
                case 13:
                case 14:
                case 15:
                case 17:
                case 18:
                case 19:
                case 20:
                case 21:
                case 24:
                case 25:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Records the damage to a destructable: the game's numbers, and the handles it was dealt between.</summary>
        public void DealDamageTo(unit source, destructable target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType)
        {
            DealtToDestructables.Add((int)amount + " attack:" + attack + " ranged:" + ranged + " " + attackType + " " + damageType + " " + weaponType);
            DealtToDestructableSources.Add(source);
            DealtToDestructableTargets.Add(target);
        }

        /// <summary>Records that the runtime asked to hear a destructable's death, once.</summary>
        public void WatchDeath(destructable destructable)
        {
            if (!Watched.Contains(destructable))
            {
                Watched.Add(destructable);
            }
        }

        /// <summary>Stops delivering a destructable's death.</summary>
        public void UnwatchDeath(destructable destructable)
        {
            Watched.Remove(destructable);
        }

        /// <summary>Records a line printed on the screen.</summary>
        public void Print(string text)
        {
            Printed.Add(text);
        }

        /// <summary>The slot state of a player; Empty until SetSlot sets it.</summary>
        public int SlotStateOf(player player)
        {
            return slotStates.TryGetValue(SlotOf(player), out var state) ? state : 0;
        }

        /// <summary>Who controls a player; User until SetSlot sets it.</summary>
        public int ControllerOf(player player)
        {
            return controllers.TryGetValue(SlotOf(player), out var control) ? control : 0;
        }

        /// <summary>Sets a slot's state and who controls it.</summary>
        public void SetSlot(int slot, SlotState state, Controller controller)
        {
            PlayerAt(slot);
            slotStates[slot] = (int)state;
            controllers[slot] = (int)controller;
        }

        /// <summary>A state of a player; 0 until it is set.</summary>
        int IWorld.StateOf(player player, PlayerState state)
        {
            return states.TryGetValue(SlotOf(player) * 16 + (int)state, out var value) ? value : 0;
        }

        /// <summary>Sets a state of a player.</summary>
        void IWorld.SetState(player player, PlayerState state, int value)
        {
            states[SlotOf(player) * 16 + (int)state] = value;
        }

        /// <summary>Records a line printed to one player.</summary>
        public void PrintTo(player player, string text, float seconds)
        {
            PrintedSlots.Add(SlotOf(player));
            PrintedTexts.Add(text);
            PrintedSeconds.Add(seconds);
        }

        /// <summary>Records an error line shown to one player, as "error: " and its text.</summary>
        public void ErrorTo(player player, string text)
        {
            PrintedSlots.Add(SlotOf(player));
            PrintedTexts.Add("error: " + text);
            PrintedSeconds.Add(2.0f);
        }

        /// <summary>Records a line printed to everyone.</summary>
        public void PrintToAll(string text, float seconds)
        {
            PrintedSlots.Add(-1);
            PrintedTexts.Add(text);
            PrintedSeconds.Add(seconds);
        }

        /// <summary>Whether the log file is written at every line; true until set, as in a development build.</summary>
        public bool LogAtOnce { get; set; } = true;

        /// <summary>Records a line appended to the log.</summary>
        public void Log(string text)
        {
            Logged.Add(text);
        }

        /// <summary>This machine's clock, in seconds since 1970; 0 until a case sets it.</summary>
        public int RealSeconds;

        /// <summary>This machine's zone's offset from UTC, in seconds.</summary>
        public int RealOffset;

        /// <summary>This machine's clock, as set.</summary>
        public int ClockSeconds()
        {
            return RealSeconds;
        }

        /// <summary>This machine's zone's offset, as set.</summary>
        public int ZoneOffset()
        {
            return RealOffset;
        }

        /// <summary>This machine's player files, by their path in CustomMapData: the pieces written.</summary>
        public Dictionary<string, List<string>> Files = new Dictionary<string, List<string>>();

        /// <summary>Keeps a player file's pieces, replacing the file.</summary>
        public void WritePreload(string path, IReadOnlyList<string> pieces)
        {
            Files[path] = new List<string>(pieces);
        }

        /// <summary>A copy of a player file's pieces; null when there is none.</summary>
        public List<string>? ReadPreload(string path)
        {
            return Files.TryGetValue(path, out var pieces) ? new List<string>(pieces) : null;
        }

        /// <summary>Drops this machine's messages not yet delivered, as a replay does: there the recorded ones arrive instead.</summary>
        public void DropPendingSync()
        {
            pendingSync.Clear();
        }

        /// <summary>The sharing messages this machine sent, in order.</summary>
        public List<string> SentSync = new List<string>();

        /// <summary>The slots whose sharing messages are listened for.</summary>
        public List<int> ListenedSync = new List<int>();

        /// <summary>This machine's messages, delivered at the next Advance, as the game delivers them a moment later.</summary>
        private readonly List<string> pendingSync = new List<string>();

        /// <summary>Records a sharing message this machine sends; the next Advance delivers it as the local player's.</summary>
        public void SendSync(string text)
        {
            SentSync.Add(text);
            pendingSync.Add(text);
        }

        /// <summary>Records that a player's sharing messages are listened for.</summary>
        public void ListenSync(player player)
        {
            ListenedSync.Add(SlotOf(player));
        }

        /// <summary>The slots whose chat lines are listened for.</summary>
        public List<int> ListenedChat = new List<int>();

        /// <summary>Records that a person's chat lines are listened for.</summary>
        public void ListenChat(player player)
        {
            ListenedChat.Add(SlotOf(player));
        }

        /// <summary>A person types a chat line; it reaches Moonwell when their chat is listened for and the line has a dash in it, anywhere, as the game's chat event gives it.</summary>
        public void Chat(Player from, string text)
        {
            if (ListenedChat.Contains(from.Slot) && text.IndexOf('-') >= 0)
            {
                Inbound.ChatArrived(from.Handle, text);
            }
        }

        /// <summary>Delivers a sharing message as if sent by another machine's player, at once, when that player is listened for.</summary>
        public void DeliverSync(Player from, string text)
        {
            if (ListenedSync.Contains(from.Slot))
            {
                Inbound.SyncArrived(from.Handle, text);
            }
        }

        private void DeliverPendingSync()
        {
            if (pendingSync.Count == 0)
            {
                return;
            }

            var waiting = new List<string>(pendingSync);
            pendingSync.Clear();
            var local = Player.Get(LocalSlot + MachineShift);
            foreach (var text in waiting)
            {
                DeliverSync(local, text);
            }
        }

        /// <summary>The time of day as last set; 0 until then, as the template map starts at midnight.</summary>
        public float HourOfDay()
        {
            return hourOfDay;
        }

        /// <summary>Sets the time of day.</summary>
        public void SetHourOfDay(float hours)
        {
            hourOfDay = hours;
        }

        /// <summary>The day's speed as last set; 1 until then.</summary>
        public float DayScale()
        {
            return dayScale;
        }

        /// <summary>Sets the day's speed.</summary>
        public void SetDayScale(float scale)
        {
            dayScale = scale;
        }

        /// <summary>Whether the day was last stopped.</summary>
        public bool IsDayStopped()
        {
            return dayStopped;
        }

        /// <summary>Stops the day or lets it go on.</summary>
        public void StopDay(bool stopped)
        {
            dayStopped = stopped;
        }

        /// <summary>The player in a slot; one is made the first time its slot is asked for.</summary>
        public player PlayerAt(int slot)
        {
            if (players.TryGetValue(slot, out var found))
            {
                return found;
            }

            found = new player();
            players[slot] = found;
            slots[found] = slot;
            return found;
        }

        /// <summary>The slot of a player the world made, or -1 for a player it did not make.</summary>
        public int SlotOf(player player)
        {
            if (slots.TryGetValue(player, out var slot))
            {
                return slot;
            }

            return -1;
        }

        /// <summary>The slot of the neutral hostile player, from NeutralAggressive.</summary>
        public int NeutralAggressiveSlot()
        {
            return NeutralAggressive;
        }

        /// <summary>The slot of the neutral victim player, from NeutralVictim.</summary>
        public int NeutralVictimSlot()
        {
            return NeutralVictim;
        }

        /// <summary>The slot of the neutral extra player, from NeutralExtra.</summary>
        public int NeutralExtraSlot()
        {
            return NeutralExtra;
        }

        /// <summary>The slot of the neutral passive player, from NeutralPassive.</summary>
        public int NeutralPassiveSlot()
        {
            return NeutralPassive;
        }

        /// <summary>The x of a player's start location; 0 when none is set.</summary>
        public float StartXOf(player player)
        {
            return startXs.TryGetValue(SlotOf(player), out var x) ? x : 0f;
        }

        /// <summary>The y of a player's start location; 0 when none is set.</summary>
        public float StartYOf(player player)
        {
            return startYs.TryGetValue(SlotOf(player), out var y) ? y : 0f;
        }

        /// <summary>Whether a player is the one at this machine: the player in LocalSlot, moved on by MachineShift.</summary>
        public bool IsLocal(player player)
        {
            return SlotOf(player) == LocalSlot + MachineShift;
        }

        /// <summary>A copy of the units placed in the world.</summary>
        public List<unit> UnitsInWorld()
        {
            return new List<unit>(PlacedUnits);
        }

        /// <summary>A copy of the items placed in the world.</summary>
        public List<item> ItemsInWorld()
        {
            return new List<item>(PlacedItems);
        }

        /// <summary>A copy of the destructables placed in the world.</summary>
        public List<destructable> DestructablesInWorld()
        {
            return new List<destructable>(PlacedDestructables);
        }

        /// <summary>Makes a unit owned by the owner's slot at the place and facing, a hero when its type's id starts with a capital letter (the id decides, seen 10-28), and keeps the facing in LastFacing; null for a type in Unmakeable, as the game's CreateUnit gives for a type it cannot make.</summary>
        public unit SpawnUnit(player owner, int typeId, float x, float y, float facing)
        {
            LastFacing = facing;
            if (Unmakeable.Contains(typeId))
            {
                return null!;
            }

            var first = (typeId >> 24) & 0xFF;
            var created = NewUnit(typeId, SlotOf(owner), first >= 'A' && first <= 'Z');
            unitXs[created] = x;
            unitYs[created] = y;
            facings[created] = facing;
            return created;
        }

        /// <summary>Makes an item at the place; null for a type in Unmakeable, as the game's CreateItem gives for a type it cannot make.</summary>
        public item SpawnItem(int typeId, float x, float y)
        {
            if (Unmakeable.Contains(typeId))
            {
                return null!;
            }

            var created = NewItem(typeId);
            itemPlaces[created] = new float[] { x, y };
            return created;
        }

        /// <summary>The type id of a unit; 0 once it is removed or vanished.</summary>
        public int UnitTypeId(unit unit)
        {
            Refuse();
            if (unitTypes.TryGetValue(unit, out var typeId))
            {
                return typeId;
            }

            return 0;
        }

        /// <summary>The type id of an item; 0 once it is removed or vanished.</summary>
        public int ItemTypeId(item item)
        {
            Refuse();
            if (itemTypes.TryGetValue(item, out var typeId))
            {
                return typeId;
            }

            return 0;
        }

        /// <summary>The type id of a destructable; 0 once it is removed or vanished. Counts the read.</summary>
        public int DestructableTypeId(destructable destructable)
        {
            DestructableTypeReads++;
            if (destructableTypes.TryGetValue(destructable, out var typeId))
            {
                return typeId;
            }

            return 0;
        }

        /// <summary>Records a unit removed by the runtime. It reads as it was in this frame, as the game's does after RemoveUnit, and its type id and every number read 0 and its owner null from the next Advance, after the drop events (seen 10-21: 0 by 0.05 s later; seen 10-26: the drops fire after the call while it still reads). The unit's items leave its slots inside the call, at its place; their drop events come after the call, at the next Advance, and the items are removed with the unit (seen 10-21 for a hero).</summary>
        public void DeleteUnit(unit unit)
        {
            RemovedUnits.Add(unit);
            if (inventories.TryGetValue(unit, out var slots))
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    var carried = slots[i];
                    if (carried != null)
                    {
                        slots[i] = null!;
                        LandAt(unit, carried);
                        pendingDrops.Add(new KeyValuePair<unit, item>(unit, carried));
                    }
                }
            }

            pendingRemovals.Add(unit);
        }

        /// <summary>Puts an item that leaves a unit's slots on the ground at the unit's place.</summary>
        private void LandAt(unit unit, item item)
        {
            unitXs.TryGetValue(unit, out var x);
            unitYs.TryGetValue(unit, out var y);
            itemPlaces[item] = new float[] { x, y };
        }

        /// <summary>Records an item removed by the runtime: a carried one leaves its slot with its drop event inside the call, as UnitRemoveItem does, and none later (seen 11-06); its type id and every number of it read 0 in the same frame (seen 10-21).</summary>
        public void DeleteItem(item item)
        {
            RemovedItems.Add(item);
            TakeFromHolder(item);
            VanishItem(item);
        }

        /// <summary>The x of a destructable; 100 unless placed, and 0 once it is removed or vanished.</summary>
        public float DestructableX(destructable destructable)
        {
            if (DestructableTypeId(destructable) == 0)
            {
                return 0.0f;
            }

            return destructablePlaces.TryGetValue(destructable, out var place) ? place[0] : 100f;
        }

        /// <summary>The y of a destructable; -50 unless placed, and 0 once it is removed or vanished.</summary>
        public float DestructableY(destructable destructable)
        {
            if (DestructableTypeId(destructable) == 0)
            {
                return 0.0f;
            }

            return destructablePlaces.TryGetValue(destructable, out var place) ? place[1] : -50f;
        }

        /// <summary>Makes a destructable where the game would put a tree, on the 64 grid (1000 is 960, seen 10-26), and records what was asked; null for a type in Unmakeable, as CreateDestructable gives for a unit's id (seen 11-02).</summary>
        public destructable SpawnDestructable(int typeId, float x, float y, float facing, float scale, int variation)
        {
            DestructableSpawns.Add(new DestructableSpawn { TypeId = typeId, X = x, Y = y, Facing = facing, Scale = scale, Variation = variation });
            if (Unmakeable.Contains(typeId))
            {
                return null!;
            }

            var created = NewDestructable(typeId);
            PlaceDestructable(created, 64f * (int)(x / 64f), 64f * (int)(y / 64f));
            return created;
        }

        /// <summary>Makes a destructable as SpawnDestructable does, recording the height asked.</summary>
        public destructable SpawnDestructableAt(int typeId, float x, float y, float z, float facing, float scale, int variation)
        {
            var made = SpawnDestructable(typeId, x, y, facing, scale, variation);
            DestructableSpawns[DestructableSpawns.Count - 1].Z = z;
            DestructableSpawns[DestructableSpawns.Count - 1].Made = made;
            return made;
        }

        /// <summary>Kills a destructable as KillDestructable does: life 0 and its death inside the call when watched; nothing for one already dead (seen 11-02).</summary>
        public void SlayDestructable(destructable destructable)
        {
            Refuse();
            if (DestructableTypeId(destructable) != 0 && DestructableLifeOf(destructable) > 0f)
            {
                Fell(destructable);
            }
        }

        /// <summary>The life of a destructable; 0 once it is removed or vanished.</summary>
        public float DestructableLifeOf(destructable destructable)
        {
            Refuse();
            if (DestructableTypeId(destructable) == 0)
            {
                return 0f;
            }

            return destructableLives.TryGetValue(destructable, out var life) ? life : DestructableLifeAtStart;
        }

        /// <summary>Sets a destructable's life as SetDestructableLife does: clamped to its most, and below 0.405 (0.4 and less, seen 11-02) it dies, its death inside the call when watched. On a dead one the life is set, as the game sets it.</summary>
        public void PutDestructableLife(destructable destructable, float life)
        {
            Refuse();
            if (DestructableTypeId(destructable) == 0)
            {
                return;
            }

            if (life < 0.405f)
            {
                if (DestructableLifeOf(destructable) > 0f)
                {
                    Fell(destructable);
                }

                return;
            }

            var most = DestructableMaxLifeOf(destructable);
            destructableLives[destructable] = life < most ? life : most;
        }

        /// <summary>The most life of a destructable; 0 once it is removed or vanished.</summary>
        public float DestructableMaxLifeOf(destructable destructable)
        {
            Refuse();
            if (DestructableTypeId(destructable) == 0)
            {
                return 0f;
            }

            return destructableMaxLives.TryGetValue(destructable, out var most) ? most : DestructableLifeAtStart;
        }

        /// <summary>Sets a destructable's most life as SetDestructableMaxLife does, kept as a whole number (80.7 is 81): a full one stays full and one above the new most falls to it (seen 11-02); one below it keeps its life, which the game was not seen to do.</summary>
        public void PutDestructableMaxLife(destructable destructable, float most)
        {
            Refuse();
            if (DestructableTypeId(destructable) == 0)
            {
                return;
            }

            var whole = (float)(int)(most + 0.5f);
            var life = DestructableLifeOf(destructable);
            var full = life >= DestructableMaxLifeOf(destructable);
            destructableMaxLives[destructable] = whole;
            if (full || life > whole)
            {
                destructableLives[destructable] = whole;
            }
        }

        /// <summary>Records the restore, and brings a dead destructable back as DestructableRestoreLife does: 0 or the most and above give the most, below 0.5 gives 0.5; nothing for a living one (seen 11-02).</summary>
        public void RestoreDestructable(destructable destructable, float life, bool birth)
        {
            Refuse();
            RestoredLives.Add(life);
            RestoredBirths.Add(birth);
            if (DestructableTypeId(destructable) == 0 || DestructableLifeOf(destructable) > 0f)
            {
                return;
            }

            var most = DestructableMaxLifeOf(destructable);
            destructableLives[destructable] = life == 0f || life >= most ? most : (life < 0.5f ? 0.5f : life);
        }

        /// <summary>The death itself: life 0, and the death event inside the call for a watched destructable.</summary>
        private void Fell(destructable destructable)
        {
            destructableLives[destructable] = 0f;
            if (Watched.Contains(destructable))
            {
                Inbound.DestructableDied(destructable);
            }
        }

        /// <summary>Records a destructable removed by the runtime, and reads its type id as 0. As the game does, it fires the death event of a watched destructable inside the call, for one that was alive and for one already dead (seen 10-23).</summary>
        public void DeleteDestructable(destructable destructable)
        {
            RemovedDestructables.Add(destructable);
            if (Watched.Contains(destructable))
            {
                Inbound.DestructableDied(destructable);
            }

            destructableTypes[destructable] = 0;
        }

        /// <summary>The calls that changed how a destructable looks or what it takes, in turn: "show True", "animate death", "queue stand", "speed 0.5", "tint 1 2 3 4", "invulnerable True", "occluder 5".</summary>
        public List<string> DestructableCalls = new List<string>();

        /// <summary>The doodad animations played, in turn: "near typeId x y radius nearestOnly name" or "in typeId left bottom right top name".</summary>
        public List<string> DoodadAnimations = new List<string>();

        /// <summary>The name every destructable's type reads.</summary>
        public string DestructableName = "Summer Tree Wall";

        private readonly HashSet<destructable> invulnerableDestructables = new HashSet<destructable>();

        private readonly Dictionary<destructable, float> occluderHeights = new Dictionary<destructable, float>();

        /// <summary>Records a show or a hide.</summary>
        public void DisplayDestructable(destructable destructable, bool shown) => DestructableCalls.Add("show " + shown);

        /// <summary>Records an animation played or queued.</summary>
        public void AnimateDestructable(destructable destructable, string name, bool queued) => DestructableCalls.Add((queued ? "queue " : "animate ") + name);

        /// <summary>Records an animation speed.</summary>
        public void PaceDestructableAnimations(destructable destructable, float speed) => DestructableCalls.Add("speed " + speed);

        /// <summary>Records a tint.</summary>
        public void TintDestructable(destructable destructable, int red, int green, int blue, int alpha) => DestructableCalls.Add("tint " + red + " " + green + " " + blue + " " + alpha);

        /// <summary>Whether a destructable was made invulnerable.</summary>
        public bool DestructableInvulnerableOf(destructable destructable) => invulnerableDestructables.Contains(destructable);

        /// <summary>Records and keeps whether a destructable is invulnerable.</summary>
        public void PutDestructableInvulnerable(destructable destructable, bool invulnerable)
        {
            DestructableCalls.Add("invulnerable " + invulnerable);
            if (invulnerable)
            {
                invulnerableDestructables.Add(destructable);
            }
            else
            {
                invulnerableDestructables.Remove(destructable);
            }
        }

        /// <summary>DestructableName.</summary>
        public string DestructableNameOf(destructable destructable) => DestructableName;

        /// <summary>The occluder height set, 0 until set.</summary>
        public float DestructableOccluderHeightOf(destructable destructable) => occluderHeights.TryGetValue(destructable, out var height) ? height : 0f;

        /// <summary>Records and keeps an occluder height.</summary>
        public void PutDestructableOccluderHeight(destructable destructable, float height)
        {
            DestructableCalls.Add("occluder " + height);
            occluderHeights[destructable] = height;
        }

        /// <summary>Records a doodad animation near a place.</summary>
        public void AnimateDoodads(int typeId, float x, float y, float radius, bool nearestOnly, string name) => DoodadAnimations.Add("near " + typeId + " " + x + " " + y + " " + radius + " " + nearestOnly + " " + name);

        /// <summary>Records a doodad animation inside a rect.</summary>
        public void AnimateDoodadsIn(int typeId, float left, float bottom, float right, float top, string name) => DoodadAnimations.Add("in " + typeId + " " + left + " " + bottom + " " + right + " " + top + " " + name);

        /// <summary>Whether a unit was made as a hero.</summary>
        public bool IsHero(unit unit)
        {
            Refuse();
            return heroes.TryGetValue(unit, out var hero) && hero;
        }

        /// <summary>Whether a unit was made an illusion with MakeIllusion.</summary>
        public bool IsIllusion(unit unit)
        {
            Refuse();
            return illusions.Contains(unit);
        }

        /// <summary>Makes a unit an illusion, as Mirror Image makes one of its hero, with the hero's type id (seen 10-26).</summary>
        public void MakeIllusion(unit u)
        {
            illusions.Add(u);
        }

        /// <summary>The player owning a unit; null for a vanished unit, slot 0 for a unit the world did not make.</summary>
        public player OwnerOf(unit unit)
        {
            Refuse();
            if (ownerless.Contains(unit))
            {
                return null!;
            }

            if (unitSlots.TryGetValue(unit, out var slot))
            {
                return PlayerAt(slot);
            }

            return PlayerAt(0);
        }

        /// <summary>Each shop's goods put on sale by AddStock: the count and the most, by item id.</summary>
        public Dictionary<unit, Dictionary<int, int[]>> Stock = new Dictionary<unit, Dictionary<int, int[]>>();

        /// <summary>Records a good put on sale.</summary>
        public void AddStock(unit shop, int itemId, int count, int max)
        {
            if (!Stock.TryGetValue(shop, out var goods))
            {
                goods = new Dictionary<int, int[]>();
                Stock[shop] = goods;
            }

            goods[itemId] = new int[] { count, max };
        }

        /// <summary>Forgets a good put on sale.</summary>
        public void RemoveStock(unit shop, int itemId)
        {
            if (Stock.TryGetValue(shop, out var goods))
            {
                goods.Remove(itemId);
            }
        }

        /// <summary>The level set on a unit for an ability; 0 when none was set.</summary>
        public int AbilityLevel(unit unit, int abilityId)
        {
            if (abilityLevels.TryGetValue(unit, out var levels) && levels.TryGetValue(abilityId, out var level))
            {
                return level;
            }

            return 0;
        }

        /// <summary>The level set for a player's slot for an upgrade; 0 when none was set.</summary>
        public int UpgradeLevel(player p, int upgradeId)
        {
            if (upgradeLevels.TryGetValue(SlotOf(p), out var levels) && levels.TryGetValue(upgradeId, out var level))
            {
                return level;
            }

            return 0;
        }

        /// <summary>Sets the level a player's slot has of an upgrade.</summary>
        public void Research(player player, int upgradeId, int level)
        {
            SetUpgradeLevel(SlotOf(player), upgradeId, level);
        }

        /// <summary>The tech limit set for a player's slot; int.MaxValue, the game's "no limit" for a unit type, when none was set.</summary>
        public int TechMaxOf(player player, int typeId)
        {
            if (techMaxes.TryGetValue(SlotOf(player), out var maxes) && maxes.TryGetValue(typeId, out var max))
            {
                return max;
            }

            return int.MaxValue;
        }

        /// <summary>Sets the tech limit of a player's slot.</summary>
        public void SetTechMax(player player, int typeId, int max)
        {
            var slot = SlotOf(player);
            if (!techMaxes.TryGetValue(slot, out var maxes))
            {
                maxes = new Dictionary<int, int>();
                techMaxes[slot] = maxes;
            }

            maxes[typeId] = max;
        }

        /// <summary>Records a call that makes an ability available or not, as "available slot id on|off".</summary>
        public void MakeAvailable(player player, int abilityId, bool available)
        {
            AbilityCalls.Add("available " + SlotOf(player) + " " + abilityId + (available ? " on" : " off"));
        }

        /// <summary>The name set for a player's slot; "Player n", counted from 1, until one is set.</summary>
        public string NameOf(player player)
        {
            var slot = SlotOf(player);
            return names.TryGetValue(slot, out var name) ? name : "Player " + (slot + 1);
        }

        /// <summary>Sets the name of a player's slot.</summary>
        public void Rename(player player, string name)
        {
            names[SlotOf(player)] = name;
        }

        /// <summary>The colour set for a player's slot; the slot's own number until one is set.</summary>
        public int ColorOf(player player)
        {
            var slot = SlotOf(player);
            return colors.TryGetValue(slot, out var color) ? color : slot;
        }

        /// <summary>Sets the colour of a player's slot.</summary>
        public void Recolor(player player, int color)
        {
            colors[SlotOf(player)] = color;
        }

        /// <summary>The team set for a player's slot; 0 until one is set.</summary>
        public int TeamOf(player player)
        {
            return teams.TryGetValue(SlotOf(player), out var team) ? team : 0;
        }

        /// <summary>Sets the team of a player's slot.</summary>
        public void MoveToTeam(player player, int team)
        {
            teams[SlotOf(player)] = team;
        }

        /// <summary>The race set for a player's slot; Human until SetRace sets it.</summary>
        public int RaceOf(player player)
        {
            return races.TryGetValue(SlotOf(player), out var race) ? race : 0;
        }

        /// <summary>The alliance set from one slot to another; None until one is set.</summary>
        public int AllianceOf(player source, player other)
        {
            return alliances.TryGetValue(SlotOf(source) * 32 + SlotOf(other), out var alliance) ? alliance : 0;
        }

        /// <summary>Sets the alliance from one slot to another; as the game does, the giver's alliance event fires inside the call, when listened for.</summary>
        public void SetAllianceOf(player source, player other, int alliance)
        {
            alliances[SlotOf(source) * 32 + SlotOf(other)] = alliance;
            if (Listening.Contains(GameEvent.Alliance))
            {
                Inbound.AllianceChanged(source);
            }
        }

        /// <summary>Each Win and Lose, in order: "win 0", with "(no dialog)" or "(no scores)" when off, or "lose 2: message".</summary>
        public List<string> Results = new List<string>();

        /// <summary>Takes a player out as a winner, as the game does: its slot stays Playing.</summary>
        public void Win(player player, bool dialog, bool scores)
        {
            Results.Add("win " + SlotOf(player) + (dialog ? "" : " (no dialog)") + (scores ? "" : " (no scores)"));
        }

        /// <summary>Takes a player out as a loser, as the game does: its slot turns Left.</summary>
        public void Lose(player player, string message)
        {
            slotStates[SlotOf(player)] = (int)SlotState.Left;
            Results.Add("lose " + SlotOf(player) + ": " + message);
        }

        /// <summary>Whether the other is an ally: the same slot, or one the source is passive to.</summary>
        public bool IsAllyOf(player source, player other)
        {
            return SlotOf(source) == SlotOf(other) || (AllianceOf(source, other) & (int)Alliance.Passive) != 0;
        }

        /// <summary>Whether the other is an enemy: not an ally.</summary>
        public bool IsEnemyOf(player source, player other)
        {
            return !IsAllyOf(source, other);
        }

        /// <summary>Records an add-ability call; a unit that lacks the ability gets it at level 1, kept among those code gave it, and the answer is true, as UnitAddAbility does (seen 11-02).</summary>
        public bool AddAbility(unit unit, int abilityId)
        {
            AbilityCalls.Add("add " + abilityId);
            if (AbilityLevel(unit, abilityId) > 0)
            {
                return false;
            }

            SetAbilityLevel(unit, abilityId, 1);
            AddedTo(unit).Add(abilityId);
            if (abilityId == CrowForm)
            {
                crowFormed.Add(unit);
            }

            return true;
        }

        /// <summary>Sets the level of an ability the unit has, kept from 1 to its level count, and answers the level set; 0, and nothing changes, for one it lacks, as SetUnitAbilityLevel does (seen 11-02).</summary>
        public int ChangeAbilityLevel(unit unit, int abilityId, int level)
        {
            if (AbilityLevel(unit, abilityId) <= 0)
            {
                return 0;
            }

            var given = level < 1 ? 1 : level;
            if (levelCounts.TryGetValue(abilityId, out var count) && given > count)
            {
                given = count;
            }

            SetAbilityLevel(unit, abilityId, given);
            return given;
        }

        /// <summary>The full cooldowns set, by unit, ability and level index; 0 when none was set.</summary>
        private readonly Dictionary<unit, Dictionary<long, float>> cooldowns = new Dictionary<unit, Dictionary<long, float>>();

        /// <summary>The mana costs set, by unit, ability and level index; 0 when none was set.</summary>
        private readonly Dictionary<unit, Dictionary<long, int>> manaCosts = new Dictionary<unit, Dictionary<long, int>>();

        /// <summary>When each unit's ability's cooldown ends, in the fake's time.</summary>
        private readonly Dictionary<unit, Dictionary<int, float>> cooldownEnds = new Dictionary<unit, Dictionary<int, float>>();

        /// <summary>The ability each unit has started to cast and that has not taken effect (StartCasting), so a stop ends it inside the call, as in the game (seen 11-08).</summary>
        private readonly Dictionary<unit, int> casting = new Dictionary<unit, int>();

        /// <summary>The key of an ability's level in the cooldown and mana tables.</summary>
        private static long LevelKey(int abilityId, int levelIndex) => ((long)abilityId * 1000) + levelIndex;

        /// <summary>The full cooldown set for a unit's ability at a level; 0 when none was set.</summary>
        public float AbilityCooldown(unit unit, int abilityId, int levelIndex)
        {
            return cooldowns.TryGetValue(unit, out var set) && set.TryGetValue(LevelKey(abilityId, levelIndex), out var seconds) ? seconds : 0.0f;
        }

        /// <summary>Sets the full cooldown of a unit's ability at a level.</summary>
        public void SetAbilityCooldown(unit unit, int abilityId, int levelIndex, float seconds)
        {
            if (!cooldowns.TryGetValue(unit, out var set))
            {
                set = new Dictionary<long, float>();
                cooldowns[unit] = set;
            }

            set[LevelKey(abilityId, levelIndex)] = seconds;
        }

        /// <summary>The seconds left of a unit's ability's cooldown, from the fake's time.</summary>
        public float AbilityCooldownLeft(unit unit, int abilityId)
        {
            if (!cooldownEnds.TryGetValue(unit, out var ends) || !ends.TryGetValue(abilityId, out var end))
            {
                return 0.0f;
            }

            return end > Time ? end - Time : 0.0f;
        }

        /// <summary>Starts a unit's ability's cooldown in place of what is left; 0 does nothing, as in the game (seen 11-08).</summary>
        public void StartAbilityCooldown(unit unit, int abilityId, float seconds)
        {
            if (seconds <= 0.0f)
            {
                return;
            }

            if (!cooldownEnds.TryGetValue(unit, out var ends))
            {
                ends = new Dictionary<int, float>();
                cooldownEnds[unit] = ends;
            }

            ends[abilityId] = Time + seconds;
        }

        /// <summary>Ends a unit's ability's cooldown.</summary>
        public void EndAbilityCooldown(unit unit, int abilityId)
        {
            if (cooldownEnds.TryGetValue(unit, out var ends))
            {
                ends.Remove(abilityId);
            }
        }

        /// <summary>The mana cost set for a unit's ability at a level; 0 when none was set.</summary>
        public int AbilityManaCost(unit unit, int abilityId, int levelIndex)
        {
            return manaCosts.TryGetValue(unit, out var set) && set.TryGetValue(LevelKey(abilityId, levelIndex), out var mana) ? mana : 0;
        }

        /// <summary>Sets the mana cost of a unit's ability at a level.</summary>
        public void SetAbilityManaCost(unit unit, int abilityId, int levelIndex, int mana)
        {
            if (!manaCosts.TryGetValue(unit, out var set))
            {
                set = new Dictionary<long, int>();
                manaCosts[unit] = set;
            }

            set[LevelKey(abilityId, levelIndex)] = mana;
        }

        /// <summary>A unit starts to cast an ability: the channel event, delivered when the runtime listens for spells; a stop before TakeEffect ends it with its endcast inside the stop.</summary>
        public void StartCasting(unit unit, int abilityId, unit? target = null, float x = 0.0f, float y = 0.0f)
        {
            casting[unit] = abilityId;
            if (Listening.Contains(GameEvent.Spell))
            {
                Inbound.SpellChannel(unit, abilityId, target, x, y);
            }
        }

        /// <summary>The spell a unit started to cast takes effect (the effect event), when it was not stopped; whether it did.</summary>
        public bool TakeEffect(unit unit, int abilityId, unit? target = null, float x = 0.0f, float y = 0.0f)
        {
            if (!casting.TryGetValue(unit, out var started) || started != abilityId)
            {
                return false;
            }

            casting.Remove(unit);
            if (Listening.Contains(GameEvent.Spell))
            {
                Inbound.SpellEffect(unit, abilityId, target, x, y);
            }

            return true;
        }

        /// <summary>The units given the summoned type.</summary>
        public HashSet<unit> Summoned = new HashSet<unit>();

        /// <summary>The seconds of timed life given each unit.</summary>
        public Dictionary<unit, float> TimedLives = new Dictionary<unit, float>();

        /// <summary>Gives a unit the summoned type.</summary>
        public void MakeSummoned(unit unit)
        {
            Summoned.Add(unit);
        }

        /// <summary>Records a unit's timed life; ExpireTimedLife ends it.</summary>
        public void ApplyTimedLife(unit unit, float seconds)
        {
            TimedLives[unit] = seconds;
        }

        /// <summary>A unit's timed life runs out: it dies with no killer, as in the game (seen 11-08).</summary>
        public void ExpireTimedLife(unit unit)
        {
            TimedLives.Remove(unit);
            SlayUnit(unit);
        }

        /// <summary>Whether an ability the unit has was marked a hero skill.</summary>
        public bool IsHeroAbility(unit unit, int abilityId)
        {
            return AbilityLevel(unit, abilityId) > 0 && heroAbilities.Contains(abilityId);
        }

        /// <summary>Records a hide-ability call.</summary>
        public void HideAbility(unit unit, int abilityId)
        {
            AbilityCalls.Add("hide " + abilityId);
        }

        /// <summary>Records a make-permanent call, and keeps the ability through a morph.</summary>
        public void MakeAbilityPermanent(unit unit, int abilityId)
        {
            AbilityCalls.Add("permanent " + abilityId);
            if (!permanentAbilities.TryGetValue(unit, out var permanent))
            {
                permanent = new HashSet<int>();
                permanentAbilities[unit] = permanent;
            }

            permanent.Add(abilityId);
        }

        /// <summary>Records a remove-ability call, takes the ability from those code gave the unit, and answers whether it had it. The game fires the end-cast of a spell being cast inside the call (seen 11-02); this world fires it for every ability the unit had, which Moonwell passes over unless a cast of that ability is running.</summary>
        public bool RemoveAbility(unit unit, int abilityId)
        {
            AbilityCalls.Add("remove " + abilityId);
            var had = AbilityLevel(unit, abilityId) > 0;
            SetAbilityLevel(unit, abilityId, 0);
            AddedTo(unit).Remove(abilityId);
            if (abilityId == CrowForm && crowFormed.Remove(unit))
            {
                floaters.Add(unit);
            }

            Dummies.AbilityRemoved(unit, abilityId);
            if (had && Listening.Contains(GameEvent.Spell))
            {
                Inbound.SpellEnded(unit, abilityId);
            }

            return had;
        }

        /// <summary>Morphs a unit as Metamorphosis or Bear Form does: it loses each ability code gave it that was not made permanent, whose level then reads 0 (seen 10-26), and each is recorded as "stripped".</summary>
        public void Morph(unit u)
        {
            var added = AddedTo(u);
            for (var i = added.Count - 1; i >= 0; i--)
            {
                if (!permanentAbilities.TryGetValue(u, out var permanent) || !permanent.Contains(added[i]))
                {
                    AbilityCalls.Add("stripped " + added[i]);
                    SetAbilityLevel(u, added[i], 0);
                    added.RemoveAt(i);
                }
            }
        }

        /// <summary>Whether a unit has an ability code gave it, not removed or stripped by a morph since.</summary>
        public bool HasAddedAbility(unit u, int abilityId)
        {
            return AddedTo(u).Contains(abilityId);
        }

        /// <summary>The abilities code gave a unit, in the order given.</summary>
        private List<int> AddedTo(unit u)
        {
            if (!addedAbilities.TryGetValue(u, out var added))
            {
                added = new List<int>();
                addedAbilities[u] = added;
            }

            return added;
        }

        /// <summary>The id of Crow Form (Amrf), whose adding and removing lets a ground unit show a fly height.</summary>
        public static readonly int CrowForm = ObjectTypes.FourCC("Amrf");

        /// <summary>Throws when CallsThrow is set: the runtime called the game about a unit or an item.</summary>
        private void Refuse()
        {
            if (CallsThrow)
            {
                throw new ExpectationFailed("the runtime called the game about a unit or an item");
            }
        }

        /// <summary>Counts a call on an item the world has removed.</summary>
        private void NoteGoneItem(item item)
        {
            if (ItemTypeId(item) == 0)
            {
                GoneCalls++;
            }
        }

        /// <summary>The charges of an item; 1 until set.</summary>
        public int ChargesOf(item item)
        {
            NoteGoneItem(item);
            if (vanishedItems.Contains(item))
            {
                return 0;
            }

            return itemCharges.TryGetValue(item, out var charges) ? charges : 1;
        }

        /// <summary>Sets the charges of an item and records "charges".</summary>
        public void SetCharges(item item, int charges)
        {
            NoteGoneItem(item);
            itemCharges[item] = charges;
            ItemCalls.Add("charges " + charges);
        }

        /// <summary>The player that owns an item; neutral passive until set, as a new item's is (seen 10-21), null once LoseItemOwner has been called for it.</summary>
        public player ItemOwnerOf(item item)
        {
            NoteGoneItem(item);
            var slot = itemSlots.TryGetValue(item, out var kept) ? kept : NeutralPassive;
            if (slot < 0)
            {
                return null!;
            }

            return PlayerAt(slot);
        }

        /// <summary>Makes the game give no owner for an item, as it may for an item nobody owns.</summary>
        public void LoseItemOwner(item item)
        {
            itemSlots[item] = -1;
        }

        /// <summary>Whether a unit has an item in a slot.</summary>
        public bool CarriesItem(unit unit, item item)
        {
            NoteGone(unit);
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether an item is in a unit's slots or bag; false once it is removed or vanished, as the game's IsItemOwned.</summary>
        public bool IsItemHeld(item item)
        {
            NoteGoneItem(item);
            if (ItemTypeId(item) == 0)
            {
                return false;
            }

            if (bagged.Contains(item) || dropping.Contains(item))
            {
                return true;
            }

            foreach (var slots in inventories.Values)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == item)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Moves an item to another, empty slot of the unit, as the game does when the player drags it; the game fires a drop for it (seen 10-15) while the unit still has the item.</summary>
        public void MoveItemSlot(unit unit, item item, int toSlot)
        {
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    slots[i] = null!;
                    slots[toSlot] = item;
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, item);
                    }

                    return;
                }
            }
        }

        /// <summary>Sets the player of an item and records "owner" with the slot and False, since the item's colour is left alone.</summary>
        public void SetItemOwner(item item, player player)
        {
            NoteGoneItem(item);
            itemSlots[item] = SlotOf(player);
            ItemCalls.Add("owner " + SlotOf(player) + " False");
        }

        /// <summary>The x of an item; 0 until placed.</summary>
        public float ItemX(item item)
        {
            NoteGoneItem(item);
            return !vanishedItems.Contains(item) && itemPlaces.TryGetValue(item, out var place) ? place[0] : 0.0f;
        }

        /// <summary>The y of an item; 0 until placed.</summary>
        public float ItemY(item item)
        {
            NoteGoneItem(item);
            return !vanishedItems.Contains(item) && itemPlaces.TryGetValue(item, out var place) ? place[1] : 0.0f;
        }

        /// <summary>The place (x, y) the game puts every moved item at when set, as it snaps a place on a tree or past the edge to a pathable spot (seen 10-21); null puts it where it was asked.</summary>
        public float[]? ItemLandsAt;

        /// <summary>Moves an item to the place given, or to <see cref="ItemLandsAt"/> when set, and records "move".</summary>
        public void MoveItem(item item, float x, float y)
        {
            NoteGoneItem(item);
            itemPlaces[item] = ItemLandsAt != null ? new float[] { ItemLandsAt[0], ItemLandsAt[1] } : new float[] { x, y };
            ItemCalls.Add("move");
        }

        /// <summary>The items in a unit's 3.0 bag or equipment slots: held, though the unit does not count them as carried.</summary>
        private readonly HashSet<item> bagged = new HashSet<item>();

        /// <summary>The items a dying unit is dropping: out of their slots, but still held while their drop events run (seen 11-02).</summary>
        private readonly HashSet<item> dropping = new HashSet<item>();

        /// <summary>Puts an item in a unit's bag, as the game does when a hero with six full slots gets one: a pick-up event, when it is listened for, while UnitHasItem reads false (seen 10-28 and 11-02).</summary>
        public void PutInBag(unit unit, item item)
        {
            bagged.Add(item);
            ListOf(bags, unit).Add(item);
            if (Listening.Contains(GameEvent.PickUp))
            {
                Inbound.PickedUp(unit, item);
            }
        }

        /// <summary>The items in a unit's 3.0 equipment slots, which are also in <see cref="bagged"/>, as both are held.</summary>
        private readonly HashSet<item> equipped = new HashSet<item>();

        /// <summary>Whether an item is in a unit's 3.0 bag, not its equipment slots.</summary>
        public bool IsInBag(item item)
        {
            return bagged.Contains(item) && !equipped.Contains(item);
        }

        /// <summary>Moves an item from a unit's slots into its bag, as the game does: a drop while the unit still has it, then a pick-up (seen 10-21).</summary>
        public void MoveToBag(unit unit, item item)
        {
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, item);
                    }

                    slots[i] = null!;
                    break;
                }
            }

            PutInBag(unit, item);
        }

        /// <summary>Equips an item from a unit's bag, as the game does: a drop, then a pick-up into the equipment slots (seen 10-21).</summary>
        public void Equip(unit unit, item item)
        {
            if (Listening.Contains(GameEvent.Drop))
            {
                Inbound.Dropped(unit, item);
            }

            ListOf(bags, unit).Remove(item);
            ListOf(gear, unit).Add(item);
            bagged.Add(item);
            equipped.Add(item);
            if (Listening.Contains(GameEvent.PickUp))
            {
                Inbound.PickedUp(unit, item);
            }
        }

        /// <summary>Each unit's bag and equipment slots, in slot order.</summary>
        private readonly Dictionary<unit, List<item>> bags = new Dictionary<unit, List<item>>(), gear = new Dictionary<unit, List<item>>();

        /// <summary>The bag size of each unit given one; 0 for the rest.</summary>
        public Dictionary<unit, int> BagSizes = new Dictionary<unit, int>();

        private static List<item> ListOf(Dictionary<unit, List<item>> lists, unit unit)
        {
            if (!lists.TryGetValue(unit, out var list))
            {
                list = new List<item>();
                lists[unit] = list;
            }

            return list;
        }

        /// <summary>Whether an item is in a unit's equipment slots.</summary>
        public bool IsEquipped(item item)
        {
            return equipped.Contains(item);
        }

        /// <summary>UnitEquipItem as the game does it: an item held in the six slots or the bag is dropped first, then picked up into the equipment slots (seen 11-08); the equipment type is not looked at.</summary>
        public void EquipItem(unit unit, item item)
        {
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, item);
                    }

                    slots[i] = null!;
                    ListOf(gear, unit).Add(item);
                    bagged.Add(item);
                    equipped.Add(item);
                    if (Listening.Contains(GameEvent.PickUp))
                    {
                        Inbound.PickedUp(unit, item);
                    }

                    FireEquip(unit, item);
                    return;
                }
            }

            if (bagged.Contains(item))
            {
                Equip(unit, item);
                FireEquip(unit, item);
                return;
            }

            ListOf(gear, unit).Add(item);
            bagged.Add(item);
            equipped.Add(item);
            if (Listening.Contains(GameEvent.PickUp))
            {
                Inbound.PickedUp(unit, item);
            }

            FireEquip(unit, item);
        }

        /// <summary>The equip event the game fires after a move into the equipment slots, by code or by hand (seen 11-08 and 11-09).</summary>
        private void FireEquip(unit unit, item item)
        {
            if (Listening.Contains(GameEvent.Equip))
            {
                Inbound.EquipEvent(unit, item);
            }
        }

        /// <summary>The player drags an item from the bag into the equipment slots in the 3.0 panel: only the equip event, no drop or pick-up (probe 11-09).</summary>
        public void EquipByHand(unit unit, item item)
        {
            ListOf(bags, unit).Remove(item);
            ListOf(gear, unit).Add(item);
            equipped.Add(item);
            FireEquip(unit, item);
        }

        /// <summary>The player drags an equipped item back into the bag in the 3.0 panel: only the unequip event, which names no item (probe 11-09).</summary>
        public void UnequipByHand(unit unit, item item)
        {
            equipped.Remove(item);
            ListOf(gear, unit).Remove(item);
            ListOf(bags, unit).Add(item);
            if (Listening.Contains(GameEvent.Equip))
            {
                Inbound.UnequipEvent(unit);
            }
        }

        /// <summary>UnitUnequipItem as the game does it: a drop, then a pick-up into the bag (seen 11-08).</summary>
        public void UnequipItem(unit unit, item item)
        {
            if (!equipped.Contains(item))
            {
                return;
            }

            if (Listening.Contains(GameEvent.Drop))
            {
                Inbound.Dropped(unit, item);
            }

            equipped.Remove(item);
            ListOf(gear, unit).Remove(item);
            ListOf(bags, unit).Add(item);
            if (Listening.Contains(GameEvent.PickUp))
            {
                Inbound.PickedUp(unit, item);
            }

            if (Listening.Contains(GameEvent.Equip))
            {
                Inbound.UnequipEvent(unit);
            }
        }

        /// <summary>The item in an equipment slot, in the order the unit equipped them.</summary>
        public item? ItemInEquipmentSlot(unit unit, int slot)
        {
            var list = ListOf(gear, unit);
            return slot < list.Count ? list[slot] : null;
        }

        /// <summary>The item in a bag slot, in the order it went in.</summary>
        public item? ItemInBagSlot(unit unit, int slot)
        {
            var list = ListOf(bags, unit);
            return slot < list.Count ? list[slot] : null;
        }

        /// <summary>A unit's bag size: BagSizes, else 0.</summary>
        public int BagSizeOf(unit unit)
        {
            return BagSizes.TryGetValue(unit, out var size) ? size : 0;
        }

        /// <summary>Puts an item in a slot of a unit without the game telling anyone, as when the item was there before the runtime came.</summary>
        public void PutInSlot(unit unit, int slot, item item)
        {
            SlotsOf(unit)[slot] = item;
        }

        /// <summary>Counts a call on a unit the world or the runtime has removed.</summary>
        private void NoteGone(unit unit)
        {
            if (UnitTypeId(unit) == 0 || RemovedUnits.Contains(unit))
            {
                GoneCalls++;
            }
        }

        /// <summary>A value kept for a unit; 0 when none was set.</summary>
        private float Kept(Dictionary<unit, float> values, unit unit)
        {
            NoteGone(unit);
            if (vanishedUnits.Contains(unit))
            {
                return 0.0f;
            }

            return values.TryGetValue(unit, out var value) ? value : 0.0f;
        }

        /// <summary>The life of a unit; 0 until set.</summary>
        public float LifeOf(unit unit)
        {
            return Kept(lives, unit);
        }

        /// <summary>Sets the life of a unit; above the most life, once one is set, it is clamped to it (seen 10-21); 0.405 or less kills it as KillUnit does (seen 10-20).</summary>
        public void SetLife(unit unit, float life)
        {
            NoteGone(unit);
            if (life <= 0.405f)
            {
                Die(unit, null);
                return;
            }

            lives[unit] = maxLives.TryGetValue(unit, out var most) && life > most ? most : life;
        }

        /// <summary>The most life of a unit; 0 until set.</summary>
        public float MaxLifeOf(unit unit)
        {
            return Kept(maxLives, unit);
        }

        /// <summary>Sets the most life of a unit, as given.</summary>
        public void SetMaxLife(unit unit, float life)
        {
            NoteGone(unit);
            maxLives[unit] = (int)life;
            if (lives.TryGetValue(unit, out var kept) && kept > (int)life)
            {
                lives[unit] = (int)life;
            }
        }

        /// <summary>The mana of a unit; 0 until set.</summary>
        public float ManaOf(unit unit)
        {
            return Kept(manas, unit);
        }

        /// <summary>Sets the mana of a unit; above the most mana, once one is set, it is clamped to it (seen 10-21).</summary>
        public void SetMana(unit unit, float mana)
        {
            NoteGone(unit);
            manas[unit] = maxManas.TryGetValue(unit, out var most) && mana > most ? most : mana;
        }

        /// <summary>The most mana of a unit; 0 until set.</summary>
        public float MaxManaOf(unit unit)
        {
            return Kept(maxManas, unit);
        }

        /// <summary>Sets the most mana of a unit, as given.</summary>
        public void SetMaxMana(unit unit, float mana)
        {
            NoteGone(unit);
            maxManas[unit] = (int)mana;
            if (manas.TryGetValue(unit, out var kept) && kept > (int)mana)
            {
                manas[unit] = (int)mana;
            }
        }

        /// <summary>The x of a unit; 0 until set.</summary>
        public float UnitX(unit unit)
        {
            return Kept(unitXs, unit);
        }

        /// <summary>The y of a unit; 0 until set.</summary>
        public float UnitY(unit unit)
        {
            return Kept(unitYs, unit);
        }

        /// <summary>Sets the player of a unit and records "owner" with the slot and True, since the unit's colour changes too; delivers the change-owner event inside the call when listened for, as Charm's would come. First it ends the unit's channel, as the game stops a unit it gives away, before the change (seen 11-03).</summary>
        public void SetOwner(unit unit, player player)
        {
            NoteGone(unit);
            Dummies.Stopped(unit);
            unitSlots[unit] = SlotOf(player);
            UnitCalls.Add("owner " + SlotOf(player) + " True");
            if (Listening.Contains(GameEvent.OwnerChange))
            {
                Inbound.OwnerChanged(unit);
            }
        }

        /// <summary>Puts a unit at the place, and records a "move".</summary>
        public void MoveUnit(unit unit, float x, float y)
        {
            NoteGone(unit);
            UnitCalls.Add("move");
            unitXs[unit] = x;
            unitYs[unit] = y;
        }

        /// <summary>Puts a unit at the place, and records a "place".</summary>
        public void PlaceUnit(unit unit, float x, float y)
        {
            NoteGone(unit);
            UnitCalls.Add("place");
            unitXs[unit] = x;
            unitYs[unit] = y;
        }

        /// <summary>The facing of a unit; 0 until set.</summary>
        public float FacingOf(unit unit)
        {
            return Kept(facings, unit);
        }

        /// <summary>Records a "turn" and the facing asked for in TurnTargets; the unit's facing stays, since a turn takes time.</summary>
        public void TurnUnit(unit unit, float degrees)
        {
            NoteGone(unit);
            UnitCalls.Add("turn");
            TurnTargets[unit] = degrees;
        }

        /// <summary>Sets the facing of a unit, and records a "face-now".</summary>
        public void FaceUnitNow(unit unit, float degrees)
        {
            NoteGone(unit);
            UnitCalls.Add("face-now");
            facings[unit] = degrees;
        }

        /// <summary>Sets the level the world gives a unit.</summary>
        public void SetLevel(unit unit, int level)
        {
            levels[unit] = level;
        }

        /// <summary>The level of a unit; 1 until set.</summary>
        public int LevelOf(unit unit)
        {
            NoteGone(unit);
            if (vanishedUnits.Contains(unit))
            {
                return 0;
            }

            return levels.TryGetValue(unit, out var level) ? level : 1;
        }

        /// <summary>Whether a unit has not been killed and is not gone: one given to RemoveUnit reads alive for the rest of its frame, and not alive from the next (seen 11-06).</summary>
        public bool IsAlive(unit unit)
        {
            NoteGone(unit);
            return !dead.Contains(unit) && !vanishedUnits.Contains(unit);
        }

        /// <summary>Kills a unit as the game does: the unit is dead with life 0, and when deaths are listened for, its death is delivered inside the call, with no killer; killing a dead unit does nothing.</summary>
        public void SlayUnit(unit unit)
        {
            NoteGone(unit);
            KillCalls++;
            Die(unit, null);
        }

        /// <summary>Kills a unit by a hit of the damage package's fake, with the source as the killer, as a killing hit's death comes inside its call (seen 10-20).</summary>
        public void KillBy(unit unit, unit? killer)
        {
            Die(unit, killer);
        }

        /// <summary>The death itself, for KillUnit, for a life set to 0.405 or less and for damage: life 0, the death inside the call when deaths are listened for, and nothing for a unit already dead (seen 10-21).</summary>
        private void Die(unit unit, unit? killer)
        {
            if (!dead.Add(unit))
            {
                // A second KillUnit on the dead unit fires nothing (seen 10-21).
                return;
            }

            lives[unit] = 0.0f;
            // Its channel's end-cast comes inside the kill (seen 11-06); before the death event is this fake's guess.
            Dummies.Stopped(unit);
            DropAll(unit);
            GameBuffs.TakeAll(unit);
            if (Listening.Contains(GameEvent.Death))
            {
                Inbound.Died(unit, killer);
            }
        }

        /// <summary>Drops a dying unit's items as the game drops a footman's: before the death event, last slot first, each out of its slot but still held when its drop event fires, while the unit is already dead, then at the unit's place (seen 11-02). A hero keeps its items, with no event; the game decides by the inventory ability, so a unit given a hero's inventory keeps them too.</summary>
        private void DropAll(unit unit)
        {
            if (heroes.TryGetValue(unit, out var isHero) && isHero)
            {
                return;
            }

            var slots = SlotsOf(unit);
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var carried = slots[i];
                if (carried != null)
                {
                    slots[i] = null!;
                    dropping.Add(carried);
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, carried);
                    }

                    dropping.Remove(carried);
                    LandAt(unit, carried);
                }
            }
        }

        /// <summary>The calls that changed a hero, in turn, as a word and its values: "experience 500 True", "add-experience -200 True", "level 5 True", "strip 3", "strength 30 True", "skill-points 3", "revive 100 200 True".</summary>
        public List<string> HeroCalls = new List<string>();

        /// <summary>The experience of each hero, 0 until set.</summary>
        private readonly Dictionary<unit, int> experiences = new Dictionary<unit, int>();

        /// <summary>The base strength, agility and intelligence of each hero, 0 until set.</summary>
        private readonly Dictionary<unit, int[]> stats = new Dictionary<unit, int[]>();

        /// <summary>The bonus to strength, agility and intelligence of each hero, 0 until set.</summary>
        private readonly Dictionary<unit, int[]> statBonuses = new Dictionary<unit, int[]>();

        /// <summary>The skill points of each hero, 0 until set.</summary>
        private readonly Dictionary<unit, int> skillPoints = new Dictionary<unit, int>();

        /// <summary>Sets the base strength, agility and intelligence the world gives a hero.</summary>
        public void SetStats(unit hero, int strength, int agility, int intelligence)
        {
            stats[hero] = new int[] { strength, agility, intelligence };
        }

        /// <summary>Sets the bonus to strength, agility and intelligence the world gives a hero.</summary>
        public void SetStatBonuses(unit hero, int strength, int agility, int intelligence)
        {
            statBonuses[hero] = new int[] { strength, agility, intelligence };
        }

        /// <summary>Sets the skill points the world gives a hero.</summary>
        public void SetSkillPoints(unit hero, int points)
        {
            skillPoints[hero] = points;
        }

        /// <summary>The experience of a hero; 0 until set.</summary>
        public int ExperienceOf(unit hero)
        {
            NoteGone(hero);
            return !vanishedUnits.Contains(hero) && experiences.TryGetValue(hero, out var experience) ? experience : 0;
        }

        /// <summary>Sets the experience of a hero, and records an "experience". It only raises, as SetHeroXP (seen 10-21): a lower value changes nothing. A higher one gives the levels that come with it (seen 11-02).</summary>
        public void SetExperience(unit hero, int experience, bool showEffect)
        {
            NoteGone(hero);
            HeroCalls.Add("experience " + experience + " " + Flag(showEffect));
            if (experience > ExperienceOf(hero))
            {
                GiveExperience(hero, experience);
            }
        }

        /// <summary>Adds experience to a hero, or takes it with a negative amount, and records an "add-experience": AddHeroXP with -400 lowers the experience but not the level (seen 10-21), and more gives the levels that come with it (seen 11-02); it stops at 0 (assumed). At the top level it changes nothing (seen 11-02; for a negative amount, assumed).</summary>
        public void AddExperience(unit hero, int experience, bool showEffect)
        {
            NoteGone(hero);
            HeroCalls.Add("add-experience " + experience + " " + Flag(showEffect));
            if (LevelOf(hero) >= HeroTable.Experience.Length)
            {
                return;
            }

            var now = ExperienceOf(hero) + experience;
            GiveExperience(hero, now < 0 ? 0 : now);
        }

        /// <summary>Sets a hero's experience, which stops at the top level's (seen 10-21 and 11-02), and raises the hero to the level of the experience table it reaches; a lower experience keeps the level.</summary>
        private void GiveExperience(unit hero, int experience)
        {
            var table = HeroTable.Experience;
            var top = table[table.Length - 1];
            var now = experience > top ? top : experience;
            experiences[hero] = now;
            var level = 1;
            while (level < table.Length && now >= table[level])
            {
                level++;
            }

            if (level > LevelOf(hero))
            {
                RaiseLevel(hero, level);
            }
        }

        /// <summary>Gives a hero the level and that level's experience, and records a "level". It only raises and stops at the top level, as SetHeroLevel (seen 10-21 and 11-02): a lower level changes nothing.</summary>
        public void LevelHero(unit hero, int level, bool showEffect)
        {
            NoteGone(hero);
            HeroCalls.Add("level " + level + " " + Flag(showEffect));
            var table = HeroTable.Experience;
            var reached = level < table.Length ? level : table.Length;
            if (reached > LevelOf(hero))
            {
                experiences[hero] = table[reached - 1];
                RaiseLevel(hero, reached);
            }
        }

        /// <summary>Raises a hero to a level and fires one level event inside the call, as the game does for a jump of several levels (seen 11-02), when the runtime listens for it.</summary>
        private void RaiseLevel(unit hero, int level)
        {
            levels[hero] = level;
            if (Listening.Contains(GameEvent.HeroLevel))
            {
                LevelEvents++;
                Inbound.HeroLevel(hero);
            }
        }

        /// <summary>Has a hero learn a skill, as SelectHeroSkill does: with a skill point, the ability's level goes up by one and the skill event fires inside the call, before the point is spent (seen 11-02); with none, nothing happens. Answers whether it learned.</summary>
        public bool LearnSkill(unit hero, int abilityId)
        {
            var points = SkillPointsOf(hero);
            if (points <= 0)
            {
                return false;
            }

            SetAbilityLevel(hero, abilityId, AbilityLevel(hero, abilityId) + 1);
            if (Listening.Contains(GameEvent.HeroSkill))
            {
                Inbound.HeroSkill(hero, abilityId);
            }

            skillPoints[hero] = points - 1;
            return true;
        }

        /// <summary>Takes levels from a hero, down to 1, and records a "strip": UnitStripHeroLevel lowers the level and sets the experience to the lower level's (seen 11-02). What it does to the skill points is not modelled.</summary>
        public void StripLevels(unit hero, int count)
        {
            NoteGone(hero);
            HeroCalls.Add("strip " + count);
            var now = LevelOf(hero) - count;
            var level = now < 1 ? 1 : now;
            levels[hero] = level;
            var table = HeroTable.Experience;
            experiences[hero] = table[(level < table.Length ? level : table.Length) - 1];
        }

        /// <summary>A stat of a hero, 0 until set: which is 0 for strength, 1 for agility and 2 for intelligence.</summary>
        private int StatOf(unit hero, int which, bool withBonus)
        {
            NoteGone(hero);
            if (vanishedUnits.Contains(hero))
            {
                return 0;
            }

            var value = stats.TryGetValue(hero, out var set) ? set[which] : 0;
            if (withBonus && statBonuses.TryGetValue(hero, out var bonus))
            {
                value += bonus[which];
            }

            return value;
        }

        /// <summary>Sets a base stat of a hero, and records its name with the value and that it is for good.</summary>
        private void SetStat(unit hero, int which, string name, int value)
        {
            NoteGone(hero);
            HeroCalls.Add(name + " " + value + " " + Flag(true));
            if (!stats.TryGetValue(hero, out var set))
            {
                set = new int[3];
                stats[hero] = set;
            }

            set[which] = value;
        }

        /// <summary>The strength of a hero.</summary>
        public int StrengthOf(unit hero, bool withBonus)
        {
            return StatOf(hero, 0, withBonus);
        }

        /// <summary>The agility of a hero.</summary>
        public int AgilityOf(unit hero, bool withBonus)
        {
            return StatOf(hero, 1, withBonus);
        }

        /// <summary>The intelligence of a hero.</summary>
        public int IntelligenceOf(unit hero, bool withBonus)
        {
            return StatOf(hero, 2, withBonus);
        }

        /// <summary>Sets the base strength of a hero.</summary>
        public void SetStrength(unit hero, int strength)
        {
            SetStat(hero, 0, "strength", strength);
        }

        /// <summary>Sets the base agility of a hero.</summary>
        public void SetAgility(unit hero, int agility)
        {
            SetStat(hero, 1, "agility", agility);
        }

        /// <summary>Sets the base intelligence of a hero.</summary>
        public void SetIntelligence(unit hero, int intelligence)
        {
            SetStat(hero, 2, "intelligence", intelligence);
        }

        /// <summary>The skill points of a hero; 0 until set.</summary>
        public int SkillPointsOf(unit hero)
        {
            NoteGone(hero);
            return !vanishedUnits.Contains(hero) && skillPoints.TryGetValue(hero, out var points) ? points : 0;
        }

        /// <summary>Adds skill points to a hero and records "skill-points"; true unless the points would go below 0, which leaves them as they were.</summary>
        public bool AddSkillPoints(unit hero, int delta)
        {
            NoteGone(hero);
            HeroCalls.Add("skill-points " + delta);
            var now = SkillPointsOf(hero) + delta;
            if (now < 0)
            {
                return false;
            }

            skillPoints[hero] = now;
            return true;
        }

        /// <summary>Revives a dead hero at the place, with full life but its mana as it was (seen 10-21), and records a "revive"; false for a hero that is not dead.</summary>
        public bool ReviveAt(unit hero, float x, float y, bool showEffect)
        {
            NoteGone(hero);
            HeroCalls.Add("revive " + (int)x + " " + (int)y + " " + Flag(showEffect));
            if (!dead.Remove(hero))
            {
                return false;
            }

            unitXs[hero] = x;
            unitYs[hero] = y;
            lives[hero] = maxLives.TryGetValue(hero, out var most) ? most : 0.0f;
            return true;
        }

        /// <summary>A flag as the hero calls record it: "True" or "False", the same on .NET and in Lua.</summary>
        private static string Flag(bool value)
        {
            return value ? "True" : "False";
        }

        /// <summary>Whether a unit is paused.</summary>
        public bool IsPausedUnit(unit unit)
        {
            NoteGone(unit);
            return !vanishedUnits.Contains(unit) && PausedUnits.Contains(unit);
        }

        /// <summary>Pauses or unpauses a unit.</summary>
        public void SetPaused(unit unit, bool paused)
        {
            NoteGone(unit);
            PausedUnits.Remove(unit);
            if (paused)
            {
                PausedUnits.Add(unit);
            }
        }

        /// <summary>The scale of a unit; 1 until set.</summary>
        public float ScaleOf(unit unit)
        {
            NoteGone(unit);
            if (vanishedUnits.Contains(unit))
            {
                return 0.0f;
            }

            return scales.TryGetValue(unit, out var scale) ? scale : 1.0f;
        }

        /// <summary>Sets the scale of a unit, and counts the call.</summary>
        public void ScaleUnit(unit unit, float scale)
        {
            NoteGone(unit);
            ScaleCalls++;
            scales[unit] = scale;
        }

        /// <summary>The name of a unit; "" until set.</summary>
        public string UnitNameOf(unit unit)
        {
            NoteGone(unit);
            return !vanishedUnits.Contains(unit) && unitNames.TryGetValue(unit, out var name) ? name : "";
        }

        /// <summary>Sets the name of a unit, and records a "rename".</summary>
        public void RenameUnit(unit unit, string name)
        {
            NoteGone(unit);
            UnitCalls.Add("rename");
            unitNames[unit] = name;
        }

        /// <summary>The icon of a unit type; "" until set with <see cref="SetIcon"/>.</summary>
        public string IconOf(int typeId)
        {
            return unitIcons.TryGetValue(typeId, out var icon) ? icon : "";
        }

        /// <summary>Sets the icon the game gives for a unit type.</summary>
        public void SetIcon(int typeId, string icon)
        {
            unitIcons[typeId] = icon;
        }

        /// <summary>The proper name of a hero; "" until set.</summary>
        public string ProperNameOf(unit hero)
        {
            NoteGone(hero);
            return !vanishedUnits.Contains(hero) && properNames.TryGetValue(hero, out var name) ? name : "";
        }

        /// <summary>Sets the proper name of a hero, and records a "proper-name".</summary>
        public void SetProperName(unit hero, string name)
        {
            NoteGone(hero);
            HeroCalls.Add("proper-name " + name);
            properNames[hero] = name;
        }

        /// <summary>Keeps the four values a unit was tinted with.</summary>
        public void TintUnit(unit unit, int red, int green, int blue, int alpha)
        {
            NoteGone(unit);
            Tints[unit] = new int[] { red, green, blue, alpha };
        }

        /// <summary>Keeps the player colour a unit was given.</summary>
        public void SetUnitTeamColor(unit unit, int color)
        {
            NoteGone(unit);
            TeamColors[unit] = color;
        }

        /// <summary>Records an animation played by name, or queued.</summary>
        public void AnimateUnit(unit unit, string name, bool queued)
        {
            Animated(unit, (queued ? "queue " : "play ") + name);
        }

        /// <summary>Records an animation played by index.</summary>
        public void AnimateUnitByIndex(unit unit, int index)
        {
            Animated(unit, "index " + index);
        }

        /// <summary>Records an animation speed.</summary>
        public void SetUnitAnimationSpeed(unit unit, float speed)
        {
            Animated(unit, "speed " + speed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Records a tag added or removed.</summary>
        public void TagUnitAnimation(unit unit, string tag, bool add)
        {
            Animated(unit, (add ? "tag " : "untag ") + tag);
        }

        private void Animated(unit unit, string what)
        {
            NoteGone(unit);
            if (!UnitAnimations.TryGetValue(unit, out var asked))
            {
                asked = new List<string>();
                UnitAnimations[unit] = asked;
            }

            asked.Add(what);
        }

        /// <summary>Hides a unit (into HiddenUnits) or shows it.</summary>
        public void SetUnitShown(unit unit, bool shown)
        {
            HiddenUnits.Remove(unit);
            if (!shown)
            {
                HiddenUnits.Add(unit);
            }
        }

        /// <summary>The units put away for a pool, in order.</summary>
        public List<unit> PutAwayUnits = new List<unit>();

        /// <summary>Puts a unit away for a pool: recorded, given to the keeper and hidden.</summary>
        public void PutAway(unit unit, player keeper)
        {
            PutAwayUnits.Add(unit);
            ((IWorld)this).SetOwner(unit, keeper);
            SetUnitShown(unit, false);
        }

        /// <summary>Whether a unit is in HiddenUnits.</summary>
        public bool IsHidden(unit unit)
        {
            return HiddenUnits.Contains(unit);
        }

        /// <summary>Records a selection change on the local machine only; the game's events follow later, which a test fires with PlayerSelects.</summary>
        public void SelectFor(player player, unit unit, bool selected)
        {
            if (IsLocal(player))
            {
                SelectionCalls.Add(SlotOf(player) + (selected ? " select" : " deselect"));
            }
        }

        /// <summary>Records a clear on the local machine only.</summary>
        public void ClearSelectionFor(player player)
        {
            if (IsLocal(player))
            {
                SelectionCalls.Add(SlotOf(player) + " clear");
            }
        }

        /// <summary>The game's selection event: a player in a slot selects or deselects a unit; delivered when the runtime listens for selections.</summary>
        public void PlayerSelects(unit unit, int slot, bool selected = true)
        {
            if (Listening.Contains(GameEvent.Selection))
            {
                Inbound.Selection(unit, PlayerAt(slot), selected);
            }
        }

        /// <summary>The fly height of a unit; 0 until set.</summary>
        public float FlyHeightOf(unit unit)
        {
            return Kept(flyHeights, unit);
        }

        /// <summary>Whether the unit's type flies.</summary>
        public bool CanFly(unit unit)
        {
            NoteGone(unit);
            return flyers.Contains(unit);
        }

        /// <summary>Sets the fly height of a unit.</summary>
        public void SetFlyHeight(unit unit, float height)
        {
            NoteGone(unit);
            flyHeights[unit] = height;
        }

        /// <summary>Sets the number of inventory slots a unit has.</summary>
        public void SetInventorySize(unit unit, int size)
        {
            inventorySizes[unit] = size;
        }

        /// <summary>The slots of a unit's inventory, made on first use.</summary>
        private item[] SlotsOf(unit unit)
        {
            if (!inventories.TryGetValue(unit, out var slots))
            {
                slots = new item[InventorySizeOf(unit)];
                inventories[unit] = slots;
            }

            return slots;
        }

        /// <summary>Records an order and gives the answer set in OrderAnswer, or a spell's (<see cref="FakeDummies"/>); when the runtime listens for orders, delivers the order event inside the call, as the game does (seen 11-03), then a spell's effect; a stop ends a channel.</summary>
        public bool OrderUnit(unit unit, string order)
        {
            NoteGone(unit);
            Orders.Add("immediate " + order);
            var spell = Dummies.Answer(unit, order, SpellKind.None, null);
            var answer = spell ?? OrderAnswer;
            if (answer)
            {
                DeliverOrder(unit, OrderIdOf(order), OrderKind.None, 0f, 0f, null, null);
            }

            if (spell == true)
            {
                Dummies.TakeEffect(unit, order, null, 0f, 0f);
            }

            if (order == "stop")
            {
                Dummies.Stopped(unit);
                if (casting.TryGetValue(unit, out var started))
                {
                    casting.Remove(unit);
                    if (Listening.Contains(GameEvent.Spell))
                    {
                        Inbound.SpellEnded(unit, started);
                    }
                }
            }

            return answer;
        }

        /// <summary>Records an order to a place and gives the answer set in OrderAnswer, or a spell's; delivers the order event inside the call when listened for, then a spell's effect.</summary>
        public bool OrderUnitAt(unit unit, string order, float x, float y)
        {
            NoteGone(unit);
            Orders.Add("point " + order);
            OrderPoints.Add(x);
            OrderPoints.Add(y);
            var spell = Dummies.Answer(unit, order, SpellKind.Point, null);
            // A channelling unit refuses a move order and channels on (seen 11-06, on Moonwell's dummy, which cannot move).
            var answer = spell ?? (OrderAnswer && !(order == "move" && Dummies.Channels(unit)));
            if (answer)
            {
                DeliverOrder(unit, OrderIdOf(order), OrderKind.Place, x, y, null, null);
            }

            if (spell == true)
            {
                Dummies.TakeEffect(unit, order, null, x, y);
            }

            return answer;
        }

        /// <summary>Records an order on a widget and gives the answer set in OrderAnswer, or a spell's; delivers the order event inside the call when listened for, then a spell's effect at the target and its place.</summary>
        public bool OrderUnitOn(unit unit, string order, widget target)
        {
            NoteGone(unit);
            Orders.Add("target " + order);
            OrderTargets.Add(target);
            var spell = Dummies.Answer(unit, order, SpellKind.Target, target as unit);
            var answer = spell ?? OrderAnswer;
            if (answer)
            {
                DeliverOrder(unit, OrderIdOf(order), OrderKind.Target, 0f, 0f, target as unit, target as item);
            }

            var aimed = target as unit;
            if (spell == true)
            {
                Dummies.TakeEffect(unit, order, aimed, aimed == null ? 0f : UnitX(aimed), aimed == null ? 0f : UnitY(aimed));
            }

            return answer;
        }

        /// <summary>Records an item use as "useitem" and gives the answer set in OrderAnswer; see UsedItem.</summary>
        public bool UseItem(unit unit, item item)
        {
            return UsedItem(unit, item, "useitem");
        }

        /// <summary>Records an item use at a place as "useitem point", its x and y in OrderPoints, and gives the answer set in OrderAnswer; see UsedItem.</summary>
        public bool UseItemAt(unit unit, item item, float x, float y)
        {
            OrderPoints.Add(x);
            OrderPoints.Add(y);
            return UsedItem(unit, item, "useitem point");
        }

        /// <summary>Records an item use on a widget as "useitem target", the widget in OrderTargets, and gives the answer set in OrderAnswer; see UsedItem.</summary>
        public bool UseItemOn(unit unit, item item, widget target)
        {
            OrderTargets.Add(target);
            return UsedItem(unit, item, "useitem target");
        }

        /// <summary>Records an item use in Orders and gives OrderAnswer. A use taken makes the unit's current order its slot's, 852008 for the first (seen 11-03) and, assumed, one more for each slot after, with no order event; an item outside its inventory leaves the order.</summary>
        private bool UsedItem(unit unit, item item, string call)
        {
            NoteGone(unit);
            Orders.Add(call);
            if (OrderAnswer)
            {
                var slots = SlotsOf(unit);
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == item)
                    {
                        currentOrders[unit] = 852008 + i;
                    }
                }
            }

            return OrderAnswer;
        }

        /// <summary>The ids of the orders the cases use, by the game's string (seen 11-03). An instance field: past the class's 200th member, CSharp.lua reads a private static field by its bare name, which the game does not have.</summary>
        private readonly Dictionary<string, int> orderIds = new Dictionary<string, int>
        {
            { "smart", 851971 }, { "stop", 851972 }, { "attack", 851983 }, { "attackground", 851984 }, { "move", 851986 },
            { "patrol", 851990 }, { "holdposition", 851993 }, { "defend", 852055 }, { "undefend", 852056 },
            { "holybolt", 852092 }, { "immolation", 852177 }, { "unimmolation", 852178 },
        };

        /// <summary>The order each unit carries out now, by id; 0 when idle.</summary>
        private readonly Dictionary<unit, int> currentOrders = new Dictionary<unit, int>();

        /// <summary>How many orders each unit has queued after the one it carries out, as a case set them; none until set.</summary>
        private readonly Dictionary<unit, int> queued = new Dictionary<unit, int>();

        /// <summary>The id of an order string the cases use; 0 for one the fake does not know.</summary>
        private int OrderIdOf(string order)
        {
            return orderIds.TryGetValue(order, out var id) ? id : 0;
        }

        /// <summary>The order a unit carries out now, as the game's GetUnitCurrentOrder reads it.</summary>
        public int CurrentOrderOf(unit unit)
        {
            return currentOrders.TryGetValue(unit, out var id) ? id : 0;
        }

        /// <summary>The game's string of an order id; "" for one the fake does not know.</summary>
        public string OrderName(int id)
        {
            foreach (var pair in orderIds)
            {
                if (pair.Value == id)
                {
                    return pair.Key;
                }
            }

            return "";
        }

        /// <summary>Sets the order a unit carries out now, with no event, as the game's own reading (a patrol under way reads 851991).</summary>
        public void SetCurrentOrder(unit unit, int id)
        {
            currentOrders[unit] = id;
        }

        /// <summary>Sets the orders a unit has queued after the one it starts next, as a Shift-queue leaves them.</summary>
        public void SetQueued(unit unit, int count)
        {
            queued[unit] = count;
        }

        /// <summary>Gives a unit an order as a player does, to a place: the event, when listened for, and no call recorded.</summary>
        public void PlayerOrders(unit unit, string order, float x, float y)
        {
            DeliverOrder(unit, OrderIdOf(order), OrderKind.Place, x, y, null, null);
        }

        /// <summary>Gives a unit an order as a player does, on a unit or an item.</summary>
        public void PlayerOrdersOn(unit unit, string order, widget target)
        {
            DeliverOrder(unit, OrderIdOf(order), OrderKind.Target, 0f, 0f, target as unit, target as item);
        }

        /// <summary>Gives a unit an order as a player does, with no place and no target.</summary>
        public void PlayerOrdersNone(unit unit, string order)
        {
            DeliverOrder(unit, OrderIdOf(order), OrderKind.None, 0f, 0f, null, null);
        }

        /// <summary>Fires an order event of any id, as the game does for its own orders.</summary>
        public void PlayerOrdersId(unit unit, int id, OrderKind kind)
        {
            DeliverOrder(unit, id, kind, 0f, 0f, null, null);
        }

        /// <summary>Makes an order the unit's current one and, when the runtime listens for orders, hands its event to Inbound with the queue the case set; a stop leaves the unit idle after its event.</summary>
        private void DeliverOrder(unit unit, int id, OrderKind kind, float x, float y, unit? targetUnit, item? targetItem)
        {
            currentOrders[unit] = id;
            var queue = 1 + (queued.TryGetValue(unit, out var count) ? count : 0);
            queued.Remove(unit);
            if (Listening.Contains(GameEvent.Order))
            {
                Inbound.Ordered(unit, id, kind, x, y, targetUnit, targetItem, null, queue);
            }

            if (id == 851972 && currentOrders.TryGetValue(unit, out var now) && now == 851972)
            {
                currentOrders[unit] = 0;
            }
        }

        /// <summary>The number of inventory slots of a unit; 6 until set.</summary>
        public int InventorySizeOf(unit unit)
        {
            NoteGone(unit);
            if (vanishedUnits.Contains(unit))
            {
                return 0;
            }

            return inventorySizes.TryGetValue(unit, out var size) ? size : 6;
        }

        /// <summary>The item in a slot; null when empty or out of range.</summary>
        public item ItemInSlot(unit unit, int slot)
        {
            NoteGone(unit);
            ItemSlotReads++;
            var slots = SlotsOf(unit);
            return slot >= 0 && slot < slots.Length ? slots[slot] : null!;
        }

        /// <summary>Puts an item in the first free slot, and as the game does, delivers the pick-up when it is listened for; false, with no event, when no slot is free. An item the unit already holds keeps its slot and fires a drop and then a pick-up (seen 10-21). An item another unit holds leaves that unit's slots first, with a drop while it still has the item, and reads that unit's place from then on (seen 10-31).</summary>
        public bool GiveItem(unit unit, item item)
        {
            NoteGone(unit);
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    InventoryCalls.Add("give");
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, item);
                    }

                    if (Listening.Contains(GameEvent.PickUp))
                    {
                        Inbound.PickedUp(unit, item);
                    }

                    return true;
                }
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    InventoryCalls.Add("give");
                    TakeFromHolder(item);
                    // A drop reaction may have given the item elsewhere or filled the slot: an item stays in one slot.
                    if (InSlots(item) || slots[i] != null)
                    {
                        return false;
                    }

                    slots[i] = item;
                    if (Listening.Contains(GameEvent.PickUp))
                    {
                        Inbound.PickedUp(unit, item);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether an item is in some unit's slots.</summary>
        private bool InSlots(item item)
        {
            foreach (var slots in inventories.Values)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == item)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Takes an item out of the slots of the unit that holds it, if one does, with the drop event first when it is listened for.</summary>
        private void TakeFromHolder(item item)
        {
            foreach (var holder in inventories)
            {
                var slots = holder.Value;
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == item)
                    {
                        if (Listening.Contains(GameEvent.Drop))
                        {
                            Inbound.Dropped(holder.Key, item);
                        }

                        if (slots[i] == item)
                        {
                            slots[i] = null!;
                        }

                        LandAt(holder.Key, item);
                        return;
                    }
                }
            }
        }

        /// <summary>Moves an item the unit holds into a slot, swapping it with the item there, and records "slot" with the slot; as the game (seen 11-02), it fires no event, answers true onto the item's own slot, and false for an item the unit does not hold or a slot it does not have.</summary>
        public bool MoveItemToSlot(unit unit, item item, int slot)
        {
            NoteGone(unit);
            var slots = SlotsOf(unit);
            if (slot < 0 || slot >= slots.Length)
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    InventoryCalls.Add("slot " + slot);
                    slots[i] = slots[slot];
                    slots[slot] = item;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Takes an item out of its slot and puts it at the unit's place; the game's drop event, when it is listened for, comes while the unit still has the item in the slot (seen 10-21).</summary>
        public void DropItem(unit unit, item item)
        {
            NoteGone(unit);
            var slots = SlotsOf(unit);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == item)
                {
                    InventoryCalls.Add("drop");
                    if (Listening.Contains(GameEvent.Drop))
                    {
                        Inbound.Dropped(unit, item);
                    }

                    if (slots[i] == item)
                    {
                        slots[i] = null!;
                    }

                    LandAt(unit, item);
                    return;
                }
            }
        }

        /// <summary>The effects the runtime made, in the order made.</summary>
        public List<FakeEffect> Effects = new List<FakeEffect>();

        /// <summary>The ground's height at a place; flat at 0 unless a case sets it.</summary>
        public System.Func<float, float, float> Ground = (x, y) => 0.0f;

        /// <summary>How many times the runtime read the ground's height.</summary>
        public int GroundReads;

        /// <summary>The fake behind each effect handle.</summary>
        private readonly Dictionary<effect, FakeEffect> effects = new Dictionary<effect, FakeEffect>();

        /// <summary>The fake behind an effect handle the runtime was given.</summary>
        public FakeEffect EffectOf(effect fx)
        {
            return effects[fx];
        }

        /// <summary>Makes an effect of a model at a place, on the ground there.</summary>
        public effect MakeEffect(string model, float x, float y)
        {
            var made = new effect();
            var fake = new FakeEffect { Model = model, X = x, Y = y, Z = Ground(x, y) };
            effects[made] = fake;
            Effects.Add(fake);
            return made;
        }

        /// <summary>Makes an effect of a model on a unit, at an attachment point, where the unit is.</summary>
        public effect MakeEffectOn(string model, unit unit, string point)
        {
            var made = new effect();
            var fake = new FakeEffect { Model = model, Unit = unit, Point = point, X = UnitX(unit), Y = UnitY(unit) };
            effects[made] = fake;
            Effects.Add(fake);
            return made;
        }

        /// <summary>Moves an effect to a place and an absolute height, and counts the moves.</summary>
        public void PlaceEffect(effect fx, float x, float y, float z)
        {
            var fake = Live(fx);
            fake.Places++;
            fake.X = x;
            fake.Y = y;
            fake.Z = z;
        }

        /// <summary>Turns an effect, in radians.</summary>
        public void TurnEffect(effect fx, float yaw, float pitch, float roll)
        {
            var fake = Live(fx);
            fake.Yaw = yaw;
            fake.Pitch = pitch;
            fake.Roll = roll;
        }

        /// <summary>Scales an effect.</summary>
        public void ScaleEffect(effect fx, float scale)
        {
            Live(fx).Scale = scale;
        }

        /// <summary>Tints an effect: its red, green, blue and alpha.</summary>
        public void TintEffect(effect fx, int red, int green, int blue, int alpha)
        {
            Live(fx).Tint = new int[] { red, green, blue, alpha };
        }

        /// <summary>Draws an effect's team-coloured parts in a player's colour.</summary>
        public void SetEffectTeamColor(effect fx, player player)
        {
            Live(fx).TeamColor = player;
        }

        /// <summary>Plays an animation of an effect by name, or queues it.</summary>
        public void AnimateEffect(effect fx, string name, bool queued)
        {
            Live(fx).Animations.Add(queued ? "queue " + name : name);
        }

        /// <summary>Plays an animation of an effect by its kind.</summary>
        public void AnimateEffectByType(effect fx, int type)
        {
            Live(fx).Animations.Add("type " + type);
        }

        /// <summary>Adds a sub-animation to an effect, or removes it.</summary>
        public void SetEffectSubAnimation(effect fx, int subAnimation, bool on)
        {
            var held = Live(fx).SubAnimations;
            held.Remove(subAnimation);
            if (on)
            {
                held.Add(subAnimation);
            }
        }

        /// <summary>Removes every sub-animation of an effect.</summary>
        public void ClearEffectSubAnimations(effect fx)
        {
            Live(fx).SubAnimations.Clear();
        }

        /// <summary>Sets the speed of an effect's animations.</summary>
        public void SetEffectSpeed(effect fx, float speed)
        {
            Live(fx).Speed = speed;
        }

        /// <summary>The fake behind an effect handle; the runtime asking anything of an effect it has ended fails the case.</summary>
        private FakeEffect Live(effect fx)
        {
            var fake = effects[fx];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about an effect it had ended");
            }

            return fake;
        }

        /// <summary>Ends an effect, with its death or at once; a second end fails the case.</summary>
        public void EndEffect(effect fx, bool playDeath)
        {
            var fake = Live(fx);
            fake.Ends++;
            fake.Ended = true;
            fake.Death = playDeath;
        }

        /// <summary>The ground's height at a place, from Ground, and counts the read.</summary>
        public float GroundZ(float x, float y)
        {
            GroundReads++;
            return Ground(x, y);
        }

        /// <summary>The weather effects, lightning, fog modifiers, pings and minimap icons the runtime made, in the order made.</summary>
        public List<FakeWeather> Weathers = new List<FakeWeather>();
        public List<FakeBolt> Bolts = new List<FakeBolt>();
        public List<FakeVision> Visions = new List<FakeVision>();
        public List<FakePing> Pings = new List<FakePing>();
        public List<FakeIcon> Icons = new List<FakeIcon>();

        /// <summary>The fog of war and the black mask, on until set.</summary>
        public bool FogOn = true;
        public bool BlackMaskOn = true;

        private readonly Dictionary<weathereffect, FakeWeather> weathers = new Dictionary<weathereffect, FakeWeather>();
        private readonly Dictionary<lightning, FakeBolt> bolts = new Dictionary<lightning, FakeBolt>();
        private readonly Dictionary<fogmodifier, FakeVision> visions = new Dictionary<fogmodifier, FakeVision>();
        private readonly Dictionary<minimapicon, FakeIcon> icons = new Dictionary<minimapicon, FakeIcon>();

        /// <summary>The world's bounds: -4096 to 4096 both ways.</summary>
        public float[] WorldBounds()
        {
            return new float[] { -4096f, -4096f, 4096f, 4096f };
        }

        /// <summary>A weather effect over a rect, off.</summary>
        public weathereffect MakeWeather(string kind, float left, float bottom, float right, float top)
        {
            var made = new weathereffect();
            var fake = new FakeWeather { Kind = kind, Left = left, Bottom = bottom, Right = right, Top = top };
            weathers[made] = fake;
            Weathers.Add(fake);
            return made;
        }

        /// <summary>Turns a weather effect on or off.</summary>
        public void ShowWeather(weathereffect weather, bool shown)
        {
            Live(weathers[weather].Ended, "weather");
            weathers[weather].Shown = shown;
        }

        /// <summary>Removes a weather effect; a second removal fails the case.</summary>
        public void EndWeather(weathereffect weather)
        {
            Live(weathers[weather].Ended, "weather");
            weathers[weather].Ended = true;
        }

        /// <summary>A lightning between two points.</summary>
        public lightning MakeLightning(string kind, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            var made = new lightning();
            var fake = new FakeBolt { Kind = kind, X1 = x1, Y1 = y1, Z1 = z1, X2 = x2, Y2 = y2, Z2 = z2 };
            bolts[made] = fake;
            Bolts.Add(fake);
            return made;
        }

        /// <summary>Moves a lightning, and counts the moves.</summary>
        public void MoveBolt(lightning bolt, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            var fake = bolts[bolt];
            Live(fake.Ended, "lightning");
            fake.Moves++;
            fake.X1 = x1;
            fake.Y1 = y1;
            fake.Z1 = z1;
            fake.X2 = x2;
            fake.Y2 = y2;
            fake.Z2 = z2;
        }

        /// <summary>Removes a lightning; a second removal fails the case.</summary>
        public void EndLightning(lightning bolt)
        {
            Live(bolts[bolt].Ended, "lightning");
            bolts[bolt].Ended = true;
        }

        /// <summary>A fog modifier over a circle.</summary>
        public fogmodifier MakeVision(player player, int state, float x, float y, float radius, bool overUnits)
        {
            return Vision(new FakeVision { Player = player, State = state, X = x, Y = y, Radius = radius, OverUnits = overUnits });
        }

        /// <summary>A fog modifier over a rect.</summary>
        public fogmodifier MakeVisionRect(player player, int state, float left, float bottom, float right, float top, bool overUnits)
        {
            return Vision(new FakeVision { Player = player, State = state, Left = left, Bottom = bottom, Right = right, Top = top, OverUnits = overUnits });
        }

        /// <summary>Ends a fog modifier; a second end fails the case.</summary>
        public void EndVision(fogmodifier vision)
        {
            Live(visions[vision].Ended, "fog modifier");
            visions[vision].Ended = true;
        }

        /// <summary>Whether the fog is on.</summary>
        public bool IsFogOn()
        {
            return FogOn;
        }

        /// <summary>Sets the fog.</summary>
        public void SetFogOn(bool on)
        {
            FogOn = on;
        }

        /// <summary>Whether the black mask is on.</summary>
        public bool IsBlackMaskOn()
        {
            return BlackMaskOn;
        }

        /// <summary>Sets the black mask.</summary>
        public void SetBlackMaskOn(bool on)
        {
            BlackMaskOn = on;
        }

        /// <summary>Records a ping at this machine.</summary>
        public void Ping(float x, float y, float seconds, int red, int green, int blue, bool flashy)
        {
            Pings.Add(new FakePing { X = x, Y = y, Seconds = seconds, Red = red, Green = green, Blue = blue, Flashy = flashy });
        }

        /// <summary>A minimap icon at a place.</summary>
        public minimapicon MakeIcon(string path, float x, float y, int red, int green, int blue)
        {
            return Icon(new FakeIcon { Path = path, X = x, Y = y, Red = red, Green = green, Blue = blue });
        }

        /// <summary>A minimap icon on a unit.</summary>
        public minimapicon MakeIconOn(string path, unit unit, int red, int green, int blue)
        {
            return Icon(new FakeIcon { Path = path, Unit = unit, Red = red, Green = green, Blue = blue });
        }

        /// <summary>Shows or hides a minimap icon.</summary>
        public void ShowIcon(minimapicon icon, bool shown)
        {
            Live(icons[icon].Ended, "minimap icon");
            icons[icon].Shown = shown;
        }

        /// <summary>Removes a minimap icon; a second removal fails the case.</summary>
        public void EndIcon(minimapicon icon)
        {
            Live(icons[icon].Ended, "minimap icon");
            icons[icon].Ended = true;
        }

        private fogmodifier Vision(FakeVision fake)
        {
            var made = new fogmodifier();
            visions[made] = fake;
            Visions.Add(fake);
            return made;
        }

        private minimapicon Icon(FakeIcon fake)
        {
            var made = new minimapicon();
            icons[made] = fake;
            Icons.Add(fake);
            return made;
        }

        // The runtime asking anything of what it has removed fails the case.
        private static void Live(bool ended, string what)
        {
            if (ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a " + what + " it had removed");
            }
        }

        /// <summary>The floating texts the runtime made, in the order made.</summary>
        public List<FakeText> Texts = new List<FakeText>();

        /// <summary>The units the player at this machine does not see; every other unit is seen.</summary>
        public List<unit> Unseen = new List<unit>();

        private readonly Dictionary<texttag, FakeText> texts = new Dictionary<texttag, FakeText>();

        /// <summary>The fake behind a text handle the runtime was given.</summary>
        public FakeText TextOf(texttag text)
        {
            return texts[text];
        }

        /// <summary>Makes a text that stays until it is ended.</summary>
        public texttag MakeText()
        {
            var made = new texttag();
            var fake = new FakeText();
            texts[made] = fake;
            Texts.Add(fake);
            return made;
        }

        /// <summary>Writes a text's words and height.</summary>
        public void WriteText(texttag text, string words, float height)
        {
            var fake = LiveText(text);
            fake.Words = words;
            fake.LetterHeight = height;
        }

        /// <summary>Puts a text at a place and counts it.</summary>
        public void PlaceText(texttag text, float x, float y, float height)
        {
            var fake = LiveText(text);
            fake.Over = null;
            fake.X = x;
            fake.Y = y;
            fake.Height = height;
            fake.Places++;
        }

        /// <summary>Puts a text over a unit, where the unit is, and counts it.</summary>
        public void PlaceTextOver(texttag text, unit unit, float height)
        {
            var fake = LiveText(text);
            fake.Over = unit;
            fake.X = UnitX(unit);
            fake.Y = UnitY(unit);
            fake.Height = height;
            fake.Places++;
        }

        /// <summary>Colours a text and counts it.</summary>
        public void ColorText(texttag text, int red, int green, int blue, int alpha)
        {
            var fake = LiveText(text);
            fake.Color = new int[] { red, green, blue, alpha };
            fake.Colors++;
        }

        /// <summary>Sets a text's velocity.</summary>
        public void DriftText(texttag text, float xVelocity, float yVelocity)
        {
            var fake = LiveText(text);
            fake.XVelocity = xVelocity;
            fake.YVelocity = yVelocity;
        }

        /// <summary>Shows or hides a text.</summary>
        public void ShowText(texttag text, bool shown)
        {
            LiveText(text).Shown = shown;
        }

        /// <summary>Ends a text; a second end fails the case.</summary>
        public void EndText(texttag text)
        {
            LiveText(text).Ended = true;
        }

        /// <summary>Whether the player at this machine sees a unit: every unit not in Unseen.</summary>
        public bool LocalSees(unit unit)
        {
            return !Unseen.Contains(unit);
        }

        /// <summary>The fake behind a text handle; the runtime asking anything of a text it has ended fails the case, as the game gives its id to the next.</summary>
        private FakeText LiveText(texttag text)
        {
            var fake = texts[text];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a text it had ended");
            }

            return fake;
        }

        /// <summary>The sounds the runtime made, in the order made.</summary>
        public List<FakeSound> Sounds = new List<FakeSound>();

        private readonly Dictionary<sound, FakeSound> sounds = new Dictionary<sound, FakeSound>();

        /// <summary>The fake behind a sound handle the runtime was given.</summary>
        public FakeSound SoundOf(sound sound)
        {
            return sounds[sound];
        }

        /// <summary>Makes a sound.</summary>
        public sound MakeSound(string path, bool looping, bool is3D)
        {
            var made = new sound();
            var fake = new FakeSound { Path = path, Looping = looping, Is3D = is3D };
            sounds[made] = fake;
            Sounds.Add(fake);
            return made;
        }

        /// <summary>Puts a sound at a place.</summary>
        public void PlaceSound(sound sound, float x, float y, float z)
        {
            var fake = LiveSound(sound);
            fake.X = x;
            fake.Y = y;
            fake.Z = z;
        }

        /// <summary>Puts a sound on a unit.</summary>
        public void PutSoundOn(sound sound, unit unit)
        {
            LiveSound(sound).Unit = unit;
        }

        /// <summary>Sets a sound's volume and lists it.</summary>
        public void SetLoudness(sound sound, int volume)
        {
            var fake = LiveSound(sound);
            fake.Volume = volume;
            fake.Volumes.Add(volume);
        }

        /// <summary>Starts a sound.</summary>
        public void StartPlaying(sound sound)
        {
            var fake = LiveSound(sound);
            fake.Started = true;
            fake.Starts++;
        }

        /// <summary>Leaves a sound to the game to free.</summary>
        public void FreeWhenDone(sound sound)
        {
            LiveSound(sound).FreedWhenDone = true;
        }

        /// <summary>Stops a sound; a second stop fails the case.</summary>
        public void StopPlaying(sound sound)
        {
            LiveSound(sound).Stopped = true;
        }

        /// <summary>The fake behind a sound handle; the runtime asking anything of a sound it has stopped fails the case.</summary>
        private FakeSound LiveSound(sound sound)
        {
            var fake = sounds[sound];
            if (fake.Stopped)
            {
                throw new ExpectationFailed("the runtime asked the game about a sound it had stopped");
            }

            return fake;
        }

        /// <summary>What the runtime asked of this machine's music, in order: "play a", "stop fading" or "stop at once", "volume 76".</summary>
        public List<string> MusicCalls = new List<string>();

        /// <summary>The length of each music file in milliseconds, as this machine reads it; a file not here reads 0.</summary>
        public Dictionary<string, int> MusicLengths = new Dictionary<string, int>();

        /// <summary>Records a file played.</summary>
        public void StartMusic(string path)
        {
            MusicCalls.Add("play " + path);
        }

        /// <summary>A file's length from MusicLengths.</summary>
        public int MusicLength(string path)
        {
            return MusicLengths.TryGetValue(path, out var length) ? length : 0;
        }

        /// <summary>Records a stop.</summary>
        public void StopAllMusic(bool fadeOut)
        {
            MusicCalls.Add(fadeOut ? "stop fading" : "stop at once");
        }

        /// <summary>Records a volume.</summary>
        public void SetMusicLoudness(int volume)
        {
            MusicCalls.Add("volume " + volume);
        }

        /// <summary>The units in a transport, each with the transport it is in; Load and LetOut change it as the game would.</summary>
        public Dictionary<unit, unit> LoadedIn = new Dictionary<unit, unit>();

        private readonly Dictionary<unit, int> cargoCapacities = new Dictionary<unit, int>();

        /// <summary>Whether a unit is in a transport.</summary>
        public bool IsLoaded(unit unit)
        {
            return LoadedIn.ContainsKey(unit);
        }

        /// <summary>A unit's cargo capacity: 0 until it is set.</summary>
        public int CargoCapacity(unit unit)
        {
            return cargoCapacities.TryGetValue(unit, out var capacity) ? capacity : 0;
        }

        /// <summary>Sets a unit's cargo capacity.</summary>
        public void SetCargoCapacity(unit unit, int capacity)
        {
            cargoCapacities[unit] = capacity;
        }

        /// <summary>The game's load: the stop order the cargo is given, the cargo in the transport, its undefend order, then, when the runtime listens, the load event (seen 11-08).</summary>
        public void Load(unit cargo, unit transport)
        {
            PlayerOrdersId(cargo, 851972, OrderKind.None);
            LoadedIn[cargo] = transport;
            PlayerOrdersId(cargo, 852056, OrderKind.None);
            if (Listening.Contains(GameEvent.Cargo))
            {
                Inbound.Loaded(cargo, transport);
            }
        }

        /// <summary>The game lets a unit out of its transport: it is no longer loaded, then it is given the stop order (seen 11-08).</summary>
        public void LetOut(unit cargo)
        {
            LoadedIn.Remove(cargo);
            PlayerOrdersId(cargo, 851972, OrderKind.None);
        }

        /// <summary>The game's random numbers: low, the first allowed, unless a case sets another way to pick.</summary>
        public System.Func<int, int, int> Pick = (low, high) => low;

        /// <summary>A number from Pick.</summary>
        public int RandomBetween(int low, int high)
        {
            return Pick(low, high);
        }

        /// <summary>A destructable the runtime asked the game to make: its type, place, facing, scale and variation.</summary>
        public sealed class DestructableSpawn
        {
            /// <summary>The type asked for.</summary>
            public int TypeId;

            /// <summary>The x asked for.</summary>
            public float X;

            /// <summary>The y asked for.</summary>
            public float Y;

            /// <summary>The height asked for; NaN for one made without a height.</summary>
            public float Z = float.NaN;

            /// <summary>The destructable made at a height; null for one made without.</summary>
            public destructable? Made;

            /// <summary>The facing asked for.</summary>
            public float Facing;

            /// <summary>The scale asked for.</summary>
            public float Scale;

            /// <summary>The variation asked for.</summary>
            public int Variation;
        }
    }
}
