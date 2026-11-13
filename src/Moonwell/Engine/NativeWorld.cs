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

        /// <summary>The triggers of each dialog's buttons, destroyed with it.</summary>
        private readonly Dictionary<dialog, List<trigger>> dialogTriggers = new Dictionary<dialog, List<trigger>>();

        /// <summary>The seconds the clock runs before it starts again: its elapsed time reads exact at 10 s, and steps by 0.0625 s at 604800 s.</summary>
        private const float Lap = 10.0f;

        /// <summary>A dummy's reach: farther than any map.</summary>
        private const float DummyRange = 99999.0f;

        /// <summary>How far a patch of vision reaches around a place; the probe's 128 let a unit there be targeted (11-03).</summary>
        private const float PatchReach = 128.0f;

        /// <summary>How far from its place a placed destructable is looked for: a rect of 32 found nothing in the game, one of 512 found it (game facts, 10-26).</summary>
        private const float PlacedReach = 512.0f;

        /// <summary>The clock: the game time is the laps it has run plus its elapsed time.</summary>
        private readonly timer clock;

        /// <summary>The game time the clock last started at, a whole number of laps.</summary>
        private float lapStart = 0.0f;

        /// <summary>The timer that calls Runtime.Wake when the next wake is due.</summary>
        private readonly timer wake;

        /// <summary>The rect of the whole map, made once; every call to GetWorldBounds makes a new one.</summary>
        private readonly rect bounds;

        /// <summary>The trigger that sees each watched destructable die, one each, destroyed as the destructable leaves, since a trigger's events cannot be taken off it.</summary>
        private readonly Dictionary<destructable, trigger> deathTriggers = new Dictionary<destructable, trigger>();

        /// <summary>The lines of the log file and when it is next written.</summary>
        private readonly LogFile log = new LogFile();

        /// <summary>Whether the time of day was last stopped; the game has no reading for it.</summary>
        private bool dayStopped;

        /// <summary>The items the last ItemsInWorld call has found.</summary>
        private List<item> foundItems = new List<item>();

        /// <summary>The destructables the last DestructablesInWorld call has found.</summary>
        private List<destructable> foundDestructables = new List<destructable>();

        /// <summary>The type PlacedDestructable looks for, while it enumerates.</summary>
        private int placedType;

        /// <summary>The place PlacedDestructable looks around: x.</summary>
        private float placedX;

        /// <summary>The place PlacedDestructable looks around: y.</summary>
        private float placedY;

        /// <summary>The squared distance of the nearest destructable PlacedDestructable has found so far.</summary>
        private float placedBest;

        /// <summary>The nearest destructable PlacedDestructable has found so far; null while none.</summary>
        private destructable? placedFound;

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
                    Register(Natives.EVENT_PLAYER_UNIT_SPELL_CHANNEL, SpellChannelAction);
                    Register(Natives.EVENT_PLAYER_UNIT_SPELL_EFFECT, SpellEffectAction);
                    Register(Natives.EVENT_PLAYER_UNIT_SPELL_FINISH, SpellFinishedAction);
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
                case GameEvent.Alliance:
                    // The game tells only the giving player's registration, with no trigger player (game facts, 11-06): one trigger for each giver.
                    for (var slot = 0; slot < Natives.bj_MAX_PLAYER_SLOTS; slot++)
                    {
                        var giver = Natives.Player(slot);
                        var watch = Natives.CreateTrigger();
                        for (var type = 0; type < 10; type++)
                        {
                            Natives.TriggerRegisterPlayerAllianceChange(watch, giver, Natives.ConvertAllianceType(type));
                        }

                        Natives.TriggerAddAction(watch, () => AllianceAction(giver));
                    }

                    break;
                case GameEvent.Loaded:
                    var loaded = Natives.CreateTrigger();
                    Natives.TriggerRegisterGameEvent(loaded, Natives.EVENT_GAME_LOADED);
                    Natives.TriggerAddAction(loaded, LoadedAction);
                    break;
                case GameEvent.Saving:
                    var saving = Natives.CreateTrigger();
                    Natives.TriggerRegisterGameEvent(saving, Natives.EVENT_GAME_SAVE);
                    Natives.TriggerAddAction(saving, SavingAction);
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
                case GameEvent.Cargo:
                    Register(Natives.EVENT_PLAYER_UNIT_LOADED, CargoAction);
                    break;
                case GameEvent.Selection:
                    Register(Natives.EVENT_PLAYER_UNIT_SELECTED, () => SelectionAction(true));
                    Register(Natives.EVENT_PLAYER_UNIT_DESELECTED, () => SelectionAction(false));
                    break;
                case GameEvent.Summon:
                    Register(Natives.EVENT_PLAYER_UNIT_SUMMON, SummonAction);
                    break;
                case GameEvent.Research:
                    Register(Natives.EVENT_PLAYER_UNIT_RESEARCH_START, () => ResearchAction(Researching.Started));
                    Register(Natives.EVENT_PLAYER_UNIT_RESEARCH_CANCEL, () => ResearchAction(Researching.Cancelled));
                    Register(Natives.EVENT_PLAYER_UNIT_RESEARCH_FINISH, () => ResearchAction(Researching.Finished));
                    break;
                case GameEvent.Sale:
                    Register(Natives.EVENT_PLAYER_UNIT_SELL_ITEM, () => SaleAction(Selling.ItemSold));
                    Register(Natives.EVENT_PLAYER_UNIT_PAWN_ITEM, () => SaleAction(Selling.ItemPawned));
                    Register(Natives.EVENT_PLAYER_UNIT_SELL, () => SaleAction(Selling.UnitSold));
                    break;
                case GameEvent.Equip:
                    Register(Natives.EVENT_PLAYER_UNIT_EQUIP_ITEM, EquipAction);
                    Register(Natives.EVENT_PLAYER_UNIT_UNEQUIP_ITEM, UnequipAction);
                    break;
            }
        }

        /// <summary>Hands a unit loaded into a transport to Inbound.</summary>
        private static void CargoAction()
        {
            try
            {
                Inbound.Loaded(Natives.GetLoadedUnit(), Natives.GetTransportUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Loaded", error);
            }
        }

        /// <summary>Hands a player's selection or deselection of a unit to Inbound.</summary>
        private static void SelectionAction(bool selected)
        {
            try
            {
                Inbound.Selection(Natives.GetTriggerUnit(), Natives.GetTriggerPlayer(), selected);
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Selection", error);
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
            if (deathTriggers.ContainsKey(destructable))
            {
                return;
            }

            var watch = Natives.CreateTrigger();
            Natives.TriggerRegisterDeathEvent(watch, destructable);
            Natives.TriggerAddAction(watch, DestructableDeathAction);
            deathTriggers[destructable] = watch;
        }

        /// <summary>Stops delivering a destructable's death: its trigger is destroyed.</summary>
        public void UnwatchDeath(destructable destructable)
        {
            if (deathTriggers.TryGetValue(destructable, out var watch))
            {
                deathTriggers.Remove(destructable);
                Natives.DestroyTrigger(watch);
            }
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

        /// <summary>Shows a line to one player in the game's message text, in the error's colour, at the left edge of the game's error line and as low as a message goes: one line above it (seen 11-08).</summary>
        public void ErrorTo(player player, string text)
        {
            // The offset moves the whole message box; below 0 it stays at 0. Blank lines before the text only move it by
            // overflowing the box, and a message shown after it pushes it up (seen 11-08).
            Natives.DisplayTimedTextToPlayer(player, 0.52f, 0.0f, 2.0f, text.Colored(Color.TooltipGold));
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
            if (log.IndexDue)
            {
                Natives.PreloadGenClear();
                Natives.PreloadGenStart();
                Natives.Preload(log.IndexLine());
                Natives.PreloadGenEnd(LogFile.IndexPath);
                log.IndexWritten();
            }

            Natives.PreloadGenClear();
            Natives.PreloadGenStart();
            foreach (var line in log.Header)
            {
                Natives.Preload("\")\n" + line + "\n//");
            }

            foreach (var line in log.Lines)
            {
                Natives.Preload("\")\n" + line + "\n//");
            }

            Natives.PreloadGenEnd("Moonwell\\log.txt");
            if (log.Slot > 0)
            {
                // The texts stay until PreloadGenClear (game facts), so the ring's file gets the same lines.
                Natives.PreloadGenEnd(log.RingPath);
            }
        }

        /// <summary>
        /// Opens the log's ring of past games (decision 264), while the map loads: Preloader runs the index file, which stores
        /// the slot in a game cache, and the log opens with the map's name and this machine's date. A machine with no index
        /// makes the cache once more, so every machine makes as many handles.
        /// </summary>
        public void OpenLog(string mapName)
        {
            Natives.Preloader(LogFile.IndexPath);
            var stored = Natives.GetStoredInteger(Natives.InitGameCache(LogFile.CacheName), "log", "next");
            if (stored == 0)
            {
                Natives.InitGameCache(LogFile.CacheName);
            }

            log.Open(stored, "Moonwell log: " + (mapName.Length > 0 ? mapName + ", " : string.Empty) + "started " + OsDate());
        }

        /// @CSharpLua.Template = "os.date('%Y-%m-%d %H:%M:%S')"
        private static string OsDate()
        {
            return string.Empty;
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

        /// <summary>Listens for a person's chat lines with a dash in them, anywhere (the event's text is not an exact match), for the chat commands.</summary>
        public void ListenChat(player player)
        {
            var trigger = Natives.CreateTrigger();
            Natives.TriggerRegisterPlayerChatEvent(trigger, player, "-", false);
            Natives.TriggerAddAction(trigger, ChatAction);
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

        private static void SavingAction()
        {
            try
            {
                Inbound.GameSaving();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Saving", error);
            }
        }

        private static void ChatAction()
        {
            try
            {
                Inbound.ChatArrived(Natives.GetTriggerPlayer(), Natives.GetEventPlayerChatString());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Chat", error);
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

        /// <summary>Shows or hides a destructable: ShowDestructable.</summary>
        public void DisplayDestructable(destructable destructable, bool shown)
        {
            Natives.ShowDestructable(destructable, shown);
        }

        /// <summary>Plays an animation of a destructable by name (SetDestructableAnimation), or queues it (QueueDestructableAnimation).</summary>
        public void AnimateDestructable(destructable destructable, string name, bool queued)
        {
            if (queued)
            {
                Natives.QueueDestructableAnimation(destructable, name);
            }
            else
            {
                Natives.SetDestructableAnimation(destructable, name);
            }
        }

        /// <summary>Sets the speed of a destructable's animations: SetDestructableAnimationSpeed.</summary>
        public void PaceDestructableAnimations(destructable destructable, float speed)
        {
            Natives.SetDestructableAnimationSpeed(destructable, speed);
        }

        /// <summary>Tints a destructable: SetDestructableVertexColor.</summary>
        public void TintDestructable(destructable destructable, int red, int green, int blue, int alpha)
        {
            Natives.SetDestructableVertexColor(destructable, red, green, blue, alpha);
        }

        /// <summary>Whether a destructable is invulnerable: IsDestructableInvulnerable.</summary>
        public bool DestructableInvulnerableOf(destructable destructable)
        {
            return Natives.IsDestructableInvulnerable(destructable);
        }

        /// <summary>Makes a destructable invulnerable or not: SetDestructableInvulnerable.</summary>
        public void PutDestructableInvulnerable(destructable destructable, bool invulnerable)
        {
            Natives.SetDestructableInvulnerable(destructable, invulnerable);
        }

        /// <summary>The name of a destructable's type, in this machine's language: GetDestructableName.</summary>
        public string DestructableNameOf(destructable destructable)
        {
            return Natives.GetDestructableName(destructable);
        }

        /// <summary>A destructable's occluder height: GetDestructableOccluderHeight.</summary>
        public float DestructableOccluderHeightOf(destructable destructable)
        {
            return Natives.GetDestructableOccluderHeight(destructable);
        }

        /// <summary>Sets a destructable's occluder height: SetDestructableOccluderHeight.</summary>
        public void PutDestructableOccluderHeight(destructable destructable, float height)
        {
            Natives.SetDestructableOccluderHeight(destructable, height);
        }

        /// <summary>Plays an animation on doodads near a place: SetDoodadAnimation, with no random variation.</summary>
        public void AnimateDoodads(int typeId, float x, float y, float radius, bool nearestOnly, string name)
        {
            Natives.SetDoodadAnimation(x, y, radius, typeId, nearestOnly, name, false);
        }

        /// <summary>Plays an animation on doodads inside a rect: SetDoodadAnimationRect on a rect made for the call and removed after it.</summary>
        public void AnimateDoodadsIn(int typeId, float left, float bottom, float right, float top, string name)
        {
            var area = Natives.Rect(left, bottom, right, top);
            Natives.SetDoodadAnimationRect(area, typeId, name, false);
            Natives.RemoveRect(area);
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

        /// <summary>ShowUnit.</summary>
        public void SetUnitShown(unit unit, bool shown)
        {
            Natives.ShowUnit(unit, shown);
        }

        /// <summary>Stops the unit, takes its buffs off, gives it to the keeper and hides it.</summary>
        public void PutAway(unit unit, player keeper)
        {
            Natives.IssueImmediateOrder(unit, "stop");
            Natives.UnitRemoveBuffs(unit, true, true);
            Natives.SetUnitOwner(unit, keeper, false);
            Natives.ShowUnit(unit, false);
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

        /// <summary>Takes a player out as a winner, as World Editor's Victory: CustomVictoryBJ.</summary>
        public void Win(player player, bool dialog, bool scores)
        {
            Natives.CustomVictoryBJ(player, dialog, scores);
        }

        /// <summary>Takes a player out as a loser, as World Editor's Defeat: CustomDefeatBJ, with the game's own words, in each player's language, for an empty message (the box's text only, so it may differ between machines).</summary>
        public void Lose(player player, string message)
        {
            Natives.CustomDefeatBJ(player, message.Length > 0 ? message : Natives.GetLocalizedString("GAMEOVER_DEFEAT_MSG"));
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

        /// <summary>The full cooldown of a unit's ability at a level.</summary>
        public float AbilityCooldown(unit unit, int abilityId, int levelIndex)
        {
            return Natives.BlzGetUnitAbilityCooldown(unit, abilityId, levelIndex);
        }

        /// <summary>Sets the full cooldown of a unit's ability at a level.</summary>
        public void SetAbilityCooldown(unit unit, int abilityId, int levelIndex, float seconds)
        {
            Natives.BlzSetUnitAbilityCooldown(unit, abilityId, levelIndex, seconds);
        }

        /// <summary>The seconds left of a unit's ability's cooldown.</summary>
        public float AbilityCooldownLeft(unit unit, int abilityId)
        {
            return Natives.BlzGetUnitAbilityCooldownRemaining(unit, abilityId);
        }

        /// <summary>Starts a unit's ability's cooldown, shown on its button (seen 11-08).</summary>
        public void StartAbilityCooldown(unit unit, int abilityId, float seconds)
        {
            Natives.BlzStartUnitAbilityCooldown(unit, abilityId, seconds);
        }

        /// <summary>Ends a unit's ability's cooldown.</summary>
        public void EndAbilityCooldown(unit unit, int abilityId)
        {
            Natives.BlzEndUnitAbilityCooldown(unit, abilityId);
        }

        /// <summary>The mana cost of a unit's ability at a level.</summary>
        public int AbilityManaCost(unit unit, int abilityId, int levelIndex)
        {
            return Natives.BlzGetUnitAbilityManaCost(unit, abilityId, levelIndex);
        }

        /// <summary>Sets the mana cost of a unit's ability at a level.</summary>
        public void SetAbilityManaCost(unit unit, int abilityId, int levelIndex, int mana)
        {
            Natives.BlzSetUnitAbilityManaCost(unit, abilityId, levelIndex, mana);
        }

        /// <summary>Gives a unit the summoned type, so dispels hurt it as the game's summons (seen 11-08).</summary>
        public void MakeSummoned(unit unit)
        {
            Natives.UnitAddType(unit, Natives.UNIT_TYPE_SUMMONED);
        }

        /// <summary>Gives a unit the game's timed life, with its bar, which kills it with no killer when it runs out (seen 11-08).</summary>
        public void ApplyTimedLife(unit unit, float seconds)
        {
            Natives.UnitApplyTimedLife(unit, ObjectTypes.FourCC("BTLF"), seconds);
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

        /// <summary>The armor of a unit, with every bonus: BlzGetUnitArmor.</summary>
        public float ArmorOf(unit unit)
        {
            return Natives.BlzGetUnitArmor(unit);
        }

        /// <summary>The middle of a unit's white damage: BlzGetUnitBaseDamage and the middle of its dice, of the first weapon.</summary>
        public float AverageDamageOf(unit unit)
        {
            return Natives.BlzGetUnitBaseDamage(unit, 0) + Natives.BlzGetUnitDiceNumber(unit, 0) * (Natives.BlzGetUnitDiceSides(unit, 0) + 1) / 2.0f;
        }

        /// <summary>The move speed of a unit: GetUnitMoveSpeed.</summary>
        public float MoveSpeedOf(unit unit)
        {
            return Natives.GetUnitMoveSpeed(unit);
        }

        /// <summary>Sets the move speed of a unit: SetUnitMoveSpeed, held to the map's least and most speed (seen 10-21).</summary>
        public void SetMoveSpeed(unit unit, float speed)
        {
            Natives.SetUnitMoveSpeed(unit, speed);
        }

        /// <summary>A unit's own regeneration a second: its UNIT_RF_HIT_POINTS_REGENERATION_RATE or UNIT_RF_MANA_REGENERATION, to which a hero's attributes add (seen 11-08).</summary>
        public float RegenOf(unit unit, bool mana)
        {
            return Natives.BlzGetUnitRealField(unit, mana ? Natives.UNIT_RF_MANA_REGENERATION : Natives.UNIT_RF_HIT_POINTS_REGENERATION_RATE);
        }

        /// <summary>Sets a unit's own regeneration a second, which the game takes at once (seen 11-08).</summary>
        public void SetRegen(unit unit, bool mana, float perSecond)
        {
            Natives.BlzSetUnitRealField(unit, mana ? Natives.UNIT_RF_MANA_REGENERATION : Natives.UNIT_RF_HIT_POINTS_REGENERATION_RATE, perSecond);
        }

        /// <summary>Sets the level-1 field of an ability on a unit: BlzSetAbilityIntegerLevelField or BlzSetAbilityRealLevelField of the unit's copy.</summary>
        public void PutBonus(unit unit, int abilityId, int fieldId, bool real, float value)
        {
            var ability = Natives.BlzGetUnitAbility(unit, abilityId);
            if (real)
            {
                Natives.BlzSetAbilityRealLevelField(ability, Natives.ConvertAbilityRealLevelField(fieldId), 0, value);
            }
            else
            {
                Natives.BlzSetAbilityIntegerLevelField(ability, Natives.ConvertAbilityIntegerLevelField(fieldId), 0, (int)value);
            }
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

        /// <summary>Draws a unit's team-coloured parts in a player colour: SetUnitColor.</summary>
        public void SetUnitTeamColor(unit unit, int color)
        {
            Natives.SetUnitColor(unit, Natives.ConvertPlayerColor(color));
        }

        /// <summary>Plays an animation of a unit by name (SetUnitAnimation), or queues it (QueueUnitAnimation).</summary>
        public void AnimateUnit(unit unit, string name, bool queued)
        {
            if (queued)
            {
                Natives.QueueUnitAnimation(unit, name);
            }
            else
            {
                Natives.SetUnitAnimation(unit, name);
            }
        }

        /// <summary>Plays an animation of a unit by index: SetUnitAnimationByIndex.</summary>
        public void AnimateUnitByIndex(unit unit, int index)
        {
            Natives.SetUnitAnimationByIndex(unit, index);
        }

        /// <summary>Sets the speed of a unit's animations: SetUnitTimeScale.</summary>
        public void SetUnitAnimationSpeed(unit unit, float speed)
        {
            Natives.SetUnitTimeScale(unit, speed);
        }

        /// <summary>Adds or removes an animation tag: AddUnitAnimationProperties.</summary>
        public void TagUnitAnimation(unit unit, string tag, bool add)
        {
            Natives.AddUnitAnimationProperties(unit, tag, add);
        }

        /// <summary>Whether a unit is hidden: IsUnitHidden.</summary>
        public bool IsHidden(unit unit)
        {
            return Natives.IsUnitHidden(unit);
        }

        /// <summary>Selects or deselects a unit on the player's machine only, unit by unit, as the game takes it in a local block (game facts, Desyncs).</summary>
        public void SelectFor(player player, unit unit, bool selected)
        {
            if (Natives.GetLocalPlayer() == player)
            {
                Natives.SelectUnit(unit, selected);
            }
        }

        /// <summary>Clears a player's selection on the player's machine only.</summary>
        public void ClearSelectionFor(player player)
        {
            if (Natives.GetLocalPlayer() == player)
            {
                Natives.ClearSelection();
            }
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

        /// <summary>Draws an effect's team-coloured parts in a player's colour: BlzSetSpecialEffectColorByPlayer.</summary>
        public void SetEffectTeamColor(effect fx, player player)
        {
            Natives.BlzSetSpecialEffectColorByPlayer(fx, player);
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

        /// <summary>Plays an animation of an effect by its kind: BlzPlaySpecialEffect.</summary>
        public void AnimateEffectByType(effect fx, int type)
        {
            Natives.BlzPlaySpecialEffect(fx, Natives.ConvertAnimType(type));
        }

        /// <summary>Adds a sub-animation to an effect, BlzSpecialEffectAddSubAnimation, or removes it, BlzSpecialEffectRemoveSubAnimation.</summary>
        public void SetEffectSubAnimation(effect fx, int subAnimation, bool on)
        {
            if (on)
            {
                Natives.BlzSpecialEffectAddSubAnimation(fx, Natives.ConvertSubAnimType(subAnimation));
            }
            else
            {
                Natives.BlzSpecialEffectRemoveSubAnimation(fx, Natives.ConvertSubAnimType(subAnimation));
            }
        }

        /// <summary>Removes every sub-animation of an effect: BlzSpecialEffectClearSubAnimations.</summary>
        public void ClearEffectSubAnimations(effect fx)
        {
            Natives.BlzSpecialEffectClearSubAnimations(fx);
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

        /// <summary>GetWorldBounds' edges; the rect is removed after.</summary>
        public float[] WorldBounds()
        {
            var world = Natives.GetWorldBounds();
            var bounds = new float[] { Natives.GetRectMinX(world), Natives.GetRectMinY(world), Natives.GetRectMaxX(world), Natives.GetRectMaxY(world) };
            Natives.RemoveRect(world);
            return bounds;
        }

        /// <summary>AddWeatherEffect over a rect made for it and removed after; the game makes it off (seen 11-06).</summary>
        public weathereffect MakeWeather(string kind, float left, float bottom, float right, float top)
        {
            var where = Natives.Rect(left, bottom, right, top);
            var weather = Natives.AddWeatherEffect(where, Natives.FourCC(kind));
            Natives.RemoveRect(where);
            return weather;
        }

        /// <summary>EnableWeatherEffect.</summary>
        public void ShowWeather(weathereffect weather, bool shown)
        {
            Natives.EnableWeatherEffect(weather, shown);
        }

        /// <summary>RemoveWeatherEffect.</summary>
        public void EndWeather(weathereffect weather)
        {
            Natives.RemoveWeatherEffect(weather);
        }

        /// <summary>AddLightningEx, drawn for every player wherever it is: the visibility check would make it on some machines only.</summary>
        public lightning MakeLightning(string kind, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            return Natives.AddLightningEx(kind, false, x1, y1, z1, x2, y2, z2);
        }

        /// <summary>MoveLightningEx, with no visibility check.</summary>
        public void MoveBolt(lightning bolt, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            Natives.MoveLightningEx(bolt, false, x1, y1, z1, x2, y2, z2);
        }

        /// <summary>DestroyLightning.</summary>
        public void EndLightning(lightning bolt)
        {
            Natives.DestroyLightning(bolt);
        }

        /// <summary>CreateFogModifierRadius, shared vision on as the game's own triggers make it, and FogModifierStart.</summary>
        public fogmodifier MakeVision(player player, int state, float x, float y, float radius, bool overUnits)
        {
            var vision = Natives.CreateFogModifierRadius(player, FogStateOf(state), x, y, radius, true, overUnits);
            Natives.FogModifierStart(vision);
            return vision;
        }

        /// <summary>CreateFogModifierRect over a rect made for it and removed after, and FogModifierStart.</summary>
        public fogmodifier MakeVisionRect(player player, int state, float left, float bottom, float right, float top, bool overUnits)
        {
            var where = Natives.Rect(left, bottom, right, top);
            var vision = Natives.CreateFogModifierRect(player, FogStateOf(state), where, true, overUnits);
            Natives.RemoveRect(where);
            Natives.FogModifierStart(vision);
            return vision;
        }

        /// <summary>FogModifierStop and DestroyFogModifier.</summary>
        public void EndVision(fogmodifier vision)
        {
            StopSeeing(vision);
        }

        /// <summary>IsFogEnabled.</summary>
        public bool IsFogOn()
        {
            return Natives.IsFogEnabled();
        }

        /// <summary>FogEnable.</summary>
        public void SetFogOn(bool on)
        {
            Natives.FogEnable(on);
        }

        /// <summary>IsFogMaskEnabled.</summary>
        public bool IsBlackMaskOn()
        {
            return Natives.IsFogMaskEnabled();
        }

        /// <summary>FogMaskEnable.</summary>
        public void SetBlackMaskOn(bool on)
        {
            Natives.FogMaskEnable(on);
        }

        /// <summary>PingMinimapEx.</summary>
        public void Ping(float x, float y, float seconds, int red, int green, int blue, bool flashy)
        {
            Natives.PingMinimapEx(x, y, seconds, red, green, blue, flashy);
        }

        /// <summary>CreateMinimapIcon, shown wherever the player has not seen the ground too (FOG_OF_WAR_MASKED), as the campaign's are.</summary>
        public minimapicon MakeIcon(string path, float x, float y, int red, int green, int blue)
        {
            return Natives.CreateMinimapIcon(x, y, red, green, blue, path, Natives.FOG_OF_WAR_MASKED);
        }

        /// <summary>CreateMinimapIconOnUnit, as MakeIcon.</summary>
        public minimapicon MakeIconOn(string path, unit unit, int red, int green, int blue)
        {
            return Natives.CreateMinimapIconOnUnit(unit, red, green, blue, path, Natives.FOG_OF_WAR_MASKED);
        }

        /// <summary>SetMinimapIconVisible.</summary>
        public void ShowIcon(minimapicon icon, bool shown)
        {
            Natives.SetMinimapIconVisible(icon, shown);
        }

        /// <summary>DestroyMinimapIcon.</summary>
        public void EndIcon(minimapicon icon)
        {
            Natives.DestroyMinimapIcon(icon);
        }

        /// <summary>The game's fog state of MakeVision's number: 0 visible, 1 fogged, 2 masked.</summary>
        private static fogstate FogStateOf(int state)
        {
            return state == 0 ? Natives.FOG_OF_WAR_VISIBLE : state == 1 ? Natives.FOG_OF_WAR_FOGGED : Natives.FOG_OF_WAR_MASKED;
        }

        /// <summary>CreateTextTag, permanent.</summary>
        public texttag MakeText()
        {
            var made = Natives.CreateTextTag();
            Natives.SetTextTagPermanent(made, true);
            return made;
        }

        /// <summary>SetTextTagText.</summary>
        public void WriteText(texttag text, string words, float height) => Natives.SetTextTagText(text, words, height);

        /// <summary>SetTextTagPos.</summary>
        public void PlaceText(texttag text, float x, float y, float height) => Natives.SetTextTagPos(text, x, y, height);

        /// <summary>SetTextTagPosUnit.</summary>
        public void PlaceTextOver(texttag text, unit unit, float height) => Natives.SetTextTagPosUnit(text, unit, height);

        /// <summary>SetTextTagColor.</summary>
        public void ColorText(texttag text, int red, int green, int blue, int alpha) => Natives.SetTextTagColor(text, red, green, blue, alpha);

        /// <summary>SetTextTagVelocity.</summary>
        public void DriftText(texttag text, float xVelocity, float yVelocity) => Natives.SetTextTagVelocity(text, xVelocity, yVelocity);

        /// <summary>SetTextTagVisibility.</summary>
        public void ShowText(texttag text, bool shown) => Natives.SetTextTagVisibility(text, shown);

        /// <summary>DestroyTextTag.</summary>
        public void EndText(texttag text) => Natives.DestroyTextTag(text);

        /// <summary>IsUnitVisible for the local player.</summary>
        public bool LocalSees(unit unit) => Natives.IsUnitVisible(unit, Natives.GetLocalPlayer());

        /// <summary>CreateSound, as the physics package makes its sounds.</summary>
        public sound MakeSound(string path, bool looping, bool is3D) => Natives.CreateSound(path, looping, is3D, false, 10, 10, "DefaultEAXON");

        /// <summary>SetSoundPosition.</summary>
        public void PlaceSound(sound sound, float x, float y, float z) => Natives.SetSoundPosition(sound, x, y, z);

        /// <summary>AttachSoundToUnit.</summary>
        public void PutSoundOn(sound sound, unit unit) => Natives.AttachSoundToUnit(sound, unit);

        /// <summary>SetSoundVolume.</summary>
        public void SetLoudness(sound sound, int volume) => Natives.SetSoundVolume(sound, volume);

        /// <summary>StartSound.</summary>
        public void StartPlaying(sound sound) => Natives.StartSound(sound);

        /// <summary>KillSoundWhenDone.</summary>
        public void FreeWhenDone(sound sound) => Natives.KillSoundWhenDone(sound);

        /// <summary>StopSound, freed when done, with no fade of the game's.</summary>
        public void StopPlaying(sound sound) => Natives.StopSound(sound, true, false);

        /// <summary>SetMapMusic and PlayMusic: PlayMusic alone plays from a place the game carries over and then goes back to the map's music (game facts, 11-06).</summary>
        public void StartMusic(string path)
        {
            Natives.SetMapMusic(path, false, 0);
            Natives.PlayMusic(path);
        }

        /// <summary>StopMusic and ClearMapMusic.</summary>
        public void StopAllMusic(bool fadeOut)
        {
            Natives.StopMusic(fadeOut);
            Natives.ClearMapMusic();
        }

        /// <summary>GetSoundFileDuration.</summary>
        public int MusicLength(string path) => Natives.GetSoundFileDuration(path);

        /// <summary>SetMusicVolume.</summary>
        public void SetMusicLoudness(int volume) => Natives.SetMusicVolume(volume);

        /// <summary>CreateQuest, disabled until ShowQuest, as the game enables a new quest.</summary>
        public quest MakeQuest()
        {
            var made = Natives.CreateQuest();
            Natives.QuestSetEnabled(made, false);
            return made;
        }

        /// <summary>QuestSetTitle, QuestSetDescription and QuestSetIconPath.</summary>
        public void WriteQuest(quest quest, string title, string description, string icon)
        {
            Natives.QuestSetTitle(quest, title);
            Natives.QuestSetDescription(quest, description);
            Natives.QuestSetIconPath(quest, icon);
        }

        /// <summary>QuestSetRequired, QuestSetDiscovered, QuestSetCompleted and QuestSetFailed.</summary>
        public void SetQuestState(quest quest, bool main, bool discovered, bool completed, bool failed)
        {
            Natives.QuestSetRequired(quest, main);
            Natives.QuestSetDiscovered(quest, discovered);
            Natives.QuestSetCompleted(quest, completed);
            Natives.QuestSetFailed(quest, failed);
        }

        /// <summary>QuestSetEnabled, on this machine only: a quest enabled on one machine shows in that player's log only (seen 11-07).</summary>
        public void ShowQuest(quest quest, bool shown) => Natives.QuestSetEnabled(quest, shown);

        /// <summary>DestroyQuest.</summary>
        public void EndQuest(quest quest) => Natives.DestroyQuest(quest);

        /// <summary>QuestCreateItem.</summary>
        public questitem MakeRequirement(quest quest) => Natives.QuestCreateItem(quest);

        /// <summary>QuestItemSetDescription and QuestItemSetCompleted.</summary>
        public void SetRequirement(questitem requirement, string text, bool done)
        {
            Natives.QuestItemSetDescription(requirement, text);
            Natives.QuestItemSetCompleted(requirement, done);
        }

        /// <summary>QuestMessageBJ's local part: a blank line and the message for 20 s, the game's sound for the kind (made by InitBlizzard on every machine), and the quest button's flash.</summary>
        public void QuestNews(QuestNews kind, string text)
        {
            var local = Natives.GetLocalPlayer();
            if (kind == Moonwell.QuestNews.Hint || kind == Moonwell.QuestNews.Warning)
            {
                // As QuestMessageBJ shows bj_QUESTMESSAGE_HINT and bj_QUESTMESSAGE_WARNING.
                var hint = kind == Moonwell.QuestNews.Hint;
                var seconds = hint ? Natives.bj_TEXT_DELAY_HINT : Natives.bj_TEXT_DELAY_WARNING;
                Natives.DisplayTimedTextToPlayer(local, 0.0f, 0.0f, seconds, " ");
                Natives.DisplayTimedTextToPlayer(local, 0.0f, 0.0f, seconds, text);
                Natives.StartSound(hint ? Natives.bj_questHintSound : Natives.bj_questWarningSound);
                return;
            }

            Natives.DisplayTimedTextToPlayer(local, 0.0f, 0.0f, Natives.bj_TEXT_DELAY_QUEST, " ");
            Natives.DisplayTimedTextToPlayer(local, 0.0f, 0.0f, Natives.bj_TEXT_DELAY_QUEST, text);
            switch (kind)
            {
                case Moonwell.QuestNews.Discovered:
                    Natives.StartSound(Natives.bj_questDiscoveredSound);
                    break;
                case Moonwell.QuestNews.Updated:
                    Natives.StartSound(Natives.bj_questUpdatedSound);
                    break;
                case Moonwell.QuestNews.Completed:
                    Natives.StartSound(Natives.bj_questCompletedSound);
                    break;
                default:
                    Natives.StartSound(Natives.bj_questFailedSound);
                    break;
            }

            Natives.FlashQuestDialogButton();
        }

        /// <summary>A trigger on the player's skip-cinematic event (Esc), which fires with the controls off too (seen 11-07).</summary>
        public void ListenEsc(player player)
        {
            var trigger = Natives.CreateTrigger();
            Natives.TriggerRegisterPlayerEvent(trigger, player, Natives.EVENT_PLAYER_END_CINEMATIC);
            Natives.TriggerAddAction(trigger, EscAction);
        }

        private static void EscAction()
        {
            try
            {
                Inbound.EscArrived(Natives.GetTriggerPlayer());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Esc", error);
            }
        }

        /// <summary>ShowInterface and EnableOcclusion, as CinematicModeExBJ does them.</summary>
        public void Letterbox(bool on, float seconds)
        {
            Natives.ShowInterface(!on, seconds);
            Natives.EnableOcclusion(!on);
        }

        /// <summary>EnableUserControl.</summary>
        public void UserControl(bool on) => Natives.EnableUserControl(on);

        /// <summary>SetCineModeVolumeGroupsBJ, or VolumeGroupReset.</summary>
        public void CinematicSounds(bool on)
        {
            if (on)
            {
                Natives.SetCineModeVolumeGroupsBJ();
            }
            else
            {
                Natives.VolumeGroupReset();
            }
        }

        /// <summary>ClearTextMessages.</summary>
        public void ClearMessages() => Natives.ClearTextMessages();

        /// <summary>SetCinematicScene, with the subtitles forced on so the line shows whatever the player's setting.</summary>
        public void ShowScene(int unitTypeId, int playerColor, string name, string text, float seconds, float talkSeconds)
        {
            Natives.ForceCinematicSubtitles(true);
            Natives.SetCinematicScene(unitTypeId, Natives.ConvertPlayerColor(playerColor), name, text, seconds, talkSeconds);
        }

        /// <summary>EndCinematicScene.</summary>
        public void EndScene() => Natives.EndCinematicScene();

        /// <summary>UnitAddIndicator in red and PingMinimap for 1 s, as TransmissionFromUnitWithNameBJ does.</summary>
        public void MarkSpeaker(unit unit, float x, float y)
        {
            Natives.UnitAddIndicator(unit, 255, 0, 0, 255);
            Natives.PingMinimap(x, y, 1.0f);
        }

        /// <summary>The cine filter as CinematicFadeCommonBJ sets it, on the white mask tinted by the colour.</summary>
        public void ScreenFilter(int red, int green, int blue, int fromAlpha, int toAlpha, float seconds)
        {
            Natives.SetCineFilterTexture(@"ReplaceableTextures\CameraMasks\White_mask.blp");
            Natives.SetCineFilterBlendMode(Natives.BLEND_MODE_BLEND);
            Natives.SetCineFilterTexMapFlags(Natives.TEXMAP_FLAG_NONE);
            Natives.SetCineFilterStartUV(0.0f, 0.0f, 1.0f, 1.0f);
            Natives.SetCineFilterEndUV(0.0f, 0.0f, 1.0f, 1.0f);
            Natives.SetCineFilterStartColor(red, green, blue, fromAlpha);
            Natives.SetCineFilterEndColor(red, green, blue, toAlpha);
            Natives.SetCineFilterDuration(seconds);
            Natives.DisplayCineFilter(true);
        }

        /// <summary>DisplayCineFilter(false).</summary>
        public void EndScreenFilter() => Natives.DisplayCineFilter(false);

        /// <summary>CreateMultiboard.</summary>
        public multiboard MakeBoard() => Natives.CreateMultiboard();

        /// <summary>MultiboardSetColumnCount and MultiboardSetRowCount: a count changed by any amount at once is safe (seen 11-07).</summary>
        public void SizeBoard(multiboard board, int rows, int columns)
        {
            Natives.MultiboardSetColumnCount(board, columns);
            Natives.MultiboardSetRowCount(board, rows);
        }

        /// <summary>MultiboardSetTitleText.</summary>
        public void TitleBoard(multiboard board, string title) => Natives.MultiboardSetTitleText(board, title);

        /// <summary>One cell, its item fetched and released: a new cell shows an eye icon until its style is set (seen 09-29).</summary>
        public void WriteCell(multiboard board, int row, int column, string text, string icon, Color color, float width)
        {
            var item = Natives.MultiboardGetItem(board, row, column);
            Natives.MultiboardSetItemStyle(item, true, icon != "");
            Natives.MultiboardSetItemValue(item, text);
            Natives.MultiboardSetItemValueColor(item, color.R, color.G, color.B, color.A);
            Natives.MultiboardSetItemWidth(item, width);
            if (icon != "")
            {
                Natives.MultiboardSetItemIcon(item, icon);
            }

            Natives.MultiboardReleaseItem(item);
        }

        /// <summary>MultiboardDisplay, on this machine.</summary>
        public void DisplayBoard(multiboard board, bool shown) => Natives.MultiboardDisplay(board, shown);

        /// <summary>DestroyMultiboard.</summary>
        public void EndBoard(multiboard board) => Natives.DestroyMultiboard(board);

        /// <summary>Whether the uber tooltip shows on this machine.</summary>
        public bool TooltipShown() => Natives.BlzFrameIsVisible(Natives.BlzGetOriginFrame(Natives.ORIGIN_FRAME_UBERTOOLTIP, 0));

        /// <summary>CreateLeaderboard, with labels, names and values shown.</summary>
        public leaderboard MakeLeaderboard()
        {
            var board = Natives.CreateLeaderboard();
            Natives.LeaderboardSetStyle(board, true, true, true, true);
            return board;
        }

        /// <summary>The rows kept by their slot keys, which follow the sort (seen 11-07); sized as LeaderboardResizeBJ does.</summary>
        public void WriteLeaderboard(leaderboard board, string title, List<string> labels, List<int> values)
        {
            Natives.LeaderboardSetLabel(board, title);
            var had = Natives.LeaderboardGetItemCount(board);
            for (var slot = had - 1; slot >= labels.Count; slot--)
            {
                Natives.LeaderboardRemovePlayerItem(board, Natives.Player(slot));
            }

            for (var slot = 0; slot < labels.Count; slot++)
            {
                var key = Natives.Player(slot);
                if (slot >= had)
                {
                    Natives.LeaderboardAddItem(board, labels[slot], values[slot], key);
                }
                else
                {
                    var at = Natives.LeaderboardGetPlayerIndex(board, key);
                    Natives.LeaderboardSetItemLabel(board, at, labels[slot]);
                    Natives.LeaderboardSetItemValue(board, at, values[slot]);
                }
            }

            Natives.LeaderboardSortItemsByValue(board, false);
            Natives.LeaderboardSetSizeByItemCount(board, Math.Max(0, title == "" ? labels.Count - 1 : labels.Count));
        }

        /// <summary>PlayerSetLeaderboard.</summary>
        public void GiveLeaderboard(player player, leaderboard board) => Natives.PlayerSetLeaderboard(player, board);

        /// <summary>LeaderboardDisplay, on this machine.</summary>
        public void DisplayLeaderboard(leaderboard board, bool shown) => Natives.LeaderboardDisplay(board, shown);

        /// <summary>DestroyLeaderboard.</summary>
        public void EndLeaderboard(leaderboard board) => Natives.DestroyLeaderboard(board);

        /// <summary>DialogCreate and DialogSetMessage.</summary>
        public dialog MakeDialog(string message)
        {
            var made = Natives.DialogCreate();
            Natives.DialogSetMessage(made, message);
            return made;
        }

        /// <summary>DialogAddButton, with a trigger on the button's click, kept to be destroyed with the dialog.</summary>
        public void AddDialogButton(dialog dialog, string text, int hotkey, Action<player> clicked)
        {
            var button = Natives.DialogAddButton(dialog, text, hotkey);
            var trigger = Natives.CreateTrigger();
            Natives.TriggerRegisterDialogButtonEvent(trigger, button);
            Natives.TriggerAddAction(trigger, () =>
            {
                try
                {
                    clicked(Natives.GetTriggerPlayer());
                }
                catch (Exception error)
                {
                    Errors.Report("Moonwell.Dialog", error);
                }
            });
            if (!dialogTriggers.TryGetValue(dialog, out var triggers))
            {
                triggers = new List<trigger>();
                dialogTriggers[dialog] = triggers;
            }

            triggers.Add(trigger);
        }

        /// <summary>DialogDisplay.</summary>
        public void DisplayDialog(player player, dialog dialog, bool shown) => Natives.DialogDisplay(player, dialog, shown);

        /// <summary>DialogDestroy, and the triggers of its buttons.</summary>
        public void EndDialog(dialog dialog)
        {
            if (dialogTriggers.TryGetValue(dialog, out var triggers))
            {
                foreach (var trigger in triggers)
                {
                    Natives.DestroyTrigger(trigger);
                }

                dialogTriggers.Remove(dialog);
            }

            Natives.DialogDestroy(dialog);
        }

        /// <summary>GetRandomInt.</summary>
        public int RandomBetween(int low, int high) => Natives.GetRandomInt(low, high);

        /// <summary>IsUnitLoaded.</summary>
        public bool IsLoaded(unit unit) => Natives.IsUnitLoaded(unit);

        /// <summary>The Cargo Capacity of the unit's cargo hold, read at its first level.</summary>
        public int CargoCapacity(unit unit)
        {
            var hold = CargoHold(unit);
            return hold == null ? 0 : Natives.BlzGetAbilityIntegerLevelField(hold, Natives.ABILITY_ILF_CARGO_CAPACITY, 0);
        }

        /// <summary>Sets the Cargo Capacity of the unit's cargo hold at its first level.</summary>
        public void SetCargoCapacity(unit unit, int capacity)
        {
            var hold = CargoHold(unit);
            if (hold != null)
            {
                Natives.BlzSetAbilityIntegerLevelField(hold, Natives.ABILITY_ILF_CARGO_CAPACITY, 0, capacity);
            }
        }

        // shortcut: a map's own cargo hold is found by a capacity above 0, so one set to 0 is found no more; keep the hold per unit if that matters.
        private ability? CargoHold(unit unit)
        {
            for (var i = 0; ; i++)
            {
                var found = Natives.BlzGetUnitAbilityByIndex(unit, i);
                if (found == null)
                {
                    return null;
                }

                var id = Natives.BlzGetAbilityId(found);
                if (cargoHolds.Contains(id) || Natives.BlzGetAbilityIntegerLevelField(found, Natives.ABILITY_ILF_CARGO_CAPACITY, 0) > 0)
                {
                    return found;
                }
            }
        }

        /// <summary>The game's cargo holds: ship, zeppelin, the others with one, the orc burrow, Devour Cargo and the entangled gold mine.</summary>
        private readonly List<int> cargoHolds = new List<int> { ObjectTypes.FourCC("Sch2"), ObjectTypes.FourCC("Sch3"), ObjectTypes.FourCC("Sch4"), ObjectTypes.FourCC("Sch5"), ObjectTypes.FourCC("Abun"), ObjectTypes.FourCC("Advc"), ObjectTypes.FourCC("Aenc") };

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

        /// <summary>Whether an item is in a unit's 3.0 equipment slots: IsItemEquipped.</summary>
        public bool IsEquipped(item item)
        {
            return Natives.IsItemEquipped(item);
        }

        /// <summary>Puts an item into a unit's equipment slots.</summary>
        public void EquipItem(unit unit, item item)
        {
            Natives.UnitEquipItem(unit, item);
        }

        /// <summary>Takes an item out of a unit's equipment slots into its bag.</summary>
        public void UnequipItem(unit unit, item item)
        {
            Natives.UnitUnequipItem(unit, item);
        }

        /// <summary>The item in an equipment slot of a unit: UnitItemInEquipmentSlot.</summary>
        public item? ItemInEquipmentSlot(unit unit, int slot)
        {
            return Natives.UnitItemInEquipmentSlot(unit, Natives.ConvertLoadoutSlot(slot));
        }

        /// <summary>The item in a slot of a unit's bag: UnitItemInBagSlot.</summary>
        public item? ItemInBagSlot(unit unit, int slot)
        {
            return Natives.UnitItemInBagSlot(unit, slot);
        }

        /// <summary>The size of a unit's 3.0 bag: UnitExtendedInventorySize.</summary>
        public int BagSizeOf(unit unit)
        {
            return Natives.UnitExtendedInventorySize(unit);
        }

        /// <summary>Hands the start of a spell's cast to Inbound.</summary>
        private static void SpellChannelAction()
        {
            try
            {
                Inbound.SpellChannel(
                    Natives.GetSpellAbilityUnit(),
                    Natives.GetSpellAbilityId(),
                    Natives.GetSpellTargetUnit(),
                    Natives.GetSpellTargetX(),
                    Natives.GetSpellTargetY());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.SpellChannel", error);
            }
        }

        /// <summary>Hands a spell that ran to its end to Inbound.</summary>
        private static void SpellFinishedAction()
        {
            try
            {
                Inbound.SpellFinished(Natives.GetSpellAbilityUnit(), Natives.GetSpellAbilityId());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.SpellFinished", error);
            }
        }

        /// <summary>Hands a sale or a pawn to Selling.</summary>
        private static void SaleAction(int kind)
        {
            try
            {
                Selling.FromGame(kind, Natives.GetSellingUnit(), Natives.GetBuyingUnit(), Natives.GetSoldItem(), Natives.GetSoldUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Sale", error);
            }
        }

        /// <summary>Puts an item type on sale in a shop with a stock count and a most.</summary>
        public void AddStock(unit shop, int itemId, int count, int max)
        {
            Natives.AddItemToStock(shop, itemId, count, max);
        }

        /// <summary>Takes an item type off sale in a shop.</summary>
        public void RemoveStock(unit shop, int itemId)
        {
            Natives.RemoveItemFromStock(shop, itemId);
        }

        /// <summary>Hands a research's start, cancel or finish to Researching.</summary>
        private static void ResearchAction(int kind)
        {
            try
            {
                Researching.FromGame(Natives.GetTriggerUnit(), Natives.GetTriggerPlayer(), Natives.GetResearched(), kind);
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Research", error);
            }
        }

        /// <summary>Hands a summon to Inbound.</summary>
        private static void SummonAction()
        {
            try
            {
                Summoning.FromGame(Natives.GetSummoningUnit(), Natives.GetSummonedUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Summon", error);
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

        /// <summary>Hands an alliance change of a giving player to Inbound.</summary>
        private static void AllianceAction(player giver)
        {
            try
            {
                Inbound.AllianceChanged(giver);
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Alliance", error);
            }
        }

        /// <summary>Hands a leave to Inbound.</summary>
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

        /// <summary>Hands an equip to Inbound: the game names the item with GetEquippedItem (seen 11-08).</summary>
        private static void EquipAction()
        {
            try
            {
                Inbound.EquipEvent(Natives.GetTriggerUnit(), Natives.GetEquippedItem());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Equip", error);
            }
        }

        /// <summary>Hands an unequip to Inbound, which names no item (seen 11-09).</summary>
        private static void UnequipAction()
        {
            try
            {
                Inbound.UnequipEvent(Natives.GetTriggerUnit());
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Unequip", error);
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

        /// <summary>The gg_unit_ global the build's script sets for every placed unit.</summary>
        public unit? PlacedUnit(string name) => GlobalUnit("gg_unit_" + name);

        /// <summary>The gg_item_ global the build's script sets for every placed item.</summary>
        public item? PlacedItem(string name) => GlobalItem("gg_item_" + name);

        /// <summary>The gg_snd_ global World Editor's script sets for every sound of the Sound Editor.</summary>
        public sound? PlacedSound(string name) => GlobalSound("gg_snd_" + name);

        /// <summary>StopSound, not freed, with the sound's own fade.</summary>
        public void StopPlacedSound(sound sound) => Natives.StopSound(sound, false, true);

        /// <summary>
        /// Looks for a placed destructable near its place, as the physics package finds a placed bridge: the game makes most
        /// placed destructables itself, so no global holds them. The build appends the type and place of each by its name.
        /// </summary>
        public destructable? PlacedDestructable(string name)
        {
            var type = PlacedDestructableType(name);
            if (type == 0)
            {
                return null;
            }

            placedType = type;
            placedX = PlacedDestructableX(name);
            placedY = PlacedDestructableY(name);
            placedFound = null;
            var area = Natives.Rect(placedX - PlacedReach, placedY - PlacedReach, placedX + PlacedReach, placedY + PlacedReach);
            Natives.EnumDestructablesInRect(area, null, ConsiderPlaced);
            Natives.RemoveRect(area);
            var result = placedFound;
            placedFound = null;
            return result;
        }

        /// <summary>Keeps the destructable the enumeration is at when it is of the type and nearer than the best so far.</summary>
        private void ConsiderPlaced()
        {
            var candidate = Natives.GetEnumDestructable();
            if (Natives.GetDestructableTypeId(candidate) != placedType)
            {
                return;
            }

            var dx = Natives.GetDestructableX(candidate) - placedX;
            var dy = Natives.GetDestructableY(candidate) - placedY;
            var distance = dx * dx + dy * dy;
            if (placedFound == null || distance < placedBest)
            {
                placedFound = candidate;
                placedBest = distance;
            }
        }

        /// @CSharpLua.Template = "_G[{0}]"
        private static unit? GlobalUnit(string name)
        {
            return null;
        }

        /// @CSharpLua.Template = "_G[{0}]"
        private static item? GlobalItem(string name)
        {
            return null;
        }

        /// @CSharpLua.Template = "_G[{0}]"
        private static sound? GlobalSound(string name)
        {
            return null;
        }

        // MoonwellPlacedDestructables = { [name] = { typeId, x, y } }, appended by the build; 0 when there is no table or no entry.
        /// @CSharpLua.Template = "(MoonwellPlacedDestructables and MoonwellPlacedDestructables[{0}] and MoonwellPlacedDestructables[{0}][1] or 0)"
        private static int PlacedDestructableType(string name)
        {
            return 0;
        }

        /// @CSharpLua.Template = "MoonwellPlacedDestructables[{0}][2]"
        private static float PlacedDestructableX(string name)
        {
            return 0.0f;
        }

        /// @CSharpLua.Template = "MoonwellPlacedDestructables[{0}][3]"
        private static float PlacedDestructableY(string name)
        {
            return 0.0f;
        }
    }
}
