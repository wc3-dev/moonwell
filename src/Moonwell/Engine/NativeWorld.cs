using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The game as the runtime sees it, through the game's natives; the only class that calls them.</summary>
    internal sealed class NativeWorld : IWorld
    {
        /// <summary>How often the log file is checked for lines that wait to be written.</summary>
        private const float LogCheck = 0.25f;

        /// <summary>The seconds the clock runs before it starts again: its elapsed time reads exact at 10 s, and steps by 0.0625 s at 604800 s.</summary>
        private const float Lap = 10.0f;

        /// <summary>A dummy's reach: farther than any map.</summary>
        private const float DummyRange = 99999.0f;

        /// <summary>How far a patch of vision reaches around a place; the probe's 128 let a unit there be targeted (11-03).</summary>
        private const float PatchReach = 128.0f;

        /// <summary>The clock: the game time is the laps it has run plus its elapsed time.</summary>
        private readonly timer clock;

        /// <summary>The game time the clock last started at, a whole number of laps.</summary>
        private float lapStart = 0.0f;

        /// <summary>The timer that calls Runtime.Wake when the next wake is due.</summary>
        private readonly timer wake;

        /// <summary>The rect of the whole map, made once; every call to GetWorldBounds makes a new one.</summary>
        private readonly rect bounds;

        /// <summary>The trigger that sees each destructable die, shared by every destructable watched.</summary>
        private readonly trigger deathTrigger;

        /// <summary>The lines of the log file and when it is next written.</summary>
        private readonly LogFile log = new LogFile();

        /// <summary>Whether the time of day was last stopped; the game has no reading for it.</summary>
        private bool dayStopped;

        /// <summary>The items the last ItemsInWorld call has found.</summary>
        private List<item> foundItems = new List<item>();

        /// <summary>The destructables the last DestructablesInWorld call has found.</summary>
        private List<destructable> foundDestructables = new List<destructable>();

        /// <summary>The location GroundZ reads the ground through; made on the first read.</summary>
        private location? spot;

        /// <summary>Starts the clock and the wake timer, and listens for units entering the map.</summary>
        public NativeWorld()
        {
            bounds = Natives.GetWorldBounds();
            clock = Natives.CreateTimer();
            Natives.TimerStart(clock, Lap, false, ClockLapped);
            wake = Natives.CreateTimer();

            var region = Natives.CreateRegion();
            Natives.RegionAddRect(region, bounds);
            var enter = Natives.CreateTrigger();
            Natives.TriggerRegisterEnterRegion(enter, region, null);
            Natives.TriggerAddAction(enter, EnterAction);

            deathTrigger = Natives.CreateTrigger();
            Natives.TriggerAddAction(deathTrigger, DestructableDeathAction);

            // First of anything that hears a saved game load, so no code meets the string type the load left (game facts, 11-03).
            var loaded = Natives.CreateTrigger();
            Natives.TriggerRegisterGameEvent(loaded, Natives.EVENT_GAME_LOADED);
            Natives.TriggerAddAction(loaded, () => LoadRepair.Strings());

            // Every machine runs the check, so a line logged inside Local.For starts no timer of one machine's.
            Natives.TimerStart(Natives.CreateTimer(), LogCheck, true, LogCheckAction);
        }

        /// <summary>Game seconds since the runtime started; in a development build, Moonwell's time on its clock (Runtime.Clock).</summary>
        public float Now()
        {
#if DEBUG
            return Runtime.Clock.Now(GameNow());
#else
            return GameNow();
#endif
        }

        /// <summary>Game seconds since the runtime started, under Moonwell's clock: what the clock, the debug world and the log file take.</summary>
        internal float GameNow()
        {
            return lapStart + Natives.TimerGetElapsed(clock);
        }

        /// <summary>Calls Runtime.Wake once after the seconds; a later request replaces it. In a development build the seconds are Moonwell's, and a request while its clock is halted stops the wake until the clock re-arms it.</summary>
        public void WakeIn(float seconds)
        {
#if DEBUG
            if (Runtime.Clock.Arm(seconds, out var gameSeconds))
            {
                Natives.TimerStart(wake, gameSeconds < 0.0f ? 0.0f : gameSeconds, false, WakeAction);
            }
            else
            {
                Natives.PauseTimer(wake);
            }
#else
            Natives.TimerStart(wake, seconds < 0.0f ? 0.0f : seconds, false, WakeAction);
#endif
        }

        /// <summary>Starts delivering an event to Inbound.</summary>
        public void Listen(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case GameEvent.Damage:
                    Register(Natives.EVENT_PLAYER_UNIT_DAMAGED, DamageAction);
                    break;
                case GameEvent.Death:
                    Register(Natives.EVENT_PLAYER_UNIT_DEATH, DeathAction);
                    break;
                case GameEvent.Spell:
                    Register(Natives.EVENT_PLAYER_UNIT_SPELL_EFFECT, SpellEffectAction);
                    Register(Natives.EVENT_PLAYER_UNIT_SPELL_ENDCAST, SpellEndedAction);
                    break;
                case GameEvent.PickUp:
                    Register(Natives.EVENT_PLAYER_UNIT_PICKUP_ITEM, PickUpAction);
                    break;
                case GameEvent.Drop:
                    Register(Natives.EVENT_PLAYER_UNIT_DROP_ITEM, DropAction);
                    break;
                case GameEvent.Leave:
                    var trigger = Natives.CreateTrigger();
                    for (var slot = 0; slot < Natives.bj_MAX_PLAYERS; slot++)
                    {
                        Natives.TriggerRegisterPlayerEvent(trigger, Natives.Player(slot), Natives.EVENT_PLAYER_LEAVE);
                    }

                    Natives.TriggerAddAction(trigger, LeaveAction);
                    break;
                case GameEvent.Loaded:
                    var loaded = Natives.CreateTrigger();
                    Natives.TriggerRegisterGameEvent(loaded, Natives.EVENT_GAME_LOADED);
                    Natives.TriggerAddAction(loaded, LoadedAction);
                    break;
                case GameEvent.HeroLevel:
                    Register(Natives.EVENT_PLAYER_HERO_LEVEL, HeroLevelAction);
                    break;
                case GameEvent.HeroSkill:
                    Register(Natives.EVENT_PLAYER_HERO_SKILL, HeroSkillAction);
                    break;
                case GameEvent.Order:
                    Register(Natives.EVENT_PLAYER_UNIT_ISSUED_ORDER, () => OrderAction(OrderKind.None));
                    Register(Natives.EVENT_PLAYER_UNIT_ISSUED_POINT_ORDER, () => OrderAction(OrderKind.Place));
                    Register(Natives.EVENT_PLAYER_UNIT_ISSUED_TARGET_ORDER, () => OrderAction(OrderKind.Target));
                    break;
                case GameEvent.OwnerChange:
                    Register(Natives.EVENT_PLAYER_UNIT_CHANGE_OWNER, OwnerChangeAction);
                    break;
            }
        }

        /// <summary>Hands a unit's change of hands to Inbound.</summary>
        private static void OwnerChangeAction()
        {
            try
            {
                Inbound.OwnerChanged(Natives.GetTriggerUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.OwnerChanged", error);
            }
        }

        /// <summary>Delivers a destructable's death to Inbound.</summary>
        public void WatchDeath(destructable destructable)
        {
            Natives.TriggerRegisterDeathEvent(deathTrigger, destructable);
        }

        /// <summary>Shows a line on the screen of the local player.</summary>
        public void Print(string text)
        {
            Natives.DisplayTimedTextToPlayer(Natives.GetLocalPlayer(), 0.0f, 0.0f, 30.0f, text);
        }

        /// <summary>The slot state of a player, from the game.</summary>
        public int SlotStateOf(player player)
        {
            var state = Natives.GetPlayerSlotState(player);
            if (state == Natives.PLAYER_SLOT_STATE_PLAYING)
            {
                return 1;
            }

            return state == Natives.PLAYER_SLOT_STATE_LEFT ? 2 : 0;
        }

        /// <summary>Who controls a player, from the game.</summary>
        public int ControllerOf(player player)
        {
            var control = Natives.GetPlayerController(player);
            if (control == Natives.MAP_CONTROL_USER)
            {
                return 0;
            }

            if (control == Natives.MAP_CONTROL_COMPUTER)
            {
                return 1;
            }

            if (control == Natives.MAP_CONTROL_RESCUABLE)
            {
                return 2;
            }

            if (control == Natives.MAP_CONTROL_NEUTRAL)
            {
                return 3;
            }

            return control == Natives.MAP_CONTROL_CREEP ? 4 : 5;
        }

        /// <summary>A state of a player, from the game.</summary>
        public int StateOf(player player, PlayerState state)
        {
            return Natives.GetPlayerState(player, NativeState(state));
        }

        /// <summary>Sets a state of a player in the game.</summary>
        public void SetState(player player, PlayerState state, int value)
        {
            Natives.SetPlayerState(player, NativeState(state), value);
        }

        private static playerstate NativeState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Gold:
                    return Natives.PLAYER_STATE_RESOURCE_GOLD;
                case PlayerState.Lumber:
                    return Natives.PLAYER_STATE_RESOURCE_LUMBER;
                case PlayerState.FoodUsed:
                    return Natives.PLAYER_STATE_RESOURCE_FOOD_USED;
                case PlayerState.FoodCap:
                    return Natives.PLAYER_STATE_RESOURCE_FOOD_CAP;
                case PlayerState.FoodCapCeiling:
                    return Natives.PLAYER_STATE_FOOD_CAP_CEILING;
                case PlayerState.GoldGathered:
                    return Natives.PLAYER_STATE_GOLD_GATHERED;
                case PlayerState.LumberGathered:
                    return Natives.PLAYER_STATE_LUMBER_GATHERED;
                case PlayerState.GoldUpkeepRate:
                    return Natives.PLAYER_STATE_GOLD_UPKEEP_RATE;
                default:
                    return Natives.PLAYER_STATE_LUMBER_UPKEEP_RATE;
            }
        }

        /// <summary>Shows a line to one player: the game shows it only on that player's machine.</summary>
        public void PrintTo(player player, string text, float seconds)
        {
            if (seconds < 0.0f)
            {
                Natives.DisplayTextToPlayer(player, 0.0f, 0.0f, text);
            }
            else
            {
                Natives.DisplayTimedTextToPlayer(player, 0.0f, 0.0f, seconds, text);
            }
        }

        /// <summary>Shows a line to every player: each machine shows it to its own player.</summary>
        public void PrintToAll(string text, float seconds)
        {
            PrintTo(Natives.GetLocalPlayer(), text, seconds);
        }

        /// <summary>Adds a line to the log, and rewrites the log file now or once the second since its last write is up.</summary>
        public void Log(string text)
        {
            if (log.Add(text, GameNow()))
            {
                WriteLog();
            }
        }

        private void LogCheckAction()
        {
            if (log.Due(GameNow()))
            {
                WriteLog();
            }
        }

        private void WriteLog()
        {
            log.Written(GameNow());
            Natives.PreloadGenClear();
            Natives.PreloadGenStart();
            foreach (var line in log.Lines)
            {
                Natives.Preload("\")\n" + line + "\n//");
            }

            Natives.PreloadGenEnd("Moonwell\\log.txt");
        }

        /// <summary>Sends a sharing message from this machine.</summary>
        public void SendSync(string text)
        {
            Natives.BlzSendSyncData(Sharing.Prefix, text);
        }

        /// <summary>One trigger for a player's sharing messages.</summary>
        public void ListenSync(player player)
        {
            var trigger = Natives.CreateTrigger();
            Natives.BlzTriggerRegisterPlayerSyncEvent(trigger, player, Sharing.Prefix, false);
            Natives.TriggerAddAction(trigger, SyncAction);
        }

        /// <summary>Writes a player file: each piece a Preload line that puts it in the store ability's tooltips (FileText).</summary>
        public void WritePreload(string path, IReadOnlyList<string> pieces)
        {
            Natives.PreloadGenClear();
            Natives.PreloadGenStart();
            for (var i = 0; i < pieces.Count; i++)
            {
                Natives.Preload(FileText.Line(i, pieces[i]));
            }

            Natives.PreloadGenEnd(path);
        }

        /// <summary>Reads a player file: every tooltip of the store ability is set to a mark no piece holds, Preloader runs the file's lines, and the pieces are read until a mark.</summary>
        public List<string>? ReadPreload(string path)
        {
            const string Mark = "\u0001";
            var store = ObjectTypes.FourCC(FileText.StoreId);
            for (var level = 0; level < 100; level++)
            {
                Natives.BlzSetAbilityTooltip(store, Mark, level);
                Natives.BlzSetAbilityExtendedTooltip(store, Mark, level);
            }

            Natives.Preloader(path);
            var pieces = new List<string>();
            for (var i = 0; i < FileText.MostPieces; i++)
            {
                var piece = i < 100 ? Natives.BlzGetAbilityTooltip(store, i) : Natives.BlzGetAbilityExtendedTooltip(store, i - 100);
                if (piece == Mark)
                {
                    break;
                }

                pieces.Add(piece);
            }

            return pieces.Count == 0 ? null : pieces;
        }

        /// <summary>This machine's seconds since 1970, from Lua's os.time.</summary>
        public int ClockSeconds()
        {
            return OsTime();
        }

        /// <summary>The zone's offset, from os.date's local and UTC fields; os.time reads the UTC fields as winter time, so summer time adds its hour.</summary>
        public int ZoneOffset()
        {
            return OsOffset();
        }

        /// @CSharpLua.Template = "os.time()"
        private static int OsTime()
        {
            return 0;
        }

        /// @CSharpLua.Template = "(os.time(os.date('*t')) - os.time(os.date('!*t')) + (os.date('*t').isdst and 3600 or 0))"
        private static int OsOffset()
        {
            return 0;
        }

        private static void LoadedAction()
        {
            try
            {
                Inbound.GameLoaded();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Loaded", error);
            }
        }

        private static void SyncAction()
        {
            try
            {
                Inbound.SyncArrived(Natives.GetTriggerPlayer(), Natives.BlzGetTriggerSyncData());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Share", error);
            }
        }

        /// <summary>The game's time of day, in hours.</summary>
        public float HourOfDay()
        {
            return Natives.GetFloatGameState(Natives.GAME_STATE_TIME_OF_DAY);
        }

        /// <summary>Sets the game's time of day; the game reads a time of 24 or more, or below 0, as 0 (game facts).</summary>
        public void SetHourOfDay(float hours)
        {
            Natives.SetFloatGameState(Natives.GAME_STATE_TIME_OF_DAY, hours);
        }

        /// <summary>How fast the time of day goes.</summary>
        public float DayScale()
        {
            return Natives.GetTimeOfDayScale();
        }

        /// <summary>Sets how fast the time of day goes.</summary>
        public void SetDayScale(float scale)
        {
            Natives.SetTimeOfDayScale(scale);
        }

        /// <summary>Whether the time of day stands still, as last set.</summary>
        public bool IsDayStopped()
        {
            return dayStopped;
        }

        /// <summary>Stops the time of day or lets it go on; the game keeps the speed, and does not count stops (game facts).</summary>
        public void StopDay(bool stopped)
        {
            dayStopped = stopped;
            Natives.SuspendTimeOfDay(stopped);
        }

        /// <summary>The player in a slot.</summary>
        public player PlayerAt(int slot)
        {
            return Natives.Player(slot);
        }

        /// <summary>The slot of a player.</summary>
        public int SlotOf(player player)
        {
            return Natives.GetPlayerId(player);
        }

        /// <summary>The slot of the neutral hostile player.</summary>
        public int NeutralAggressiveSlot()
        {
            return Natives.PLAYER_NEUTRAL_AGGRESSIVE;
        }

        /// <summary>The slot of the neutral victim player.</summary>
        public int NeutralVictimSlot()
        {
            return Natives.bj_PLAYER_NEUTRAL_VICTIM;
        }

        /// <summary>The slot of the neutral extra player.</summary>
        public int NeutralExtraSlot()
        {
            return Natives.bj_PLAYER_NEUTRAL_EXTRA;
        }

        /// <summary>The slot of the neutral passive player.</summary>
        public int NeutralPassiveSlot()
        {
            return Natives.PLAYER_NEUTRAL_PASSIVE;
        }

        /// <summary>The x of a player's start location.</summary>
        public float StartXOf(player player)
        {
            return Natives.GetPlayerStartLocationX(player);
        }

        /// <summary>The y of a player's start location.</summary>
        public float StartYOf(player player)
        {
            return Natives.GetPlayerStartLocationY(player);
        }

        /// <summary>Whether a player is the one at this machine.</summary>
        public bool IsLocal(player player)
        {
            return Natives.GetLocalPlayer() == player;
        }

        /// <summary>Every unit in the world, by player slot: the world's rect misses hidden and Locust units, a player's units do not.</summary>
        public List<unit> UnitsInWorld()
        {
            var group = Natives.CreateGroup();
            var units = new List<unit>();
            for (var slot = 0; slot < Natives.bj_MAX_PLAYER_SLOTS; slot++)
            {
                Natives.GroupEnumUnitsOfPlayer(group, Natives.Player(slot), null);
                var count = Natives.BlzGroupGetSize(group);
                for (var i = 0; i < count; i++)
                {
                    units.Add(Natives.BlzGroupUnitAt(group, i));
                }

                Natives.GroupClear(group);
            }

            Natives.DestroyGroup(group);
            return units;
        }

        /// <summary>Every item in the world.</summary>
        public List<item> ItemsInWorld()
        {
            foundItems = new List<item>();
            Natives.EnumItemsInRect(bounds, null, CollectItem);
            return foundItems;
        }

        /// <summary>Every destructable in the world.</summary>
        public List<destructable> DestructablesInWorld()
        {
            foundDestructables = new List<destructable>();
            Natives.EnumDestructablesInRect(bounds, null, CollectDestructable);
            return foundDestructables;
        }

        /// <summary>Creates a unit of a type for an owner at a place and facing.</summary>
        public unit SpawnUnit(player owner, int typeId, float x, float y, float facing)
        {
            return Natives.CreateUnit(owner, typeId, x, y, facing);
        }

        /// <summary>Creates an item of a type at a place.</summary>
        public item SpawnItem(int typeId, float x, float y)
        {
            return Natives.CreateItem(typeId, x, y);
        }

        /// <summary>The type id of a unit; 0 once the game has removed it.</summary>
        public int UnitTypeId(unit unit)
        {
            return Natives.GetUnitTypeId(unit);
        }

        /// <summary>The type id of an item; 0 once the game has removed it.</summary>
        public int ItemTypeId(item item)
        {
            return Natives.GetItemTypeId(item);
        }

        /// <summary>The type id of a destructable; 0 once the game has removed it.</summary>
        public int DestructableTypeId(destructable destructable)
        {
            return Natives.GetDestructableTypeId(destructable);
        }

        /// <summary>The x of a destructable.</summary>
        public float DestructableX(destructable destructable)
        {
            return Natives.GetDestructableX(destructable);
        }

        /// <summary>The y of a destructable.</summary>
        public float DestructableY(destructable destructable)
        {
            return Natives.GetDestructableY(destructable);
        }

        /// <summary>Creates a destructable of a type at a place, with a facing, a scale and a variation.</summary>
        public destructable SpawnDestructable(int typeId, float x, float y, float facing, float scale, int variation)
        {
            return Natives.CreateDestructable(typeId, x, y, facing, scale, variation);
        }

        /// <summary>Kills a destructable through the game: KillDestructable.</summary>
        public void SlayDestructable(destructable destructable)
        {
            Natives.KillDestructable(destructable);
        }

        /// <summary>The life of a destructable, from the game.</summary>
        public float DestructableLifeOf(destructable destructable)
        {
            return Natives.GetDestructableLife(destructable);
        }

        /// <summary>Sets the life of a destructable in the game.</summary>
        public void PutDestructableLife(destructable destructable, float life)
        {
            Natives.SetDestructableLife(destructable, life);
        }

        /// <summary>The most life of a destructable, from the game.</summary>
        public float DestructableMaxLifeOf(destructable destructable)
        {
            return Natives.GetDestructableMaxLife(destructable);
        }

        /// <summary>Sets the most life of a destructable in the game.</summary>
        public void PutDestructableMaxLife(destructable destructable, float most)
        {
            Natives.SetDestructableMaxLife(destructable, most);
        }

        /// <summary>Brings a dead destructable back to life: DestructableRestoreLife.</summary>
        public void RestoreDestructable(destructable destructable, float life, bool birth)
        {
            Natives.DestructableRestoreLife(destructable, life, birth);
        }

        /// <summary>Removes a unit from the world.</summary>
        public void DeleteUnit(unit unit)
        {
            Natives.RemoveUnit(unit);
        }

        /// <summary>Removes an item from the world.</summary>
        public void DeleteItem(item item)
        {
            Natives.RemoveItem(item);
        }

        /// <summary>Removes a destructable from the world.</summary>
        public void DeleteDestructable(destructable destructable)
        {
            Natives.RemoveDestructable(destructable);
        }

        /// <summary>Whether a unit is a hero.</summary>
        public bool IsHero(unit unit)
        {
            return Natives.IsUnitType(unit, Natives.UNIT_TYPE_HERO);
        }

        /// <summary>Whether a unit is an illusion: IsUnitIllusion.</summary>
        public bool IsIllusion(unit unit)
        {
            return Natives.IsUnitIllusion(unit);
        }

        /// <summary>The player that owns a unit.</summary>
        public player OwnerOf(unit unit)
        {
            return Natives.GetOwningPlayer(unit);
        }

        /// <summary>Sets the owner of a unit and its colour: SetUnitOwner.</summary>
        public void SetOwner(unit unit, player player)
        {
            Natives.SetUnitOwner(unit, player, true);
        }

        /// <summary>The level a player has of an upgrade; 0 when the player has none.</summary>
        public int UpgradeLevel(player p, int upgradeId)
        {
            return Natives.GetPlayerTechCount(p, upgradeId, true);
        }

        /// <summary>Sets the level a player has of an upgrade, in the game.</summary>
        public void Research(player player, int upgradeId, int level)
        {
            Natives.SetPlayerTechResearched(player, upgradeId, level);
        }

        /// <summary>How many of a unit type a player may have, or the top level of an upgrade, from the game.</summary>
        public int TechMaxOf(player player, int typeId)
        {
            return Natives.GetPlayerTechMaxAllowed(player, typeId);
        }

        /// <summary>Sets how many of a unit type a player may have, or the top level of an upgrade, in the game.</summary>
        public void SetTechMax(player player, int typeId, int max)
        {
            Natives.SetPlayerTechMaxAllowed(player, typeId, max);
        }

        /// <summary>Makes an ability available to a player's units or not, in the game.</summary>
        public void MakeAvailable(player player, int abilityId, bool available)
        {
            Natives.SetPlayerAbilityAvailable(player, abilityId, available);
        }

        /// <summary>The name of a player, from the game.</summary>
        public string NameOf(player player)
        {
            return Natives.GetPlayerName(player);
        }

        /// <summary>Sets the name of a player in the game.</summary>
        public void Rename(player player, string name)
        {
            Natives.SetPlayerName(player, name);
        }

        /// <summary>The colour of a player, as its number in the game (PLAYER_COLOR_RED is 0).</summary>
        public int ColorOf(player player)
        {
            var color = Natives.GetPlayerColor(player);
            for (var i = 0; i < 24; i++)
            {
                if (color == Natives.ConvertPlayerColor(i))
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Sets the colour of a player in the game, and of the units it has: the game leaves those as they were.</summary>
        public void Recolor(player player, int color)
        {
            var playerColor = Natives.ConvertPlayerColor(color);
            Natives.SetPlayerColor(player, playerColor);
            var group = Natives.CreateGroup();
            Natives.GroupEnumUnitsOfPlayer(group, player, null);
            var count = Natives.BlzGroupGetSize(group);
            for (var i = 0; i < count; i++)
            {
                Natives.SetUnitColor(Natives.BlzGroupUnitAt(group, i), playerColor);
            }

            Natives.DestroyGroup(group);
        }

        /// <summary>The team of a player, from the game.</summary>
        public int TeamOf(player player)
        {
            return Natives.GetPlayerTeam(player);
        }

        /// <summary>Sets the team of a player in the game.</summary>
        public void MoveToTeam(player player, int team)
        {
            Natives.SetPlayerTeam(player, team);
        }

        /// <summary>The race of a player, as Race counts it.</summary>
        public int RaceOf(player player)
        {
            var race = Natives.GetPlayerRace(player);
            if (race == Natives.RACE_HUMAN)
            {
                return 0;
            }

            if (race == Natives.RACE_ORC)
            {
                return 1;
            }

            if (race == Natives.RACE_UNDEAD)
            {
                return 2;
            }

            if (race == Natives.RACE_NIGHTELF)
            {
                return 3;
            }

            return race == Natives.RACE_DEMON ? 4 : 5;
        }

        /// <summary>What one player gives another, from the game: bit n is ALLIANCE type n, as Alliance's flags are.</summary>
        public int AllianceOf(player source, player other)
        {
            var alliance = 0;
            for (var i = 0; i < 10; i++)
            {
                if (Natives.GetPlayerAlliance(source, other, Natives.ConvertAllianceType(i)))
                {
                    alliance |= 1 << i;
                }
            }

            return alliance;
        }

        /// <summary>Sets every alliance type one player gives another in the game, on or off as the flags say.</summary>
        public void SetAllianceOf(player source, player other, int alliance)
        {
            for (var i = 0; i < 10; i++)
            {
                Natives.SetPlayerAlliance(source, other, Natives.ConvertAllianceType(i), (alliance & (1 << i)) != 0);
            }
        }

        /// <summary>Whether the game counts the other player as the player's ally.</summary>
        public bool IsAllyOf(player source, player other)
        {
            return Natives.IsPlayerAlly(source, other);
        }

        /// <summary>Whether the game counts the other player as the player's enemy.</summary>
        public bool IsEnemyOf(player source, player other)
        {
            return Natives.IsPlayerEnemy(source, other);
        }

        /// <summary>The level of an ability on a unit; 0 when the unit has none.</summary>
        public int AbilityLevel(unit unit, int abilityId)
        {
            return Natives.GetUnitAbilityLevel(unit, abilityId);
        }

        /// <summary>Gives a unit an ability: UnitAddAbility, which answers false for an ability the unit has (seen 11-02).</summary>
        public bool AddAbility(unit unit, int abilityId)
        {
            return Natives.UnitAddAbility(unit, abilityId);
        }

        /// <summary>Sets the level of an ability on a unit: SetUnitAbilityLevel, which keeps it within the ability's levels and answers 0 for an ability the unit lacks (seen 11-02).</summary>
        public int ChangeAbilityLevel(unit unit, int abilityId, int level)
        {
            return Natives.SetUnitAbilityLevel(unit, abilityId, level);
        }

        /// <summary>Whether an ability the unit has is a hero skill, by its hero ability field.</summary>
        public bool IsHeroAbility(unit unit, int abilityId)
        {
            var ability = Natives.BlzGetUnitAbility(unit, abilityId);
            return ability != null && Natives.BlzGetAbilityBooleanField(ability, Natives.ABILITY_BF_HERO_ABILITY);
        }

        /// <summary>Hides an ability on a unit.</summary>
        public void HideAbility(unit unit, int abilityId)
        {
            Natives.BlzUnitHideAbility(unit, abilityId, true);
        }

        /// <summary>Makes an ability stay through a morph: UnitMakeAbilityPermanent.</summary>
        public void MakeAbilityPermanent(unit unit, int abilityId)
        {
            Natives.UnitMakeAbilityPermanent(unit, true, abilityId);
        }

        /// <summary>Takes an ability from a unit: UnitRemoveAbility.</summary>
        public bool RemoveAbility(unit unit, int abilityId)
        {
            return Natives.UnitRemoveAbility(unit, abilityId);
        }

        /// <summary>The life of a unit, from the game.</summary>
        public float LifeOf(unit unit)
        {
            return Natives.GetWidgetLife(unit);
        }

        /// <summary>Sets the life of a unit in the game.</summary>
        public void SetLife(unit unit, float life)
        {
            Natives.SetWidgetLife(unit, life);
        }

        /// <summary>The most life a unit can have, from the game: BlzGetUnitMaxHP (SetUnitState does not change the most, seen 10-21).</summary>
        public float MaxLifeOf(unit unit)
        {
            return Natives.BlzGetUnitMaxHP(unit);
        }

        /// <summary>Sets the most life a unit can have in the game: BlzSetUnitMaxHP, which takes a whole number.</summary>
        public void SetMaxLife(unit unit, float life)
        {
            Natives.BlzSetUnitMaxHP(unit, (int)life);
        }

        /// <summary>The mana of a unit, from the game.</summary>
        public float ManaOf(unit unit)
        {
            return Natives.GetUnitState(unit, Natives.UNIT_STATE_MANA);
        }

        /// <summary>Sets the mana of a unit in the game.</summary>
        public void SetMana(unit unit, float mana)
        {
            Natives.SetUnitState(unit, Natives.UNIT_STATE_MANA, mana);
        }

        /// <summary>The most mana a unit can have, from the game: BlzGetUnitMaxMana (SetUnitState does not change the most, seen 10-21).</summary>
        public float MaxManaOf(unit unit)
        {
            return Natives.BlzGetUnitMaxMana(unit);
        }

        /// <summary>Sets the most mana a unit can have in the game: BlzSetUnitMaxMana, which takes a whole number.</summary>
        public void SetMaxMana(unit unit, float mana)
        {
            Natives.BlzSetUnitMaxMana(unit, (int)mana);
        }

        /// <summary>The x of a unit, from the game.</summary>
        public float UnitX(unit unit)
        {
            return Natives.GetUnitX(unit);
        }

        /// <summary>The y of a unit, from the game.</summary>
        public float UnitY(unit unit)
        {
            return Natives.GetUnitY(unit);
        }

        /// <summary>Moves a unit through the game's pathing: SetUnitPosition.</summary>
        public void MoveUnit(unit unit, float x, float y)
        {
            Natives.SetUnitPosition(unit, x, y);
        }

        /// <summary>Puts a unit at a place with no pathing: SetUnitX and SetUnitY.</summary>
        public void PlaceUnit(unit unit, float x, float y)
        {
            Natives.SetUnitX(unit, x);
            Natives.SetUnitY(unit, y);
        }

        /// <summary>The facing of a unit, from the game.</summary>
        public float FacingOf(unit unit)
        {
            return Natives.GetUnitFacing(unit);
        }

        /// <summary>Orders a unit to turn at its turn rate: SetUnitFacing.</summary>
        public void TurnUnit(unit unit, float degrees)
        {
            Natives.SetUnitFacing(unit, degrees);
        }

        /// <summary>Sets the facing of a unit at once: BlzSetUnitFacingEx.</summary>
        public void FaceUnitNow(unit unit, float degrees)
        {
            Natives.BlzSetUnitFacingEx(unit, degrees);
        }

        /// <summary>The level of a unit, from the game.</summary>
        public int LevelOf(unit unit)
        {
            return Natives.GetUnitLevel(unit);
        }

        /// <summary>The name of a unit, from the game: GetUnitName.</summary>
        public string UnitNameOf(unit unit)
        {
            return Natives.GetUnitName(unit);
        }

        /// <summary>Sets the name of one unit: BlzSetUnitName.</summary>
        public void RenameUnit(unit unit, string name)
        {
            Natives.BlzSetUnitName(unit, name);
        }

        /// <summary>A hero's proper name, from the game: GetHeroProperName.</summary>
        public string ProperNameOf(unit hero)
        {
            return Natives.GetHeroProperName(hero);
        }

        /// <summary>Sets a hero's proper name: BlzSetHeroProperName.</summary>
        public void SetProperName(unit hero, string name)
        {
            Natives.BlzSetHeroProperName(hero, name);
        }

        /// <summary>The value a type declares for a field, from the table the build appends to the script.</summary>
        public object? TypeValue(int typeId, string field)
        {
            return TypeValueOf(typeId, field);
        }

        // MoonwellTypeValues = { [typeId] = { [fieldId] = value } }, written by the build only for a map that reads Default;
        // nil when there is no table or no entry.
        /// @CSharpLua.Template = "(MoonwellTypeValues and MoonwellTypeValues[{0}] and MoonwellTypeValues[{0}][{1}])"
        private static object? TypeValueOf(int typeId, string field)
        {
            return null;
        }

        /// <summary>Deals damage from a unit to another, with the game's attack, damage and weapon numbers; the damage event runs inside the call. Whether the game dealt it.</summary>
        public bool DealDamage(unit source, unit target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType)
        {
            return Natives.UnitDamageTarget(source, target, amount, attack, ranged, Natives.ConvertAttackType(attackType), Natives.ConvertDamageType(damageType), Natives.ConvertWeaponType(weaponType));
        }

        /// <summary>Deals damage from a unit to a destructable, with the game's attack, damage and weapon numbers.</summary>
        public void DealDamageTo(unit source, destructable target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType)
        {
            Natives.UnitDamageTarget(source, target, amount, attack, ranged, Natives.ConvertAttackType(attackType), Natives.ConvertDamageType(damageType), Natives.ConvertWeaponType(weaponType));
        }

        /// <summary>Whether the game counts a unit as alive.</summary>
        public bool IsAlive(unit unit)
        {
            return Natives.UnitAlive(unit);
        }

        /// <summary>Kills a unit in the game: KillUnit.</summary>
        public void SlayUnit(unit unit)
        {
            Natives.KillUnit(unit);
        }

        /// <summary>A hero's experience, from the game.</summary>
        public int ExperienceOf(unit hero)
        {
            return Natives.GetHeroXP(hero);
        }

        /// <summary>Sets a hero's experience in the game.</summary>
        public void SetExperience(unit hero, int experience, bool showEffect)
        {
            Natives.SetHeroXP(hero, experience, showEffect);
        }

        /// <summary>Adds experience to a hero in the game, or takes it with a negative amount.</summary>
        public void AddExperience(unit hero, int experience, bool showEffect)
        {
            Natives.AddHeroXP(hero, experience, showEffect);
        }

        /// <summary>Gives a hero a level in the game.</summary>
        public void LevelHero(unit hero, int level, bool showEffect)
        {
            Natives.SetHeroLevel(hero, level, showEffect);
        }

        /// <summary>Takes levels from a hero in the game.</summary>
        public void StripLevels(unit hero, int levels)
        {
            Natives.UnitStripHeroLevel(hero, levels);
        }

        /// <summary>A hero's strength, from the game.</summary>
        public int StrengthOf(unit hero, bool withBonus)
        {
            return Natives.GetHeroStr(hero, withBonus);
        }

        /// <summary>A hero's agility, from the game.</summary>
        public int AgilityOf(unit hero, bool withBonus)
        {
            return Natives.GetHeroAgi(hero, withBonus);
        }

        /// <summary>A hero's intelligence, from the game.</summary>
        public int IntelligenceOf(unit hero, bool withBonus)
        {
            return Natives.GetHeroInt(hero, withBonus);
        }

        /// <summary>Sets a hero's base strength for good, in the game.</summary>
        public void SetStrength(unit hero, int strength)
        {
            Natives.SetHeroStr(hero, strength, true);
        }

        /// <summary>Sets a hero's base agility for good, in the game.</summary>
        public void SetAgility(unit hero, int agility)
        {
            Natives.SetHeroAgi(hero, agility, true);
        }

        /// <summary>Sets a hero's base intelligence for good, in the game.</summary>
        public void SetIntelligence(unit hero, int intelligence)
        {
            Natives.SetHeroInt(hero, intelligence, true);
        }

        /// <summary>The skill points a hero has, from the game.</summary>
        public int SkillPointsOf(unit hero)
        {
            return Natives.GetHeroSkillPoints(hero);
        }

        /// <summary>Adds skill points to a hero in the game.</summary>
        public bool AddSkillPoints(unit hero, int delta)
        {
            return Natives.UnitModifySkillPoints(hero, delta);
        }

        /// <summary>Revives a hero at a place in the game.</summary>
        public bool ReviveAt(unit hero, float x, float y, bool showEffect)
        {
            return Natives.ReviveHero(hero, x, y, showEffect);
        }

        /// <summary>Whether a unit is paused, from the game.</summary>
        public bool IsPausedUnit(unit unit)
        {
            return Natives.IsUnitPaused(unit);
        }

        /// <summary>Pauses or unpauses a unit in the game.</summary>
        public void SetPaused(unit unit, bool paused)
        {
            Natives.PauseUnit(unit, paused);
        }

        /// <summary>The scaling value of a unit, from the game.</summary>
        public float ScaleOf(unit unit)
        {
            return Natives.BlzGetUnitRealField(unit, Natives.UNIT_RF_SCALING_VALUE);
        }

        /// <summary>Sets the scale of a unit on all three axes: SetUnitScale.</summary>
        public void ScaleUnit(unit unit, float scale)
        {
            Natives.SetUnitScale(unit, scale, scale, scale);
        }

        /// <summary>Tints a unit: SetUnitVertexColor.</summary>
        public void TintUnit(unit unit, int red, int green, int blue, int alpha)
        {
            Natives.SetUnitVertexColor(unit, red, green, blue, alpha);
        }

        /// <summary>The fly height of a unit, from the game.</summary>
        public float FlyHeightOf(unit unit)
        {
            return Natives.GetUnitFlyHeight(unit);
        }

        /// <summary>Whether the unit's type flies: IsUnitType with UNIT_TYPE_FLYING.</summary>
        public bool CanFly(unit unit)
        {
            return Natives.IsUnitType(unit, Natives.UNIT_TYPE_FLYING);
        }

        /// <summary>Sets the fly height of a unit with a rate of 0: SetUnitFlyHeight.</summary>
        public void SetFlyHeight(unit unit, float height)
        {
            Natives.SetUnitFlyHeight(unit, height, 0.0f);
        }

        /// <summary>Gives a unit an order with no place and no target: IssueImmediateOrder.</summary>
        public bool OrderUnit(unit unit, string order)
        {
            return Natives.IssueImmediateOrder(unit, order);
        }

        /// <summary>Gives a unit an order to a place: IssuePointOrder.</summary>
        public bool OrderUnitAt(unit unit, string order, float x, float y)
        {
            return Natives.IssuePointOrder(unit, order, x, y);
        }

        /// <summary>Gives a unit an order on a widget: IssueTargetOrder.</summary>
        public bool OrderUnitOn(unit unit, string order, widget target)
        {
            return Natives.IssueTargetOrder(unit, order, target);
        }

        /// <summary>The order a unit carries out now: GetUnitCurrentOrder.</summary>
        public int CurrentOrderOf(unit unit)
        {
            return Natives.GetUnitCurrentOrder(unit);
        }

        /// <summary>The game's string of an order id: OrderId2String.</summary>
        public string OrderName(int id)
        {
            return Natives.OrderId2String(id) ?? "";
        }

        /// <summary>The number of inventory slots of a unit, from the game.</summary>
        public int InventorySizeOf(unit unit)
        {
            return Natives.UnitInventorySize(unit);
        }

        /// <summary>The item in a slot of a unit's inventory, from the game.</summary>
        public item ItemInSlot(unit unit, int slot)
        {
            return Natives.UnitItemInSlot(unit, slot);
        }

        /// <summary>Gives a unit an item: UnitAddItem.</summary>
        public bool GiveItem(unit unit, item item)
        {
            return Natives.UnitAddItem(unit, item);
        }

        /// <summary>Takes an item out of a unit's inventory and drops it where the unit stands: UnitRemoveItem.</summary>
        public void DropItem(unit unit, item item)
        {
            Natives.UnitRemoveItem(unit, item);
        }

        /// <summary>Whether a unit has an item in its inventory: UnitHasItem.</summary>
        public bool CarriesItem(unit unit, item item)
        {
            return Natives.UnitHasItem(unit, item);
        }

        /// <summary>Whether an item is in a unit's inventory, bag or equipment slots: IsItemOwned.</summary>
        public bool IsItemHeld(item item)
        {
            return Natives.IsItemOwned(item);
        }

        /// <summary>The group UnitsInRange enumerates into, made on first use and kept.</summary>
        private group? rangeGroup;

        /// <summary>The units within a radius of a place: GroupEnumUnitsInRange.</summary>
        public List<unit> UnitsInRange(float x, float y, float radius)
        {
            rangeGroup ??= Natives.CreateGroup();
            Natives.GroupEnumUnitsInRange(rangeGroup, x, y, radius, null);
            var units = new List<unit>();
            var count = Natives.BlzGroupGetSize(rangeGroup);
            for (var i = 0; i < count; i++)
            {
                units.Add(Natives.BlzGroupUnitAt(rangeGroup, i));
            }

            Natives.GroupClear(rangeGroup);
            return units;
        }

        /// <summary>The area of an ability a unit has, at its level: BlzGetAbilityRealLevelField of ABILITY_RLF_AREA_OF_EFFECT; 0 when it has none.</summary>
        public float AbilityArea(unit unit, int abilityId)
        {
            var level = Natives.GetUnitAbilityLevel(unit, abilityId);
            if (level <= 0)
            {
                return 0.0f;
            }

            return Natives.BlzGetAbilityRealLevelField(Natives.BlzGetUnitAbility(unit, abilityId), Natives.ABILITY_RLF_AREA_OF_EFFECT, level - 1);
        }

        /// <summary>Range 99999, mana 0 and cooldown 0 on a unit's copy of an ability at a level: BlzSetAbilityRealLevelField, BlzSetUnitAbilityManaCost, BlzSetUnitAbilityCooldown.</summary>
        public void FreeCasting(unit unit, int abilityId, int level)
        {
            Natives.BlzSetAbilityRealLevelField(Natives.BlzGetUnitAbility(unit, abilityId), Natives.ABILITY_RLF_CAST_RANGE, level - 1, DummyRange);
            Natives.BlzSetUnitAbilityManaCost(unit, abilityId, level - 1, 0);
            Natives.BlzSetUnitAbilityCooldown(unit, abilityId, level - 1, 0.0f);
        }

        /// <summary>The longer of ABILITY_RLF_DURATION_NORMAL and _HERO at a level.</summary>
        public float SpellLasts(unit unit, int abilityId, int level)
        {
            var ability = Natives.BlzGetUnitAbility(unit, abilityId);
            var normal = Natives.BlzGetAbilityRealLevelField(ability, Natives.ABILITY_RLF_DURATION_NORMAL, level - 1);
            var hero = Natives.BlzGetAbilityRealLevelField(ability, Natives.ABILITY_RLF_DURATION_HERO, level - 1);
            return normal > hero ? normal : hero;
        }

        /// <summary>ABILITY_IF_MISSILE_SPEED of a unit's copy of an ability.</summary>
        public int ProjectileSpeedOf(unit unit, int abilityId)
        {
            return Natives.BlzGetAbilityIntegerField(Natives.BlzGetUnitAbility(unit, abilityId), Natives.ABILITY_IF_MISSILE_SPEED);
        }

        /// <summary>GetPlayerTechCount, counting all.</summary>
        public int TechCount(player player, int techId)
        {
            return Natives.GetPlayerTechCount(player, techId, true);
        }

        /// <summary>IsUnitVisible.</summary>
        public bool Sees(player player, unit unit)
        {
            return Natives.IsUnitVisible(unit, player);
        }

        /// <summary>A visible fog modifier of radius 128, started: CreateFogModifierRadius and FogModifierStart.</summary>
        public fogmodifier SeeAround(player player, float x, float y)
        {
            var patch = Natives.CreateFogModifierRadius(player, Natives.FOG_OF_WAR_VISIBLE, x, y, PatchReach, true, false);
            Natives.FogModifierStart(patch);
            return patch;
        }

        /// <summary>FogModifierStop and DestroyFogModifier.</summary>
        public void StopSeeing(fogmodifier patch)
        {
            Natives.FogModifierStop(patch);
            Natives.DestroyFogModifier(patch);
        }

        /// <summary>Whether an item is in a unit's 3.0 bag: IsItemInBag.</summary>
        public bool IsInBag(item item)
        {
            return Natives.IsItemInBag(item);
        }

        /// <summary>Moves an item a unit holds into a slot, swapping it with the item there: UnitDropItemSlot.</summary>
        public bool MoveItemToSlot(unit unit, item item, int slot)
        {
            return Natives.UnitDropItemSlot(unit, item, slot);
        }

        /// <summary>The charges of an item, from the game.</summary>
        public int ChargesOf(item item)
        {
            return Natives.GetItemCharges(item);
        }

        /// <summary>Sets the charges of an item: SetItemCharges.</summary>
        public void SetCharges(item item, int charges)
        {
            Natives.SetItemCharges(item, charges);
        }

        /// <summary>The player that owns an item, from the game.</summary>
        public player ItemOwnerOf(item item)
        {
            return Natives.GetItemPlayer(item);
        }

        /// <summary>Sets the owner of an item without changing its colour: SetItemPlayer.</summary>
        public void SetItemOwner(item item, player player)
        {
            Natives.SetItemPlayer(item, player, false);
        }

        /// <summary>The x of an item, from the game.</summary>
        public float ItemX(item item)
        {
            return Natives.GetItemX(item);
        }

        /// <summary>The y of an item, from the game.</summary>
        public float ItemY(item item)
        {
            return Natives.GetItemY(item);
        }

        /// <summary>Moves an item to a place: SetItemPosition.</summary>
        public void MoveItem(item item, float x, float y)
        {
            Natives.SetItemPosition(item, x, y);
        }

        /// <summary>Makes an effect of a model at a place, on the ground there: AddSpecialEffect.</summary>
        public effect MakeEffect(string model, float x, float y)
        {
            return Natives.AddSpecialEffect(model, x, y);
        }

        /// <summary>Makes an effect of a model on a unit, at an attachment point: AddSpecialEffectTarget.</summary>
        public effect MakeEffectOn(string model, unit unit, string point)
        {
            return Natives.AddSpecialEffectTarget(model, unit, point);
        }

        /// <summary>Moves an effect to a place and an absolute height: BlzSetSpecialEffectPosition.</summary>
        public void PlaceEffect(effect fx, float x, float y, float z)
        {
            Natives.BlzSetSpecialEffectPosition(fx, x, y, z);
        }

        /// <summary>Turns an effect, in radians: BlzSetSpecialEffectOrientation.</summary>
        public void TurnEffect(effect fx, float yaw, float pitch, float roll)
        {
            Natives.BlzSetSpecialEffectOrientation(fx, yaw, pitch, roll);
        }

        /// <summary>Scales an effect: BlzSetSpecialEffectScale.</summary>
        public void ScaleEffect(effect fx, float scale)
        {
            Natives.BlzSetSpecialEffectScale(fx, scale);
        }

        /// <summary>Tints an effect: BlzSetSpecialEffectColor and BlzSetSpecialEffectAlpha.</summary>
        public void TintEffect(effect fx, int red, int green, int blue, int alpha)
        {
            Natives.BlzSetSpecialEffectColor(fx, red, green, blue);
            Natives.BlzSetSpecialEffectAlpha(fx, alpha);
        }

        /// <summary>Plays an animation of an effect by name, BlzSetSpecialEffectAnimation, or queues it, BlzQueueSpecialEffectAnimation.</summary>
        public void AnimateEffect(effect fx, string name, bool queued)
        {
            if (queued)
            {
                Natives.BlzQueueSpecialEffectAnimation(fx, name);
            }
            else
            {
                Natives.BlzSetSpecialEffectAnimation(fx, name);
            }
        }

        /// <summary>Sets the speed of an effect's animations: BlzSetSpecialEffectTimeScale.</summary>
        public void SetEffectSpeed(effect fx, float speed)
        {
            Natives.BlzSetSpecialEffectTimeScale(fx, speed);
        }

        /// <summary>Ends an effect: DestroyEffect, which plays its death animation, or BlzRemoveEffect, which removes it at once.</summary>
        public void EndEffect(effect fx, bool playDeath)
        {
            if (playDeath)
            {
                Natives.DestroyEffect(fx);
            }
            else
            {
                Natives.BlzRemoveEffect(fx);
            }
        }

        /// <summary>The ground's height at a place, on this machine: GetLocationZ through one location, made on the first read.</summary>
        public float GroundZ(float x, float y)
        {
            if (spot == null)
            {
                spot = Natives.Location(x, y);
            }
            else
            {
                Natives.MoveLocation(spot, x, y);
            }

            return Natives.GetLocationZ(spot);
        }

        /// <summary>Runs a callback once, after the map's main function has returned.</summary>
        internal static void Later(Action callback)
        {
            Natives.TimerStart(Natives.CreateTimer(), 0.0f, false, callback);
        }

        /// <summary>The clock's callback: counts the lap and starts the clock again.</summary>
        private void ClockLapped()
        {
            lapStart += Lap;
            Natives.TimerStart(clock, Lap, false, ClockLapped);
        }

        /// <summary>Registers a player unit event for every player slot, with one trigger that runs the action.</summary>
        private static void Register(playerunitevent playerEvent, Action action)
        {
            var trigger = Natives.CreateTrigger();
            for (var slot = 0; slot < Natives.bj_MAX_PLAYER_SLOTS; slot++)
            {
                Natives.TriggerRegisterPlayerUnitEvent(trigger, Natives.Player(slot), playerEvent, null);
            }

            Natives.TriggerAddAction(trigger, action);
        }

        /// <summary>The wake timer's callback: runs the timers that are due, and keeps the wake alive if that fails.</summary>
        private static void WakeAction()
        {
            try
            {
                Runtime.Wake();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Wake", error);
                Runtime.Scheduler.Rearm();
            }
        }

        /// <summary>Hands a unit's entry to Inbound.</summary>
        private static void EnterAction()
        {
            try
            {
                Inbound.Entered(Natives.GetTriggerUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Enter", error);
            }
        }

        /// <summary>Hands a unit's damage to Inbound, and sets the damage to the amount it returns.</summary>
        private static void DamageAction()
        {
            try
            {
                var amount = Natives.GetEventDamage();
                var result = Inbound.Damaged(
                    Natives.BlzGetEventDamageTarget(),
                    Natives.GetEventDamageSource(),
                    amount,
                    Natives.BlzGetEventIsAttack());
                if (result != amount)
                {
                    Natives.BlzSetEventDamage(result);
                }
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Damage", error);
            }
        }

        /// <summary>Hands a unit's death to Inbound.</summary>
        private static void DeathAction()
        {
            try
            {
                Inbound.Died(Natives.GetDyingUnit(), Natives.GetKillingUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Death", error);
            }
        }

        /// <summary>Hands an order to Inbound, with its kind, as its event gave it: a target order's place is 0, 0, and the queue counts it and the orders queued after it.</summary>
        private static void OrderAction(OrderKind kind)
        {
            try
            {
                var ordered = Natives.GetOrderedUnit();
                Inbound.Ordered(
                    ordered,
                    Natives.GetIssuedOrderId(),
                    kind,
                    Natives.GetOrderPointX(),
                    Natives.GetOrderPointY(),
                    kind == OrderKind.Target ? Natives.GetOrderTargetUnit() : null,
                    kind == OrderKind.Target ? Natives.GetOrderTargetItem() : null,
                    kind == OrderKind.Target ? Natives.GetOrderTargetDestructable() : null,
                    Natives.BlzGetUnitOrderCount(ordered));
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Ordered", error);
            }
        }

        /// <summary>Hands a spell's effect to Inbound.</summary>
        private static void SpellEffectAction()
        {
            try
            {
                Inbound.SpellEffect(
                    Natives.GetSpellAbilityUnit(),
                    Natives.GetSpellAbilityId(),
                    Natives.GetSpellTargetUnit(),
                    Natives.GetSpellTargetX(),
                    Natives.GetSpellTargetY());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.SpellEffect", error);
            }
        }

        /// <summary>Hands the end of a spell's cast to Inbound.</summary>
        private static void SpellEndedAction()
        {
            try
            {
                Inbound.SpellEnded(Natives.GetSpellAbilityUnit(), Natives.GetSpellAbilityId());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.SpellEnded", error);
            }
        }

        /// <summary>Hands a hero's level-up to Inbound.</summary>
        private static void HeroLevelAction()
        {
            try
            {
                Inbound.HeroLevel(Natives.GetLevelingUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.HeroLevel", error);
            }
        }

        /// <summary>Hands a learned skill to Inbound.</summary>
        private static void HeroSkillAction()
        {
            try
            {
                Inbound.HeroSkill(Natives.GetLearningUnit(), Natives.GetLearnedSkill());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.HeroSkill", error);
            }
        }

        /// <summary>Hands a pickup to Inbound.</summary>
        private static void PickUpAction()
        {
            try
            {
                Inbound.PickedUp(Natives.GetManipulatingUnit(), Natives.GetManipulatedItem());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.PickUp", error);
            }
        }

        /// <summary>Hands a drop to Inbound.</summary>
        private static void LeaveAction()
        {
            try
            {
                Inbound.Left(Natives.GetTriggerPlayer());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Leave", error);
            }
        }

        private static void DropAction()
        {
            try
            {
                Inbound.Dropped(Natives.GetManipulatingUnit(), Natives.GetManipulatedItem());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Drop", error);
            }
        }

        /// <summary>Hands a destructable's death to Inbound.</summary>
        private static void DestructableDeathAction()
        {
            try
            {
                Inbound.DestructableDied(Natives.GetTriggerDestructable());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.DestructableDeath", error);
            }
        }

        /// <summary>Adds the item the enumeration is at to the items ItemsInWorld returns.</summary>
        private void CollectItem()
        {
            foundItems.Add(Natives.GetEnumItem());
        }

        /// <summary>Adds the destructable the enumeration is at to the destructables DestructablesInWorld returns.</summary>
        private void CollectDestructable()
        {
            foundDestructables.Add(Natives.GetEnumDestructable());
        }
    }
}
