using System.Collections.Generic;

namespace Moonwell.Hosting
{
    /// <summary>The game as the runtime sees it; the game implements it later, and the tests fake it.</summary>
    internal interface IWorld
    {
        /// <summary>Game seconds since the runtime started.</summary>
        float Now();

        /// <summary>Calls Runtime.Wake() once after the seconds; replaces any earlier request.</summary>
        void WakeIn(float seconds);

        /// <summary>Starts delivering this event to Inbound; called once per event.</summary>
        void Listen(GameEvent gameEvent);

        /// <summary>Delivers this destructable's death to Inbound.</summary>
        void WatchDeath(destructable destructable);

        /// <summary>Stops delivering a destructable's death, as it has left the game.</summary>
        void UnwatchDeath(destructable destructable);

        /// <summary>Shows a line on every player's screen.</summary>
        void Print(string text);

        /// <summary>Shows a line to one player, for the seconds, or for the game's own time when the seconds are below 0.</summary>
        void PrintTo(player player, string text, float seconds);

        /// <summary>Shows a line to every player, for the seconds, or for the game's own time when the seconds are below 0.</summary>
        void PrintToAll(string text, float seconds);

        /// <summary>Shows a line to one player in the error's colour, just above where the game shows its errors, for two seconds.</summary>
        void ErrorTo(player player, string text);

        /// <summary>Appends a line to CustomMapData\Moonwell\log.txt.</summary>
        void Log(string text);

        /// <summary>Sends a sharing message from this machine to every machine; at most 255 bytes arrive.</summary>
        void SendSync(string text);

        /// <summary>Starts delivering a player's sharing messages to Inbound; called once per player.</summary>
        void ListenSync(player player);

        /// <summary>Starts delivering a person's chat lines with a dash in them, anywhere, to Inbound; called once per person.</summary>
        void ListenChat(player player);

        /// <summary>Writes a player file's pieces with Preload, replacing the file at the path in CustomMapData.</summary>
        void WritePreload(string path, IReadOnlyList<string> pieces);

        /// <summary>The pieces of the file at the path in CustomMapData, read with Preloader; null when there is none.</summary>
        List<string>? ReadPreload(string path);

        /// <summary>This machine's clock: its seconds since 1970, in UTC.</summary>
        int ClockSeconds();

        /// <summary>How many seconds this machine's time zone is ahead of UTC now, summer time included.</summary>
        int ZoneOffset();

        /// <summary>The game's time of day, in hours.</summary>
        float HourOfDay();

        /// <summary>Sets the game's time of day, in hours from 0 up to the day's hours.</summary>
        void SetHourOfDay(float hours);

        /// <summary>How fast the time of day goes: 1 is the game's own, 0 stops it, below 0 runs it backward.</summary>
        float DayScale();

        /// <summary>Sets how fast the time of day goes.</summary>
        void SetDayScale(float scale);

        /// <summary>Whether the time of day stands still, apart from its speed; the game has no reading for it, so the world remembers what was set.</summary>
        bool IsDayStopped();

        /// <summary>Stops the time of day or lets it go on, keeping its speed.</summary>
        void StopDay(bool stopped);

        /// <summary>The player in a slot.</summary>
        player PlayerAt(int slot);

        /// <summary>The slot of a player.</summary>
        int SlotOf(player player);

        /// <summary>The slot of the neutral hostile player.</summary>
        int NeutralAggressiveSlot();

        /// <summary>The slot of the neutral victim player.</summary>
        int NeutralVictimSlot();

        /// <summary>The slot of the neutral extra player.</summary>
        int NeutralExtraSlot();

        /// <summary>The slot of the neutral passive player.</summary>
        int NeutralPassiveSlot();

        /// <summary>The x of a player's start location.</summary>
        float StartXOf(player player);

        /// <summary>The y of a player's start location.</summary>
        float StartYOf(player player);

        /// <summary>Whether a player is the one at this machine.</summary>
        bool IsLocal(player player);

        /// <summary>Every unit in the world.</summary>
        List<unit> UnitsInWorld();

        /// <summary>Every item in the world.</summary>
        List<item> ItemsInWorld();

        /// <summary>Every destructable in the world.</summary>
        List<destructable> DestructablesInWorld();

        /// <summary>Creates a unit of a type for an owner at a place and facing.</summary>
        unit SpawnUnit(player owner, int typeId, float x, float y, float facing);

        /// <summary>Creates an item of a type at a place.</summary>
        item SpawnItem(int typeId, float x, float y);

        /// <summary>The type id of a unit; 0 once the game has removed it.</summary>
        int UnitTypeId(unit unit);

        /// <summary>The type id of an item; 0 once the game has removed it.</summary>
        int ItemTypeId(item item);

        /// <summary>The type id of a destructable; 0 once the game has removed it.</summary>
        int DestructableTypeId(destructable destructable);

        /// <summary>The x of a destructable.</summary>
        float DestructableX(destructable destructable);

        /// <summary>The y of a destructable.</summary>
        float DestructableY(destructable destructable);

        /// <summary>Creates a destructable of a type at a place, with a facing, a scale and a variation; null for a type the game makes none of.</summary>
        destructable SpawnDestructable(int typeId, float x, float y, float facing, float scale, int variation);

        /// <summary>Kills a destructable through the game (KillDestructable); the game delivers its death before the call returns, and nothing for one already dead.</summary>
        void SlayDestructable(destructable destructable);

        /// <summary>The life of a destructable; 0 when it is dead or the game has removed it.</summary>
        float DestructableLifeOf(destructable destructable);

        /// <summary>Sets the life of a destructable (SetDestructableLife), clamped to its most life; 0.4 or less kills it, the death delivered before the call returns.</summary>
        void PutDestructableLife(destructable destructable, float life);

        /// <summary>The most life of a destructable; 0 once the game has removed it.</summary>
        float DestructableMaxLifeOf(destructable destructable);

        /// <summary>Sets the most life of a destructable (SetDestructableMaxLife), which the game keeps as a whole number.</summary>
        void PutDestructableMaxLife(destructable destructable, float most);

        /// <summary>Brings a dead destructable back to life (DestructableRestoreLife), with or without its birth animation; nothing for a living one.</summary>
        void RestoreDestructable(destructable destructable, float life, bool birth);

        /// <summary>Removes a unit from the world.</summary>
        void DeleteUnit(unit unit);

        /// <summary>Removes an item from the world.</summary>
        void DeleteItem(item item);

        /// <summary>Removes a destructable from the world.</summary>
        void DeleteDestructable(destructable destructable);

        /// <summary>Whether a unit is a hero.</summary>
        bool IsHero(unit unit);

        /// <summary>Whether a unit is an illusion, as Mirror Image and a Wand of Illusion make.</summary>
        bool IsIllusion(unit unit);

        /// <summary>The player that owns a unit; null a while after the game has removed it.</summary>
        player OwnerOf(unit unit);

        /// <summary>Sets the player that owns a unit, changing the unit's colour to the new player's.</summary>
        void SetOwner(unit unit, player player);

        /// <summary>The slot state of a player, as SlotState counts it (0 empty, 1 playing, 2 left).</summary>
        int SlotStateOf(player player);

        /// <summary>Who controls a player, as Controller counts it (0 user, 1 computer, 2 rescuable, 3 neutral, 4 creep, 5 none).</summary>
        int ControllerOf(player player);

        /// <summary>A state of a player: its gold, lumber, food or upkeep.</summary>
        int StateOf(player player, PlayerState state);

        /// <summary>Sets a state of a player.</summary>
        void SetState(player player, PlayerState state, int value);

        /// <summary>The level a player has of an upgrade; 0 when the player has none.</summary>
        int UpgradeLevel(player p, int upgradeId);

        /// <summary>Sets the level a player has of an upgrade.</summary>
        void Research(player player, int upgradeId, int level);

        /// <summary>How many of a unit type a player may have, or the top level of an upgrade it may research; int.MaxValue for a unit type without a limit.</summary>
        int TechMaxOf(player player, int typeId);

        /// <summary>Sets how many of a unit type a player may have, or the top level of an upgrade; 0 forbids it.</summary>
        void SetTechMax(player player, int typeId, int max);

        /// <summary>Makes an ability available to a player's units or not.</summary>
        void MakeAvailable(player player, int abilityId, bool available);

        /// <summary>The name of a player.</summary>
        string NameOf(player player);

        /// <summary>Sets the name of a player.</summary>
        void Rename(player player, string name);

        /// <summary>The colour of a player, as PlayerColor counts it.</summary>
        int ColorOf(player player);

        /// <summary>Sets the colour of a player and of the units it has.</summary>
        void Recolor(player player, int color);

        /// <summary>The team of a player.</summary>
        int TeamOf(player player);

        /// <summary>Sets the team of a player.</summary>
        void MoveToTeam(player player, int team);

        /// <summary>The race of a player, as Race counts it.</summary>
        int RaceOf(player player);

        /// <summary>What one player gives another, as Alliance's flags.</summary>
        int AllianceOf(player source, player other);

        /// <summary>Sets what one player gives another, as Alliance's flags; it goes one way.</summary>
        void SetAllianceOf(player source, player other, int alliance);

        /// <summary>Whether the game counts the other player as the player's ally.</summary>
        bool IsAllyOf(player source, player other);

        /// <summary>Whether the game counts the other player as the player's enemy.</summary>
        bool IsEnemyOf(player source, player other);

        /// <summary>Takes a player out of the game as a winner, with the game's victory box for a person.</summary>
        void Win(player player, bool dialog, bool scores);

        /// <summary>Takes a player out of the game as a loser, with the game's defeat box for a person; an empty message is the game's own words.</summary>
        void Lose(player player, string message);

        /// <summary>The level of an ability on a unit; 0 when the unit has none.</summary>
        int AbilityLevel(unit unit, int abilityId);

        /// <summary>Puts an item type on sale in a shop with a stock count and a most (AddItemToStock).</summary>
        void AddStock(unit shop, int itemId, int count, int max);

        /// <summary>Takes an item type off sale in a shop (RemoveItemFromStock).</summary>
        void RemoveStock(unit shop, int itemId);

        /// <summary>Gives a unit an ability at level 1; false, and nothing changes, when the unit has it already or there is no such ability.</summary>
        bool AddAbility(unit unit, int abilityId);

        /// <summary>Sets the level of an ability on a unit, kept within the ability's levels, and answers the level it set; 0, and nothing changes, when the unit lacks the ability.</summary>
        int ChangeAbilityLevel(unit unit, int abilityId, int level);

        /// <summary>Gives a unit the summoned type.</summary>
        void MakeSummoned(unit unit);

        /// <summary>Gives a unit timed life for so many seconds, more than 0, with the game's bar.</summary>
        void ApplyTimedLife(unit unit, float seconds);

        /// <summary>Whether an ability the unit has is a hero skill; false when the unit lacks it.</summary>
        bool IsHeroAbility(unit unit, int abilityId);

        /// <summary>Hides an ability on a unit.</summary>
        void HideAbility(unit unit, int abilityId);

        /// <summary>Makes an ability code gave a unit stay through a morph, which otherwise takes it away.</summary>
        void MakeAbilityPermanent(unit unit, int abilityId);

        /// <summary>Takes an ability from a unit; false when the unit lacks it.</summary>
        bool RemoveAbility(unit unit, int abilityId);

        /// <summary>The life of a unit.</summary>
        float LifeOf(unit unit);

        /// <summary>Sets the life of a unit; a unit left at 0.405 life or less dies.</summary>
        void SetLife(unit unit, float life);

        /// <summary>The most life a unit can have.</summary>
        float MaxLifeOf(unit unit);

        /// <summary>Sets the most life a unit can have, as a whole number; a life above it falls to it.</summary>
        void SetMaxLife(unit unit, float life);

        /// <summary>The mana of a unit.</summary>
        float ManaOf(unit unit);

        /// <summary>Sets the mana of a unit.</summary>
        void SetMana(unit unit, float mana);

        /// <summary>The most mana a unit can have.</summary>
        float MaxManaOf(unit unit);

        /// <summary>Sets the most mana a unit can have, as a whole number; a mana above it falls to it.</summary>
        void SetMaxMana(unit unit, float mana);

        /// <summary>The armor of a unit, with every bonus.</summary>
        float ArmorOf(unit unit);

        /// <summary>The middle of a unit's white damage: its base damage and the middle of its dice, without bonuses.</summary>
        float AverageDamageOf(unit unit);

        /// <summary>The move speed of a unit.</summary>
        float MoveSpeedOf(unit unit);

        /// <summary>Sets the move speed of a unit, which the game holds to the map's least and most speed.</summary>
        void SetMoveSpeed(unit unit, float speed);

        /// <summary>A unit's own life or mana regeneration a second, without what a hero's attributes add.</summary>
        float RegenOf(unit unit, bool mana);

        /// <summary>Sets a unit's own life or mana regeneration a second.</summary>
        void SetRegen(unit unit, bool mana, float perSecond);

        /// <summary>Sets the level-1 field of an ability a unit has, a whole number unless real; the game takes it at once for the stats' bonus abilities (seen 11-08).</summary>
        void PutBonus(unit unit, int abilityId, int fieldId, bool real, float value);

        /// <summary>The x of a unit.</summary>
        float UnitX(unit unit);

        /// <summary>The y of a unit.</summary>
        float UnitY(unit unit);

        /// <summary>Moves a unit to a place the way the game does for a unit that is told to stand there: it finds a free spot and the unit's order ends.</summary>
        void MoveUnit(unit unit, float x, float y);

        /// <summary>Puts a unit at a place with no pathing check; the unit's order goes on.</summary>
        void PlaceUnit(unit unit, float x, float y);

        /// <summary>The facing of a unit, in degrees.</summary>
        float FacingOf(unit unit);

        /// <summary>Orders a unit to turn to a facing, in degrees, at its turn rate.</summary>
        void TurnUnit(unit unit, float degrees);

        /// <summary>Sets the facing of a unit, in degrees, at once.</summary>
        void FaceUnitNow(unit unit, float degrees);

        /// <summary>The level of a unit; a hero's hero level.</summary>
        int LevelOf(unit unit);

        /// <summary>The name of a unit, in this machine's language; a hero's is its type's name, not its proper name.</summary>
        string UnitNameOf(unit unit);

        /// <summary>Sets the name of one unit.</summary>
        void RenameUnit(unit unit, string name);

        /// <summary>A hero's proper name.</summary>
        string ProperNameOf(unit hero);

        /// <summary>Sets a hero's proper name.</summary>
        void SetProperName(unit hero, string name);

        /// <summary>The value a type declares for a field, as the build wrote it for the map; null when it wrote none.</summary>
        object? TypeValue(int typeId, string field);

        /// <summary>Deals damage from a unit to another, with the game's attack, damage and weapon numbers; the damage event runs inside the call. Whether the game dealt it.</summary>
        bool DealDamage(unit source, unit target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType);

        /// <summary>Deals damage from a unit to a destructable, with the game's attack, damage and weapon numbers (UnitDamageTarget).</summary>
        void DealDamageTo(unit source, destructable target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType);

        /// <summary>Whether a unit is alive.</summary>
        bool IsAlive(unit unit);

        /// <summary>Kills a unit through the game; the game then delivers the unit's death, with no killer, before the call returns.</summary>
        void SlayUnit(unit unit);

        /// <summary>A hero's experience.</summary>
        int ExperienceOf(unit hero);

        /// <summary>Sets a hero's experience; the game gives the levels that come with it, with the level-up effect when asked. It only raises: a lower value changes nothing.</summary>
        void SetExperience(unit hero, int experience, bool showEffect);

        /// <summary>Adds experience to a hero, or with a negative amount takes it away, which lowers the experience but not the level.</summary>
        void AddExperience(unit hero, int experience, bool showEffect);

        /// <summary>Gives a hero a level, with the level-up effect when asked, and the experience of that level. It only raises: a lower level changes nothing.</summary>
        void LevelHero(unit hero, int level, bool showEffect);

        /// <summary>Takes a number of levels from a hero.</summary>
        void StripLevels(unit hero, int levels);

        /// <summary>A hero's strength; with the bonus items and auras give when asked.</summary>
        int StrengthOf(unit hero, bool withBonus);

        /// <summary>A hero's agility; with the bonus items and auras give when asked.</summary>
        int AgilityOf(unit hero, bool withBonus);

        /// <summary>A hero's intelligence; with the bonus items and auras give when asked.</summary>
        int IntelligenceOf(unit hero, bool withBonus);

        /// <summary>Sets a hero's base strength for good.</summary>
        void SetStrength(unit hero, int strength);

        /// <summary>Sets a hero's base agility for good.</summary>
        void SetAgility(unit hero, int agility);

        /// <summary>Sets a hero's base intelligence for good.</summary>
        void SetIntelligence(unit hero, int intelligence);

        /// <summary>The skill points a hero has to spend.</summary>
        int SkillPointsOf(unit hero);

        /// <summary>Adds skill points to a hero, or takes them with a negative number; the game's answer.</summary>
        bool AddSkillPoints(unit hero, int delta);

        /// <summary>Revives a dead hero at a place, with the revive effect when asked; the game's answer.</summary>
        bool ReviveAt(unit hero, float x, float y, bool showEffect);

        /// <summary>Whether a unit is paused.</summary>
        bool IsPausedUnit(unit unit);

        /// <summary>Pauses or unpauses a unit.</summary>
        void SetPaused(unit unit, bool paused);

        /// <summary>The size of a unit's model, as a factor.</summary>
        float ScaleOf(unit unit);

        /// <summary>Sets the size of a unit's model on all three axes.</summary>
        void ScaleUnit(unit unit, float scale);

        /// <summary>Tints a unit's model; each value is 0-255.</summary>
        void TintUnit(unit unit, int red, int green, int blue, int alpha);

        /// <summary>Plays an animation of a unit's model by name, or queues it after the one playing.</summary>
        void AnimateUnit(unit unit, string name, bool queued);

        /// <summary>Plays an animation of a unit's model by its index in the model.</summary>
        void AnimateUnitByIndex(unit unit, int index);

        /// <summary>Sets the speed of a unit's animations as a factor of the model's own.</summary>
        void SetUnitAnimationSpeed(unit unit, float speed);

        /// <summary>Adds an animation tag to a unit ("alternate"), or removes it.</summary>
        void TagUnitAnimation(unit unit, string tag, bool add);

        /// <summary>Whether a unit is hidden (ShowUnit false).</summary>
        bool IsHidden(unit unit);

        /// <summary>Shows a unit or hides it: ShowUnit.</summary>
        void SetUnitShown(unit unit, bool shown);

        /// <summary>Puts a unit of a pooled class away: its order stopped, its buffs taken off, given to the keeper without changing its colour, and hidden.</summary>
        void PutAway(unit unit, player keeper);

        /// <summary>Selects a unit for a player, or deselects it: only the player's own machine changes its selection, and the game's events tell every machine.</summary>
        void SelectFor(player player, unit unit, bool selected);

        /// <summary>Clears a player's selection, on the player's own machine only.</summary>
        void ClearSelectionFor(player player);

        /// <summary>The height a unit flies at above the ground.</summary>
        float FlyHeightOf(unit unit);

        /// <summary>Whether the unit's type flies.</summary>
        bool CanFly(unit unit);

        /// <summary>Sets the height a unit flies at, at once.</summary>
        void SetFlyHeight(unit unit, float height);

        /// <summary>Gives a unit an order that needs no place and no target; the game's answer.</summary>
        bool OrderUnit(unit unit, string order);

        /// <summary>Gives a unit an order to a place; the game's answer.</summary>
        bool OrderUnitAt(unit unit, string order, float x, float y);

        /// <summary>Gives a unit an order on a widget (a unit, an item or a destructable); the game's answer.</summary>
        bool OrderUnitOn(unit unit, string order, widget target);

        /// <summary>The id of the order a unit carries out now; 0 when it is idle.</summary>
        int CurrentOrderOf(unit unit);

        /// <summary>The game's string of an order id; "" for the few with none.</summary>
        string OrderName(int id);

        /// <summary>The number of inventory slots of a unit.</summary>
        int InventorySizeOf(unit unit);

        /// <summary>The item in a slot of a unit's inventory (0-based); null when the slot is empty.</summary>
        item ItemInSlot(unit unit, int slot);

        /// <summary>Gives a unit an item to pick up into its inventory, which the game counts as a pick-up; the game's answer.</summary>
        bool GiveItem(unit unit, item item);

        /// <summary>Makes a unit drop an item from its inventory, which the game counts as a drop.</summary>
        void DropItem(unit unit, item item);

        /// <summary>Whether a unit has an item in its inventory.</summary>
        bool CarriesItem(unit unit, item item);

        /// <summary>Whether an item is in a unit's inventory, its 3.0 bag or its equipment slots.</summary>
        bool IsItemHeld(item item);

        /// <summary>Whether an item is in a unit's 3.0 bag, not its inventory or equipment slots.</summary>
        bool IsInBag(item item);

        /// <summary>The units within a radius of a place, as the game finds them, in a new list.</summary>
        List<unit> UnitsInRange(float x, float y, float radius);

        /// <summary>The area of an ability a unit has, at its level; 0 when it has none.</summary>
        float AbilityArea(unit unit, int abilityId);

        /// <summary>Takes the range, mana and cooldown off a unit's own copy of an ability at a level, so a dummy casts it anywhere at once (seen 11-03): range 99999, mana 0, cooldown 0.</summary>
        void FreeCasting(unit unit, int abilityId, int level);

        /// <summary>The longer of an ability's normal and hero duration at a level, on a unit's copy.</summary>
        float SpellLasts(unit unit, int abilityId, int level);

        /// <summary>The missile speed of a unit's copy of an ability; 0 for one with no missile.</summary>
        int ProjectileSpeedOf(unit unit, int abilityId);

        /// <summary>How many of a research's levels, or of a unit type, a player has: GetPlayerTechCount, counting all.</summary>
        int TechCount(player player, int techId);

        /// <summary>Whether a player sees a unit, so that a unit of the player's may target it.</summary>
        bool Sees(player player, unit unit);

        /// <summary>Starts a patch of vision for a player over a place, as small as lets a unit there be targeted (128 was seen, 11-03).</summary>
        fogmodifier SeeAround(player player, float x, float y);

        /// <summary>Stops and ends a patch of vision.</summary>
        void StopSeeing(fogmodifier patch);

        /// <summary>Moves an item a unit holds into a slot of its inventory, swapping it with the item there, with no event; the game's answer.</summary>
        bool MoveItemToSlot(unit unit, item item, int slot);

        /// <summary>The number of charges of an item.</summary>
        int ChargesOf(item item);

        /// <summary>Sets the number of charges of an item.</summary>
        void SetCharges(item item, int charges);

        /// <summary>The player that owns an item.</summary>
        player ItemOwnerOf(item item);

        /// <summary>Sets the player that owns an item, leaving the item's colour as it is.</summary>
        void SetItemOwner(item item, player player);

        /// <summary>The x of an item.</summary>
        float ItemX(item item);

        /// <summary>The y of an item.</summary>
        float ItemY(item item);

        /// <summary>Moves an item to a place.</summary>
        void MoveItem(item item, float x, float y);

        /// <summary>Makes an effect of a model at a place, on the ground there.</summary>
        effect MakeEffect(string model, float x, float y);

        /// <summary>Makes an effect of a model on a unit, at an attachment point of its model; it follows the unit.</summary>
        effect MakeEffectOn(string model, unit unit, string point);

        /// <summary>Moves an effect to a place and an absolute height.</summary>
        void PlaceEffect(effect fx, float x, float y, float z);

        /// <summary>Turns an effect: yaw, pitch and roll in radians.</summary>
        void TurnEffect(effect fx, float yaw, float pitch, float roll);

        /// <summary>Scales an effect on all three axes.</summary>
        void ScaleEffect(effect fx, float scale);

        /// <summary>Tints an effect: red, green and blue, and its alpha, each 0 to 255.</summary>
        void TintEffect(effect fx, int red, int green, int blue, int alpha);

        /// <summary>Plays an animation of an effect by name, or queues it after the one playing.</summary>
        void AnimateEffect(effect fx, string name, bool queued);

        /// <summary>Sets the speed of an effect's animations; 1 is the model's own.</summary>
        void SetEffectSpeed(effect fx, float speed);

        /// <summary>Ends an effect: with its death animation when asked, else at once.</summary>
        void EndEffect(effect fx, bool playDeath);

        /// <summary>The ground's height at a place, on this machine: it only places what is drawn.</summary>
        float GroundZ(float x, float y);

        /// <summary>The world's bounds, the map's edges included: left, bottom, right and top.</summary>
        float[] WorldBounds();

        /// <summary>Makes a weather effect of a four-letter id over a rect, off until shown.</summary>
        weathereffect MakeWeather(string kind, float left, float bottom, float right, float top);

        /// <summary>Turns a weather effect on or off, on this machine.</summary>
        void ShowWeather(weathereffect weather, bool shown);

        /// <summary>Removes a weather effect.</summary>
        void EndWeather(weathereffect weather);

        /// <summary>Draws a lightning of a code between two points, each at an absolute height.</summary>
        lightning MakeLightning(string kind, float x1, float y1, float z1, float x2, float y2, float z2);

        /// <summary>Moves a lightning's two ends.</summary>
        void MoveBolt(lightning bolt, float x1, float y1, float z1, float x2, float y2, float z2);

        /// <summary>Removes a lightning.</summary>
        void EndLightning(lightning bolt);

        /// <summary>Starts a fog modifier of a player over a circle: state 0 visible, 1 fogged, 2 masked; shared with the allies who share the player's vision.</summary>
        fogmodifier MakeVision(player player, int state, float x, float y, float radius, bool overUnits);

        /// <summary>Starts a fog modifier of a player over a rect, as MakeVision.</summary>
        fogmodifier MakeVisionRect(player player, int state, float left, float bottom, float right, float top, bool overUnits);

        /// <summary>Stops and ends a fog modifier.</summary>
        void EndVision(fogmodifier vision);

        /// <summary>Whether the fog of war is on.</summary>
        bool IsFogOn();

        /// <summary>Turns the fog of war on or off, for every player.</summary>
        void SetFogOn(bool on);

        /// <summary>Whether the black mask is on.</summary>
        bool IsBlackMaskOn();

        /// <summary>Turns the black mask on or off, for every player.</summary>
        void SetBlackMaskOn(bool on);

        /// <summary>Pings the minimap on this machine: a colour of 0-255 parts, flashy or not.</summary>
        void Ping(float x, float y, float seconds, int red, int green, int blue, bool flashy);

        /// <summary>Makes a minimap icon of a model at a place, coloured, shown over the black mask.</summary>
        minimapicon MakeIcon(string path, float x, float y, int red, int green, int blue);

        /// <summary>Makes a minimap icon of a model on a unit, which it follows, as MakeIcon.</summary>
        minimapicon MakeIconOn(string path, unit unit, int red, int green, int blue);

        /// <summary>Shows or hides a minimap icon, on this machine.</summary>
        void ShowIcon(minimapicon icon, bool shown);

        /// <summary>Removes a minimap icon.</summary>
        void EndIcon(minimapicon icon);

        /// <summary>Makes a floating text that stays until it is ended: CreateTextTag, made permanent so the game never frees it (and gives its id to another).</summary>
        texttag MakeText();

        /// <summary>Sets a text's words and its height in the game's units (World Editor's size * 0.023 / 10).</summary>
        void WriteText(texttag text, string words, float height);

        /// <summary>Puts a text at a place, its height above the ground there.</summary>
        void PlaceText(texttag text, float x, float y, float height);

        /// <summary>Puts a text over a unit, its height above the unit's health bar; it does not follow the unit.</summary>
        void PlaceTextOver(texttag text, unit unit, float height);

        /// <summary>Colours a text: red, green and blue, and its alpha, each 0 to 255.</summary>
        void ColorText(texttag text, int red, int green, int blue, int alpha);

        /// <summary>Moves a text on by itself, in the game's units (Blizzard.j's speed * 0.071 / 128 a second).</summary>
        void DriftText(texttag text, float xVelocity, float yVelocity);

        /// <summary>Shows or hides a text on this machine.</summary>
        void ShowText(texttag text, bool shown);

        /// <summary>Ends a text: DestroyTextTag.</summary>
        void EndText(texttag text);

        /// <summary>Whether the player at this machine sees a unit.</summary>
        bool LocalSees(unit unit);

        /// <summary>Makes a sound of a file, looping or not, 3D or not; it does not play yet.</summary>
        sound MakeSound(string path, bool looping, bool is3D);

        /// <summary>Puts a 3D sound at a place and a height.</summary>
        void PlaceSound(sound sound, float x, float y, float z);

        /// <summary>Puts a 3D sound on a unit, which it follows.</summary>
        void PutSoundOn(sound sound, unit unit);

        /// <summary>Sets a sound's volume, 0 to 127.</summary>
        void SetLoudness(sound sound, int volume);

        /// <summary>Starts a sound.</summary>
        void StartPlaying(sound sound);

        /// <summary>Lets the game free a sound when it has played.</summary>
        void FreeWhenDone(sound sound);

        /// <summary>Stops a sound at once and lets the game free it.</summary>
        void StopPlaying(sound sound);

        /// <summary>Plays one file of music on this machine now, and makes it the map's music, which the game goes back to.</summary>
        void StartMusic(string path);

        /// <summary>Stops the music on this machine, fading or at once, and leaves the map with no music to go back to.</summary>
        void StopAllMusic(bool fadeOut);

        /// <summary>A file's length in milliseconds as this machine reads it; 0 when the game has no such file.</summary>
        int MusicLength(string path);

        /// <summary>Sets the music's volume on this machine, 0 to 127.</summary>
        void SetMusicLoudness(int volume);

        /// <summary>Makes a quest in the log (F9), disabled until shown.</summary>
        quest MakeQuest();

        /// <summary>Sets a quest's title, description and icon.</summary>
        void WriteQuest(quest quest, string title, string description, string icon);

        /// <summary>Sets whether a quest is a main quest (required), discovered, completed and failed.</summary>
        void SetQuestState(quest quest, bool main, bool discovered, bool completed, bool failed);

        /// <summary>Shows or hides a quest in the log on this machine.</summary>
        void ShowQuest(quest quest, bool shown);

        /// <summary>Takes a quest out of the log: DestroyQuest.</summary>
        void EndQuest(quest quest);

        /// <summary>Adds a requirement to a quest.</summary>
        questitem MakeRequirement(quest quest);

        /// <summary>Sets a requirement's text and whether it is done.</summary>
        void SetRequirement(questitem requirement, string text, bool done);

        /// <summary>On this machine: shows a quest message's lines, plays its sound and flashes the quest button, as QuestMessageBJ does.</summary>
        void QuestNews(QuestNews kind, string text);

        /// <summary>Listens for a person's Esc: the game's skip-cinematic event, which comes on every press.</summary>
        void ListenEsc(player player);

        /// <summary>On this machine: hides the HUD behind the letterbox, or shows it again, over the seconds.</summary>
        void Letterbox(bool on, float seconds);

        /// <summary>On this machine: lets the player give orders and move the camera, or not, and shows the cursor or not.</summary>
        void UserControl(bool on);

        /// <summary>On this machine: lowers the game's sounds as cinematic mode does, or puts them back.</summary>
        void CinematicSounds(bool on);

        /// <summary>On this machine: clears the lines printed on the screen.</summary>
        void ClearMessages();

        /// <summary>On this machine: shows a speaker's talking portrait, name and line for the seconds, the portrait talking for talkSeconds.</summary>
        void ShowScene(int unitTypeId, int playerColor, string name, string text, float seconds, float talkSeconds);

        /// <summary>On this machine: ends the speaker's portrait and line at once.</summary>
        void EndScene();

        /// <summary>On this machine: marks a unit that talks with a red flash and pings the minimap at it.</summary>
        void MarkSpeaker(unit unit, float x, float y);

        /// <summary>On this machine: covers the screen with a colour, its alpha going from one value to another (0 to 255) over the seconds.</summary>
        void ScreenFilter(int red, int green, int blue, int fromAlpha, int toAlpha, float seconds);

        /// <summary>On this machine: takes the screen's colour cover away.</summary>
        void EndScreenFilter();

        /// <summary>Makes a multiboard, not displayed.</summary>
        multiboard MakeBoard();

        /// <summary>Sets a multiboard's rows and columns.</summary>
        void SizeBoard(multiboard board, int rows, int columns);

        /// <summary>Sets a multiboard's title.</summary>
        void TitleBoard(multiboard board, string title);

        /// <summary>Writes a cell: its text, its icon (none when empty, with no eye icon), its text's colour and its width.</summary>
        void WriteCell(multiboard board, int row, int column, string text, string icon, Color color, float width);

        /// <summary>On this machine: displays a multiboard or hides it.</summary>
        void DisplayBoard(multiboard board, bool shown);

        /// <summary>Destroys a multiboard.</summary>
        void EndBoard(multiboard board);

        /// <summary>On this machine: whether the game's tooltip for a button shows.</summary>
        bool TooltipShown();

        /// <summary>Makes a leaderboard, not displayed.</summary>
        leaderboard MakeLeaderboard();

        /// <summary>Sets a leaderboard's title and rows, row n keyed to slot n (a slot never has two), sorted by value, highest first, and sized to them.</summary>
        void WriteLeaderboard(leaderboard board, string title, List<string> labels, List<int> values);

        /// <summary>Makes a leaderboard a player's: PlayerSetLeaderboard, on every machine.</summary>
        void GiveLeaderboard(player player, leaderboard board);

        /// <summary>On this machine: displays a leaderboard or hides it.</summary>
        void DisplayLeaderboard(leaderboard board, bool shown);

        /// <summary>Destroys a leaderboard.</summary>
        void EndLeaderboard(leaderboard board);

        /// <summary>Makes a dialog with its message.</summary>
        dialog MakeDialog(string message);

        /// <summary>Adds a button, whose click runs clicked with the clicking player, a synced event.</summary>
        void AddDialogButton(dialog dialog, string text, int hotkey, System.Action<player> clicked);

        /// <summary>Shows a dialog to a player or hides it from them: DialogDisplay, on every machine.</summary>
        void DisplayDialog(player player, dialog dialog, bool shown);

        /// <summary>Destroys a dialog and its buttons' triggers.</summary>
        void EndDialog(dialog dialog);

        /// <summary>Whether a unit is in a transport: IsUnitLoaded.</summary>
        bool IsLoaded(unit unit);

        /// <summary>The Cargo Capacity of the unit's cargo hold, the first of its abilities that has one; 0 for a unit with none.</summary>
        int CargoCapacity(unit unit);

        /// <summary>Sets the Cargo Capacity of the unit's cargo hold, for this unit alone; nothing for a unit with none.</summary>
        void SetCargoCapacity(unit unit, int capacity);

        /// <summary>A whole number from low to high, both included, from the game's random numbers, which every machine shares.</summary>
        int RandomBetween(int low, int high);
    }
}
