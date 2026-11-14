# Making a map with Moonwell

This guide describes Moonwell 0.15.1; the guide for 0.15.0 is at the
[`v0.15.0` tag](https://github.com/wc3-dev/moonwell/blob/v0.15.0/docs/guide.md).

## Start a map

Moonwell needs the .NET 10 SDK and Warcraft III 3.0, whose `common.j` and `Blizzard.j` the build reads from the
install. Name an install outside `C:\Program Files (x86)\Warcraft III` with the `MOONWELL_WAR3` environment variable.
Install the `moonwell` command once:

```
dotnet tool install --global Moonwell.Cli
```

Then make a map and play it:

```
moonwell new MyMap
cd MyMap
moonwell run
```

`dotnet new moonwell -n MyMap` makes the same project, after `dotnet new install Moonwell.Templates`.

The game opens the map and shows the Moonwell version. Player 1 has a Champion, a Paladin of the map's own, with
three footmen to fight. Learn Smite and cast it: the screen names the target. Each kill shows the Champion's count and heals it a tenth of its life.

## The project

| Path | What it is |
|---|---|
| `MyMap.csproj` | The C# project; the built map takes its name |
| `Map.cs` | The map's settings, and the components the game starts with |
| `Start.cs` | The game's start: makes the Champion and the footmen |
| `Heroes/` | One feature: the Champion, its spell and its kill counter |
| `map/` | The World Editor map, saved as a folder |
| `assets/` | Files packed into the map at the same paths (models, icons, sounds; a PNG becomes a BLP; a `.looks.csv` lists copies the build makes), named with ASCII characters only; make it when you need it ([Assets](#assets)) |
| `build/` | What the build writes: the staged map and `MyMap.w3x`; do not commit it |

Code can sit in any folder and any namespace not named like a native (MW0044): the build finds objects and the map
class by what they inherit. The template keeps one folder per feature, each with its own namespace (`MyMap.Heroes`).
Don't name one after a group of `Moonwell.Objects` (`Human`, `Items`): with `MyMap.Items` in the project, `Items.X`
finds that namespace and fails with CS0234.

World Editor is for terrain and placed things: regions, cameras, units, items, trees. Open `map/` in World Editor,
change it, and save it as a folder again. Objects, triggers and the map's settings are in code; the build replaces the
saved script.

## The map's settings

`Map.cs` holds the one class of the project that inherits `Map`. Its constructor sets `Name`, `Author`, `Description`
and `RecommendedPlayers`; one left empty keeps World Editor's value. `DataSet` and `DataVersion` pick the balance the
map plays with, as World Editor's Data Set and Data Version. `Components` lists the game components the game
starts with: `Components.Add(new Start(), new Rules());` adds them, and `Components = new() { new Start(), new Rules() };`
replaces the list.

`PlayerComponents` lists what every player in the game starts with, people and computers:
`PlayerComponents.Add(() => new Aim(), () => new Score());`. A component is on one actor only, so each `() =>` makes a
new one for each player. They are added when the game starts, before the units already in the map come in, so their
`OnUnitCreated` hears of their own player's units among those. One added later, such as `player.Add(new Aim())` for a
single player or a neutral one, does not.

`Slots` sets up the player slots in place of World Editor's players and forces. Each slot, numbered from 0 as
`Player.Get` takes it, is on a team, played by a person or a computer, with a race or, without one, the player's pick.
A team is declared on the map class as a static field, so code finds it by name; its players are allies, and
`SharedVision` and `SharedControl` share more between them.

```csharp
public sealed class Arena : Map
{
    public static readonly Team West = new("West") { SharedVision = true };
    public static readonly Team East = new("East") { SharedVision = true };

    public Arena()
    {
        Slots.Add(
            new Slot(0, West, Controller.User),
            new Slot(1, West, Controller.User),
            new Slot(4, East, Controller.User, Race.Orc),
            new Slot(10, East, Controller.Computer, Race.Undead) { Name = "The Scourge" });
    }
}
```

The build writes them into the map's `war3map.w3i` with fixed player settings: the lobby shows the teams, nobody changes
their team or race there, and the game starts with each team's alliances set. Teams are numbered in the order the
slots first name them. A slot starts at its start location placed in World Editor; one without starts in the map's
middle, and the build notes it. Fixed settings lock alliance changes: the Allies menu (F11) is greyed out, and only
code changes alliances. A map with no slots keeps World Editor's setup.

## Objects

A unit, hero, item, ability, buff, destructable, doodad or upgrade is a class that inherits one of the game's own,
from `Moonwell.Objects`, by race and kind: `Human.Units.Paladin`, `Human.Abilities.Defend`, `Human.Upgrades.Defend`,
`Campaign.Units.ArthasHart`, and `Items.ClawsOfAttack15`, `Destructables.X` and `Doodads.X` for the kinds with no race.
`using Moonwell.Objects;` reaches all of them; `using static Moonwell.Objects.Human;` lets a file write
`Abilities.Defend`. A class takes the object's in-game name. Where objects of one race and kind share a name, the one
World Editor lists without an editor suffix keeps it and the others get their id added: `Orc.Abilities.Shockwave` is
the Tauren Chieftain's, `Orc.Abilities.ShockwaveAOs2` Cairne's. Campaign units that share a name all get their id
(`ArthasHart`, `ArthasUear`). A class's hover shows its name, editor suffix and id: "Shockwave (Cairne), AOs2."
Its constructor changes fields; a field it does not change keeps its base's value. Every new object
has its id on the class, as World Editor shows it: a hero's starts with an upper-case letter, another unit's with a
lower-case one.

```csharp
[ObjectId("H001")]
public sealed class Champion : Human.Units.Paladin
{
    public Champion()
    {
        Name = "Champion";
        MaxLife = MaxLife + 200;
        HeroAbilities.Replace<Human.Abilities.HolyLight, Smite>();
        Components.Add(new KillCounter());
    }
}
```

- A field's hover in the editor gives World Editor's label, its id and what it allows:
  `Acquisition Range (uacq), 0 to 20000.`
- A field with levels takes `Levels(75f, 150f, 225f)`, one value for each level (another count fails the build),
  `PerLevel(level => ...)` to compute each level, or one value for every level: an upgrade's
  `Name = "Sharpened Blades"`. Raising an ability's `LevelCount` past the game's writes every level you give; a field
  you leave keeps the game's own data there (a Storm Bolt of 6 levels deals 325 at levels 4 to 6, its table's 4th
  level), and reads so.
- A field World Editor offers a list of values for holds an enum of them, named as the game's data spells them, or
  from World Editor's label where the data has a code, which stays in the hover (`WeaponType.MissileSplash`,
  `UpgradeEffect.HitPointBonus`): `AttackType = AttackType.Magic`. One that takes several, such as the targets, holds flags:
  `Attack1TargetsAllowed = Targets.Air | Targets.Ground`. A flags field of 0 has none of them; `Targets.None` is
  World Editor's target "none", a value of its own. The numbers are Moonwell's, in World Editor's order, not the
  natives' (JASS's `ATTACK_TYPE_MAGIC` is 4, `AttackType.Magic` is 6): never cast one to or from a native's value.
  The placement fields are the exception, named as World Editor shows them: `PlacementRequires =
  PathingRequire.Buildable`, `PlacementPreventedBy = PathingPrevent.Unblighted`. The game stores each as its opposite
  ("unbuildable", "blighted"), and the build writes that.
  In the game an enum of `Moonwell.Objects` has its numbers only, no names: `ToString` (also `choice?.ToString()`), a
  string made with one (`+`, `+=`, interpolation, or a call that takes an `object`: `string.Format`,
  `StringBuilder.Append`, a method of your own), a list of them given to `string.Join`, and `Enum.GetValues` and the
  other `Enum` methods are rejected as error MW0010, a nullable choice too; compare with its members, `|`, `&`,
  `HasFlag` and `CompareTo`, and show the number with `(int)`, `Convert.ToInt32` or `ToString("D")`.
- `AttackType`, `DefenseType` and `WeaponSound` are core Moonwell's (`Moonwell.AttackType`), shared with hits and the
  damage package, and keep their names in the game. A file writes them with `using Moonwell;`; a name of the old
  `Moonwell.Objects.AttackType` is error MW0011. `Attack1WeaponSound` takes the 13 sounds World Editor lists: `None` and
  the stab, claw and wood slice sounds are for hits, and are error MW0009 there, as what the game does with them on a
  unit is not known. In a unit's class the field goes straight into a hit:
  `this.Damage(target, 10f, sound: Attack1WeaponSound)`.
- A field that a `Unit`, `Hero`, `Item` or `Destructable` also has as a live value has one name for both. In the
  constructor it sets what every object of the class starts with; anywhere else it is this object (`MaxLife += 10` in
  `OnKill`). The names are `MaxLife`, `MaxMana`, `Mana` (what it starts with), `Scale`, `FlyHeight`, `Name` and `Icon` on a
  unit and hero, `Level` on a unit that is not a hero, `Strength`, `Agility` and `Intelligence` on a hero, `Charges` on an
  item, and `MaxLife` on a destructable. They were `HitPoints`, `ManaPoints`, `ManaInitialAmount`, `ScalingValue`,
  `Height`, `UnitLevel`, `StartingStrength`, `StartingAgility`, `StartingIntelligence`, `NumberOfCharges` and a
  destructable's `HitPoints`, which are gone. Outside the constructor the old names read the declared value; write
  `Default.MaxLife` (`Default.Name`) for that on a unit or item. A destructable has no `Default`, so its declared most
  life is read only in its constructor. An item's `Level`, `Scale` and `HitPoints` and a doodad's `Scale` keep their
  names, and six ability fields are
  named apart: `Inferno.InfernalDuration`, `Taunt.PreferHostiles` and `PreferFriendlies`, `WindWalk.BackstabDamage` and
  `Backstab`, `HealingModifier.HealingPercent`.
- A field with no live value (`Model`, `Tooltip`, `GoldCost`) keeps World Editor's name, is set only in the constructor
  or in a method that only constructors call (Setup that classes share, below), and reads the declared value anywhere.
  A set anywhere else fails the build, which names the class and method but no line:
  `MyMap.Heroes.Champion.OnKill sets GoldCost; declared fields are set only in the constructor, or in a method that only
  constructors call`. C# itself accepts `unit.GoldCost = 5` in any code, as the setter is public for such a method to
  reach; the build is what stops it.
- A unit's portrait follows its `Model` by itself: a class that sets only `Model` shows that model in the portrait
  box, though the unit it is based on names a portrait file of its own. `PortraitModelFile` need not be set. Seen in
  classic graphics; HD graphics was not tried.
- A hero's `MaxLife`, `MaxMana` and `Mana` in its constructor are the totals it starts with: `MaxLife = 800` starts a
  Paladin at 800 life. The build takes off what its starting strength and intelligence give (25 life per strength and
  15 mana per intelligence, or the map's own in `war3mapMisc.txt`) and writes the rest as the base field. A total the
  attributes alone pass fails the build with its line: `Champion.MaxLife is 400; its 30 strength already give 750
  (751 at least)`. A hero class that sets `Strength` but not `MaxLife` keeps its base, so its life follows the strength.
  A hero starts at level 1: `Level = ...` or `Set("ulev", ...)` in a hero's constructor is error MW0012; use `SetLevel`
  in the game. A unit's `Level` is set only in its declared class's constructor, since the game has no setter: anywhere
  else it is error MW0015, and a build error too.
- A list takes `Set<A, B, C>()` (the whole list, in order), `Replace<Old, New>()` (in the old one's place), `Add<T>()`
  and `Remove<T>()`. Where the game's list differs by level, as a tiered upgrade's requirements do, the build makes the
  same changes to each level's own list: `Requires.Add<Blacksmith>()` on a copy of Iron Forged Swords keeps the Keep and
  Castle its later levels need. Read in C#, such a list is level 1's with the changes.
- A requirement counts unit types, so a class based on a standard building does not count as it: a copy of the
  Stronghold does not meet the "requires Stronghold" a class based on the Beastiary inherits. Two ways out. Name the
  map's own class, `Requires.Set<MyHold>()`; a real Stronghold then does not count. Or make the class count as the
  standard building, by listing it on that building: `DependencyEquivalents.Add<MyHold>()` in a `[Replaces]` class of
  `Orc.Units.Stronghold`. Listing the Stronghold on the copy does nothing. A hero's tier requirements name buildings
  the same way; they were not tried in the game.
- The tooltips are World Editor's four: `Tooltip` (Tooltip - Normal), `TooltipExtended` (Tooltip - Normal - Extended),
  `TooltipLearn` (Tooltip - Learn) and `TooltipLearnExtended` (Tooltip - Learn - Extended). A tooltip shows an
  object's values with World Editor's placeholders: `<A000,Dur1>` an ability's at a level, `<h000,realHP>` a unit's,
  `<I000,uses>` an item's charges. The game rounds a value down to a whole number, so a decimal one takes `,.` (one
  decimal, `<A000,DataA1,.>`) or `,%` (times 100). The game shows a map's own buff's extended tooltip only up to its
  first comma, quoted or not, so a comma in it fails the build, and placeholders, which hold one, cannot go in it. A new
  buff that keeps a standard text with a comma (Flame Strike's) fails too: set `TooltipExtended`. Write `‚` (U+201A)
  for a comma there: the game draws it as one and does not cut the text at it.
- `Describe` writes an ability's, an item's or an upgrade's tooltips from one text, so they show its own numbers:
  `Describe("Smite", tip => $"Deals {tip[Damage]} damage.", TipLines.Cooldown)`. In a level's text `tip[Damage]` is
  that level's value in the game's tooltip gold; in the learn tooltip, which tells of every level, it is `75/150/225`,
  or one number when every level has the same. `tip.Percent(...)` shows 0.25 as 25%, `tip.Number(250)` a number that
  doesn't change by level, and `tip.Level` the level (0 in the learn tooltip). An ability gets its four tooltips,
  titled as the game's own (`Smite - [Level 2]`, the name alone for a unit's ability of one level, and
  `Learn Smite - [Level %d]`); `TipLines` adds mana, cooldown, range, area and duration lines from its fields, each
  left out where it is 0. An item gets `Purchase Tonic` and its extended tooltip. An upgrade gets `Upgrade to` each
  level's name, as in `Describe(Levels("Iron Swords", "Steel Swords", "Mithril Swords"), ...)`, and each level's
  text. `Describe` reads the fields when it is called, so it goes after the values it shows, and a tooltip set after
  it wins. Like a set, it belongs in the constructor: anywhere else fails the build.
- A field with no name of its own is set by its id: `Set("Htb1", 2, 250f)`, or `Set("ua1w", WeaponType.Missile)` for a
  choice. The value has exactly the type the field holds, so `1.5f` or `5` in the wrong field, a choice's number and an
  id the class doesn't have are error MW0049. A list of numbers held as text (`RequirementsLevels = "1,2"`) is only
  numbers: any other part is error MW0009, which `[BeyondLimits]` doesn't lift.
- A constructor may compute, but must not call the game: the build fails at such a line. A live member with no declared
  field there (`Life = 300`, `Kill()`, `X`) is error MW0012, which says what to set instead where there is one: a unit
  or destructable starts with full life, so set `MaxLife`.
- A class with no `[ObjectId]` is error MW0013, and the build names a free id to use. A buff also gives the id of its
  carrier, the hidden ability the build makes to show its icon, so no class of the map declares that id:
  `[ObjectId("B001", Carrier = "A002")]`, or `[ObjectId(Carrier = "A002")]` with `[Replaces]`. An id is four ASCII
  characters, none of them a comma (the game splits a list field that names the object at a comma), and only a hero's
  starts with an upper-case letter (MW0014).
- `[Replaces]` makes the class the game's object itself: every unit of that type is made as the class.
- A library project of the map's own can name, make and inherit the standard classes, as an abstract base the map's
  classes inherit. An object class itself is declared in the map's own project: one in a library fails the build.
- `Unit.Create<Champion>(player, x, y, scope)` makes one in the game, in the scope named (scopes, below), facing 270
  degrees unless a facing follows; `Item.Create<EmberBlade>(x, y, scope)` makes an item, `(hero, scope)` in a unit's
  inventory and `(hero, slot, scope)` into a slot of it, or at the unit's feet when it has no room, and
  `Destructable.Create<Gate>(x, y, scope)` a destructable, facing 270 degrees, at scale 1 and in its type's first model
  (variation 0) unless they follow. `new Champion()` gives an object with nothing in the game behind it, and is error
  MW0046.
- `[Pooled]` on a unit class keeps its units when they end (`Remove()`, or their scope ending) hidden, and
  `Unit.Create` gives a kept one out again, as a new object of the class: making a unit costs the game about ten times
  what hiding, moving and showing one does (0.4 ms against 0.04), which shows when a wave of units comes at once. A
  kept unit has its items removed and its buffs taken off, and comes out with its life and mana as made; anything else
  code changed on the unit itself (its scale, its colour, abilities added with natives) stays. A dead unit, a hero, an
  illusion, a unit inside a transport and a transport carrying units are never kept,
  and `[Pooled]` on a hero class fails the build.
- A unit keeps the class it came in as, also when the game changes its type. A morph (Bear Form, Metamorphosis) and a
  building's upgrade change the type of the same unit: a morph when its morph time is up, an upgrade when it finishes.
  `TypeId` reads the type the unit has now, so `hall.TypeId == ObjectTypes.IdOf<Keep>()` tells that a `Hall` has
  become a keep. Everything else stays the first class's: the object, its reactions and components, its declared
  fields and its `Default`. The new type's class runs nothing for it, `OnCreated` neither; its declared values are
  `Unit.Default<Keep>()`'s. A unit of a `[Pooled]` class that has morphed is removed, not kept.

Each value a class sets is checked against World Editor's limits. One outside them fails the build with its line, and
a constant is marked in the editor as error MW0009:

```
Heroes/Giant.cs:12: Giant.MaxLife is 600000; Hit Points Maximum (Base) (uhpm) allows 1 to 500000; [BeyondLimits] goes past it
```

`[BeyondLimits(nameof(MaxLife))]` on the class goes past a range on purpose. It lifts ranges, lengths and counts
only: an empty name, a negative on a field the game never lets go below 0, and a choice that is none of the enum's
values still fail. The values a standard class has are the game's, and are never checked.

To see a standard object's values, go to its class: Go to Definition on `Paladin` opens the class's source, which
`Moonwell.Objects` embeds in its library, in an editor that navigates to embedded sources (the editors on Microsoft's C#
language server do). Its constructor sets each value of the base tables, World Editor's
Default. A map on another data set plays that set's values for the fields it leaves unset.

### Turning

Four fields of a unit's class set how it turns. Measured in the game (3.0.1, classic graphics):

| | |
|---|---|
| `TurnRate` (0.1 to 3) | how soon the unit sets off toward a place it doesn't face; higher is sooner. For a place straight behind: 0.66 s at 0.1, 0.14 s at 0.6, 0.06 s at 3. It doesn't set how fast the model turns |
| `OrientationInterpolation` (0 to 8) | how fast the unit's facing, and so its model, comes round. Not a ramp. A half turn, fastest first: 6 0.5 s (the fastest start), 0 0.5 s, 2 0.7 s, 1 1 s, 5 1 s and 4 1.2 s (both most of the way round in 0.6 s, then a long ease), 3 1.6 s, 7 2 s, 8 3.3 s |
| `PropulsionWindowDegrees` (1 to 180) | how far off its heading the unit may be and still walk. A wider one lets a slow-turning unit set off sooner, turning as it goes: at `TurnRate` 0.3, 0.38 s at 1, 0.26 s at 60 and at once at 180 |
| `AnimationBlendTimeSeconds` (0 to 1) | the time the model takes to blend from one animation into the next, as from walking into standing. Looks only: nothing about turning or walking changes |

- For a snappy turn, set `TurnRate = 3` and `OrientationInterpolation = 6` (or 0). At `TurnRate` 3 the unit sets
  off at once, so the window adds nothing.
- With a high `TurnRate` and a slow interpolation the unit walks off before its facing has come round.
- `Facing = 180` (and `SetUnitFacing`) turns at the interpolation's speed whatever the `TurnRate`.
- `FaceNow` toward an order's place from `OnOrdered` turns the unit's facing at once, and it sets off at once.

### Setup that classes share

Classes with different bases cannot share a base class. What their constructors have in common goes in a method that
takes the object, a setup helper, and each constructor calls it:

```csharp
public static class Clan
{
    public static void Look(StandardUnit unit, string name, string model, int x, int y)
    {
        unit.Name = name;
        unit.Model = @"war3mapImported\Clan_" + model + ".mdx";
        unit.Icon = @"ReplaceableTextures\CommandButtons\BTNClan" + model + ".blp";
        unit.Tooltip = "Train " + name;
        unit.ButtonPositionX = x;
        unit.ButtonPositionY = y;
    }
}

[ObjectId("o000")]
public sealed class ClanGrunt : Orc.Units.Grunt
{
    public ClanGrunt()
    {
        Clan.Look(this, "Clan Grunt", "Grunt", 0, 0);
    }
}

[ObjectId("o001")]
public sealed class ClanRaider : Orc.Units.Raider
{
    public ClanRaider()
    {
        Clan.Look(this, "Clan Raider", "Raider", 1, 0);
        AttackRange = 450;
    }
}
```

- The helper takes the kind base its classes share: `StandardUnit`, `StandardHero`, `StandardItem`, `StandardAbility`,
  `StandardBuff`, `StandardUpgrade`, `StandardDestructable` or `StandardDoodad`. A hero is not a `StandardUnit`, so
  heroes take a helper of their own.
- It sets fields, changes lists (`unit.Requires.Clear()`) and may call other helpers. `Set` by id and `Describe` are
  protected, so they stay in the class: in its constructor, or in a method of it that only constructors call. A unit's
  `Level` stays in the constructor.
- Only constructors call a helper, straight or through another helper, as its sets do nothing in the game. A call from
  anywhere else fails the build, naming the caller:
  `MyMap.Clan.Look sets Model, and MyMap.ClanGrunt.OnDeath calls it; declared fields are set only in the constructor, or
  in a method that only constructors call`. A helper held as a delegate (`var look = Clan.Look;`), a set in a lambda
  inside a helper and a helper nothing calls fail the same way.

### The default object and the name

`Default` is the class's declared values as an object, which never changes when the unit does. Every unit, hero and
item has it, a plain standard unit too: `unit.Default.MaxMana` is 255 whatever this Champion's is now. From the class
alone it is `Unit.Default<Champion>().MaxMana`: `Unit.Default<T>()` takes a unit class (`Hero.Default<T>()` works too),
`Item.Default<T>()` an item class; a class of another kind (`Item.Default<Guard>()`) or a destructable class, which has
no default object, is error MW0037. On a declared class `Default` is typed as its kind base (`StandardUnit`,
`StandardHero` or `StandardItem`), so it holds every field of it (`Default.Model`, `Default.GoldCost`); the class's own
members come through `Unit.Default<T>()`. Its live actions do nothing. In the class's own constructor the default
object is what the constructor is making, so `Default` and `Unit.Default<T>()` of the class there are error MW0012:
read the field itself (`MaxLife`, not `Default.MaxLife`). Setting a member through a default object anywhere else
(`unit.Default.MaxLife = 500`) is error MW0054: in the game it does nothing, and in the build, which runs on .NET, it
changes the one object every read of that default gives, so the build and the game would differ. Declare the value in
the class's constructor, or set it on the unit or item.

An ability class has no default object either. `unit.AbilityOf<Smite>()` is the object a unit has of it, made if the
unit has none yet, and its fields read what the class declares. A destructable's read on one in the game.

`Default` of a type made or changed in World Editor reads its values as World Editor made them; a class of the map with
its id wins over them.

`unit.Name` reads the game's name, which is in each player's language, so reading it is error MW0002 outside
`Local.For`; setting it is fine anywhere outside `Local.For`. Reading `Name` in a declared class's constructor, and on
`Default`, is allowed, and the same on every machine. A local copy of a default (`var d = unit.Default; d.Name`) is
still MW0002, a false alarm: write `unit.Default.Name`. A hero's `Name` is its type's ("Paladin"). `Hero.ProperName`
("Arius the Seeker") has no declared value: it is read inside `Local.For` only, set anywhere outside it, and empty on a
default.

## Components and reactions

A reaction is a method on the thing involved: `OnDamaged`, `OnDeath` and `OnKill` on a unit, `OnCast` on an ability,
`OnPickedUp` on an item, `OnDeath` and `OnRestored` on a destructable. A component is a class that carries reactions and state, and is added to a unit, item,
destructable, player or the game: it inherits `UnitComponent`, `ItemComponent`, `DestructableComponent`,
`PlayerComponent` or `GameComponent`.

```csharp
public sealed class KillCounter : UnitComponent
{
    private int kills;

    public override void OnKill(Unit killedUnit)
    {
        kills++;
        Unit.Owner.Print("Kills: " + kills);
    }
}
```

- A type lists the components its instances start with in its constructor. `unit.Add(new KillCounter())` adds one in the
  game, and `unit.Remove<KillCounter>()` removes it (`End()` ends a component from inside). `HasEnded` is false inside
  `OnRemoved`, `OnLeave` and `OnEnd`, and true once the removal or `End()` returns. A component object is on one
  actor only: adding it to a second, applying a buff that is on a unit already, or removing a component from an actor
  it is not on is reported and refused, in a release build too. A component's `Actor` is set from `OnAdded` on: read in
  its constructor, a development build reports it.
- Reactions to the whole map (`OnGameStart`, `OnUnitCreated`, `OnUnitDied`, `OnUnitCast`, `OnHeroLevelUp`,
  `OnHeroSkillLearned`, `OnLeave`, `OnAllianceChanged`) go on a game or player component. A game component hears them for every unit; a
  player's components only for its own player's units: the unit's owner (the caster's for `OnUnitCast`), and for
  `OnUnitDied` the dying unit's owner, then the killer's. A reaction to every unit goes on a game component.
  `OnUnitSelected` and `OnUnitDeselected` go there too, and run for the selecting player's components ([Players](#players)).
  `OnUnitCreated` runs once for each unit, when it comes into the game (one that came in before anything wanted it
  runs none); a unit's own is `OnCreated`. `OnDawn` and `OnDusk` go on a game component only (see the time of day
  below), and so does `OnGameLoaded`, which runs once when a game saved with the game's own Save Game is loaded, after
  Moonwell has made the map's frames again (see Frames).
- A hero's own reactions, `OnLevelUp(int reachedLevel)` and `OnSkillLearned(Ability learnedAbility)`, go on a hero
  class or a `UnitComponent`, where they run only for a hero. The game fires one event for a jump of several levels,
  and `OnLevelUp` runs once for each level gained, in order, while `Level` already reads the last; levels taken away
  run nothing. An ability's `OnLearned` runs first when a hero learns it or raises its level. Inside these the skill
  point is not spent yet: `SkillPoints` reads one more than after. A hero skill given in code, by `AddAbility` or a
  higher `Level`, runs them too, once for each level gained, and `RemoveAbility` runs the ability's `OnLost`, where a
  passive undoes what `OnLearned` set up. Code spends no skill point, and taking a skill gives none back.
- A reaction is a method the class overrides: one written without `override`, or with `new`, never runs, and is error
  MW0041, as is a method named like a reaction (`On...`) with other parameters. Moonwell calls a reaction: a call to one
  runs it with no event behind it and is error MW0047, except `base.OnDeath(killerUnit)` inside its own override. What a
  reaction shares with other code goes in a method of its own.
- A reaction's parameters say the role: `OnPickedUp(Unit carrierUnit)`, `OnUnitDied(Unit dyingUnit, Unit? killerUnit)`,
  `OnKill(Unit killedUnit)`. The killer is null when no unit's damage killed: `Kill()` or `Life = 0` from code, timed
  life running out, or a unit's own damage.
- An event runs the type's reaction first, then its components' in the order added, then the game's, then those of
  the players whose units it involves (above). A reaction that fails is reported, and the others still run.
- An event the game fires inside a call a reaction makes runs its reactions inside that call: `next.Kill()` in an
  `OnDeath` runs the other unit's `OnDeath` before it returns. The game stops running events nested too deep, with no
  error: past 98 in a plain Lua map, and sooner in C#, where each `try` takes some of that room. Events nested more
  than 20 deep are reported once for the chain, with the reaction the newest one runs (`game events nested more than
  20 deep, ... at OnDeath for Bomb`), in a release build too; they still run. A chain that long starts its next step
  from a timer, which runs a moment later with nothing nested: `Game.Instance.After(0f, () => next.Kill())`.
- A buff is a class that inherits `Buff`, or a standard buff to show its icon. `Duration` and `Tick` set its time,
  `OnApply`, `OnTick` and `OnExpire` are its reactions, and `unit.Apply(new Burn())` puts it on a unit. A second
  apply of its class refreshes the one there, unless its `Stacking` says otherwise: that one lasts the new apply's
  `Duration` from then on, and keeps its other values and what it has counted. `Stacking.Strongest` keeps one buff whose
  `Strength` (set in the constructor) is the strongest of its applies still on: `OnApply` runs again whenever that
  changes, up or down, so a stronger aura takes over at once and a weaker one takes over again with no gap when it
  leaves. A buff class's own member named `Strength` hides it.
- The game's dispels (Dispel Magic, Abolish Magic, Purge) and spell immunity do not reach a buff put on by `Apply`: it
  goes on and its icon stays, on a spell-immune unit too. A dispel of the map's own removes it, with `unit.Remove<Burn>()`, which takes
  the first of a class, whoever added it.
- A buff class a spell lists (`Buffs.Set<Frozen>()` in an ability class, or a `[Replaces]` class of a standard buff)
  runs its code when the game puts its buff on, from a spell, an aura or an attack. Moonwell sees it in the same instant
  after a hit, a cast or an aura, and within a second otherwise; its `Source` is the caster, the attacker or the
  strongest aura holder in reach, and its `Level` that ability's level (null and 0 when nothing matched). A second cast,
  a hit or a stronger aura gives it again: the new `Source` and `Level`, and `OnApply` again. The game decides when it
  ends (the spell's duration, a dispel, death), and `Remove<Frozen>()` takes the game's buff off too, which ends its slow
  or stun. Such a class is not applied with `Apply`, does not set `Duration`, `Stacking` or `EndsOnDeath`, and has a
  constructor with no parameters (error MW0051); for the same effect from code, write a second class that shares the
  code through a base class.
- A component or buff belongs to the actor it is added to, whoever added it: it ends when it is removed or that actor
  ends. Held by a scope, it also ends when that scope ends: `unit.Add(new Haste(), heldBy: Item.Carry)`,
  `pair.Other.Apply(new Chilled(), Holder, heldBy: pair)`. A buff given again from another holder joins the one there,
  as its `Stacking` says, and ends with its last holder or at its `Duration`, whichever comes first; one given with no
  holder stays after its holders let go. A component is never joined, so two holders give two. A holder that has
  ended adds nothing, and a player as the holder is error MW0043.
- `Remove<T>()` takes the first component of the class, whoever added it, so an item that removes its gift in
  `OnDropped` or `OnUnequipped` takes another item's when two give the same class: warning MW0055. Give it with
  `heldBy: Item.Carry` instead, and it ends with this item's carry.
- Game work starts in a reaction. Top-level code and static fields make nothing in the game.

**Scopes.** What code makes belongs to the scope named where it is made, and ends when that scope ends: the game, a
unit, item or destructable, a component, a buff, an ability, a cast, an area, an interaction or its pair, or a gizmo.
Timers are made on their scope: inside a class, `After(2f, Puff)` and `Every(1f, Pulse)` are the class's own;
elsewhere the line names it, as `cast.Every(0.5f, Pulse)` or `Game.Instance.After(30f, Respawn)`. `Create`, `Fire`,
`Wall.Create` and `Interact` take the scope as an argument. Nothing has a default, so a static helper is given one. A
player is a scope too, but it lasts the whole game, and one where a scope goes is most often meant as the owner
(`Gizmo.Create(gizmo, x, y, scope, owner)`), so it is error MW0043: write `Game.Instance`, or pass it as the owner. A
map's classes cannot inherit `Scope` itself, or `Scoped`, `Actor`, `Widget` or `MapComponent`: a scope the map ends
itself is a game component, as `var round = Game.Instance.Add(new Round());` and later `round.End();`.

```csharp
public sealed class Respawns : UnitComponent
{
    public override void OnDeath(Unit? killerUnit)
    {
        Game.Instance.After(30f, Respawn); // the game's: runs even if the corpse goes first
        After(2f, Puff);                   // the component's: ends when the corpse goes
    }
}

static void Smoke(Scope scope, Unit unit) // a helper is told whose
{
    scope.Every(1f, () => Puff(unit));
}
```

- An ability's scope lasts as long as the caster is in the game, so what its `OnCast` makes there runs after the spell
  has ended; work that should stop with a channel is made on the cast: `cast.Every(0.5f, Pulse)`. On a spell with no
  channel a cast lasts only as long as the spell's animation, or no time at all.
- Death is not an end: a unit and its components last until the corpse goes, which can be at once (exploded or raised),
  so a respawn timer is made on the game.
- `After(0, action)` inside a timer runs in the same instant, as the game's own 0 s timer does. More than 10000 timer
  runs in one instant (a timer that keeps starting itself, say) are reported, and the rest wait 1/32 s. `Every` needs
  seconds above 0: 0 or less is reported, and that timer never runs.
- A development build reports a timer its scope ended in the instant it was made, before it ran: its line named a scope
  on its way out, as `After` in a missile's `OnImpact` does when a `Stop` ends the missile. Two game events in one frame
  can trip it when the line was right: a buff whose `OnApply` makes a timer, applied by a hit that kills, or a channel
  stopped in the frame it began. A halt or a step of the debug mode in between does not.

`Game.Time` is the seconds since the game started, on the timers' clock. `Game.TimeOfDay` is the hour of the game's
day, read and set: `6.5f` is 6:30, and a time past the day's 24 hours wraps (`26f` is 2:00). `Game.IsDay` is true from
dawn (6:00) up to dusk (18:00). `Game.DaySpeed` is 1 for an hour every 20 s, 2 for twice as fast, 0 to stop and below 0
to run backward; `Game.DayStopped = true` stops the day and keeps its speed for when it goes on. A map whose gameplay
constants change dawn, dusk or the day's hours gets its own, but the game reads no time past 24, and the build warns of a
longer day. The debug mode halts `Game.Time` but not the day.

A game component's `OnDawn` runs when `Game.IsDay` turns true, and `OnDusk` when it turns false: within 0.25 s after
the time passes the hour (Moonwell checks four times a second, on the timers' clock), and at once when `Game.TimeOfDay`
is set across it. A time set within the day, or over a whole night (17:00 to 7:00), runs neither, as it is day before
and after. Running backward, passing dusk is the dawn and passing dawn the dusk.

```csharp
public sealed class Torches : GameComponent
{
    public override void OnDusk() => Game.Print("Light the torches.");
    public override void OnDawn() => Game.Print("Put them out.");
}
```

`RealTime.Now` is the real-world date and time, in UTC and the same on every machine, so any code can use it:
`if (RealTime.Now.Month == 12)`. It is the first person's clock, shared while the map loads (the game's start waits for
it), and the game time since, so a long pause makes it run behind the wall clock; after a game saved with the game's
own Save Game is loaded, it is shared again. `RealTime.LocalNow` is this machine's clock in its own time zone, read
inside `Local.For`, to show a player their own time. A `RealTime` has `Year`, `Month`, `Day`, `Hour`, `Minute`,
`Second`, `DayOfWeek` and `Seconds` (since 1970), `AddDays` and the like, compares with `<` and `==`, and writes itself
as "2026-11-03 14:05". It lasts to January 2038, as the game's own clock does. .NET's `DateTime` and `TimeSpan` fail in
the game, whose integers are 32 bits, and are MW0030.

## Units, heroes and items

A unit's members read and change the game's. Once the game has removed a unit, item or destructable they give the last
value read or set, and setting does nothing. A member never read or set while it was in the game gives 0 then, as in
`OnLeave` after the game removed it, and a development build reports that read: read it into a variable while the unit
is in the game, and use that. Where a unit died is kept as its `X` and `Y` when a death reaction (`OnDeath`, `OnKill`,
`OnUnitDied`) sees the death, so a timer started there can read them after the corpse is gone. In the
quarter second before Moonwell notices a removal, a read that gives 0 on a removed unit keeps the last value too. A read is safe in
`Local.For`, a change is not.

| Unit | |
|---|---|
| `Life`, `MaxLife`, `Mana`, `MaxMana` | read and set; a `Life` of 0.405 or less kills, above the most it clamps; the most are whole numbers |
| `Owner` | read and set; the player the unit belongs to; setting gives the unit the new owner's colour |
| `X`, `Y` | read; `MoveTo(x, y)` through the game's pathing, `PlaceAt(x, y)` with none, not even the map's edge |
| `Facing` | read and set (the unit turns over time, [as its type's fields say](#turning), so a read at once gives the old one); `FaceNow(degrees)` turns at once, a walking unit turns back |
| `Level` | read; a hero's hero level; a unit's level is set only in its declared class's constructor |
| `IsAlive`, `Kill()` | whether it lives; kills it, with `OnDeath` run |
| `IsIllusion` | an illusion (Mirror Image, a Wand of Illusion) has its hero's type but is a plain unit, not the hero's class: no starting components, no `OnCreated`; map-wide reactions still see it |
| `IsPaused`, `Scale`, `FlyHeight` | read and set; a paused unit takes no orders but still animates; `Scale` reads its type's scale until set; `FlyHeight` also lifts a ground unit |
| `Name` | read inside `Local.For` only (each player's language), set anywhere outside it |
| `Icon` | its type's icon path, read inside `Local.For` only, as the game picks a type's icon by each player's graphics mode (the game's Phoenix names one icon for Reforged and another for the other modes); set only in its declared class's constructor, as the game has no icon per unit |
| `Tint(red, green, blue, alpha)` | colours the model, each value clamped to 0-255; an alpha of 0 makes the unit's portrait black too, where a `Scale` of 0.01 hides the model and keeps the portrait |
| `SetTeamColor(PlayerColor.Blue)` | draws the model's team-coloured parts in that colour, whoever owns the unit; nothing to read back; a new `Owner`, or a set of the owner's `Color`, gives it that player's colour again |
| `PlayAnimation(name)`, `QueueAnimation(name)` | as an effect's ([below](#special-effects)); it plays once, then an idle unit stands; a walking unit keeps walking and only stutters; a name the model lacks plays its stand, and a name picks no variant (`"attack 2"` is any attack) |
| `PlayAnimation(index)` | by the animation's index in the model, from 0: the one way to pick a variant; indices differ between models |
| `AnimationSpeed` | read and set, 1 unless set; 0.5 plays at half speed, 0 holds the unit still mid-animation |
| `AddAnimationTag(tag)`, `RemoveAnimationTag(tag)` | `AnimationTag.Defend`, `Alternate`, `Gold`, `Lumber`, `Work`, or any tag the game takes (`"upgrade first"`): the unit's animations play with it until it is removed |
| `Order(order)`, `OrderAt(order, x, y)`, `OrderOn(order, target)`, `Stop()` | orders by the game's order string, `Orders.Move` and the other constants, or a spell's (`"holybolt"`) |
| `UseItem(item)`, `UseItemAt(item, x, y)`, `UseItemOn(item, target)` | use an item the unit carries, as a click on it does, giving the game's answer; false once the unit, the item or the target is gone |
| `CurrentOrder`, `LastOrder`, `Reissue()` | the name of what the game says the unit does now, "" when idle; the last order worth giving again (below); gives it again |
| `InventorySize`, `ItemIn(slot)`, `Give(item)`, `Give(item, slot)`, `Drop(item)` | the inventory, slots from 0; `Give` and `Drop` run the pick-up and drop reactions inside the call; `Give` answers false, with no reaction, for a full inventory; giving an item the unit holds runs a drop and then a pick-up, but giving it into a slot only moves it, swapping with the item there |
| `AddAbility<T>()`, `RemoveAbility<T>()`, `HasAbility<T>()` | give an ability at level 1, kept through a morph, and return its object; take one, its type's own too; whether the unit has it, a carried item's included. Each takes an id too |
| `CastOn<T>(target, scope)`, `CastAt<T>(x, y, scope)` | cast a real spell for the unit through a hidden dummy ([below](#casting-real-spells)) |

`unit.AddAbility<Smite>().Level = 3` gives an ability and sets its level. A level the ability doesn't have is kept
between 1 and its last, as the game does, and a level set on an ability the unit lacks does nothing; a development
build reports both. Removing an ability ends a cast of it that is running, and adding it back clears its cooldown.

`OnOrdered(Order order)` on a unit's class and its components, and `OnUnitOrdered(Order order)` on game and player
components, hear each order a player or code gives (code's inside the call), before the unit carries it out:

```csharp
public sealed class Border : GameComponent
{
    public override void OnUnitOrdered(Order order)
    {
        // No walking past x 2000: a right-click is Orders.Smart.
        if (order.Kind == OrderKind.Place && order.X > 2000f && (order.Name == Orders.Move || order.Name == Orders.Smart))
        {
            order.Cancel();
        }
    }
}
```

- `order.Cancel()` refuses it: the unit carries on with the order it was carrying out, which Moonwell gives back, or stops
  when it had none (after a stop or a build), and the order's other reactions still run and read `IsCancelled`. An order
  given in a reaction replaces the one heard; a stop given there holds too, as Moonwell repeats it a moment later. A
  refused Shift-queued order ends the queue, as the game can't skip one.
- `order.Kind` is `None`, `Place` or `Target`; a target order's `X` and `Y` are its target's place, and one with
  neither the unit's. The game's own orders run nothing: the stunned order, undefend, and the order a stun's end gives
  back.
- `LastOrder` keeps the last three orders worth giving again (moves, attacks, right-clicks, patrols, holding position,
  spells; not stops, toggles or items), each reaching the one before with `Previous`; an order that repeats the last
  takes its place. `Reissue()` gives the last again, after a knockback say; a spell already cast is not cast again. A
  map listens for orders only when its code writes an order reaction or reads `LastOrder` or `Reissue`.

| Hero (a unit the game makes as a hero) | |
|---|---|
| `Experience` | read and set; a higher value gives the levels that come with it, a lower one keeps the level |
| `AddExperience(amount)` | adds, or takes with a negative amount, as `Experience` does; `showEffect:` turns the effect off |
| `SetLevel(level)`, `LevelUp()`, `Revive(x, y)` | `showEffect:` turns the effect off; a lower level takes levels away; `LevelUp` gives one level, none at the top |
| `ExperienceToNextLevel`, `LevelProgress` | read; what the next level still needs, and how far through the level from 0 to 100; 0 and 100 at the top level |
| `Strength`, `Agility`, `Intelligence` | the base, read and set |
| `StrengthWithBonus`, `AgilityWithBonus`, `IntelligenceWithBonus` | with items and auras, read |
| `StrengthBonus`, `AgilityBonus`, `IntelligenceBonus` | what items and auras add alone, read |
| `SkillPoints` | read and set |
| `ProperName` | read inside `Local.For` only, set anywhere outside it |

The experience each level needs, and the top level, come from the map's gameplay constants (`war3mapMisc.txt` in the
map or its assets, as for a hero's life above), else the standard ones: 200 for level 2, then 100 more per level, up
to 5400 for level 10. The build gives the map's own to the runtime.

| Item | |
|---|---|
| `Charges`, `Owner` | read and set; 0 charges leaves the item in place; `Owner` is the player the item belongs to: each pick-up makes it the carrier's player, and a drop leaves it |
| `X`, `Y` | read; `MoveTo(x, y)` goes to a pathable spot; while a unit holds the item, in its inventory, bag or equipment slots, they read the unit's place |
| `Carrier`, `IsCarried` | the unit that picked it up, while it still holds it: null for an item in a bag or equipped (the 3.0 inventory), and in `OnDropped` it is still the unit, except for a drop at a death or after `RemoveUnit` (below), where it is already null |
| `Carry` | a scope from each pick-up into the inventory or an equipment slot to the drop: what it holds, `heldBy: Item.Carry` in `OnPickedUp`, ends at the drop, with no `OnDropped` to write; still live in `OnDropped`; it ends inside `Drop`, and a moment after a drop the player makes, since the game fires a drop for a move between slots too, which keeps it; the 3.0 bag starts none, so a gift held by it is not given there |

| `Place` | read: `ItemPlace.Ground` (or gone), `Inventory` (the six slots), `Bag` or `Equipped` (the 3.0 slots) |

`ItemIn` is not for `Local.For`: it takes in an item Moonwell has not seen, which has to happen on every machine.
Moonwell listens for pick-ups and drops from the start, so an item a unit picks up is taken in; one a unit held before
the map began has no `Carrier`, and its `X` and `Y` read where it lay before. A unit whose inventory drops its items at
death (a footman's) drops them as it dies, and the drop reactions run before its `OnDeath`, with `Carrier` already null;
a hero keeps them. When a unit is removed from the game (`RemoveUnit`), its items leave it inside the call, but the game
fires the drops after it. For a unit Moonwell removed (`Remove()`, or its scope ending) no drop reaction runs; for one a
native removed, the drop reactions run for an object about to be swept, with `Carrier` already null. The items of a
removed hero are removed with it, as seen; other units are assumed to do the same. A member's exact behaviour is in its
comment.

| Destructable | |
|---|---|
| `Life`, `MaxLife` | read and set; a `Life` of 0.4 or less kills (0.405 does not), above the most it clamps, and on a dead destructable it does nothing; the most is a whole number, and a full destructable stays full |
| `IsAlive`, `Kill()` | whether it lives; kills it, with `OnDeath` run; it stays in the game, dead, until it is removed or restored |
| `Restore()` | brings a dead destructable back at its most life, with `OnRestored` run; `Restore(showBirth: true)` plays its birth animation; does nothing to a living one |
| `X`, `Y` | read; where the game put it, which is not always where it was asked: a tree asked at 1000 stands at 960 |
| `Open()`, `Close()`, `IsOpen` | a gate, as World Editor's gate action: `Open` kills it with its "death alternate" animation, `Close` brings it back with its birth; neither runs `OnDeath` or `OnRestored` |
| `Show()`, `Hide()`, `IsShown` | a hidden destructable keeps its life and is still found |
| `PlayAnimation(name)`, `QueueAnimation(name)`, `AnimationSpeed` | as on units, by name only |
| `Tint(red, green, blue, alpha)` | tints its model; nothing to read back |
| `Invulnerable`, `OccluderHeight` | read and set; `Kill()` and `Life` still kill an invulnerable one |
| `Name` | its type's name, in each player's language, so read inside `Local.For` |

A destructable brought back by the native `DestructableRestoreLife` runs no `OnRestored`, as the game tells nothing. The
game sends no damage event for a destructable and names no killer in its death, so there is no `OnDamaged`. A tree that
regrows is a component of Moonwell.Interactions: `tree.Add(new Regrow(30f))` brings it back 30 seconds after each death.

The game gives no handle to a doodad, so code can only play an animation on the doodads of a type in a place:
`Doodad.PlayAnimation<T>(x, y, radius, "death")`, with `nearestOnly: true` for the nearest one, and
`Doodad.PlayAnimationIn<T>(left, bottom, right, top, name)` inside a rect.

### Transports

A unit loaded into a zeppelin, a ship or a burrow runs `OnLoaded(transport)`, then its components, then the
transport's `OnCargoLoaded(cargo)` and its components, then `OnUnitLoaded(cargo, transport)` on the game's components
and on the cargo's player's. Coming out runs `OnUnloaded`, `OnCargoUnloaded` and `OnUnitUnloaded` the same way: for an
unload, an unload all, and a transport that dies or is removed, which puts its cargo down alive where it was. The game
has no unload event; Moonwell sees it from the order the game gives the cargo as it comes out. A unit that dies or is
removed inside runs no unload reaction.

`unit.Transport` is the transport carrying it, or null, and `transport.Cargo` the units aboard in the order they were
loaded; the game finds no loaded unit around a transport, so `Cargo` is the way to them. A loaded unit's `X` and `Y`
read where it was loaded. `CargoCapacity` reads and sets how much the cargo hold holds, for this unit alone (8 for a
zeppelin). `transport.Load(unit)`, `Unload(unit)` and `UnloadAll(x, y)` are the game's orders.

```csharp
public sealed class Ferry : Unit
{
    public override void OnCargoLoaded(Unit loadedUnit)
    {
        Owner.Print(Cargo.Count + " aboard");
        if (Cargo.Count == CargoCapacity) UnloadAll(4000f, 0f);
    }
}
```

## Placed things

What any editor placed in the map is in the class `Placed`, which the build writes from the map's files before it
compiles. Each member is named as World Editor names its variable, without `gg_`: a unit, item or destructable by its
type's id and number, a region, camera or sound by its name with `_` for spaces. A name that is gone from the map is a
compile error.

```csharp
Placed.Units.Hpal_0002?.Kill();                       // null once the unit has left the game
var spawn = Placed.Regions.Spawn_Left;                // a PlacedRegion: Name, Left, Bottom, Right, Top, CenterX, CenterY
Unit.Create<Footman>(player, spawn.CenterX, spawn.CenterY, Game.Instance);
Placed.Sounds.Intro?.Play();                          // with the Sound Editor's volume, pitch, 3D and fades
```

| Kind | In `Placed` | By a name built in code |
|---|---|---|
| Unit | `Placed.Units.hfoo_0001`, typed as the map's class when its `[ObjectId]` is the unit's type | `Unit.Named("hfoo_0001")` |
| Item | `Placed.Items.ratc_0005`, typed the same way | `Item.Named("ratc_0005")` |
| Destructable | `Placed.Destructables.LTg1_0002`, trees left out | `Destructable.Named("LTlt_0012")`, trees too |
| Region | `Placed.Regions.Spawn_Left`, a `PlacedRegion`; goes where `Bounds` does | `AreaShape.Region("Spawn Left")` |
| Camera | `Placed.Cameras.Intro`, a `PlacedCamera`; goes where `CameraSetup` does | `CameraSetup.Named("Intro")` |
| Sound | `Placed.Sounds.Intro`, a `PlacedSound` with `Play()`, `PlayAt(x, y)`, `PlayOn(unit)`, `Stop()` | `PlacedSound.Named("Intro")` |

- A lookup gives null for a name the map does not have, and once the thing has left the game; the build fails at the
  line of a name written as text that the map does not have.
- A unit or item placed of the map's own class comes back as that class, as `Unit.Of` gives it.
- A destructable is found nearest its place among those of its type, as the game makes most of them itself.
- `Placed` changes when the map does: after placing something in an editor, build once before naming it.

## Stats and scores

A stat is a number a unit or a player has: a base, then modifiers, flat or percent, each held by a scope, which takes it
off when it ends (an item's `Item.Carry`, a buff, a talent, the unit itself). Percents add up, then multiply:
(base + flat) × (1 + percents / 100), so +20% and +30% make +50%.

```csharp
public static class Stats
{
    public static readonly Stat SpellPower = new Stat("Spell power");
}

// A buff: +20% damage and -30% move speed while it is on.
protected override void OnApply()
{
    Target.Modify(Stat.Damage, percent: 20, heldBy: this);
    Target.Modify(Stat.MoveSpeed, percent: -30, heldBy: this);
}

// An item component: +5 armor and +10 spell power while carried.
Carrier.Modify(Stat.Armor, 5, heldBy: Item.Carry);
Carrier.Modify(Stats.SpellPower, 10, heldBy: Item.Carry);

var power = hero.Get(Stats.SpellPower);
```

`Modify` gives back the modifier, whose `End()` takes it off sooner. A holder that has ended adds nothing, reported in
development builds. A map's own stat (`new Stat(name)`, or `new Stat(name, base)`) is only a number the map reads, on
units and players. The game's stats are units' only, and change the unit in the game:

| Stat | |
|---|---|
| `Damage`, `Armor` | the card's green bonus, red when negative; whole numbers; a percent of damage is of the middle of the white damage |
| `AttackSpeed` | percent points, as an item's "+15% attack speed": no base |
| `MaxLife`, `MaxMana` | the unit's own most, shown white; the most life never goes below 1 |
| `MoveSpeed` | the unit's own, held to the map's least and most speed (150 to 400 unless its gameplay constants say otherwise) |
| `LifeRegen`, `ManaRegen` | the unit's own a second, to which a hero's strength and intelligence add |
| `Strength`, `Agility`, `Intelligence` | a hero's base, shown white; whole numbers |

A game stat never goes below 0. Its base is the game's number less Moonwell's part, so what the game changes meanwhile (a
level's life, a tome) stays; a percent of a base the game changed is put again at the stat's next change. `Get` reads
what the game shows, without what items and the game's own abilities add to damage and attack speed: a hero's +10% life
covers the life its strength gave when the life last changed, and 5 strength added after adds its life unscaled.
Damage, armor and attack speed go through three abilities Moonwell declares, so a map that reads them needs
Moonwell.Objects.

A score is a whole number for each player that the map counts itself, as a kill means something else in each map:

```csharp
public static readonly Score Kills = new Score("Kills");

public override void OnUnitDied(Unit dyingUnit, Unit? killerUnit)
{
    if (killerUnit != null && killerUnit.Owner.IsEnemy(dyingUnit.Owner))
    {
        Kills.Add(killerUnit.Owner, 1);
    }
}
```

`Kills[player]` reads it, 0 until counted, and `Set` sets it. Scores and stats are not saved by themselves: a save class
copies what it keeps ([Saves](#saves)). The game's own counts stay on its score screen, which a map cannot add to.

## Spells

A spell is an ability class: the game keeps its button, targeting, mana and cooldown, and the class adds what the
game cannot. `OnCasting` runs as the caster starts to cast, before anything is spent, and can refuse the cast;
`OnCast` runs when it takes effect; `OnFinished` only when it runs to its end.

```csharp
public sealed class Execute : Creeps.Abilities.Channel
{
    public override void OnCasting(Cast cast)
    {
        if (cast.Target!.Life > cast.Target.MaxLife / 2)
        {
            cast.Refuse("Target must be below half life"); // the game's error line; no mana, cooldown or effect
        }
    }

    public override void OnCast(Cast cast) => cast.Caster.Damage(cast.Target!, 500f);

    public override void OnFinished(Cast cast) => cast.Caster.Owner.Print("The channel ran its whole time.");
}

hero.AbilityOf<Execute>().ResetCooldown();
```

- `OnCasting` gets the cast `OnCast` gets later, with its target, place and level. `cast.Refuse(text)` stops the
  caster, shows the text to its owner where the game shows its errors, with the sound, and spends nothing; empty text
  shows no line. Anywhere but `OnCasting` it is too late, and a development build reports it.
- A cast stopped before it takes effect ends with no `OnCast`. `OnFinished` runs before the cast ends, when a channel
  ran its whole time or another spell its casting animation; one stopped early, by a stun, an order or a death, ends
  without it.
- These run on the ability class only, not on the caster's components or the game's, and a dummy's cast runs none.
- Cooldown and mana, per unit: `CooldownLeft` reads what is left; `StartCooldown(seconds)` starts it at any length,
  shown on the button, and 0 makes the spell ready, as `ResetCooldown()` does. `LiveCooldown` and `LiveManaCost` read
  and set the full cooldown and the cost at the ability's level now, for its next casts, where the declared fields
  `Cooldown` and `ManaCost` set every unit's in the constructor; a level gained later has its own. On a unit without
  the ability they read 0 and setting does nothing; a development build reports it.

## Casting real spells

A unit, or a player with no unit, casts any spell through a hidden dummy: a Storm Bolt from a hero's passive, a trap's
Flame Strike, an item's Polymorph. The spell is the game's own, with its looks, immunities and rules.

```csharp
public sealed class Tempest : Creeps.Abilities.Channel
{
    public override void OnCast(Cast cast)
    {
        Unit.CastOn<Human.Abilities.StormBolt>(cast.Target!, cast, level: Level);
        Unit.CastAt<Human.Abilities.Blizzard>(cast.X, cast.Y, cast); // stops when this cast ends
    }
}

Player.NeutralAggressive.CastAt<Human.Abilities.FlameStrike>(x, y, Game.Instance);
```

- `CastOn<T>(target, scope, level: 1)` casts at a unit, item or destructable, `CastAt<T>(x, y, scope, level: 1)` at a
  place, and each takes an id too: of the map's own abilities, declared or from World Editor, or of a standard one its
  code names as a class. A spell with no target (Thunder Clap) is cast around the target or the place. They
  answer as the game does: false for a target the spell can't take (an ally for an enemy spell, a spell-immune or dead
  unit), a spell the player hasn't researched, a unit cast for that is gone, or a scope that has ended.
- The dummy stands at the unit cast for, so a missile flies from it, and the spell's range, mana and cooldown are taken
  off the dummy's own copy: the unit spends none and needn't have the spell. Hero skills cast at any level they have.
- The spell's buff, hits and kills are the unit's: a spell buff's `Source`, a hit's `Source` (the unit's
  `OnDealtDamage` runs), and the killer in `OnDeath`, `OnKill` and `OnUnitDied`; the game gives the bounty and
  experience to its player. A player's cast names no unit: the `Source` and the killer are null.
- A dummy's cast runs no cast reaction: not the spell class's `OnCast`, not the unit's, not `OnUnitCast`. Call a spell's
  own code directly.
- Ending the scope stops what the dummy still does, a channel; a missile or a wave already on its way still lands. A
  channel also stops when the unit cast for changes hands, as the unit's own would.
- A target in the player's fog is seen for the instant of the cast: its ground stays explored afterwards, and the game
  shows it for a moment as the spell lands. An invisible target is refused, as for any caster.
- A spell that needs research the player lacks is refused, and a development build says which; a copy without the
  requirement casts:
  ```csharp
  [ObjectId("A0P1")]
  public sealed class DummyPolymorph : Human.Abilities.Polymorph
  {
      public DummyPolymorph() { Requires.Remove<Human.Upgrades.SorceressAdeptTraining>(); }
  }
  ```
- A spell aimed at its caster lands on the dummy: one whose targets are only "self" is reported in a development build,
  and one with no target that acts on its caster alone (Avatar, Divine Shield) does nothing useful.
- The dummy is a unit type the build declares when the map's code names `CastOn` or `CastAt`, with the id `hMWd`, which
  no class or object of the map may have. Map code never meets a dummy: no `OnUnitCreated`, no order reactions, no object
  from `Unit.Of`. A dummy keeps its player until its spell is over (its channel, its duration and its missile's
  flight), as a hit names its source when it lands, and goes with its unit when the unit changes hands.
- The calls change what every machine shares, so they are not for `Local.For`.

## Summons

`unit.Summon<T>(x, y, seconds, scope)` makes a unit of class `T` for the unit's player, as the game's summons are: it
has the summoned type, so a dispel hurts it (Dispel Magic took 200 life), and with seconds above 0 the game's timed
life and its bar, which kills it with no killer when it runs out. `Summon(typeId, ...)` takes an id.

```csharp
public sealed class CallWolves : Creeps.Abilities.Channel
{
    public override void OnCast(Cast cast)
    {
        // At most three wolves from one shaman: a fourth kills the oldest. They die with him too.
        cast.Caster.Summon<SpiritWolf>(cast.X, cast.Y, 60f, this, limit: 3, diesWithSummoner: true);
    }
}

public sealed class Howl : GameComponent
{
    public override void OnUnitSummoned(Unit summoningUnit, Unit summonedUnit) => summoningUnit.Owner.Print("A summon!");
}
```

- `summon.Summoner` is the unit that summoned it, and `unit.Summons` the living ones it summoned, oldest first. The
  game's own summons (a Water Elemental, Feral Spirit's wolves, an illusion) are linked too, from the game's summon
  event, once the map's code reads `Summoner` or `Summons` or a reaction below is written; a dummy's spell links them
  to the unit it cast for.
- A summon dies when its scope ends, as when its time runs out, and leaves its corpse; it is not removed. Made in a
  scope that has ended, it dies at once and runs no reaction. With `diesWithSummoner: true` it dies when its summoner
  dies or leaves the game; otherwise it lives on, as a Water Elemental outlives its Archmage.
- `limit:` above 0 keeps at most that many of the class from that summoner: a summon past it kills the oldest.
- `OnSummon(Unit summonedUnit)` runs on the summoner's class, then its components, then `OnUnitSummoned(summoningUnit,
  summonedUnit)` on the game's components and the summoner's player's, for a summon by code or by the game. The summon's
  own `OnCreated` and `OnUnitCreated` run first, while its `Summoner` is still null.

## Special effects

A special effect is a model the game draws at a point or on a unit: a glow while a buff lasts, a ring when a spell is
cast. It is in the scope named where it is made, and ends with it.

```csharp
public sealed class Blessed : Buff
{
    protected override void OnAdded() // once: OnApply runs again at each refresh
    {
        Unit.AddEffect(@"Abilities\Spells\Human\InnerFire\InnerFireTarget.mdl", AttachPoint.Overhead, this);
    }
}

SpecialEffect.PlayOnce(@"Abilities\Spells\Human\ThunderClap\ThunderClapCaster.mdl", x, y); // made and gone at once
var mark = SpecialEffect.Create(@"Abilities\Spells\Other\TalkToMe\TalkToMe.mdl", x, y, this, shownTo: caster.Owner);
mark.Height = 60f;
After(3f, mark.End);
```

| SpecialEffect | |
|---|---|
| `X`, `Y`, `Height`, `PlaceAt(x, y)` | read and set; `Height` is above the ground and stays so as it moves; on a unit's effect `X` and `Y` read the unit's place, `Height` 0, and setting does nothing |
| `Facing`, `Pitch`, `Roll` | read and set, in degrees; `Facing` reads 0 to 360 as a unit's does, and a positive pitch noses it down |
| `Scale`, `AnimationSpeed` | read and set, 1 unless set |
| `Tint` | read and set, a `Color`; its alpha is the model's opacity; white unless set |
| `TeamColor` | read and set, a `Player`: the model's team-coloured parts are drawn in that player's colour; null unless set, and setting null does nothing |
| `PlayAnimation(name)`, `QueueAnimation(name)` | by name: `Animation.Stand`, `Animation.Attack`, or any name the model has (`"attack slam"`); the name picks exactly, so `"morph"` is the plain one and `"morph alternate"` the other |
| `PlayAnimation(type)` | by kind: `AnimationType.Morph`, `AnimationType.Attack`; which of that kind plays goes by the effect's sub-animations, so with none it is the plain one |
| `AddSubAnimation(sub)`, `RemoveSubAnimation(sub)`, `ClearSubAnimations()` | `SubAnimation.Alternate`, `Gold`, `Defend` and the rest of the game's: the word after the kind in an animation's name; with `Alternate` added, `AnimationType.Morph` plays "Morph Alternate" |
| `PlaysDeath` | true unless set; false makes it vanish at once instead of playing its death |
| `Model`, `Unit`, `AttachPoint`, `IsShownTo(player)` | read |
| `Handle` | the game's effect, for anything Moonwell does not do: stretching by axis |

- `End()` ends an effect, and its scope's end does too. It plays its death animation, the way the game's effects go,
  unless `PlaysDeath` is false. A model with no death animation stands about 4 to 5 seconds before it goes, so set
  `PlaysDeath = false` for one to go at once. An effect on a unit also ends when the unit ends, if that comes first.
- `unit.AddEffect(model, point, scope)` puts one on a unit, at an attachment point of its model, and it follows the
  unit; it stays over the corpse when the unit dies, until the corpse goes. `AttachPoint` has the common points
  (`Overhead`, `Origin`, `Chest`, `HandLeft`, `WeaponRight` and the rest) as the text the game takes, so any other text
  works too (`"chest mount"`); Moonwell does not check it, and at a point the model lacks the game draws nothing. Effects on items
  and destructables are not drawn, so only units have it.
- `SpecialEffect.PlayOnce(model, x, y)` and `unit.PlayEffectOnce(model, point)` play one once: it is made and ended at
  once, and the game shows its death. One that needs a scale or a tint first is made with `Create`, set, and `End()`ed.
- `shownTo: player` on any of them shows it to that player only: the other machines make it with an empty model, so the
  game's handles stay the same everywhere. Who sees it cannot change later. Seen in single player; with two players it
  is not checked yet.
- Reads give what the map set, the same on every machine, so they are fine inside `Local.For`; setting and making are
  not.
- An effect made on a cast with no channel time can end in the instant it is made, and show only its birth and death:
  make one that should last on the ability, as `mark` above, and end it with `After`.
- With `Moonwell.Maths`, `effect.Position()` and `effect.PlaceAt(vector)`.
- Missiles keep their own: `Model`, `Scale`, `Tint` and `AddEffect` on the missile, and `missile.Effect` for the rest.

## Floating text

Floating text is words the game draws at a place or over a unit: a damage number, gold, a label. As with effects, one
is played once or held in a scope.

```csharp
target.PopText("-" + (int)hit.Amount, Color.Red);                          // rises, fades and goes by itself
FloatingText.Pop("+25", x, y, Color.TooltipGold, shownTo: killer.Owner);   // for one player

var label = boss.AddText("Enraged", this);                                  // follows the unit
label.Color = Color.Red;
var sign = FloatingText.Create("Shop", x, y, Game.Instance);
sign.Duration = 3f;                                                         // goes after 3 s,
sign.FadeOut = 1f;                                                          // fading over the last 1
sign.Effect = TextEffect.Rainbow();                                         // its colours turning by themselves
```

| FloatingText | |
|---|---|
| `Text`, `Size`, `Color` | read and set; `Size` is World Editor's font size, 10 unless set; `Color`'s alpha is the opacity a fade lowers from |
| `Height`, `PlaceAt(x, y)` | read and set; above the ground at a place, above the health bar on a unit (0 is just over it); `PlaceAt` does nothing on a unit |
| `Speed`, `Angle` | read and set; how fast it moves by itself, a second, and which way in degrees (90, the default, is up the screen) |
| `Duration`, `FadeOut` | read and set; the seconds from when it was made to when it ends by itself (0, the default, lasts until ended), and the seconds before that end over which it fades |
| `Effect` | read and set; a `TextEffect` that moves its colours by itself, null unless set ([Colours and text](#colours-and-text)) |
| `Unit`, `IsShownTo(player)` | read |

- `FloatingText.Pop(text, x, y)` and `unit.PopText(text)` play one once: it rises 64 a second, lasts 2 s and fades over
  the last 1. A colour follows the text, and `shownTo:` a player. A unit's starts above its health bar and rises from
  there.
- `FloatingText.Create(text, x, y, scope)` and `unit.AddText(text, scope)` hold one in a scope, which ends it when it
  ends, as `End()` and its `Duration` do. A unit's follows the unit, stays over its corpse after a death, and ends when
  the unit ends, if that comes first. It does not move by its `Speed`.
- Text on a unit shows only to the players who see the unit: in the fog, or invisible, it is hidden. A Pop is checked
  when it is made, a held text as it follows.
- `shownTo: player` makes the text on that player's machine only, so each player has the game's room for text to
  themselves. A text takes no handle id (seen in single player); with two players it is not checked yet. Who sees it
  cannot change later.
- Moonwell times and fades every text itself, on the timers' clock, and never lets the game end one: the game gives an
  ended text's number to the next text made, and a late end would take that one. 120 texts at once all showed; the
  game's old limit of 100 was not seen.
- Reads give what the map set, the same on every machine, so they are fine inside `Local.For`; setting and making are
  not.

## Sound and music

```csharp
Sound.Play(@"Sound\Interface\Rescue.wav");                  // 2D, everyone
Sound.Play(path, x, y, volume: 60);                         // 3D at a place, once
unit.PlaySound(path, heardBy: unit.Owner);                  // 3D on the unit, one player

var hum = unit.AddSound(@"...\Loop.wav", this);             // loops, follows the unit, ends with the scope
hum.Volume = 50;
hum.FadeOut = 1f;

Music.Play(trackA, trackB, trackC);                         // a playlist
Music.Play(new[] { trackA, trackB, trackC }, shuffle: true);
Music.PlayOnce(victory);                                    // cuts in once, then the playlist goes on
Music.Stop();
Music.SetVolume(60);
```

- `Sound.Play(path)` plays a file once, 2D, as loud on the whole screen; `Sound.Play(path, x, y)` 3D at a place, and
  `unit.PlaySound(path)` 3D on a unit, which it follows. `volume:` is 0 to 100. The game frees each when it has played.
- `Sound.Create(path, scope)`, `Sound.Create(path, x, y, scope)` and `unit.AddSound(path, scope)` loop until their scope
  ends, as `End()` does, or the unit ends. `Volume` (0 to 100) and `FadeOut` (0.5 s unless set, 0 to stop at once) are
  read and set, as on the physics package's `LoopingSound`, and `PlaceAt(x, y)` moves one at a place. The fade runs on a
  timer of the game's, so it outlives the scope.
- `heardBy: player` plays it for that player only: every machine makes and plays the sound, the others at volume 0, so
  the game's handles stay in step. Reads give what the map set, the same on every machine.
- `Music.Play` plays one file again and again, or a playlist: its files in order from the first, then again.
  `shuffle: true` mixes the order once, with the game's random numbers, on every machine. Moonwell plays a playlist
  itself, one file at a time, each for its length: the game's own list plays once from a place it picks, then goes back
  to the map's music (seen). The lengths are read on the first person's machine and shared, so the first file plays at
  once and the next comes at the same moment on every machine. The music goes on while the game is paused, and the
  playlist moves on when play does; it goes on after a saved game loads (seen).
- `PlayOnce` cuts in with a file that plays once, on Moonwell's clock too; the playlist then goes on from the file it is
  at, and with no playlist the music stops. It is not the game's own cut-in (`PlayThematicMusic`), after which a stop
  did not hold (seen). `Stop()` fades the music out, the file played once included; `Stop(fadeOut: false)` stops it at
  once. After a stop the map has no music until the next `Play`, its own included.
- Every `Music` call takes `heardBy:` too, which changes the music for that player only; the others' stays as it is.
- A file of the map's own goes in `assets/` ([Assets](#assets)). A path to one of the game's sounds works with `.wav`,
  `.flac` or `.ogg` alike.

## Weather, lightning, vision and the minimap

Weather, lightning, a player's vision and minimap icons are in the scope named where they are made, and end with it,
as effects do. Places are plain numbers: a rect is its left, bottom, right and top edges.

```csharp
var rain = Weather.Create(WeatherKind.LordaeronHeavyRain, this);               // the whole map
Weather.Create(WeatherKind.NorthrendLightSnow, -2048f, -2048f, 0f, 0f, this);  // a rect
var drain = Lightning.Between(LightningKind.DrainLife, caster, target, cast);  // follows both, ends with the cast
Lightning.PlayOnce(LightningKind.ChainLightningPrimary, caster, first);        // one hop
var sight = player.Reveal(x, y, 600f, this);
player.Hide(x, y, 600f, this, black: true, overUnits: true);
Game.Ping(x, y);
var skull = boss.AddMinimapIcon(MinimapIcon.Boss, Color.Red, this);
```

- `WeatherKind` names the game's 48 weathers (rain, snow, wind, dungeon fog, rays of light, energy fields and the
  rest), and any id of the map's own Weather.slk works too. `Weather.Create(kind, scope)` covers the whole map,
  `Weather.Create(kind, left, bottom, right, top, scope)` a rect.
- `Lightning.Create(kind, x1, y1, x2, y2, scope)` draws one between points, which `MoveTo` moves;
  `Lightning.Between(kind, from, to, scope)` one between units, moved every 1/32 s, which also ends when either unit
  ends. Each end is drawn `FromHeight` and `ToHeight` (60 unless set) above the ground, or above a unit's feet and
  its flying height. Of the game's looks, Drain Life, Drain Mana and Aerial Shackles stay drawn; Chain Lightning,
  Healing Wave, Spirit Link, Mana Burn, Mana Flare, Forked Lightning and Finger of Death fade at once by themselves
  (seen), so they are for `Lightning.PlayOnce`, which draws one and removes it 2 s later. `LightningKind` says which
  each is; the rest are not seen yet.
- Every player sees a lightning, in the fog and the black mask too (seen), so a link to a unit in the fog shows where
  it is. The game draws no colour set on a lightning (seen), so it has neither a tint nor `shownTo`.
- `player.Reveal(x, y, radius, scope)` lets the player see a circle, `Reveal(left, bottom, right, top, scope)` a rect
  and `Reveal(scope)` the whole map; allies who share the player's vision see it too (seen). The ground stays explored
  after. `player.Hide` covers a place in fog, or in the black mask with `black: true`; `overUnits: true` asks the game
  to cover what the player's units see there too: it beats a Reveal or Hide without it (seen). Of a player's visions on
  one place, the newest wins: the game itself keeps the oldest once it recomputes the fog, so each new vision makes the
  player's older ones again after its own (seen).
- `Game.Fog` and `Game.BlackMask` turn the fog of war and the black mask off and on for every player. With the fog off
  and the mask on, the ground a player has not explored is still black (seen). For one player, reveal the whole map:
  `player.Reveal(this)`.
- `Game.Ping(x, y)` pings the minimap for everyone and `player.Ping(x, y)` for one player, in white for 3 s unless
  given a colour and seconds; they change nothing in the game, as `Print` does. `PingStyle.Flashy` adds rings, and
  `PingStyle.Attack` is the game's red warning ping. A pure red (255, 0, 0) is drawn 254, 0, 0, as `Blizzard.j` does,
  since the game would make it the warning ping.
- `MinimapIcon.Create(path, x, y, color, scope)` puts an icon on the minimap and `unit.AddMinimapIcon(path, color,
  scope)` one that follows a unit and ends when the unit ends. `MinimapIcon.QuestGiver`, `QuestObjective`,
  `QuestBonus`, `QuestTurnIn`, `Boss` and `ControlPoint` are the campaign's, the same in HD and SD, and any model works.
  An icon shows over the fog and the black mask, as the campaign's do.
- `shownTo: player` on weather and icons shows them to that player only: every machine makes them and turns them on
  (weather) or leaves them shown (icons) at that player's alone, so the game's handles stay the same everywhere. Seen
  in single player; with two players it is not checked yet.
- Reads give what the map made, the same on every machine; making, setting and ending are not for `Local.For`, and
  pings are fine anywhere. `Handle` on each is the game's, for anything Moonwell does not do.

## Damage

`OnDamaged` sees a hit after armor. `source.Damage(target, 10f)` deals damage from code, as a spell does: magic,
of the spells attack type. `DamageKind.Fire`, `attackType:`, `isAttack:`, `ranged:`, `sound:` and `tag:` (read back as
`damage.Tag`) change it. The reactions run inside the call, which returns the damage they saw, or null when the game
dealt nothing. In a unit's own class, write `this.Damage(target, 10f)`: there `Damage` alone is the field, attack 1's
base damage, and is error MW0050.

For more, add the `Moonwell.Damage` package to the project, at the same version as
Moonwell (the template's `.csproj` has the line, commented). The build starts it once the map's code uses one of its
types, and a map that does not carries none of its code. Its reactions come through interfaces, which a unit's class,
its components and buffs implement, writing only the methods they need as public methods:

```csharp
public sealed class Shield : Buff, IDamageTarget
{
    public void OnTaken(Hit hit) { hit.Amount *= 0.5f; }
}
```

The game sends two events for each hit. Before armor, the hit has its full amount and its types can still change;
after armor, the target's armor has cut it and only the amount can change. The life drops after that. Armor cuts only
`DamageKind.Normal`, what attacks deal, so `source.Damage`'s magic is not cut.

| Stage | When | `IDamageSource` | `IDamageTarget` | `IDamageWatch` |
|---|---|---|---|---|
| Before armor | the hit starts | `OnDealing` | `OnTaking` | `OnUnitTaking` |
| After armor | armor has counted | `OnDealt` | `OnTaken` | `OnUnitTaken` |
| Lethal | the hit would kill | `OnKilling` | `OnDying` | `OnUnitDying` |
| Landed | 0 s later, the life has dropped | `OnLanded` | `OnHurt` | `OnUnitHurt` |
| Burst | 0 s later, once per source | `OnBurst` | | `OnUnitBurst` |

- Each stage runs the source and its components, then the target and its components, then the game's components that
  implement `IDamageWatch`, then the target's player's and the source's player's (once when one player owns both; the
  burst, the source's player's only). A damage meter of every hit goes on a game component. `OnDamaged` and
  `OnDealtDamage` still run, first in the after-armor stage.
- A `Hit` has the amount, `Original`, `IsAttack`, `IsSpell`, `IsMelee`, `IsRanged`, `IsCode` and `Tag`. Before armor
  a reaction may also change `DamageKind`, `WeaponSound`, `AttackType`, the target's `DefenseType` and its `Armor` for
  this hit; the target has its own defense type and armor back afterwards. A `DamageKind` or `WeaponSound` the game
  has no number for, such as a cast of 99, is refused: a development build reports it, and the hit keeps the one it had.
- The lethal stage sees the life after any hit the after-armor reactions dealt, such as an on-hit bonus, and after any
  hit an earlier lethal reaction dealt. `hit.Spare()` keeps the target alive, and the lethal reactions after it no
  longer run. In the landed stage `hit.Killed` tells whether this hit was the one that killed.
- `source.Damage(...)` goes through the stages too, and what it returns is the `Hit`: `(Hit?)source.Damage(target, 10f)`.
  It is null when the hit dealt nothing, as when spell immunity stops it; such a hit runs before armor only.
- Every hit made while a reaction runs belongs to the hit it reacted to, whether it comes from `source.Damage`, a native
  or an area. A chain ends at 8 hits, so two units that hit each other back never loop: a ninth `source.Damage`
  throws, which ends that reaction with its error, and a ninth hit from the game is set to 0 and reported.
- A hit of 0 that the game starts and that is no attack, such as Storm Bolt's first and Fireball's extra two, runs no
  reaction of the package; `OnDamaged` still sees it. An attack of 0, and a hit of 0 from `source.Damage`, are not
  silenced.
- A reaction the package would never call is an error, MW0036, for the physics interfaces (`IPushTarget`,
  `IMissileTarget`) as for these: one that is not public or takes another parameter, a
  public one a letter off a reaction's name, a name from before 0.3, a reaction in a unit or component without its
  interface, or an interface on a class the package does not call it on. `#pragma warning disable MW0036` keeps a
  helper's name.

## Maths

Add the `Moonwell.Maths` package (the template's `.csproj` has the line, commented) for vectors, angles, easing,
bounds, polygons and raycasts. A map that does not reference it carries none of its code.

```csharp
var start = target.Position();
var end = start.Offset(caster.AngleToPoint(target), 300f);
target.PlaceAt(Vector2.Lerp(start, end, Ease.OutQuad(t)));
```

- Angles are degrees, as `Facing`: 0 along +X, 90 along +Y. `MathF.Sin` takes radians; use `Angles.Sin`.
- y grows north, where Godot's grows down the screen: `Vector2.Up` is `(0, 1)` here and `(0, -1)` there, and `Rotated`
  and `Cross` turn the other way on the screen. In a `Vector3`, height is Z, as in the game's natives; Godot's is Y.
- `Vector2`, `Vector3`, `Bounds`, `Ray` and `RayHit` are values that never change once made, so they are shared and
  never copied. Their names follow Godot's where the idea is the same.
- A `Bounds` is made by `FromCorners`, `FromSize` or `FromCenter`. Its edges are inside it: `Contains` counts all four,
  and `Intersects` counts touching. Godot's `Rect2` leaves them out.
- In the game a vector is a table to make: in code that runs every tick on many objects, use the float forms in
  `Maths` (`Maths.Distance`, `Maths.AngleToPoint`, `Maths.Polar`), which make none.
- `unit.Position()` is a method. `PlaceAt` ignores pathing and keeps the unit's order, which a push wants; `MoveTo`
  goes through pathing and ends it.
- `Raycast.Circle`, `Segment`, `Bounds` and `Polygon` give the first hit along a segment in an `out` value, with a
  normal for `Bounce` and `Slide`. After a hit, a step goes on for the rest of its time with the new velocity, as
  below; moved only to `hit.Point`, something pulled into a surface at every step (by gravity) hits at the start of each
  step and stands where it landed. A segment that starts on a surface (within 0.02) hits it only when it goes in, and
  the hit is then put on the surface; one that starts deeper inside hits at once, with the normal against its direction.
- A wall (`Segment`) has no inside, so a point that slides along it, or meets it within 0.01 of where another wall
  joins it, can end up on its far side. Make something solid from a `Polygon` or `Bounds`.
- The game's numbers are not .NET's. These pass on .NET and go wrong only in the game:
  - A float that should be whole can land just under it, and a cast truncates: with `count` at 10, `(int)(count * 0.7f)`
    is 6. Round first: `(int)MathF.Round(count * 0.7f)`.
  - A loop on a float can run once more: `for (var t = 0f; t < 1f; t += 0.1f)` runs 11 times. Count in an `int` and work
    out `t = i * 0.1f`.
  - Added-up floats run low: a clock adding 0.03 each tick is 15 s short after an hour, and an addition drops what is
    below two float steps of its result (0.004 at 20,000). Count ticks in an `int` and multiply, or step by 1/32, which
    stays exact.
  - Compare with `Near`, not `==`: its own tolerance grows with the value, as a float's step does. It sees only the two
    numbers, so a short distance read at 20,000, where points are off by 0.002, needs a tolerance: `Near(a, b, 0.01f)`.
  - `MathF.Sqrt` is up to 2 parts in 100,000 high; `Maths.Sqrt`, `Length` and `DistanceTo` are right to a float's
    rounding. Sines and cosines are off by up to 6 parts in a million.
  - Dividing by 0 gives a number, and which one depends on what is divided (1 gives 2^127, 100 gives 0). A product past
    the float's range gives 0 or nonsense (1e30 times 1e30 is 0). Test first (MW0042).

```csharp
var left = dt;                                     // the step's seconds
for (var hits = 0; hits < 4 && left > 0f; hits++)  // at most 4 hits in a step
{
    var to = pos + vel * left;
    if (!Raycast.Polygon(pos, to, rock, out var hit)) { pos = to; break; }
    pos = hit.Point;
    vel = vel.Slide(hit.Normal);                   // or Bounce
    left *= 1f - hit.Fraction;
}
```

## Terrain

Add the `Moonwell.Terrain` package (the template's `.csproj` has the line, commented) for the ground's height, slope,
pathing and rays. A map that does not reference it carries none of its code and no grid.

```csharp
var z = Terrain.Height(target.X, target.Y);     // or Terrain.Height(target.Position())
if (Terrain.Raycast(from, to, out var hit)) { /* the missile meets the ground at hit.Point */ }
```

- The build writes the height grid from the map's terrain file, so it is the same on every machine. It does not see
  deformations or bridges; `Terrain.LocalHeight`, inside `Local.For` only, reads the live height and sees both.
- A height blends the four corners of each 128 tile, as the game does. It is the game's at the corners, and on Lost
  Temple it fit the game's within 0.05 on 8,536 of the 8,648 sloped tiles away from cliffs, ramps and water. Within 128
  of a cliff or ramp it is rough, up to 96 off, and it can miss at water's edge; over the rest of the ground it was 0.05
  off on average and 77 at worst. `IsCliff` tells a cliff tile, for treating it as a wall. Water reads its surface where
  that is above the ground. Over the water at the map's outer edge, far outside the playable area, heights can differ
  from the game's.
- `IsWalkable`, `IsFlyable`, `IsBuildable` and `IsWater` are terrain only, as the game's own. After changing pathing with
  natives, call `Terrain.Refresh(area)`. `CanStand` also sees trees and buildings and needs walkable ground, but it
  places a hidden item, so it changes game state and is not for `Local.For`.
- Pathing is read from the game a block of 16 by 16 cells of 32 at a time, the first time a cell in it is asked for
  (1,024 reads, about 0.3 ms), and kept: all of the largest map is about 8 MB of Lua's memory in the test host's Lua
  (not measured in the game).
- On a bridge, `Height` reads the water or ground beneath, and `CanStand` reads false. `IsWalkable` reads the map's
  pathing file, which World Editor writes walkable under a placed bridge's deck; under a bridge made at runtime, or in
  a map whose pathing file a tool wrote, it reads the water or ground beneath.
- `Terrain.Raycast(from, to, out hit)` is for a missile against the ground; a ridge narrower than 32 can be stepped over.
- Each query of a point takes `(x, y)` or a `Vector2`. A vector is a table in the game's Lua, so code that runs every
  tick on many points passes the floats.
  As with the maths raycasts, a missile goes on when its step moves on after a hit for the rest of its time. Only the
  part of a ray over the map is cast: past the map's edge it meets nothing, and a long ray reads the ground at most
  about 2,700 times (across the largest map, about 25 ms).
- A `Height` read costs about 9 microseconds in the game, so code that runs every tick on many objects should read it
  sparingly.

### Changing the ground

Every change is held in a scope and taken back when it, or the scope, ends; `Game.Instance` keeps it for the game. Each
returns a `TerrainChange` to `End()` early.

```csharp
Terrain.Raise(x, y, 300f, 200f, Game.Instance);                     // a hill; Lower digs
Terrain.Block(x, y, 128f, Pathing.Walk | Pathing.Build, cast);      // Allow gives pathing
Terrain.SetTile(x, y, Tile.LordaeronSummerGrass, 256f, cast);      // or a Bounds
Terrain.Blight(x, y, 400f, Game.Instance);                          // Cleanse takes it off
Terrain.WaterTint(new Color(120, 255, 120), poison);
Terrain.Ripple(x, y, 400f, 30f, 2f);                                // Wave and Shake too: look only
```

- Where changes of a kind overlap, the newest held wins, and ending one shows what is under it. Raises add up, as the
  game's craters do. `Height`, `Normal`, `Raycast` and physics follow a raise on every machine; ripples, waves and
  shakes are only for the look.
- A saved game keeps neither the game's own craters nor its painted tiles rightly. Moonwell takes its raises out of the
  game while a save is written and makes them again after it and at a load, and paints its tiles again at a load,
  before `OnGameLoaded`, so the ground comes back as it was; pathing lasts by itself. Craters and tiles a map makes
  with natives are not looked after.
- Pathing changes are per 32 cell, and the reads see them at once. A tile is painted per 128 corner; the game draws only
  the map's own tiles, so a development build reports another. The map class lists the tiles its code paints,
  `Tiles.Add(Tile.LordaeronWinterSnow, Tile.BarrensDesert);`, and the build adds those World Editor's list lacks to its
  end, painted ground unchanged. The game draws up to 64 (16 in a map saved before patch 2.0.3); past that the build
  fails.
- `Raise`, `SetTile` and `Block` also take a grid of corners as one change: `Terrain.Raise(x, y, columns, amounts,
  scope)` moves each corner by its own amount, from the map's corner nearest `(x, y)`, row by row from the bottom
  left. An amount or tile of 0, or `Pathing.None`, leaves a corner. A blocked corner takes the 16 cells around it.
- A raise is the game's lasting crater, and the game makes each new one slower the more it holds: about 10 µs with
  none, 50 µs at 25,000 and 500 µs at 85,000 (3.0.1.24371), so a grid raise of a corner each stalls past some tens of
  thousands. `Terrain.Raise(x, y, columns, amounts, scope, spacing: 4, radius: 768f)` raises with fewer, wider craters:
  one every `spacing` corners, an amount each, each moving the corners within its radius by its amount x cos(90 degrees
  x distance / radius), so a corner moves by the sum of the craters that reach it. Ending craters gives back neither
  their cost nor their memory at once: with 85,000 made and all ended, a new one still cost 110 µs.
- The game can't take off blight a building made, and a development build reports a `Cleanse` whose centre stays
  blighted. Blight the map was made with goes under an ended `Blight` too.
- The water can't be made, moved or raised at runtime. `WaterTint` multiplies its colour: white leaves it.
- `Terrain.Spread(radius, speed, (r, scope) => ..., scope)` grows a change from nothing to a radius; `Terrain.Trail(unit,
  seconds, (x, y, scope) => ..., scope)` lays one behind a moving unit, each piece ending after its seconds.

## Pathing

Add the `Moonwell.Pathing` package for where a unit fits, the nearest spot it can stand, free spots for a group, and
paths by its size. It brings `Moonwell.Terrain` and `Moonwell.Objects` with it.

```csharp
if (!caster.CanStand(x, y)) { var landing = caster.NearestSpot(x, y); }
var spots = Paths.Spots(31f, x, y, 5);           // five free spots, nearest first, not overlapping
var route = unit.FindPath(x, y);                   // route.Points, route.Length, route.Reaches
if (unit.CanReach(x, y)) { /* it can walk there and stand */ }
Paths.Wall(x1, y1, x2, y2, cast);                  // walkers and ships can't cross it while the scope holds it
```

- A unit takes a square of the game's 32 cells by its collision size: below 16 one cell, 16 to 31 two, 32 to 47 three,
  48 and up four. A gap of 64 lets sizes up to 31 through; 128 lets every size through.
- `CanStand`, `NearestSpot` and `Spots` are the game's own answer: a hidden helper of the unit's square is put there,
  about 10 microseconds a try. They see terrain, trees, buildings and the units standing there. A unit's own
  `CanStand` and `NearestSpot` leave the unit itself out, so its own place reads free: it gets Ghost (Visible) for the
  moment of the try, which fires no event and keeps its orders and selection.
- `FindPath` and `CanReach` are Moonwell's own search on the same cells. They see terrain, destructables, buildings and
  pathing set by code, never units. A target that can't be reached gives the route to the reachable spot nearest it,
  where the game's own walker would stop, with `Reaches` false. The game's walkers also go round units that stand in
  the way when they are ordered, and stop farther from a unit than its square needs.
- A route never squeezes between two blocked cells that touch only at a corner, as the game's walkers don't.
- `Paths.Wall` takes walking and floating from the cells along a line through `Terrain.Block`, one cell thick; fliers
  cross it. It ends with its scope.
- Ships take `Movement.Float`, amphibious units `Movement.Amphibious` and fliers `Movement.Fly` (a straight line); a
  unit's own calls take its movement from the unit. Foot, horse and hover units are all `Movement.Ground`.
- A path search keeps what the game said of each cell. Moonwell's own destructable changes, the buildings it sees come
  and go, a building's death, and the death of a destructable next to a cell a search found blocked all drop it. After
  changing destructables or buildings with natives, call `Paths.Refresh(area)`.
- These calls move a hidden helper or item, so they change game state and are not for `Local.For`.

## Procedural terrain

Add the `Moonwell.Procgen` package to make ground from a seed while the game runs: hills, the tiles on them, where
units can't walk, and trees. It brings `Moonwell.Terrain` and `Moonwell.Maths` with it.

```csharp
using Moonwell.Procgen;

var hills = new TerrainGenerator(Game.Random.Int(0, 1000000)) { Highest = 400f, FeatureSize = 2048f };
hills.Paint(Tile.LordaeronSummerGrass);
hills.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.5f, cover: 0.6f);   // patches on the upper half
hills.Paint(Tile.LordaeronSummerRock, fromSlope: 25f);                  // later rules paint over earlier ones
hills.Block(Pathing.Walk | Pathing.Build, fromSlope: 35f);
hills.Level(spawn.X, spawn.Y, 512f);                                    // a level place, never blocked
hills.Plan(Terrain.Playable).Apply(Game.Instance);
```

- The same seed, settings and area give the same ground on every machine and in every game. A seed from
  `Game.Random` gives new ground each game; a fixed number gives the same ground each time.
- The ground comes from where each corner is in the world, so an area's hills depend on where it is, and plans of
  one generator meet where their areas touch: plan a world in pieces with `EdgeFade = 0` and the shared edges have
  the same heights, tiles and blocked corners, a `Level` place across an edge too.
- `Plan(area)` only works the ground out: a `TerrainPlan` with a height, a tile and blocked pathing for each 128
  corner. A map may change it (`plan.Heights[column, row]`, `plan.Tiles`, `plan.Blocked`) before `Apply`.
- `Apply(scope)` makes it as terrain changes held in the scope: ending the scope takes the ground back.
- `Make(area, scope)` makes the same ground in steps over frames, for an area that would freeze the game in one go, and
  gives a `TerrainMaking` to draw a bar from (the bar is the map's own):

  ```csharp
  var making = hills.Make(Terrain.Playable, Game.Instance);
  Every(0.1f, () =>
  {
      bar.Value = making.Progress;   // 0 to 1
      label.Text = making.Step;      // "Roads", "Heights", "Tiles", "Blocked", "Trees", "Places"
  });
  making.Done += StartPlay;
  ```

  `Progress` never goes back and is 1 only once the ground is made; `Done` runs once, then. Each step does a fixed
  amount of work counted in corners, never timed, so every machine makes the same corners at the same moment.
  `msPerStep` (4 unless set) is how much: more finishes sooner and costs smooth frames, as suits a loading screen that
  holds play. It takes the settings as they are at the call, so a plan made this way cannot be changed before it is
  made. Ending the scope stops it and takes back what was made so far. A crater counts as costlier the more are held,
  as the game makes them, so late steps of the heights stay near `msPerStep`. A development build reports the first
  step that took over a second of real time, with how many craters the making had asked for by then.
- Heights stay between `Lowest` and `Highest` (150 below to 150 above, unless set), measured from the ground as it
  was; most areas reach close to both, and a few corners sit flat on them. A range all above 0 makes a plateau with
  a steep rim. `EdgeFade` (512 unless set) brings the heights back to the ground at the area's edge, so an area
  inside a hand-made map has no seam; set it to 0 for pieces that meet each other.
- An area of up to 20,000 corners is raised a crater a corner. A bigger one, as a whole 480 by 480 map, would need
  more craters than the game can hold (see Terrain), so `Plan`, `Make` and `Apply` raise it with one wide crater every
  4 by 4 corners, fitted to the hills, and the plan's heights are what those craters make, so tiles, blocking and trees
  follow the ground as it is. Its hills are a little smoother than a small area's: on `SnowyForest`'s settings typically
  16 to 18 off, and most of the steep rocky corners gone. Level places and roads keep their heights exactly, with a
  crater a corner. Its heights come down to the ground as it was at its edge, whatever `EdgeFade` is, so big areas
  don't meet seamlessly. A whole 480 by 480 map of `SnowyForest` with a road was made at 20 ms a step in 19 s
  (3.0.1.24371). For every bump of the plan over a map that size, grow the world instead (`Grow`).
- Since 0.15, the same seed gives other ground than in 0.13 and 0.14: the ground follows the world, an area's
  heights are no longer stretched to its own lowest and highest, and a `Level` place is level at the ground as it was.
- A rule's heights are a share of the range (0 lowest, 1 highest) and its slopes are degrees. Slopes are read from the
  heights the plan adds, so the rules fit ground that starts flat.
- The game makes no cliffs, ramps or water while it runs, so the ground is slopes, and tiles come from the map's own
  list: declare the ones `Paint` uses in the map class's `Tiles`. A height changes no pathing by itself: `Block` rules do.
- The minimap keeps the ground World Editor saved (`war3mapMap.blp`): with a whole map made into snowy forest it still
  showed the old tiles and no trees (3.0.1.24371). That nothing redraws it while the game runs is inferred.
- Trees are real destructables: they block, peasants cut them for lumber, they die and `Regrow` brings them back. Three
  rules stand them, each limited by height, slope and tile as `Paint` is:

  ```csharp
  var tree = ObjectTypes.FourCC("LTlt");                              // Summer Tree Wall
  hills.Forest(tree, cover: 0.4f, density: 0.8f, toSlope: 30f);       // patches about 1024 across
  hills.Wall(tree, spacing: 2048f, width: 2f, gaps: 0.15f);           // winding walls, with openings
  hills.Scatter(tree, chance: 0.02f, onTile: Tile.LordaeronSummerGrass); // single trees
  ```

  - `Forest`'s `cover` is as `Paint`'s, and `density` is the share of a patch's corners with a tree. `Wall` stands
    lines through the world about `spacing` apart and `width` trees thick, so they edge open ground and channel
    movement; `gaps` is the share of their length left open, in gaps about 512 across. `Scatter`'s `chance` is the share
    of the corners that get one.
  - A corner has one tree at most, at the corner itself, and a later tree rule takes a corner from an earlier one.
    `onTile` is a tile this generator paints there, so put the `Paint` rules first; 0 (the default) is any corner.
    Trees keep off `Level` places.
  - `variations` is how many models the type has (10 unless set, as the game's tree walls have); each tree takes one
    of them from the seed.
  - The plan holds them as `plan.Trees` (a type for each corner, 0 for none) and `plan.TreeVariations`. `Apply` and
    `Make` stand them after the ground, at its height, so they stand on the hills; `Destructable.Create` with a height
    (`Destructable.Create(type, x, y, Terrain.Height(x, y), scope)`) does the same for a map's own trees, as the game
    draws a destructable made without one at the ground as it was, sunk into a hill. Ending the scope removes every
    tree, cut and dead ones too.
  - Like the ground, the trees follow the world: plans of one generator stand the same trees on a shared edge. To
    make a world in pieces with no tree twice, let each corner belong to one piece (one ends a corner before the next
    starts).
  - Costs (3.0.1.24371): about 45 µs a tree to make and 30 to remove (200 trees in 9 ms and 6 ms). Each tree made and
    removed leaves about 5 KB and a handle id that the game does not give back, and its death registration stays,
    which slows Moonwell's death trigger a little, so a game that makes and takes back forests again and again grows:
    about 20 MB per 10,000 trees. A path search (`FindPath`) round a long wall took about 100 ms.
- Biomes give parts of the world their own hill shape, tiles, trees and blocking. A `Biome` takes the generator's
  settings (`Lowest`, `Highest`, `FeatureSize`, `Layers`, `Roughness`) and rule calls, and the generator's own are the
  ground outside every biome:

  ```csharp
  var snow = new Biome { Lowest = -50f, Highest = 300f };
  snow.Paint(Tile.LordaeronWinterSnow);
  snow.Paint(Tile.LordaeronWinterRock, fromSlope: 22f);
  snow.Forest(ObjectTypes.FourCC("WTst"), cover: 0.5f);

  hills.Use(snow, north);                                       // one kind of land in an area
  hills.Mix(south, 4096f, new Grassland(), new RockyHills());   // several, in patches about 4096 across
  ```

  - `Use` puts one biome in an area. `Mix` puts several in patches about the size across, picked from the seed; a
    biome listed twice covers about twice the ground. Later calls lie over earlier ones.
  - Borders blend over `BiomeBlend` (512 unless set): past the edge of a `Use`'s or `Mix`'s area into what lies under
    it, and across the borders of a `Mix`'s patches. The heights blend smoothly; each corner takes its tiles, trees and
    blocking from one biome, so the border is ragged and each side keeps its own rules. 0 gives sharp borders.
  - A rule's heights are a share of its own biome's range. Level places, roads, `RoadTile` and the edge fade are the
    generator's and work across biomes: a road over a border keeps its easy slope.
  - Borders come from the world, so plans of one generator agree on a border across their edge. A plan reads each
    biome as it is when the plan starts (for `Make`, at the call).
  - Cost, inferred and not measured: a corner reads the hills of every biome with a share of it, and a `Mix` two layers
    of noise for each of its biomes, so a border or a mix of three costs about three times the heights alone.
- Presets are biomes to start from: `Grassland` and `RockyHills` (Lordaeron Summer), `SnowyForest` (Lordaeron Winter)
  and `Desert` (Barrens). Each names its rules, and a map changes them one by one:

  ```csharp
  var north = new SnowyForest();
  north.Highest = 250f;                                       // the hill shape
  north.Trees.Density = 0.4f;                                 // a rule's setting
  north.Trees.Type = ObjectTypes.FourCC("NTtw");
  north.Remove(north.Rock);                                   // drop a rule
  north.Scatter(ObjectTypes.FourCC("NTtw"), chance: 0.02f);   // add one
  hills.Use(north, northArea);
  ```

  - Every rule call (`Paint`, `Block`, `Forest`, `Wall`, `Scatter`), on a biome or the generator, gives its rule, with
    its settings as fields to change later: `FromHeight`, `ToHeight`, `FromSlope`, `ToSlope`, and `Tile` and `Cover`,
    `Kinds`, or `Type`, `OnTile`, `Variations` and the kind's own (`Density`, `Spacing`, `Chance` and so on).
  - The rules each preset names: `Grassland` `Grass`, `DarkGrass`, `Rock`, `Groves`, `Trees`; `RockyHills` `Dirt`,
    `Grass`, `Rock`, `Cliffs` (blocks steep ground), `Trees`; `SnowyForest` `Snow`, `GrassySnow`, `Rock`, `Trees`,
    `Single`; `Desert` `Sand`, `DarkSand`, `Pebbles`, `Rock`, `Trees`.
  - A preset's tiles go in the map's tile list: `Tiles.AddRange(new SnowyForest().Tiles);` in the map class. A map's own
    presets are classes of its own deriving from `Biome`, setting their rules in the constructor as these do.
- Places are level ground for bases, camps, shops and exits: level at the ground as it was, never raised, as the game
  draws buildings and items sunk on raised ground; never blocked, with no trees. A map sets its own with `Level` and has
  the generator pick more from the seed:

  ```csharp
  var home = hills.Level(spawn.X, spawn.Y, 768f);                                  // the map's own spot
  var camps = hills.Places("Camp", Terrain.Playable, 20, radius: 320f, spacing: 1500f, from: home, difficulties: 3);
  camps.Add(1, ObjectTypes.FourCC("nkob"), ObjectTypes.FourCC("nkob"));           // a group for difficulty 1
  camps.Add(1, ObjectTypes.FourCC("ngno"), ObjectTypes.FourCC("ngno"));           // another: each camp takes one
  camps.Add(2, ObjectTypes.IdOf<Ogre>(), ObjectTypes.IdOf<Ogre>());               // a class of the map's
  camps.Add(3, ObjectTypes.FourCC("nrdk"));
  var shops = hills.Places("Shop", Terrain.Playable, 2, radius: 384f, owner: Player.NeutralPassive);
  shops.Add(1, ObjectTypes.FourCC("ngme"));                                         // a building goes in the middle
  var gate = hills.Exit("North gate", Terrain.Playable, Side.Top);                  // a spot on the top side
  ```

  - `Places` picks up to the count, each at least `spacing` from every other place and never overlapping one, the whole
    place inside the area; fewer come back when no more fit. With `from`, the nearest share are difficulty 1 and the
    farthest `difficulties`, in even shares; without it, all are 1. The same seed and calls pick the same spots.
  - `Add(difficulty, types...)` adds a group; each place takes one group of its difficulty, picked from the seed, and
    it is made when the place's plan is made: the first type at the centre, the rest in a ring round it facing in,
    owned by Neutral Hostile unless `owner:` says. `place.Units` lists them; ending the scope removes them. A place
    whose difficulty has no group, or one from `Level` or `Exit`, makes nothing.
  - `Exit` puts a place on one side of an area, where it joins the next, at a spot from the seed; the map puts what
    leads on (a gate, a way to the next area) there. Its centre is on the side itself, so on the edge of
    `Terrain.Playable` its outer half is the map's boundary; give a zone's own area for an exit to the next zone.
  - A place reads `Name`, `X`, `Y`, `Radius`, `Difficulty` and `Units`. It is made by the one plan holding the corner
    nearest its centre (`plan.Places`), so in a world made in pieces each place is made once.
- Roads join places with path data a map can send units along:

  ```csharp
  hills.RoadTile = Tile.LordaeronSummerRoughDirt;
  var top = hills.Road(west, east, through: new Vector2(-6000f, 6000f));     // three lanes between two bases
  var middle = hills.Road(west, east, width: 384f);
  var bottom = hills.Road(west, east, through: new Vector2(6000f, -6000f));
  hills.Join();                                                             // roads until every place is reached
  ```

  - A road runs from one place's centre to the other's, through the points given, bent a little from the seed. Where a
    plan holds it, the ground along it is cut and filled to an easy slope (about 8 degrees), painted with `RoadTile` (0
    leaves the tiles the rules paint), and kept clear of blocked corners and trees; past its edge the ground blends
    back to the hills over 256. It goes over hills, not round them. `Width` is 256 unless set. `Join` adds roads 256
    wide, the shortest gaps first, until every place reaches every other.
  - Roads come from the seed and the calls, not from a plan, so plans of one generator (with `EdgeFade = 0`) agree on a
    road that crosses their edge. They see only the generator's ground: the map's own cliffs, water and buildings are
    not in them, and `unit.CanReach` (`Moonwell.Pathing`) checks the live ground.
  - A road is path data: `Points` from `From` to `To`, a point about every 256 with straight lines down its middle,
    `Length` and `Width`. `road.Toward(place)` gives it as a `Route`, the type `FindPath` gives, ending at that place,
    and `hills.Route(a, b)` the shortest way over the roads (the start alone, with `Reaches` false, when no roads join
    them). Several roads between two places are several lanes; read each one's own.
  - A unit is sent along a route a point at a time, the next order when it is near the point it was sent to:

    ```csharp
    public sealed class FollowRoute : UnitComponent   // creep.Add(new FollowRoute(top.Toward(east)))
    {
        private readonly Route route;
        private int next = 1;

        public FollowRoute(Route route) { this.route = route; }

        protected override void OnAdded()
        {
            Unit.OrderAt(Orders.Attack, route.Points[next].X, route.Points[next].Y);   // attack-move: fights on the way
            Every(0.25f, () =>
            {
                if (next == route.Points.Count - 1 || new Vector2(Unit.X, Unit.Y).DistanceTo(route.Points[next]) > 192f) return;
                next++;
                Unit.OrderAt(Orders.Attack, route.Points[next].X, route.Points[next].Y);
            });
        }
    }
    ```

  - `road.Outline(from, to)` is a `Polygon` of a stretch of it, from one length along it to another, as wide as the
    road, for an area that hears units enter and leave that stretch (`Moonwell.Areas`); with no lengths, the whole road:

    ```csharp
    public sealed class LaneSection : Area
    {
        public LaneSection(Polygon stretch) { Shape = AreaShape.Polygon(stretch); }
        public override void OnUnitEntered(Unit enteringUnit) { /* a creep wave reached this stretch */ }
    }

    Game.Instance.Add(new LaneSection(top.Outline(0f, top.Length / 3f)));
    ```

  - Costs, inferred and not yet measured: a road near a plan about 15 µs a point and 1 µs a corner near it, a group
    about 0.3 ms a unit. `Make` works roads out first, as "Roads", and makes the groups last, as "Places".
- A growing world makes the ground only near heroes, a chunk at a time, for a world too big to make at once:

  ```csharp
  var world = hills.Grow(Terrain.Playable, Game.Instance);   // chunks of 16 by 16 corners, 2048 across
  world.Made += chunk => { /* chunk.Area, and chunk.Scope: what the map makes in it goes with it */ };
  world.TakingBack += chunk => { /* just before its ground goes */ };
  ```

  - Every living hero, any player's, makes the chunks within `Near` of it (2048 unless set), nearest first; a made
    chunk is taken back once every hero is farther than `Far` (4096). A dead hero holds none until it lives again.
    `chunkSize:` in `Grow` sets the corners a chunk is across.
  - A chunk's ground is its part of `Plan(area)`: each chunk holds its own corners, so chunks meet in any order, come
    back the same, and the world's own edge fades by `EdgeFade` as a whole plan's does. Set the generator and its
    biomes up before `Grow`: a chunk is made from the settings and rules as they are when it is made.
  - Chunks are made one at a time in steps of 4 ms, as `Make` makes ground, and taken back one a step, so no frame
    does more than one.
  - Once a chunk is made, a unit by one of its new trees or blocked corners is put back through the game's pathing at
    its own place (`MoveTo`): the game finds the nearest free spot, and the unit's order ends. Flying, hidden and dead
    units are left as they are. Then `Made` runs. Taking a chunk back traps nobody, and moves nobody.
  - Taking a chunk back removes its heights, tiles, blocked corners and trees, cut and dead ones too, and what the map
    made in `chunk.Scope`. A cut tree stands again when its chunk is made again.
  - A camp is made the first time its chunk is, and is held by the world, not the chunk: it stays while the chunk is
    gone, on level ground that never changes, so a cleared camp stays cleared.
  - Ending the scope the world was grown in takes all of it back, camps too, with no `TakingBack`.
  - Each tree made and taken back leaves about 5 KB and a handle id behind (3.0.1.24371), so a chunk of 50 trees taken
    back and made again 100 times grows the game by about 25 MB. The gap between `Near` and `Far` keeps a hero walking
    along a chunk's edge from making it again and again; keep it at least a chunk wide.
  - It holds only the craters of the chunks near heroes, a crater a corner, so the game stays quick over a long game:
    holding 8 chunks of 16 by 16 and making and taking back 100,000 craters cost 20 to 27 µs a crater the whole way
    (3.0.1.24371). Chunks never take a big area's wide craters.
- `Noise` and `Field` are the numbers under it, for a map's own generators: `Noise.At(seed, x, y)` is smooth noise
  from 0 to 1, `Noise.Layered` adds finer layers, and a `Field` is a grid of numbers to fill (`Field.FromNoise`),
  `Normalize`, `FadeEdges` and `Smooth`.
- A plan of 53 by 53 corners took 62 ms to work out in the game, and its 2,601 craters 14 ms to make (3.0.1.24371).
  Planned and made in one go with tiles and blocked corners it took 132 ms, a frozen frame; made in steps of 1 to
  8 ms no frame was longer than the game's own (about 30 ms), and at 4 ms a step it took 1 s (3.0.1.24371).

## AI: brains and blackboards

Add the `Moonwell.AI` package for units, players and the game that think on their own: brains that think on a tick or
when woken, and blackboards of facts they read.

```csharp
using Moonwell.AI;

public sealed class Guard : UnitBrain
{
    public static readonly Fact<Unit?> Alarm = new Fact<Unit?>("alarm");          // written by the map
    static readonly Fact<bool> Hurt = new Fact<bool>("hurt", keep: 0.5f,          // worked out when read
        read: board => board.Unit!.Life < board.Unit!.MaxLife / 2f);

    public float HomeX, HomeY;

    protected override void OnAdded() => ThinkEvery = 0.25f;

    public override void OnDamaged(Damage damage) => Wake();    // think now, not at the next tick

    protected override void Think()
    {
        var raider = Read(Alarm);
        if (Read(Hurt))
        {
            OrderAt(Orders.Move, HomeX, HomeY);
        }
        else if (raider != null)
        {
            OrderOn(Orders.Attack, raider);                       // reaches the game once, not every think
        }
    }
}

unit.Add(new Guard { HomeX = unit.X, HomeY = unit.Y });
player.Blackboard().Set(Guard.Alarm, raider);                    // every guard of the player reads it
```

- A brain is a component, a `UnitBrain`, `PlayerBrain` or `GameBrain`: it is added like any other, hears its actor's
  reactions, and ends with it. Its `Think` runs every `ThinkEvery` seconds, when woken, or both. `ThinkEvery` is
  rounded to whole milliseconds, as timers are, then to whole steps of 0.02 s, a half step up (0.25 s is 13 steps,
  0.03 s is 2), and is at least one step; 0, the default, thinks only when woken. Set it in `OnAdded` or at any time:
  a change thinks within the new tick from then.
- `Wake()` has the brain think at the next step: once, however many wakes come and with a tick due then too. A wake from
  inside a `Think` goes to the step after, unless the woken brain still waits to think from a wake in this one: then that
  think answers it.
- One loop runs every brain: a timer of 0.02 s steps, on while any brain is. Brains spread their first thinks over
  their tick, each on a phase of its own among the step numbers, so 1,000 brains started at once at 0.25 s think about
  77 a step, not 1,000 in one, and neither do brains started at a steady rate land on one step. Within a step, brains
  think in the order they became due. A `Think` that fails is reported, and the other brains still think. The time is
  read once a step, and facts written and aged in a `Think` use it.
- A fact is a typed key made once, as a static readonly field. A written fact is set with `blackboard.Set(fact, value)`,
  and `Remove` takes it off. `Read` gives `default` (0, false, null) when nothing holds it, and `Age` the seconds since
  the value found was written, `float.MaxValue` when there is none.
- A worked-out fact has a `read` function instead, run for the blackboard the read starts on: its value is kept there
  and given again until it is older than `keep` seconds (0 works it out once an instant). It never looks up the chain.
  `Set` on one is reported and refused, in a release build too, and a `keep` below 0 throws.
- Each brain has a blackboard of its own, which ends with it; each player has one (`player.Blackboard()`), and so has the
  game (`Game.Instance.Blackboard()`). A read goes up a chain, and the first blackboard holding the fact wins: a unit
  brain's, then its unit's owner's, then the game's; a player brain's, then its player's, then the game's; a game
  brain's, then the game's; a player's, then the game's. A blackboard's `Unit` and `Player` say whose it is, for a read
  function: a unit brain's `Player` is the unit's owner now.
- A unit given to another player (`unit.Owner = other`) has its brain read the new owner's blackboard from then on,
  with its own facts kept. The brain is not woken.
- A unit brain's `Order`, `OrderAt` and `OrderOn` reach the game only when the order is new. A repeat, the same order
  on the same target or to a place within 32 of the last, gives true and is dropped while the unit still carries out
  the order its `CurrentOrder` read just after the last one was sent, or while the unit is idle and has done it:
  within 64 of the place asked for, its target dead or gone, or neither. It is sent again once another order took
  over, when the unit is idle with its target alive, and after its owner changed, as the game stops a unit it gives
  away. A unit idle short of the place gets it once more; idle again within 32 of where it stood then, as in a crowd
  jammed at one place, it has done it, and only an idle stop elsewhere, an order that is no repeat or a new owner sends
  it again. An order the game refused is not kept, so the next is sent. The unit's own `Order`, `OrderAt` and
  `OrderOn` send every time.
- Brains and blackboards are the same on every machine: think, read and write outside `Local.For`.

### Full control

A unit brain leans on the game by default: between its orders the game's own unit AI acts, as for any unit. An idle
unit picks enemies near it and fights them, hits back, answers a call for help, a creep goes home, and a computer's
unit is sent back after its attack-move ends. Set `FullControl = true`, in `OnAdded` or at any time, and the unit does
only what code orders:

```csharp
protected override void OnAdded()
{
    ThinkEvery = 0.25f;
    FullControl = true;                                           // the unit no longer fights or walks by itself
}
```

- A target the unit picks by itself while it carries no order, idle or on Moonwell's hold, is answered at once with
  hold position, and the unit's brains are woken to think at the next step, so a unit that killed its target is
  ordered on within 0.02 s. A unit that code put on hold position already holds, and gets nothing.
- Every order not from code, the game's own, an AI script's or a person's click, is answered with hold position at the
  loop's next step, within 0.02 s. The unit's brains forget the order they last sent, so their next one reaches the
  game, and are woken to think in that step. It is not answered inside its event: there hold position doesn't hold,
  and for a computer's unit sent back after an attack-move it closes the game. Several in one step get one answer, an
  order from code before the step drops it, and so does a reaction that refuses it. The unit's `OnOrdered` and the
  map's `OnUnitOrdered` still hear them. An item's use, a toggle or an autocast change nothing the unit does and get
  no answer.
- An order from code always goes through: the brain's own, the unit's `Order`, `OrderAt` and `OrderOn`, and the stop
  that `MoveTo` or a change of owner gives. On the brain's attack-move the unit fights what it meets, as an attack-move
  does. An order the unit already carries when control begins goes on until the brain orders it.
- An enemy right beside the unit is still hit, as on hold position. The unit doesn't sleep at night.
- The unit is under full control while any of its brains has it. Setting it false, or the last such brain ending,
  gives the unit back: it sleeps again if it could, and one left on Moonwell's hold gets a stop, so the game picks up
  at once. A hold from code is left as it is, and a dead or removed unit gets no stop.

The melee and campaign AI scripts are the game's natives: `Natives.StartMeleeAI(p, "human.ai")` runs from a Moonwell
map, as do `StartCampaignAI`, `PauseCompAI` and `CommandAI`. A full-control brain on a unit of such a player undoes
the script's orders to it.

## AI: choosing by scores

A brain with several things it could do can score each one at every think and run the best, which is called utility
AI. An option's score comes from facts on the blackboard, each put through a curve.

```csharp
using Moonwell.AI;
using Moonwell.Interactions;                               // Find, from the Moonwell.Interactions package

public sealed class Grunt : UnitBrain
{
    static readonly Fact<float> Life = new Fact<float>("life", keep: 0.25f,
        read: board => board.Unit!.Life / board.Unit!.MaxLife);
    static readonly Fact<int> EnemiesNear = new Fact<int>("enemies near", keep: 0.5f, read: board =>
        Find.InRange<Unit>(board.Unit!.X, board.Unit!.Y, 600f, u => board.Player!.IsEnemy(u.Owner)).Count);

    public float HomeX, HomeY, GoalX, GoalY;
    readonly Choice choice;

    public Grunt()
    {
        choice = new Choice
        {
            new Option("flee", Flee)
            {
                { Life, Curve.Falling },                     // life is 0..1 already
                { EnemiesNear, 0, 6, Curve.Rising },         // 0 to 6 enemies, scaled to 0..1
            },
            new Option("fight", Fight) { { Life, Curve.S(0.3f, 10f) } },
            new Option("rest", Rest, weight: 0.2f),          // no inputs: scores its weight
        };
    }

    protected override void OnAdded() => ThinkEvery = 0.25f;

    protected override void Think() => choice.Run(Blackboard);   // scores the options and runs the best

    void Flee() => OrderAt(Orders.Move, HomeX, HomeY);
    void Fight() => OrderAt(Orders.Attack, GoalX, GoalY);       // attacks what it meets on the way
    void Rest() => Order(Orders.Stop);
}

unit.Add(new Grunt { HomeX = unit.X, HomeY = unit.Y, GoalX = camp.X, GoalY = camp.Y });
```

- An option is a name, the method it runs and a `weight`, 1 unless given; a weight below 0 throws `ArgumentException`,
  in the constructor or when `Weight` is set. Its inputs go in braces after it, each a fact and a curve.
- An input reads its fact as `Read` does: up the chain, and worked out when the fact is. A `float` fact with no range is
  taken as 0..1 already. With a range, the value is scaled from min..max to 0..1, as (value - min) / (max - min), and
  clamped: 4 enemies of 0 to 6 is 0.67, and 9 is 1. A max below the min turns the range round, and a min equal to the
  max throws `ArgumentException` when the input is added. An `int` fact always has a range, and a `bool` fact none:
  false is 0, true is 1.
- An option's score is its inputs' scores multiplied, evened out over how many there are (the n-th root: the square
  root for two), times its weight. With life at 0.2 and 4 enemies near, flee's inputs give 0.8 (0.2 through `Falling`)
  and 0.67 (4 of 0 to 6 through `Rising`): 0.8 × 0.67 = 0.53, evened over two inputs 0.73. Fight scores 0.27 and rest
  0.2, so the grunt flees. One input's score is taken as it is, and an option with no inputs scores its weight.
- Inputs are read in the order added, and once one scores 0 the option scores 0 and the facts after it are not read:
  put a fact that rules an option out first, and a costly worked-out one last.
- `choice.Run(board)` scores every option once and runs the best, on a tie the one added first. The option it picked
  stays `Current` until another scores more than its score times 1 + `Keep`; then the best of those wins. `Keep` is
  0.1, so 10% (below 0 throws `ArgumentException`): flee at 0.73 stays against fight at 0.80 and gives way to 0.81.
  This stops a unit flipping between two options that score about the same: a brain's orders drop only a repeat of
  the same order, and every new one holds the unit up.
- An option scoring 0 never wins: a current one at 0 gives way to anything above 0, and when every option scores 0, or
  there are none, nothing runs and `Current` is null.
- `Pick(board)` gives the winner, or null, without running it, and `Current` is what the last `Pick` gave; `Run` is a
  `Pick`, then the winner's `Run()`. `option.Score(board)` gives one option's score. A `Choice` belongs to one brain,
  as it keeps `Current`: make it in the brain's constructor, where the options' methods are the brain's own.
- A curve turns an input of 0..1 into a score of 0..1, and `curve.Of(x)` gives it. The input is clamped to 0..1 first,
  and so is the result.

| Curve | Gives |
|---|---|
| `Curve.Rising` | x |
| `Curve.Falling` | 1 - x |
| `Curve.Line(from, to)` | from + (to - from) × x: `from` at 0, `to` at 1, a straight line between |
| `Curve.Power(exponent)` | x to the power `exponent`: above 1 it rises late, below 1 early; 0 or less throws `ArgumentException` |
| `Curve.S(middle, steepness)` | 1 / (1 + e^(-steepness × (x - middle))): 0.5 at `middle`, sharper the higher `steepness`; a negative one falls |
| `Curve.Step(at)` | 0 below `at`, 1 from `at` on |
| `Curve.Points(x, y, x, y, ...)` | straight lines between the points |

- `Points` takes x, y pairs, x never going down: below the first x it gives the first y, past the last x the last y,
  and two points at the same x make a jump, the later y at exactly that x. No points, an odd count or an x below the
  one before throws `ArgumentException`. A map makes no curve of its own: `Points` draws any shape.
- Weights, ranges and whole curves can come from a CSV in `data` ([Data files](#data-files)), a curve as its `Points`
  in one list column. With `data\grunt.csv` holding the header `Id,Weight,Enemies,Curve:float` and the row
  `Flee,1,6,0;1;0.5;0.6;1;0`, the constructor reads `var flee = Data.Grunt.Flee;` and gives `weight: flee.Weight`,
  `{ Life, Curve.Points(flee.Curve) }` and `{ EnemiesNear, 0, flee.Enemies, Curve.Rising }`. The `:float` keeps a
  curve of whole numbers, such as `0;1;1;0`, a list of `float`s, as `Points` takes. `Keep` can be set from data too.
- An option's action is a plain method, so it can run anything, a behavior tree included.

## AI: behaviour trees

A brain can think with a behaviour tree: nodes built once, run from the root at each think, going on where the last
pass left off.

```csharp
using Moonwell.AI;
using static Moonwell.AI.Nodes;

public sealed class Guard : UnitBrain
{
    public static readonly Fact<Unit?> Alarm = new Fact<Unit?>("alarm");
    static readonly Fact<bool> Hurt = new Fact<bool>("hurt", keep: 0.5f,
        read: board => board.Unit!.Life < board.Unit!.MaxLife * Data.Guards.Gate.FleeBelow);

    public float HomeX, HomeY;
    public Route? Out, Back;
    readonly Tree tree;

    public Guard()
    {
        tree = new Tree(this,
            Select(
                When(Hurt, MoveTo(() => HomeX, () => HomeY)),   // cuts in when Hurt turns true
                When(Alarm, Attack(Alarm)),
                Repeat(Sequence(FollowRoute(() => Out), FollowRoute(() => Back)))));
    }

    protected override void OnAdded() => ThinkEvery = 0.25f;
    protected override void Think() => tree.Run();
}
```

- A brain makes its tree in its constructor, `new Tree(this, root)`, from the helpers of `Nodes`, and runs it in
  `Think`: each `tree.Run()` is one pass from the root, giving `Status.Running`, `Succeeded` or `Failed`. A node that
  gives Running goes on at the next pass, and a root that ended starts again. A node is in one tree, at one place, so
  each brain builds its own: a node put in a second place throws. `tree.Stop()` cuts off every running node; called
  from a node during a pass, it waits for the pass to end. A `Run` from inside a `Run` of the same tree is reported
  and refused.
- Control: `Sequence` runs its children in order until one fails, `Select` until one succeeds; `Parallel` runs every
  child at each pass, failing as soon as one fails; `Repeat(child, times)` starts the child anew each time it
  succeeds, forever without `times`; `Invert` swaps success and failure, and `Succeed` succeeds however its child ends.
- Checks and waits: `If(condition)` checks once, as a sequence's guard, and `When(condition, child)` runs the child
  while the condition holds, checked at every pass; both also take a fact, a `Fact<bool>` holding when true and a fact
  of a class when not null. `Wait(seconds)` waits; `Cooldown(seconds, child)` fails without starting the child until
  that long after it ended; `Timeout(seconds, child)` halts a child that has run that long and fails. `Do(() => ...)`
  is a leaf of your own function.
- Unit actions order the unit through its brain, so a repeat it carries out never reaches the game. Each fails for a
  dead or removed unit and a refused order, and on a player or game brain's tree is reported and fails.
  - `MoveTo(() => x, () => y)` succeeds within 64 of the place, read at every pass so a moving place is followed.
  - `Attack(target)`, a function or a `Fact<Unit?>`, follows a new target, succeeds once it dies, and fails when the
    unit stands idle with it alive, as one that can't see or reach it.
  - `Cast(order)`, at a place or on a target, runs while the unit carries the order out and succeeds once it stops: a
    stun reads the same as a spell that went off. A second cast of the same spell is sent, and so is one on another
    target; a cast the unit still carries out as its brain last gave it is only watched.
  - `FollowRoute(() => route)` walks a `Route`'s points from the one after the nearest;
    `FollowRoute(() => route, Orders.Attack)` attack-moves. A route has no loop, so a patrol is a way out and a way
    back, repeated. A map gets a `Route` from `unit.FindPath(x, y)` (Moonwell.Pathing) or a procgen road's
    `road.Toward(place)`.
  - `UseItem(() => item)`, at a place or on a target, watches the use as `Cast` does, and a use of the item the unit
    still carries out is only watched. A potion leaves no order to watch, so it succeeds at once.
  - `MoveTo` and `FollowRoute` also fail when the unit is idle short of the place just after the order, as one jammed in
    a crowd after its one more try.
- Utility AI: `Pick(Scored(() => score, child), ...)` runs the option with the best score, re-scored at each pass; 0 or
  less is never picked. A running option is kept unless another beats it by more than a tenth or its own score drops
  to 0 or less, and with none above 0 it fails.
- Interrupts: a `When` directly in a `Select` is watched. While a child below it runs on, the first `When` above it
  whose condition holds and whose child does not fail at once cuts in: its branch takes over, then the child that ran
  is stopped, so that child's `Stop` runs after the new branch has started. A `When` whose child fails at once, as an
  attack on a dead target, leaves the running child alone. A running `When` whose condition no longer holds fails, and
  the select goes on to the next child in the same pass. Nothing outside the select is watched, and an `If` never is.
- Thresholds and timings are numbers to tune: keep them in a `data` file and read them through `Data`, as
  `Data.Guards.Gate.FleeBelow` above comes from `data\guards.csv` ([Data files](#data-files)).

A map's own node derives from `Node`:

```csharp
public sealed class Chop : Node
{
    protected override void Start() => OrderOn(Orders.Harvest, Read(TreeNear)!);
    protected override Status Run() => Read(TreeNear)!.IsAlive ? Status.Running : Status.Succeeded; // until it falls
    protected override void Stop() { }   // cut off by an interrupt
}
```

- `Start` runs when the node starts, at the first pass that reaches it and again after it ended; `Run` gives its status
  at each pass; `Stop` runs only when it is cut off while running, by an interrupt, its parent ending or `tree.Stop()`,
  never when it ends by itself.
- It reads `Unit`, `Blackboard` and `Read(fact)` (here `TreeNear`, a map's `Fact<Destructable?>`) and orders the unit
  through the brain with `Order`, `OrderAt` and `OrderOn`, so repeats are dropped as the actions' are.

## AI: combat

Add the `Moonwell.AI.Combat` package, on `Moonwell.AI`, for units that walk a lane and fight what they meet: a ready
`LaneWalker` brain, and the fighter and tree nodes it is built of, for a map's own brain.

```csharp
using Moonwell.AI.Combat;

unit.Add(new LaneWalker(road.Toward(camp))                // a Route: a procgen road, or unit.FindPath(x, y)
{
    MeetRange = 600f,                                     // 0, the default: the unit's acquisition range
    Targets = { Target.Nearest, Target.Weakest.Weight(2f), Target.By(u => u is Hero ? 1f : 0.3f) },
    Spells =
    {
        Spell.On(StormBolt, "thunderbolt", when: t => t is Hero),               // on its target
        Spell.Self(ThunderClap, "thunderclap", when: f => f.EnemiesNear >= 3),  // no target
        Spell.OnSelf(FrostArmor, "frostarmor", when: f => f.EnemiesNear >= 2),  // on itself
        Spell.Ally(HolyLight, "holybolt", when: a => a.Life < a.MaxLife / 2),   // a hurt ally, itself counted
        Spell.At(Blizzard, "blizzard"),                                         // at its target's place
    },
    PullBack = { Below = 0.25f },                         // the rest stay at their defaults
});
```

- `StormBolt` and the others are ability ids, `ObjectTypes.FourCC("AHtb")`; `Spell.On<StormBolt>("thunderbolt")`
  takes a registered ability class instead. The order string is the one the game casts the ability with.
- A lane walker thinks every 0.25 s with full control ([Full control](#full-control)), both set in its `OnAdded`; a
  map's own `OnAdded` changes them after `base.OnAdded()`. Its tree, built at its first think, pulls back while it
  should, else fights while it meets an enemy, else walks its route.
- It meets, at each think, a living enemy unit its owner sees (an order at an unseen unit is refused), structures too,
  within its meet range, and one that damaged it in the last 3 s however far. It keeps the spot where it stood at the
  first think with a target, and an enemy farther than `Leash` from it is not fought: 2,100, the default, about the
  game's own chase limit. That limit ends only a chase of a target the unit picked itself; an `attack` from code chased
  3,960 in 15 s (3.0.1.24371), so the leash is the walker's only one. A fleeing enemy that crosses it is dropped and the
  walker goes back. Once back on its route, within 64 of it anywhere along it, with no target, it forgets the spot.
- An enemy it can't attack is set aside for 3 s, and the next best is fought at once: one whose attack the game
  refuses, or one still alive when the unit is found idle after its attack on it, as it can't reach or see it. It
  still counts near.
- It walks its route with `move`, which never fights on the way, taking it up at the point after the one nearest it, as
  `FollowRoute` does, so after a fight it goes on from where it is. With no route it stands and fights where it is,
  and walks back to where its fight started.

The target is picked by rules, each scoring every enemy met from 0 to 1 against the others met at the same think, so
the best of them scores 1 and none scores 0:

| Rule | Scores an enemy |
|---|---|
| `Target.Nearest` | the nearest distance over its distance, a distance below 16 counted as 16 |
| `Target.Weakest` | the least life left over its life left, as amounts, not shares of the most |
| `Target.MostDangerous` | its damage a second over the most: the middle of its white damage over its attack cooldown, below 1 counted as 1 |
| `Target.Attacker` | 1 when it damaged the walker in the last 3 s, else 0.5 |
| `Target.By(u => ...)` | the function's value, clamped to 0..1; 0 rules the enemy out |

- An enemy's score is its rules' scores, each to the power of its weight, multiplied, then to 1 over the sum of the
  weights: (s1^w1 × s2^w2)^(1 / (w1 + w2)). `Weight(2f)` counts a rule twice, and 0 leaves its score out but still
  rules out at 0; a weight below 0 throws `ArgumentException`. With no rules, `Nearest`.
- With `Nearest` and `Weakest`, an enemy 200 away at full life and one 400 away at half life both score
  (1 × 0.5)^(1/2) = 0.71, and the first in the game's list of units in range wins. With `Weakest.Weight(2f)` the far
  one scores (0.5 × 1²)^(1/3) = 0.79 against (1 × 0.5²)^(1/3) = 0.63.
- The best wins, among equals the first in the game's list of units in range (an attacker met from beyond it comes
  after them), and stays the target until another scores more than its score times 1.1, 10% more, so a walker does
  not flip between two enemies. A target that died, can't be seen or is no longer met is dropped.
- Casting: at each think in a fight, the first spell in `Spells` whose aim has someone, whose condition holds and that
  is ready is cast through the brain: ready, the unit has the ability, its cooldown is over and its mana is at least
  the ability's mana cost now, all read from the game. The walker watches the cast as `Cast` does, while the unit's
  order id is the one read just after it, then goes back to the attack. At a cast's end the game leaves the unit idle,
  so the brain is woken once the unit's order id changes and attacks again within 0.04 s, not at its next think. A cast
  on a target out of range walks up to it first. The brain forgets its last order before a cast, as `Cast` does, so
  the same spell cast again once the last is over reaches the game.
- A spell the game refuses gives way to the next, and then the attack, in the same pass. A refused cast leaves the
  attack the unit carries out alone (3.0.1.24371), so that attack is not sent again.
- `On` and `At` take a condition on the target, `Self` and `OnSelf` (a spell on a unit, cast at the walker itself)
  one on the fighter, and `Ally` one on each ally in meet range, its own unit counted, checked from the most hurt (the
  least share of its most life) until it holds. With no condition, `Ally` takes the most hurt whose life is below its
  most, and the others always cast when ready.
- Pulling back: below 30% of its life (`Below`), or outnumbered, with at least 2 (`Outnumbered`) enemies in its meet
  range to each allied unit there, its own counted, structures on neither side and attackers beyond its meet range not
  counted. It walks back with `move` along its route to the start, from the point before the one nearest it, or to
  `To`, and stands there on hold position, or as near as it gets in a crowd. Only there does it judge again, and it
  comes back when its own reason is gone: pulled back hurt, at or above 70% of its life (`ComeBackAt`); outnumbered,
  once no longer outnumbered in its meet range where it stands, at any life. Then it is judged afresh. A `Below` and an
  `Outnumbered` of 0 never pull back.

A map's own brain makes a `Fighter` and builds a tree of its own from the same nodes:

```csharp
using Moonwell.AI;
using Moonwell.AI.Combat;
using static Moonwell.AI.Nodes;
using static Moonwell.AI.Combat.CombatNodes;

public sealed class CampGuard : UnitBrain
{
    public float HomeX, HomeY;
    readonly Fighter fighter;
    readonly Tree tree;

    public CampGuard()
    {
        fighter = new Fighter(this) { MeetRange = 800f, Targets = { Target.MostDangerous } };
        tree = new Tree(this,
            Select(
                When(fighter.ShouldPullBack, PullBack(fighter)),
                When(fighter.Meets, Fight(fighter)),
                MoveTo(() => HomeX, () => HomeY)));
    }

    protected override void OnAdded() => ThinkEvery = 0.25f;
    public override void OnDamaged(Damage damage) => fighter.Hurt(damage);   // notes the attacker
    protected override void Think() => tree.Run();
}
```

- `fighter.Meets()` and `fighter.ShouldPullBack()` check once an instant, whichever comes first: they find the enemies
  met and pick the target. `Target`, `EnemiesNear`, `AlliesNear` and `PullingBack` read what the last check found.
  Once it pulls back, `ShouldPullBack` holds until the unit is within 64 of where `PullBack` takes it.
- `Targets` and `Spells` are lists every machine shares: change them outside `Local.For` (MW0001).
- `fighter.Hurt(damage)` in `OnDamaged` notes the attacker and wakes the brain when it is new; a lane walker does it.
- `Fight(fighter)` casts or attacks the target and fails with none; `PullBack(fighter)` walks back and stands;
  `WalkLane(fighter)` walks the fighter's `Route`. A class that inherits `LaneWalker` overrides `Root()` for its own
  tree.
- In a fast simulation a walker meets, walks, fights, casts and pulls back. A cast is held as any order, then its
  cooldown starts and its mana is spent, and the walker attacks again; what the spell does stays the game's (see
  [Fast simulation](#fast-simulation)).

## AI: tools

In a development build, the [debug mode](#debug-mode) shows what a brain thinks and lets you push it from chat. Select
a unit, a player or the game holding a brain (Ctrl + click, or `-brain Guard`), and the panel shows each of its brains:

```
Guard  every 0.25 s  logged
1 > Select
2   When hurt false
3     MoveTo
4 >   Repeat
5 >     Sequence
6 >       FollowRoute
7         FollowRoute
choice 1
8 > flee 0.73  life 0.20, enemies near 4.00
9   fight 0.27
facts
  hurt false 0.1 s
  food (Player 1) 12 3.0 s
```

- The head line: the brain's class, how it thinks (every so many seconds, or on wake), and `logged` while the log
  follows it.
- Its trees, in the order made, a line a node: indented by depth, `>` on a running node, the fact a `When`, `If` or
  `Attack` was made with, a `When`'s or `If`'s last value, and `forced`.
- Its choices, each one picked on the brain's blackboard: the options with their last scores, `>` on `Current`, and the
  current one's inputs with their last values.
- Its facts: every fact held on its blackboard and up its chain, with its value and age; one from up the chain says
  whose. An enum shows as its number, as the game's Lua keeps it.
- Nodes and options are numbered for `-force`, through the object's brains in the order added: each brain's trees
  depth first, then its choices' options. A brain whose lines don't fit shows each subtree that isn't running as one
  line ending in `+n`, the nodes it folds, and the numbers skip them.

| Chat | Does |
|---|---|
| `-brain Guard` | counts the brains of a class (in any case) and selects the next object holding one |
| `-brainlog` | logs the selected object's brains; `-brainlog Guard` every brain of a class, those made later too; either again stops the log and writes it to `CustomMapData\Moonwell\brains.txt` |
| `-force 3` | forces line 3: a `Select`'s or `Pick`'s child runs, the parent trying no other; a `When`'s condition reads true; a choice's option wins whatever the scores. `-force` alone lets go of every force on the selected object |
| `-fact hurt true` | sets a written fact on the selected object's brains' blackboards: the value is the last word, read as the fact's `int`, `float`, `bool` (`true`, `false`, `1`, `0`) or `string` |
| `-wake` | wakes the selected object's brains |

- The log keeps a line only when what won changes: a choice's `Current`, a `Select` or `Pick` running another child, or
  a `When` cutting in or ending. A line has the game time, the brain's object (and its class when the object holds
  more than one brain), the change, and up to 8 facts the brain read in that think, with their values and ages:

  ```
  12.25 Footman 3  choice 1: flee 0.61 (was fight 0.40)  life 0.31 0.2s, enemies near 4 0.1s
  12.40 Footman 3  tree: line 2 When hurt cut in  hurt true 0.0s
  ```

  One log runs at a time. It keeps the newest 2,000 lines, and the panel shows the last 5 while it runs.
- A force holds until `-force` alone, with the mode off too. A `When` forced in a `Select` is both: the select runs it,
  and its condition reads true.
- `-fact` finds the fact by its name, in any case, among the facts made so far. It refuses a worked-out fact, a name
  two facts share, and a fact of another type.
- The commands answer only while the mode is on and are listed by `-help`; a map's own command of the same name wins.
  Everything they read and change is the same on every machine. A release build has none of it.

## Fast simulation

The `Moonwell.Simulation` package runs a map's own code in .NET on a fake of the game, faster than real time: its
components, brains, reactions and timers, with units that walk, chase and fight by their object data. A test plays a
map's AI through a fight or a long game and checks what it decided. The same seed gives the same run.

It goes in a test project beside the map project, not in its folder: the build compiles every `.cs` under the map
project into the map. Only the test project references `Moonwell.Simulation`; a package the map project uses goes into
the map's Lua.

```
MyMap\MyMap.csproj                the map
MyMap.Tests\MyMap.Tests.csproj    the tests
MyMap.Tests\GuardTests.cs
```

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyMap\MyMap.csproj" />
    <PackageReference Include="Moonwell.Simulation" Version="0.16.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

</Project>
```

`dotnet test MyMap.Tests` runs them. A map whose code names `Data`, `Placed` or `Lua` needs a `moonwell build` first,
which writes those classes for the compile.

```csharp
using Moonwell;
using MyMap;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // one simulation at a time

public class GuardTests
{
    [Fact]
    public void AGuardAnswersTheAlarmOnce()
    {
        var sim = Simulation.Start(new Settings(), seed: 7);     // the map's start, as the game runs it
        var guard = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.One, 0f, 0f, Game.Instance);
        guard.Add(new Guard { HomeX = 0f, HomeY = 0f });
        var raider = Unit.Create(ObjectTypes.FourCC("ogru"), Player.Two, 1500f, 0f, Game.Instance);
        Player.One.Blackboard().Set(Guard.Alarm, raider);

        sim.Run(1f);                                             // the guard's brain thinks on its own meanwhile
        Assert.Equal(1, sim.OrdersOf(guard));                    // one attack reached the game, not one a think
        Assert.InRange(sim.StoodOf(guard), 0.15f, 0.17f);        // held once, at that order
        Assert.True(guard.X > 0f);                               // and on its way
    }

    [Fact]
    public void TheRaidsAreBeaten()
    {
        var sim = Simulation.Start(new Settings());
        Assert.True(sim.RunUntil(() => sim.Alive(Player.Two) == 0, 600f));   // ten minutes of the map's own raids
        Assert.True(sim.Alive(Player.One) > 0);
    }
}
```

- `Simulation.Start(map)` starts the map as the game would: its slots playing on their teams (slot 0 as a person when
  it lists none), its object classes registered, its `Components`, its `PlayerComponents` on each playing slot, then
  every `OnGameStart`. `Simulation.Start()` starts an empty game, with nothing of the map's, for units and brains the
  test makes by id. Either takes a `seed`: it gives the dice of the attacks, and the seed after it the map's own random
  numbers (`Game.Random` and a `Dice` made without a seed).
- `Run(seconds)` plays steps of 0.02 s, the seconds rounded to whole steps, a half step up. `RunUntil(check, seconds)`
  stops at the first step its check holds and gives true, or false when it never did. In each step Moonwell's timers
  run first, the brains' own 0.02 s step among them, so brains think as in the game; then each unit acts, in the order
  made. `sim.World.Advance` moves the fake's time without the play, so units neither walk nor fight: use `Run`.
- `Alive(player)` counts the player's units alive and not removed, Moonwell's dummies left out. `OrdersOf(unit)` counts
  the orders the unit was given, repeats included, and `StoodOf(unit)` the seconds it stood at their holds. `Time` is
  the game's seconds so far.
- A unit plays by its type's numbers: on .NET those of its object data (the map's class for the type, else the standard
  object), or those `SetNumbers(typeId, new UnitNumbers { ... })` gives for units made from then on. A unit takes its
  attack range, cooldown, damage, dice and acquisition range when it is made; its life, move speed and armor start from
  them and count as they are at each step. A unit of a type with no numbers never moves, and its orders are neither
  counted nor held.
- Every order holds the unit 0.16 s (8 steps) before it starts, a repeat of the same order too, as the game holds a
  unit up at each order. Then a move walks straight at the move speed and ends on the point. An attack on a unit chases
  to its range and swings at each cooldown, until the target is dead or gone. `smart` attacks an enemy and walks to
  anything else. An attack to a point fights the nearest enemy within acquisition range on the way, then walks on. Hold
  position never moves and swings at an enemy within range, its `CurrentOrder` reading "" as in the game. `stop` leaves
  the unit idle, patrol stands, and any other order that is no cast (`attackground`, `harvest`) ends after its hold.
- The fake knows the game's id of every order string in its ability data and of its base orders, in any case of the
  string. A name the game has no id for leaves the unit idle at once, and is neither counted nor held.
- A spell's order casts the unit's ability whose order string it is, read from the ability's object data on .NET (a
  test can give one with `sim.World.SetAbilityOrder(abilityId, "thunderbolt")`). The cast is taken, with its order
  event, and held as any order; then it goes off: the ability's cooldown starts and its mana cost is spent, those of
  the unit's level, and the unit is left idle, as the game leaves it at a cast's end. The cooldown and cost are the
  unit's own (`LiveCooldown`, `LiveManaCost`), from the object data unless code changed them. While the cooldown runs, or
  with less mana than the cost, the order is refused as the game refuses it: false, no order event, and the unit's
  attack goes on. A spell's order of an ability the unit lacks is taken as a name the fake does not know.
- A cast goes off where the unit stands, whatever its range, at once after its hold, with no cast or backswing time; it
  fires no spell events, deals no damage and gives no buff.
- A swing deals its damage and dice, less the target's armor as the game counts it, as an attack: `OnDamaged` runs, and
  a death runs `OnDeath` and `OnKill` with the attacker as the killer. An idle unit with an acquisition range and a
  damage attacks the nearest enemy within that range, never Neutral Passive or Moonwell's dummies, with no order event,
  not counted or held. A unit under a brain's full control is put on hold position at that moment instead, as in the
  game.
- Left to the game: pathing round blocked ground, trees and other units; collision and crowding (units here pass
  through each other, where in the game 300 footmen ordered at once started 0.56 s after the order on average,
  3.0.1.24371); turning, vision, buffs, and what spells do; an attack's damage point and a missile's flight
  (a swing lands at once); heroes' attributes (a hero fights on its base numbers); units placed in World Editor; and the
  Damage, Terrain, Areas, Input and Physics systems. A simulation shows how the map's own logic decides, not exactly how
  a fight goes in the game.
- The game's runtime is one per process, so one simulation runs at a time: the `CollectionBehavior` line keeps xunit
  from running tests side by side.

## Interactions and finding

Add the `Moonwell.Interactions` package (the template's `.csproj` has the line, commented) for things that react to what
comes near them, gizmos, and finding. A map that does not reference it carries none of its code.

```csharp
public sealed class FireAura : Interaction<Unit, Unit> // held by a unit, pairs with units
{
    public FireAura() { Range = 300f; Tick = 0.5f; }
    public override bool Accepts(Unit other) => Holder.Owner.IsEnemy(other.Owner);
    public override void OnTick(Pair pair) { Holder.Damage(pair.Other, 10f); }
}

public sealed class FrostAura : Interaction<Unit, Unit>
{
    public FrostAura() { Range = 600f; }
    public override void OnMeet(Pair pair) { pair.Other.Apply(new Chilled(Holder.Level), Holder, heldBy: pair); } // ends when the last aura parts
}

public sealed class Chilled : Buff
{
    public Chilled(int level) { Stacking = Stacking.Strongest; Strength = 0.1f * level; } // two levels: the stronger counts
    public override void OnApply() { /* slow the unit by Strength */ }
}

unit.Interact(new FireAura(), this); // in a buff's scope: it ends with the buff, or with the unit
var well = Gizmo.Create(new Well(), x, y, this, caster.Owner); // a gizmo is a map's own object with a place
var near = Find.InRange<Unit>(x, y, 500f);                // a new list; or Find.InRange(x, y, 500f, list)
```

- `Range` is centre to centre and flat. A pair is found at the next tick after they come within range, so a 1 s tick
  may start up to 1 s late (interactions started together take turns over its steps). Only a unit in the game (alive,
  not hidden, not loaded), a visible item on the ground, a live destructable and a gizmo have a place; `OnPart` runs
  once, when a pair ends. A timer made on the pair (`pair.After`) ends when it parts, one made with plain `After`
  is the interaction's; a buff or component the reactions add is the unit's, and held by the pair (`heldBy: pair`) it
  ends when the pair parts, as `FrostAura`'s does, or with the last of several auras that give it.
- `Accepts` is asked before a meeting and again before each tick: a no parts the pair (`OnPart`), and the two meet
  again once it says yes, so `FireAura` stops burning a unit that changes sides (Charm, a new owner). Set
  `RecheckAccepts = false` in the constructor to ask only before a meeting, a call less per pair at each tick. Units
  with Locust pair like any other, so a map filters its dummies here.
- A thing moved far in one jump (a teleport, a unit unloaded or revived, an item dropped far from where it was picked
  up) is found at its new place within `CellCheck` (0.1 s). A pair whose holder and other moved together stays paired.
- From the first interaction or `Find` that asks for units, Moonwell takes in every unit in the game, and each one
  that comes in after, also those the game makes with no code of the map (trained, summoned, made by a native).
- The first interaction or `Find` that asks for destructables takes in every destructable on the map at once: 0.3 s
  and 18 MB on a map of 18,600 (measured). Asking for items starts a look for new ground items once a second. `Actor`
  and `Widget` ask for every kind (`Find.InRange<Actor>`, `Interaction<Gizmo, Actor>`), so ask for `Unit` or a gizmo
  class when that is what is meant. The holder of an interaction is tracked on its own: a tree that holds one takes
  in no other destructable. Past 2,000 destructables taken in, the check for those a raw native removed takes turns, so
  it notices one later: up to 2.5 s on a map of 18,600, where `Remove()` is at once.
- A check or `Find` reads every cell of its box, empty or not: a `Range` of 5,000 is about 6,400 cells, near 2.6 ms a
  check (estimated). Keep a range to what the interaction needs, and a search of the whole map for a rare moment.
- `Find` has `InRange`, `InRect`, `InPolygon`, `AlongLine` and `Closest`. Each gives a new list, or fills a list the
  map keeps (which makes none; use it every tick); `Closest` gives the nearest one, or the nearest few into a list.
- `Interactions.CellSize` (128), `CellCheck` (0.1), `Slack` (64) and `Step` (1/32) are set in the map's constructor.
- `Find` is fine inside `Local.For`: it reads and changes nothing there (a kind nothing outside has asked for yet is
  searched through the objects Moonwell knows, slower). `IsTree()` needs `Moonwell.Objects`; the build warns if missing.

## Physics and missiles

Add the `Moonwell.Physics` package (the template's `.csproj` has the line, commented) for missiles, knockback, walls
and ballistics. It brings `Moonwell.Maths`, `Moonwell.Terrain` and `Moonwell.Interactions` with it. A map that does not
reference it carries none of its code.

```csharp
public sealed class Firebolt : Missile // a bolt that flies straight and burns the first enemy it meets
{
    public Firebolt()
    {
        Model = @"Abilities\Weapons\RedDragonBreath\RedDragonMissile.mdl";
        Speed = 900f;
        Radius = 24f;
        ImpactDamage = 75f;
        DamageKind = DamageKind.Fire;
    }

    public override bool CanHit(Actor other) => other is Unit unit && Source!.Owner.IsEnemy(unit.Owner);
}

caster.Fire(new Firebolt(), target, this); // in this scope; or Fire(missile, x, y, scope), Fire(missile, x, y, z, scope)

public sealed class Shell : Missile // a mortar shell that arcs to a point and blasts what is near where it lands
{
    public Shell()
    {
        Model = @"Abilities\Weapons\Mortar\MortarMissile.mdl";
        Speed = 700f;
        Arc = 0.35f;                // the game's Missile Arc: the peak is this times the distance
        UnitContact = Contact.None; // flies over units; only the ground stops it
        Components.Add(new Shadow());
    }

    public override void OnImpact(Impact impact)
    {
        foreach (var unit in Find.InRange<Unit>(impact.X, impact.Y, 200f, u => Source!.Owner.IsEnemy(u.Owner)))
        {
            Source!.Damage(unit, 60f, tag: this);
        }
    }
}

target.Knockback(caster.AngleToPoint(target), 300f, 0.5f); // 300 away over half a second, slowing to a stop
```

- Every missile moves the same way: `Acceleration` (up to `MaxSpeed`), `Gravity` (`Physics.Gravity` is 800), then the
  move. Set `Speed` or `Velocity`, and `Radius` (16), `Mass`, `Bounciness`, `Friction`, `Duration` or `MaxDistance`, in
  the constructor.
- `Fire` starts it `LaunchHeight` (60) above the unit, with the unit as `Source`. With `Arc` it sets its own `Gravity`
  and lands on the mark: the peak is above the line from launch to mark, an arc at a point lands with its bottom on the
  ground there, and an arc at a unit lands on its middle.
  `Missile.Create(missile, x, y, z, scope, owner)` places one at rest; one made touching the ground with
  `GroundContact = Stop` ends at once.
- `ImpactDamage` and the rest go through `source.Damage` with the missile as `Tag`. A missile with damage and no
  `Source` deals none. `Homing { TurnRate }` is given its target by `Fire(missile, unit)`.
- **Contacts**: `UnitContact` and `GroundContact` are `Stop`; `DestructableContact`, `ItemContact`, `MissileContact`
  and `WallContact` are `None` (not checked). The others are `Bounce`, `Pass` and, for ground and walls, `Slide`.
- A unit is met as an upright cylinder its collision size around and 3 times that tall (a footman 93, a tauren 144),
  from the ground plus its fly height; a `Body` on the unit sets its own `Height`.
- **At a contact**: the missile's `OnImpact` and its components'; then `OnStruck` of `IMissileTarget` on a unit, so the
  defender has the last word (a shield sets `impact.Contact` to `Bounce`); then the damage; then the outcome.
- A `Bounce` or `Slide` that something keeps pulling onto the ground or into a wall, such as a slope's gravity, goes on
  along it for the rest of its step with no impact, as the loop in Maths does after a hit; it does not stand still.
  Going along the ground, both lose their `Friction` (200) a second, and rest (`OnRest`) where a slope's pull, by
  `Physics.Gravity` whatever their own `Gravity`, cannot move them against it, or where walls take that pull (a corner
  on a hill: one resting against a wall wakes when the wall ends or moves away from it); with `Friction = 0` they never
  slow.
- **Scope**: a missile is in the scope `Fire` or `Create` names. A `Stop` ends it right after `OnImpact`, so what should
  outlast it is made on another scope, such as the ability's or `Game.Instance`.
- **Knockback** keeps the unit's order. It stops at trees, buildings, cliffs, walls (`Wall.Create`) and a bridge's
  rail, which keeps its body on the deck (a unit wider than the deck rides with it over both sides). A stopped push
  stops short of the edge, by up to a step's move and the unit's collision size (about 37 and its collision size at
  1,200 a second). `IPushTarget` has `OnPushed`, `OnSlammed` and `OnGrounded`; an `OnPushed` that pushes its own unit
  calls itself again, without end.
- **Bridges**: placed bridges are ground, for missiles landing (from above: one below a deck flies under it) and units
  standing, until the bridge dies; missiles do not meet it as a destructable. Brought back with `Restore()` it is ground
  again at once. With its birth animation the game's own deck comes back lower and rises to its old height within 7
  seconds (26 below at first on a Long Stone Bridge). Brought back by the native, it stays no ground. A deck stands on the ground beneath any water at its bridge's place, as in the game; the build writes
  that ground, which `Terrain.Height` does not read (it reads the water's surface). A bridge made by code is not
  ground, nor a type with no deck Moonwell knows (an ice floe, or the map's own type with its model changed): the build
  warns of it. A pushed unit uses the deck, whatever the pathing under it; the game's own pathing (move orders) walks a
  deck only where the map's pathing file says so, which World Editor writes for a placed bridge, but not for a bridge
  made at runtime or in a map whose pathing file a tool wrote.
- Everything runs on every machine. Reads are safe in `Local.For`; firing, pushing and every set are not.
- In the debug mode, a selected missile's panel shows its speed, velocity and radius, and a pushed unit's its push;
  `-stats` counts the missiles (and how many rest) and the pushed units.
- **Known limits**:
  - A ball rolling along a wall into a square or sharper corner on a hill comes to rest the moment it meets the second
    wall, without bouncing back.
  - A `Bounce` with low `Friction` (about 20) in a very shallow corner (about 160 degrees) on a steep hill (1 in 1) can
    bounce between the walls for more than 16 seconds before it rests.
  - Walls do not push missiles: a wall moved into a resting missile goes into it, and lowering a wall's `Height` does
    not wake what rests against it or on its top.
  - A wall moved slowly away from a ball resting against it on a hill (under about 0.2 a step) wakes it each time it
    rests again, so `OnRest` runs once for every short roll after the wall.
  - On a bridge turned off the axes, a unit standing with its body slightly over the side (a footman, about 1 or 2.5
    over) may be stopped when pushed along the deck.
  - A unit as wide as a bridge's deck crosses it only when pushed straight along it; pushed slightly sideways, it stops
    where the rail begins.
  - A unit pushed on a bridge's deck costs about half again as much a step as one pushed on the ground.

## Spell templates

Add the `Moonwell.Spells` package (the template's `.csproj` has the line, commented) for ready spells: each is an
ability class to inherit and set numbers on, on a Channel ability whose targeting the template sets. It brings
`Moonwell.Objects` and `Moonwell.Physics`.

```csharp
using Moonwell.Spells;

[ObjectId("A0F1")]
public sealed class Firebolt : Bolt
{
    public Firebolt()
    {
        Name = "Firebolt";
        Model = @"Abilities\Weapons\RedDragonBreath\RedDragonMissile.mdl";
        Damage = new PerLevel(75f, 150f, 225f); // or Damage = 75f at every level
    }

    public override void OnHit(Cast cast, Unit target) => target.Apply(new Burn(), cast.Caster);
}
```

| Template | What it does | Its numbers |
|---|---|---|
| `Bolt` | a missile at a unit, which it follows, or at a place; hits the first unit | `Model`, `Speed`, `Arc`, `TurnRate` |
| `Wave` | goes out along the ground and hits each unit it passes once | `Model`, `Speed`, `Distance`, `Radius` |
| `Dash` | the caster rushes towards the place, hitting each unit it passes once | `Distance`, `Seconds`, `Radius` |
| `Leap` | the caster jumps in an arc and slams where it lands | `Speed`, `Radius`, `Model` |
| `Blast` | hits everything near the place once; `AroundCaster` for no target | `Radius`, `Model`, `AroundCaster` |
| `PulseChannel` | hits near the place at once and every `Interval` while it channels (5 s) | `Interval`, `Radius`, `Model`, `AroundCaster` |
| `Chain` | hits the target, then jumps to the nearest unit not hit yet | `Jumps`, `JumpRange`, `Falloff`, `Delay`, `Look` |
| `Summon<T>` | summons units of class `T` in front of the caster ([Summons](#summons)) | `Count`, `Lifetime`, `Limit`, `DiesWithSummoner` |

- Every template but `Summon<T>` deals `Damage` (by the cast's level, magic unless `DamageKind` says otherwise) to
  each unit it hits, from the caster, with the ability as the hit's tag, then runs `OnHit`. `CanHit(cast, target)`
  chooses whom it hits: living enemies of the caster's player unless overridden.
- The map's class adds what the game needs: its name, icon, hotkey and levels, an Area of Effect for the targeting
  circle, and a Base Order ID of its own when one unit has two Channel abilities. A channel's length is its Follow
  Through Time: 0 for every template but `PulseChannel`, so the cast ends at once. Each template's button sits at
  (0, 2), the bottom row's left: two on one unit need a Button Position of their own.
- What a template sends out (a missile, a jump, the next link of a chain) belongs to the ability, so it goes on after
  an instant cast has ended; `PulseChannel`'s pulses belong to the cast, and stop with the channel.
- `Dash` and `Leap` move the caster as a knockback does, so trees, buildings, cliffs and walls stop them short, and
  they land where Physics leaves the caster, with no search for a free spot.

## Areas

Add the `Moonwell.Areas` package (the template's `.csproj` has the line, commented) for parts of the map that units enter,
stay in and leave. A map that does not reference it carries none of its code.

```csharp
public sealed class ShopFloor : Area // a game component: Components.Add(new ShopFloor()) in the map, or Game.Instance.Add
{
    public ShopFloor() { Shape = AreaShape.Region("Shop"); Tick = 1f; } // the region's name in World Editor
    public override bool Accepts(Unit unit) => unit is Hero;
    public override void OnUnitEntered(Unit enteringUnit)
    {
        enteringUnit.Owner.Print("Welcome.");
        enteringUnit.Apply(new Rested(), heldBy: Stay(enteringUnit)); // a buff of the map's: it ends when the unit leaves
    }
    public override void OnUnitStays(Unit stayingUnit) { stayingUnit.Life += 20f; }
    public override void OnUnitLeft(Unit leavingUnit) { leavingUnit.Owner.Print("Come again."); }
}
```

- A shape is a World Editor region by name, a rect, a polygon, a circle, or `AreaShape.Of(a, b)` for several. The build
  fails at the line of an `AreaShape.Region("Shop")` whose name the map does not have; a name it cannot read there (a
  variable, `using static`, an alias) throws when the area is added. `OnUnitStays` runs every `Tick` seconds; 0 never
  runs it.
- Only units count, alive and with a place: a dead, hidden or loaded unit leaves, and one hidden at the start enters
  once shown (the game sends no event for hiding, so that is up to 0.25 s late). `End()` lets every unit inside leave.
  Units with Locust count like any other, so a map filters its dummies in `Accepts`.
- `Accepts` is asked as a unit comes inside, and again every 0.25 s for each unit inside and each one in the area it
  refused: a no makes a unit leave (`OnUnitLeft`), a later yes lets it enter. An area whose answer for a unit never
  changes sets `RecheckAccepts = false;` in its constructor to save the calls; a refused unit is then asked again only
  when it next comes inside.
- The game sees an area in whole 32 cells: a rect or World Editor region is rounded out to the cells it touches (an
  edge on the 32 grid ends its cells, so two that share an edge do not overlap), a polygon or circle is the cells whose
  centre is inside, and always the cell its own centre is in (a polygon's centroid). So a small circle (a landmine) or a
  band thinner than a cell is never left without cells, but it may be only that one. `Contains` is the true shape, so
  the two can disagree within a cell of an edge.
- `Stay(unit)` is the scope of one unit's stay, from its `OnUnitEntered` until it leaves or the area ends, so what an
  area gives needs no taking back: a buff, component or stat modifier held by it (`heldBy: Stay(enteringUnit)`) ends
  when the unit leaves, right after `OnUnitLeft`, and a timer made on it (`Stay(unit).Every(1f, Pulse)`) ends then
  too. A buff two areas give this way stays until the unit has left both, where `Remove<T>()` in `OnUnitLeft` takes it
  at the first. For a unit that is not inside, as in `Accepts` and `OnUnitLeft`, the stay has ended and holds nothing,
  which a development build reports.
- A timer an area's reaction makes with plain `After` is the area's and ends with it, so one made in `OnUnitLeft` while
  the area ends is ended at once; make it on `Game.Instance` to outlive the area. A buff or component added with no
  `heldBy` is the unit's.
- Costs: a unit inside, hidden, dead or refused keeps a check running every 0.25 s; the 1/32 s step for `OnUnitStays`
  runs only while an area with a `Tick` has a unit inside. Adding an area lists every unit of every player once to find
  those already inside, so on a map of thousands of units an area made at each cast pays that at each cast.

## Mouse, keyboard and camera

Add the `Moonwell.Input` package (the template's `.csproj` has the line, commented) for each player's mouse, keyboard and
camera.
A map that never names `Mouse` or `IMouseWatch` listens to no mouse event. One that never reads a mouse's `X`, `Y`,
`Position` or `HasPosition` and writes no `OnMouseMoved` listens to presses and releases only, not to the moves, which
are synced events, 120 to 240 a second for each player moving the mouse.

```csharp
public sealed class Aim : PlayerComponent, IMouseWatch // write only the reactions needed
{
    public void OnMousePressed(Mouse mouse, MouseButton button) { /* the press is synced: safe to change the game */ }
}
player.Camera().Apply(CameraSetup.Named("Intro"), 2f); // World Editor's camera, checked by the build
```

- The mouse's `Position`, `X`, `Y`, `HasPosition` and `IsDown` are the same on every machine and read anywhere;
  `ScreenX`, `ScreenY`, `FocusUnit` and `IsHeldNow` are this machine's own, whichever player's mouse they are read from:
  inside `Local.For`. `IsDown` is for holds: a press and its release can arrive in one instant, so a click is heard by
  `OnMousePressed`, not by checking `IsDown` on a timer.
- Online the events arrive after the network delay. A press over the command card, minimap or portrait arrives with the
  ground under it. The position does not follow a camera move, and (0, 0) and the world's top-right corner less 32 are
  not places: the mouse keeps its last position on them.
- A camera is read inside `Local.For`, and a read is this machine's camera whichever player's it is read from; changing
  it (`PanTo`, `SetDistance`, `Follow`, `Apply`, `Reset`) is safe anywhere and moves only that player's camera.
  `CameraSetup.Named` may sit in a field: named before the package starts, it is looked up when applied.

A map whose code names `Keyboard` or `IKeyWatch` hears every key of each person playing, with any modifiers, as synced
events; the registrations (about 6,000 for each person) are made only for such a map, while it loads.

```csharp
public sealed class Dash : PlayerComponent, IKeyWatch // write only the reactions needed
{
    public void OnKeyPressed(Keyboard keyboard, Key key)
    {
        if (key == Key.Q && keyboard.Shift) { /* the press is synced: safe to change the game */ }
    }
}
```

- `OnKeyPressed` runs once per press: a held key's repeats run nothing, and `keyboard.IsDown(Key.W)` stays true until
  its release, which is what movement reads. `Shift`, `Control` and `Alt` are what the player's last key event held, the
  same on every machine, so a mouse reaction reads them too (`mouse.Player.Keyboard().Shift`). `IsHeldNow` is this
  machine's own, inside `Local.For`.
- The game sends no release for a key held when it loses the keyboard (Alt+Tab, a click outside its window, the Start
  menu): each machine checks its own keyboard every quarter second and sends one after two checks in a row find the key
  let go, so that `OnKeyReleased` comes up to half a second and the network delay late. Windows' switch of keyboard
  language (Alt+Shift) makes the game forget the keys held, so they come up then too, though still held. A player who
  leaves holds no keys from then on, and no reaction runs for them.
- The game still does what it does with a key (a hotkey, the arrows moving the camera), and keys typed in the chat box
  fire nothing. `Key` has .NET's names (`Key.Enter`, `Key.D0` for the 0 above the letters, `Key.LeftShift`); the
  punctuation keys are numbered (`Key.Oem1`), as each keyboard layout puts other characters there.

## Frames

Add the `Moonwell.Frames` package (the template's `.csproj` has the line, commented) for the map's own UI: panels, labels,
images, buttons and the other controls on each player's screen. A frame is made with `new`, set, and added: to the screen in a scope, which ends it
when it ends, or to another frame, which ends it with itself. It is added once: adding it again, to the screen or to a
frame, is refused and reported.

```csharp
public sealed class Hud : GameComponent
{
    public override void OnGameStart()
    {
        var quest = Screen.Add(new Panel { Width = 0.24f, Height = 0.1f, Anchor = FramePoint.TopRight, Offset = new Vector2(-0.01f, -0.05f) }, this);
        quest.Add(new Label { Text = "Find the sword", Anchor = FramePoint.Top, Offset = new Vector2(0f, -0.015f), Alignment = FramePoint.Top });
        quest.Add(new Image { Texture = @"ReplaceableTextures\CommandButtons\BTNFootman.blp", Width = 0.03f, Height = 0.03f, Anchor = FramePoint.Left, Offset = new Vector2(0.015f, 0f) });
        Screen.Add(new Label { Text = "Only you see this" }, this, shownTo: Player.One);
    }
}

public sealed class Shop : Panel // a window of the map's own, made for one buyer: Screen.Add(new Shop(), this, shownTo: buyer)
{
    public Shop()
    {
        Width = 0.3f;
        Height = 0.16f;
        var row = Add(new Row { Width = 0.26f, Height = 0.04f, Gap = 0.01f, Anchor = FramePoint.Top, Offset = new Vector2(0f, -0.03f) });
        var buy = row.Add(new Button { Text = "Buy", Width = 0.12f, Height = 0.035f });
        buy.Clicked += who => who.Gold -= 100; // synced: safe to change the game
        var amount = Add(new Slider { Max = 10f, Anchor = FramePoint.Bottom, Offset = new Vector2(0f, 0.03f) });
        amount.ValueChanged += (who, value) => buy.Text = "Buy " + value;
    }
}
```

- Two layers: `Screen.Add` puts a frame on the whole width of each player's window, beneath the game's HUD, whose art
  draws over it; `Screen.AddOver` puts one over the HUD, within the screen's 4:3 middle, for what must cover it. No frame
  a map makes can do both (seen). Places and sizes are in the game's units: the 4:3 middle is 0.8 wide and 0.6 high
  from its bottom left, and the whole width runs from 0.4 - 0.3 * the window's width / height to 0.4 + 0.3 * it. Frames
  placed by the screen's edges follow the window when it is resized, within half a second.
- A frame sits by its `Anchor` on the same point of its parent, the screen or the frame it was added to, moved by
  `Offset`; `Center` unless set. `Attach(point, other, otherPoint, x, y)` ties one of its points to another frame's in
  place of the anchor, and a second `Attach` stretches it between the two; `Fill(other)` covers another frame. A frame
  tied to one not added yet is placed when that one is added. `Attach` or `Fill` to a frame that has ended is refused
  and reported, as its game frame may go to the next frame of its kind, and so is a `Fill` of the frame itself or of
  one that covers it. `Width`, `Height`, `Scale`, `Visible`, `Alpha` (0 to 255) and `Level` (drawn above siblings of a
  lower one) do as named. A side left at 0 keeps the size the frame's template gives, read once, when the frame is made
  (a template's size reads at once, seen), so a button given only a `Width` keeps its height; one made by its type, as
  a label is, has 0 there, and a label given only a `Width` is as tall as its lines.
- `panel.Add(child, x, y, width, height)` places a child from the panel's top left, y down, with its size, so a layout
  copied from pixels is written as it is. The numbers are in the units `Screen.DesignHeight` sets, the screen's height:
  0.6, the game's, unless set. Set it once, before adding (`Screen.DesignHeight = 768` for a layout made for a screen
  768 high); a frame added already keeps its place. `Offset`, `Width` and `Height` still read the game's units. Given a
  frame already added to the panel, it places it again, as a kit part's pieces are moved.
- Tweens move a value over time: `MoveTo(offset, seconds)`, `SizeTo(width, height, seconds)`, `ScaleTo`, `FadeTo` (an
  alpha) and a label's `ColorTo`, each with an ease from `Ease` (linear unless given), and `Tween(seconds, ease, t =>
  ...)` for anything else, given how far along it is, eased, ending with 1. A tween steps 32 times a second on every
  machine, as a timer in the frame's scope, so it ends with the frame and goes on after a load; a new tween of a kind
  stops the frame's last one of that kind, from where it had got to. Each gives a `Tween` with `Stop()`, `IsRunning` and
  `Finished`, which runs once the end is reached, not when stopped. `SizeTo` grows from the size the map set, so give the
  frame one first. Not inside `Local.For`.

  ```csharp
  toast.FadeTo(255, 0.3f).Finished += () => toast.After(2f, () => toast.FadeTo(0, 0.5f, Ease.InQuad));
  panel.MoveTo(new Vector2(0f, -0.05f), 0.4f, Ease.OutBack);
  icon.Tween(1f, Ease.InOutSine, t => icon.Scale = 1f + 0.2f * t);
  ```
- `Panel` holds others and has the game's window look (`EscMenuBackdrop`) unless its `Template` says otherwise;
  `Template = ""` makes an empty one that draws nothing. `Label` shows `Text` in the game's font, set where its
  `Alignment` says (a `FramePoint`: `Left` for the middle of the left edge; `TopLeft` unless set). `Size` sets the
  text's height in the units `Screen.DesignHeight` sets (12 is the game's own at 768), `Font` a `.ttf` in the map's
  assets by its path in the map (`"war3mapImported\\Fonts\\Title.ttf"`), and `Shadow` a black shadow. They are read when
  the label is made, so set them before adding it: the game takes a label's font and height only from a template, and
  changing them later does nothing (seen). The build writes a template for each height from 0.005 to 0.04 of the
  game's units, in steps of 0.0005, for the game's font and each `.ttf` in the assets, and a label takes the nearest.
  `Scale` sizes the text too, and leaves the label's place and box alone: the game scales a label's offset and box with
  it (seen), so Moonwell gives them divided by it. `Image` shows a `Texture`; a piece, a colour or a round cut of one
  is a copy the build makes ([Assets](#assets)). A map's own panel inherits `Panel` and keeps its frames in fields.
- A panel with a look catches the clicks on it, so they do not reach the units under it, unless `Enabled` is false.
  Labels, images and empty panels let clicks through, until they are listened to for the mouse.
- A frame's events are C# events, run with the player, and a value where there is one, as synced events: so a handler
  may change the game. Every frame has `Wheel` (notches, up positive); `Button` has
  `Clicked` and `DoubleClicked` (two clicks by one player within 0.3 s), `IconButton` `Clicked`, `CheckBox` `Toggled`,
  `Slider` `ValueChanged`, and `TextBox` `TextChanged` and `Submitted` (Enter). A frame listened to for the mouse takes
  it, so a click there stops there. There are no events for the mouse coming onto a frame or going off it: listening for
  them makes the cursor flicker over the frame (seen), and a tooltip shows what hovering is for. A handler that throws is reported, and the frame's other handlers still run. A frame for one
  player hears only that player, and subscribing inside `Local.For` is refused (MW0048).
- After a click on a button, an icon button or a check box, Moonwell gives the keyboard back to the game on that
  player's machine, so the hotkeys go on (a clicked button keeps it otherwise, seen); a text box keeps it while typed in.
  Giving it back blinks the clicked frame's tooltip and the cursor once (seen). `KeepsKeyboard = true` leaves the
  keyboard with the frame instead: no blink, but the game's hotkeys stop until the player clicks the world.
- Drag and drop is two clicks, as a frame gives no mouse down or up (seen): a click picks up a frame with `Draggable =
  true`, and a click on a frame listened to for `Dropped` drops it there, running `Dropped` with the player and the
  frame dropped, synced. Right-click, Esc, a click on the frame held, or a left click on anything else puts it back; a
  click on another draggable frame picks that one up instead. A click that picks up or drops runs nothing else. While a
  frame is held, its `DragIcon` (unless set, an icon button's `Icon` or an image's `Texture`), the frame's size or
  0.04 square, follows that player's cursor on their screen alone; a frame with none to draw, as an empty slot, is not
  picked up, and its click is a click. A load puts every drag back.

  ```csharp
  var sword = bag.Add(new IconButton { Icon = item.Icon, Draggable = true });
  var hand = gear.Add(new IconButton());
  hand.Dropped += (who, dragged) => hand.Icon = ((IconButton)dragged).Icon; // synced: safe to move the item
  ```
- `CheckBox` (`Checked`), `Slider` (`Min`, `Max`, `Step`, `Value`) and `TextBox` (`Text`, `MaxLength`) are one player's
  only: add them to a frame made for that player, as the shop above; a development build reports one that is not, so
  the screens never disagree. They read what their last event brought or what the map last set, the same on every
  machine, and an event that brings the value they already read runs nothing, so a value the map sets never runs its
  own handler. Once a text box has a `MaxLength` of the map's, setting 0 is refused and reported, as the game's own
  limit cannot be given back. Moonwell draws the check box from the game's textures, as the game's own cannot be set
  from code (seen).
  A slider being dragged sends about 85 events a second, each synced; what that costs online is not measured yet.
- `TextArea` shows lines (`Text`, `AddLine`), the last one added at the bottom; `ProgressBar` draws its `Texture` over
  the share of its width that `Value` is of `Min` to `Max`: the fill is as high as the bar, and its width is the share of
  the bar's `Width`, or of the frame it covers with `Fill` (or that frame covers), whose new size it follows (the game
  gives a size from points too late to read), and a bar's look from a `.looks.csv` is cut over that share, not
  squashed ([Assets](#assets)); a label's `Color` colours its text, white unless set, as a colour code
  around it: a label lets clicks through by being turned off, and the game draws a turned-off label's text colour grey
  (seen). Codes in the text draw over it, and its alpha is not drawn, so fade a label with `Alpha`. A label's `Effect`
  moves its colours by itself ([Colours and text](#colours-and-text)).
- `frame.Tooltip = new Tooltip { Text = "..." }` shows a bordered box with the text above the frame while a player's
  mouse rests on it, on that machine alone and with no event sent; the frame takes the mouse for it. It is made for the
  frame's player, on the frame's layer (beneath the HUD for a frame added with `Screen.Add`, over it for one added with
  `Screen.AddOver`), and ends with the frame; taken away, or ended, it shows no more; it is 0.2 by 0.06 unless sized, and placing it (`Anchor`,
  `Attach`) puts it elsewhere. Unplaced, it opens above the frame, or below it where there is no room above (near the
  top bar), and kept in at the screen's sides: as it shows, Moonwell judges the room from the cursor on that machine,
  as a frame's place on the screen cannot be read.
- `Row`, `Column` and `Grid` draw nothing unless given a `Template`, and place the frames added to them in the order
  added: a row left to right, a column top to bottom, a grid in cells (`Columns`, `CellWidth`, `CellHeight`), with `Gap`
  between and `Padding` around; when one ends the rest close up. Their size, and their frames', are the map's.
- `shownTo: player` makes a frame for that player only: it is made on every machine, so the game's handles stay the same
  everywhere, and shown on that player's. A frame added to it is shown to the same player. Seen in single player; with
  two players it is not checked yet.
- Reads give what the map set, the same on every machine, so they are fine inside `Local.For`; setting, making and
  adding are not. `label.ShowLocal(text)`, inside `Local.For`, shows a text on that machine only, as a number only one
  player sees; `Text` keeps the shared text, and setting it shows that everywhere again. `label.ShowNameOf(unit)`,
  outside `Local.For`, shows each machine its own name for a unit (a hero's proper name, else the unit's name, as both
  are in each player's language), and `Text` reads the type's name. It shows the name at the call: call it again after
  a rename.
- `End()` hides a frame with the frames added to it. Nothing is destroyed, as destroying a frame can desync (a claim):
  the next frame of its class and template is given the hidden one, with none of its old values. One sized on a
  template goes only to a frame given a size. One that a frame still showing is tied to is not given out again, so the
  tied frame stays where it was.
- A template the game does not have, as one from an `.fdf` whose TOC is not loaded, is reported in every build, and the
  frame is made by its type instead.
- `player.GameUi()` is the player's own view of the game's HUD: what is set on it changes only that player's screen,
  and lasts until set again, as a camera's does. `Visible = false` hides the whole HUD: the console's art goes, clicks
  reach the world down to the window's bottom with nothing left behind taking them, the map's frames still draw, and
  the command buttons and life bars stay, as the game leaves them; the hero buttons' glows go too. Its pieces,
  `Minimap`, `MinimapButtons`, `Portrait`, `HeroBar`, `HeroButtons`, `TopBar`, `CommandButtons`, `ItemButtons`,
  `SelectionIcons` (the selection panel: a group's icons), `UnitDetails` (one unit's damage, armour and stat boxes),
  `UnitName` (the selected unit's or building's name with its class or level line), `BuildQueue` (a building's
  training or research: its name, the "Training" line and bar, the icon in progress and the queued icons, which cancel
  when clicked), `InventoryText` and `InventoryCover` (the stone over the slots of a unit with no inventory), each have
  `Visible`. With the whole HUD hidden, a piece set `Visible` shows
  alone: item buttons come without the inventory's text and cover, and the selection icons without one unit's details,
  a building's queue and the name, unless those are set `Visible` too. The queue moves with the selection icons, and
  hidden it takes the building's name with it. What the game shows or places again by itself is set again every second
  on the player's machine, and at the end of a cinematic with a letterbox, which brings back the portrait and the
  selection panel. The minimap, the command buttons, the item buttons and the selection icons also move,
  `Attach(point, frame, framePoint)` to a map's frame, and size by `Scale`; they keep their own size between two
  `Attach` points. `Fill(frame)` puts one centred on a map's frame at the frame's size: a command button (0.039 square)
  or an item button (0.031) takes the size, and the minimap, which draws at one size, about 0.139 square, whatever size
  it is given, is scaled to fit inside the frame (seen). A moved button leaves nothing behind taking
  clicks, and the first move stops the game placing its HUD again when the window changes. The selection icons move at
  no size, so they take no clicks: a group's icons draw up and left of the point, one unit's name hangs from it. The
  inventory's bar sits in the selection panel, so moving the icons moves the inventory's text, and the item buttons not
  moved, with them. A map's frame
  ties itself to a piece with `Attach` too: `label.Attach(FramePoint.Bottom, player.GameUi().Minimap, FramePoint.Top)`,
  with the label added by `Screen.AddOver`, as the console's art covers a frame beneath the HUD there. The chat and the
  game's messages are not offered, nor moving the hero icons, as the game crashes or draws them wrong (seen).
  `Portrait.Fill(frame)` puts the portrait, the first unit selected, on a map's frame at its place and size: the game
  places the portrait across the whole window's width, not the 4:3 middle, so Moonwell works out where the frame is
  from how the map placed it and squeezes it for the window's shape, again every second and when the window changes
  (seen in classic and Reforged graphics). A frame tied to a piece of the HUD, or with no size, is refused, as its
  place cannot be known; the game has one portrait, so it shows in one frame at a time. In a
  development build, `-frames` writes the game's frame tree to `CustomMapData\Moonwell\frames.txt`:
  each frame's index under its parent, name, size, whether it shows and how many children it has.
- `Screen.AddTo(frame, gameFrame, scope)` adds a frame that shows only while one of the game's HUD frames shows, named
  as in the game's `.fdf` files (`"EscMenuMainPanel"`, the F10 menu; `"SimpleInfoPanelUnitDetail"`, one unit
  selected) or given as a piece (`player.GameUi().Portrait`). Under the F10 menu's panel, the minimap or the portrait
  the frame is the game frame's own child, placed from it. The rest of the HUD is simple frames, under which the game
  gives a map's frame another parent and it never draws (seen), so there Moonwell adds it over the HUD, placed as
  `AddOver` places it, and shows and hides it with the game frame, checked every 0.03 s on each player's machine. A
  String or Texture by name is error MW0057, as reading whether it shows crashes the game.
- A window from the map's own `.fdf` comes whole: put the `.fdf` files in `assets/`, and the build lists them in a TOC
  the package loads after the game's. `new Panel { Template = "ShopWindow" }` makes one, and any kind takes a template
  of the map's, as a label with the font and size its `.fdf` gives. `window.Child<Label>("ShopTitle")` finds a part of
  the window by its name in the `.fdf`, at any depth, as the kind asked for: the same object each time, shown to the
  window's player, and ending with the window; the frames its kind's constructor adds are made in it. It is null, and
  reported, before the window is added or once it has ended, for a name the window lacks, for a part that has ended or
  was found as another kind, and for a `CheckBox`, `IconButton` or `ProgressBar`, which Moonwell draws from parts of
  its own. A window whose parts were found is not given out again when it ends, as the game cannot give back what was
  set on them. A template that inherits one of the game's (`INHERITS "EscMenuBackdrop"`) is made empty (seen), so the `.fdf`
  gives its look in full; a `SIMPLEFRAME` template is not made, as no kind is one (seen).
- `Handle` is the game's frame, for anything Moonwell does not do yet.
- Some of the HUD's parts that `Natives.BlzGetFrameByName` finds are a String or a Texture of a simple frame in the
  game's `.fdf` files, not frames of their own: `SimpleNameValue`, `SimpleClassValue`, `InventoryCoverTexture` and the
  info panel's texts among them. The game crashes to the desktop given one to `BlzFrameSetVisible`, `IsVisible`,
  `SetAlpha`, `SetLevel`, `SetEnable`, `SetScale`, `SetParent`, `GetParent` or `BlzDestroyFrame`, so that is error
  MW0057 when the name is written as text. Clearing its points, absolute points, a size and its text took one with no
  crash (seen).
- A game saved with the game's own Save Game and loaded loses every frame the map made: they keep their handles but
  show nothing, and setting them does nothing (seen). So Moonwell makes every frame on the screen again at the load,
  with all it keeps: text, colour, values, checks, visibility, size, places, tooltips, the frames found in a window's
  `.fdf`, and the events listened to; ended frames stay gone. Each player's HUD is set again as the map left it. The
  objects stay the same, but `Handle` is a new frame, so what the map did itself on it is redone in a game
  component's `OnGameLoaded`, which runs after. A text box loses the keyboard, and a slider being dragged stops.

## Quests

A quest is a line in the game's quest log (F9): main quests on the left, optional ones on the right. As with effects,
it is in a scope, which takes it out of the log when it ends.

```csharp
var sword = Quest.Create("Find the sword", "The blade lies in the crypt.", this);       // the new-quest message
var wolves = sword.AddRequirement("Wolves killed: 0/5");
wolves.Text = "Wolves killed: 3/5";                                                    // no message
wolves.Done = true;                                                                    // the quest-updated message
sword.Complete();                                                                      // completed: message, sound, flash
Quest.Create("Your own task", text, this, shownTo: hero.Owner, optional: true);       // in one player's log
Quest.Create("The hidden shrine", text, this, discovered: false).Discover();           // later: the new-quest message
```

- `Create`, `Discover()`, `Complete()`, `Fail()` and a requirement turned `Done` each show the game's quest message: a
  gold heading ("MAIN QUEST", "OPTIONAL QUEST COMPLETED", "QUEST UPDATED") and the title, for 20 s, with the game's own
  sound for it and the quest button's flash, as World Editor's Quest Message does. `quiet: true` leaves one out, and a
  quest's `Quiet` (or `Create(..., quiet: true)`) every one. The new-quest message of `Create` comes in the instant after,
  so it lists the requirements added on the lines after it.
- A requirement's `Text` changes with no message, so it can count. One turned done on a quest that is undiscovered,
  completed or failed shows none.
- `Title`, `Description` and `Icon` are read and set; `Quest.DefaultIcon` unless given. `IsOptional`, `IsDiscovered`,
  `IsCompleted` and `IsFailed` read the state. `Complete` after `Fail` turns it completed, and the other way round.
- The log shows an undiscovered quest as "Undiscovered Quest" with a "?" icon, a done requirement as "- text ( Completed
  )" and a completed or failed quest with "Completed" or "Failed" under its title (seen).
- `shownTo: player` puts the quest in that player's log only, with its messages: it is made on every machine and shown
  on that player's (seen in single player; with two players it is not checked yet).
- Reads give what the map set, the same on every machine, so they are fine inside `Local.For`; setting and making are
  not.

## Cinematics

A cinematic plays a scene on its watchers' screens: a method that returns `IEnumerable<Wait>` and waits at each
`yield return`, as GDScript's `await` waits for a timer. Moonwell goes on with the method when the line, the fade or the
wait is over.

```csharp
public sealed class Intro : GameComponent
{
    private static readonly CameraSetup Gate = CameraSetup.Named("Gate");

    public override void OnGameStart()
    {
        var cine = Cinematic.Play(Scene, this);
        cine.Ended += skipped => cine.Camera().Reset();          // at the end, or after Esc
    }

    private IEnumerable<Wait> Scene(Cinematic cine)
    {
        yield return cine.FadeIn(1f);
        cine.Camera().Apply(Gate, 2f);                             // Moonwell.Input: every watcher's camera
        yield return cine.Say(arthas, "We march at dawn.", @"Sound\Dialogue\Arthas01.flac");
        yield return Wait.Seconds(2f);
        yield return cine.Say(uther, "As you command.");
        yield return cine.FadeOut(1f);
    }
}

Transmission.Play(uther, "Hold the bridge!", voice);              // a line in play, with no cinematic
```

- A cinematic is a scope: what is made on it (its lines, `cine.After` timers, effects, sounds, frames) ends with it, when
  the scene's method returns or at a skip, so a skip stops the scene at the step it is at. `Ended` runs after, with
  `true` for a skip: the place to set things up for play, whichever way it ended. The scene starts in the instant after
  `Play`, so `Ended` can be set first. A scene that throws is reported, and the cinematic ends.
- While it plays, each watcher's screen has the letterbox and no HUD, no controls (no orders, no camera moves, no
  cursor), the game's sounds lowered as its cinematic mode does, and the messages before it cleared. The map's frames
  beneath the HUD go with it (seen); those over it are hidden by Moonwell, but those made on the cinematic, as a
  `Screen.AddOver(new Label { Text = "Esc to skip" }, cine)`. `letterbox: false` keeps the HUD for a scene in play, its
  controls still off. Fog, the game's speed, the day and the random numbers are left as they are, unlike World Editor's
  cinematic mode: stop the day (`Game.DayStopped`) or show the place (`Reveal`) yourself.
- Esc skips it once every person watching has pressed it (one press in single player). `skippable: false` turns that
  off, and `cine.Skip()` skips it from code. Esc is the game's skip-cinematic event, which comes on every press, also with
  the controls off (seen); a map that names `Keyboard` hears it too, as `Key.Escape`.
- `shownTo: player` plays it on that player's screen only, and only their Esc counts. A player watches one cinematic at a
  time: a new one for them ends the one before (not as a skip). `Cinematic.Of(player)` is the one they watch, or null.
- `cine.Say(unit, text, voice)` is a line of the scene, to its watchers: the unit's talking portrait in its owner's
  colour, its name (a hero's proper name, or `name:`) and the text, in the letterbox, and the voice. It lasts the voice's
  length, read on the first person's machine and shared, as music's lengths are; with no voice 2 s and 0.05 s a letter;
  `seconds:` sets it. The portrait stays 1.5 s more, unless the next line comes. The unit flashes red and the minimap is
  pinged at it, as the game's transmissions do. The subtitles show whatever the player's setting. A line starts once
  the letterbox is in, 0.6 s after `Play`: said while the HUD fades, the game shows its words and no portrait (seen).
- Esc that starts a cinematic, heard by a trigger of the map's own, does not also skip it: a press in the instant the
  cinematic starts is not counted.
- `Transmission.Play(unit, text, voice)` says a line with no cinematic: the portrait shows in the HUD's portrait place
  (seen). `Transmission.Play<Paladin>("Uther", text)` uses a unit class's portrait, for a speaker not on the map. Both
  take `shownTo:`, and a scene can `yield return` one; `IsOver` reads whether it is over.
- `cine.FadeOut(seconds)` covers the watchers' screens with black, or a `Color`, and `cine.FadeIn(seconds)` uncovers
  them from it; a scene that starts with `FadeIn` starts black. A cinematic that ends covered fades back in over 1 s.
- `cine.Camera()`, with `Moonwell.Input`, changes the camera of every person watching, as a player's `Camera()` does.
  The camera stays where the scene left it: put it back in `Ended`.

## The UI kit

Add the `Moonwell.Kit` package (the template's `.csproj` has the line, commented) for UI made of parts on top of frames:
windows, tabs, lists, icon slots and rich tooltips. Each part is a frame: set it, listen to its events, add frames to it,
or inherit it.

```csharp
var bag = Screen.Add(new Window { Title = "Bag", Width = 0.3f, Height = 0.25f }, this, shownTo: owner);
var tabs = bag.Add(new Tabs());
var slots = tabs.Page("Gear").Add(new SlotGrid { Columns = 4, Rows = 3 });
slots[0].Icon = @"ReplaceableTextures\CommandButtons\BTNSteelMelee.blp";
slots[0].Tooltip = new RichTooltip { Icon = slots[0].Icon, Title = "Sword", Text = "+10 damage", Cost = "150 gold" };
slots.Moved += (who, from, to) => from.SwapWith(to); // synced: move the items too
var heroes = tabs.Page("Heroes").Add(new ScrollList<Unit> { Rows = 6 });
heroes.Items = party;
heroes.Show = (row, hero) => row.Text = hero.Name;
heroes.Picked += (who, hero) => who.Select(hero);
```

- `Window` is a panel 0.3 square with a `Title`, a close button (`Closable`) that hides it and runs `Closed`, and a
  title bar: a click on it picks the window up and the next puts it down, following that player's cursor on their
  screen alone until then; its new place reaches every machine when it is put down, as its `Offset` from the screen's
  centre. Only a window added to the screen moves, and `Movable = false` keeps it still. A frame added to a window with
  no place or size of its own covers the body, below the title.
- `Tabs` has a row of tab buttons over its pages: `Page(title)` adds one and gives its panel. The first shows unless
  `Selected` is set; the chosen tab's button is turned off. A click on a tab shows its page and runs `Changed`.
- `ScrollList<T>` shows `Rows` rows (8 unless set) of its `Items`, as frames cannot be clipped: the rows stay and the
  items move through them, with the wheel or the two buttons at its side. `Show` writes an item onto its row, a button
  (its text unless set), and `Picked` runs with a clicked row's item. After changing the list itself, call `Refresh()`.
- `SlotGrid` is `Columns` by `Rows` icon slots (`IconSlot`, with its `Grid` and `Index`), 0.04 square unless
  `SlotSize` is set, sized by them. A slot showing an icon can be dragged, as frames are, onto another slot of any grid,
  which runs that grid's `Moved` with the two slots; an empty slot only takes drops. The grid keeps nothing but icons,
  so the map moves what the slots hold. `from.SwapWith(to)` in `Moved` swaps the two slots' icons and tooltips, and
  the tooltips move and do not end, as one set in place of another does; a handler that does not call it leaves both
  slots as they were.
- `RichTooltip` is a tooltip with an `Icon`, a `Title` and a `Cost` (written as the map likes, "150 gold") over its
  `Text`, 0.24 by 0.12 unless sized.
- A list, tabs and every other part keeps one set of values for everyone, as frames do: a click on a tab or the wheel
  over a list changes every player's view of it. Give each player a window of their own with `shownTo:`.
- `Kit.Theme` sets the look of the windows and rich tooltips made from then on, and a window's `Theme` its own:
  `Theme.PlayersRace` (unless set) is the game's window, which shows each player their own race's art; `Theme.Human`,
  `Orc`, `NightElf` and `Undead` show that race's to everyone. A theme is the template windows are made from and the
  colours of titles and text, so `new Theme { Window = "MyWindow" }` uses a window of the map's own `.fdf`. The build
  writes the four races' templates into a map that uses the kit, before the map's own `.fdf` files.
- `ShopWindow` is a ready shop: `Sell(new ShopEntry { Icon, Name, Text, Cost })` puts a good in it (four a line unless
  `Columns` is set), as an icon button with a rich tooltip, and `Remove` takes one out. A click runs `Bought` with the
  player and the good; the shop takes no gold and gives nothing, so the map does both there. Inherit `ShopEntry` to
  carry what is sold.

  ```csharp
  var shop = Screen.Add(new ShopWindow { Title = "Blacksmith" }, this, shownTo: buyer);
  shop.Sell(new ShopEntry { Icon = @"ReplaceableTextures\CommandButtons\BTNSteelMelee.blp", Name = "Sword", Text = "+10 damage", Cost = 150 });
  shop.Bought += (who, entry) =>
  {
      if (who.Gold >= entry.Cost)
      {
          who.Gold -= entry.Cost; // synced: give the item here too
      }
  };
  ```

  `Sell<T>(entry)` makes the window sell an item class itself: a click checks that there is a `Buyer`, that the good is
  in stock and that the player has its `Cost` in gold and its `Lumber`, takes them and gives the `Buyer` the item; a
  refusal shows the game's error line. `Stock` is the most the window holds (0, no limit) and `Restock` the seconds until
  one comes back; a good sold out is greyed, and `Left` says how many are left. The stock is the window's, shared by
  everyone it is shown to, and `Bought` does not run for such a good. The game gives no item's cost at runtime, so the
  entry carries it.

  ```csharp
  var potion = Item.Default<Items.PotionOfHealing>();
  var shop = Screen.Add(new ShopWindow { Buyer = hero }, this, shownTo: hero.Owner);
  shop.Sell<Items.PotionOfHealing>(new ShopEntry { Icon = potion.Icon, Name = potion.Name, Cost = potion.GoldCost, Stock = 3, Restock = 30f });
  ```
- `SettingsWindow` is a ready settings window, one player's: a line for each setting, `Check(label, on)` a check box,
  `Slider(label, min, max, value, step)` a slider, and `Choice(label, "Slow", "Fast")` a button that steps through
  choices. Each gives a `Setting` with its `Value` and a `Changed` event, synced. The values are kept in the player's
  `kit-settings.txt` (`FileName`) and come back in their next game a moment after the window is added, running
  `Changed` once, so the map applies a setting in one place; a spoiled file, or a value out of range, is let be.
  Reading the file needs the `Moonwell.Objects` package, as every file does.

  ```csharp
  var settings = new SettingsWindow();
  var damage = settings.Check("Show damage numbers", on: true);
  var zoom = settings.Slider("Camera distance", 1650f, 3000f);
  zoom.Changed += (who, distance) => { /* synced: set that player's camera distance */ };
  Screen.Add(settings, this, shownTo: player);
  ```
- `Scoreboard` is a ready scoreboard: a row for each player in the game, in their colour, and a column for each
  `Column(score)` ([scores](#stats-and-scores)), `Column(name, player => number)` or `Column(name, player => text)`.
  When a team has more than one player, the players go under a line for each team with its totals. Rows are sorted by
  the first number column, highest first, or the one `SortBy(name)` names. A score's column follows the score by
  itself; call `Refresh()` once what another column reads has changed. It shows and hides with `Visible`; to show it
  while a key is held, set `Visible` in `OnKeyPressed` and `OnKeyReleased`.

  ```csharp
  var board = Screen.Add(new Scoreboard(), Game.Instance);
  board.Column(Kills);
  board.Column("Gold", player => player.Gold);
  ```
- `TalentWindow` is a ready talent tree, one hero's. A talent is a class inheriting `Talent`, whose constructor sets
  `Name`, `Icon`, `Text`, `MaxRank`, `Tree`, `Row`, `Column` and `Needs<OtherTalent>(rank)`. The window adds it to the
  hero as a component at its first rank, so it can override reactions, and `OnRankChanged` runs at each rank with
  `RankScope` new, which ends at the next rank: a modifier held by it is that rank's. A click by the hero's owner learns
  a rank when the hero has a point, the talent is below its most rank, `Row × PointsPerRow` points (5 unless set) are
  spent and the talent it needs is at its rank; `Learned` runs after. The map gives the points (`tree.Points += 1`, as
  in `OnLevelUp`), shown under the grid. Talents that set `Tree = "Arms"` go on a tab of that name: the trees share the
  points, and a row opens by the points spent in its own tree; every talent of a window names a tree, or none does.
  `Ranks()` gives a list for a `[Saved]` field of a save class and `Restore(ranks)` brings it back, running each talent
  once at its rank; the save keeps `Points` itself. `Reset()` takes every rank back and gives the points back.

  ```csharp
  public sealed class Cleave : Talent
  {
      public Cleave()
      {
          Name = "Cleave";
          Icon = @"ReplaceableTextures\CommandButtons\BTNCleavingAttack.blp";
          MaxRank = 3;
          Column = 1;
      }

      protected override void OnRankChanged()
      {
          Hero.Modify(Stat.Damage, percent: 5 * Rank, heldBy: RankScope);
      }
  }

  var tree = Screen.Add(new TalentWindow(hero), hero, shownTo: hero.Owner);
  tree.Add(() => new Cleave());
  ```
- Events run as frames' do: synced, with the player, and not inside `Local.For`.

### HUD parts

The kit's HUD parts are frames for a unit's frame, its bars, action buttons, a cast bar, a row of buffs and a list of the
units selected. Each is laid out in the theme (`Kit.Theme.Hud` for the border, `LifeBar`, `ManaBar`, `CastBar`,
`Interrupted` and `Backing` for the bars), and each of its pieces is a frame of its own, which a map may place again,
size or give another texture.

```csharp
var frame = Screen.Add(new UnitFrame { Anchor = FramePoint.TopLeft }, this, shownTo: player);
frame.Show(hero);                               // icon, name, level, life, mana and buffs, followed from now on
frame.Add(frame.Life, 90, 45, 119, 12);         // a piece placed again, in Screen.DesignHeight units: Add with a place
                                                // moves a frame already added to this one
frame.Life.Bar.Texture = Looks.Wow.HealthFill;  // and given the map's own art
frame.Art.Texture = Looks.Wow.PlayerFrame;      // over the bars, under the texts

var fire = bar.Add(new ActionButton { Icon = @"ReplaceableTextures\CommandButtons\BTNFireBolt.blp", Hotkey = "Q" });
fire.Clicked += who => CastFireball(who);
fire.Cooldown(8f);                              // dark, then uncovered clockwise over 8 s
fire.Count = 3;

var cast = Screen.Add(new CastBar(), this, shownTo: player);
cast.Start("Fireball", 2.5f, @"ReplaceableTextures\CommandButtons\BTNFireBolt.blp");
cast.Interrupt();                               // red, "Interrupted", faded out

var party = Screen.Add(new SelectionList { Anchor = FramePoint.Left }, this, shownTo: player);
party.Follow(player);
```

- `StatusBar` is a `Backing`, a `Bar` and a `Label` over both. `Follow(unit, Vital.Life)` or `Vital.Mana` follows a
  unit, read 20 times a second on one timer for every part, and drawn again only when it changed; a dead or gone unit
  reads 0, and `null` empties the bar. `Text` is `BarText.Values` (`123 / 450`, life rounded up as the game shows it),
  `Percent` or `None`.
- `UnitFrame` has `Portrait`, `NameLabel`, `LevelLabel`, `Life`, `Mana`, `Buffs` (a `BuffRow` below the frame) and
  `Art`, an empty image over the bars and under the texts for a map's own frame art. It is hidden until `Show(unit)`,
  and `Show(null)` hides it again. The portrait is the unit's icon, each machine its own as the graphics mode can give
  another, so its `Texture` reads empty; the name is each machine's own, as `ShowNameOf` gives it. To put the game's
  moving portrait in it, `player.GameUi().Portrait.Fill(frame.Portrait)`.
- `ActionButton` is an icon button with `Hotkey` (the key's text in the top right corner; it only shows the key, see
  below), `Count` (a number in the bottom right, `null` for none) and `Cooldown(seconds)`, which darkens the icon and
  uncovers it clockwise from the top until the time is up; `Cooldown(0)` ends it and `CooldownLeft` reads what is left.
  The button still takes clicks while it cools. The sweep is 64 dark images the build writes into a map that uses the
  kit: the game's own sweep model, put in a frame, draws at a size of its own, dark blue, with a second sweep standing
  under it (seen 11-14).
- A key that presses an action button is the map's, from `Moonwell.Input`:

  ```csharp
  public sealed class ActionKeys : PlayerComponent, IKeyWatch
  {
      public void OnKeyPressed(Keyboard keyboard, Key key)
      {
          if (key == Key.Q) { CastFireball(Player); }
      }
  }
  ```

- `CastBar` is driven by the map: `Start(name, seconds, icon)` shows it and fills it over the seconds, then hides it;
  `Interrupt()` fills it in the theme's `Interrupted` texture, says `InterruptedText` and fades it out over half a
  second. A `Start` replaces a cast running or fading. It does not follow a unit's own game casts.
- `BuffRow.Show(unit)` shows the unit's buffs that have an icon, in the order they came on, with the seconds left under
  each (`2m` from a minute on, nothing for a buff with no expiry). The icon is the buff class's `HudIcon`, set in its
  constructor; with none, a declared buff class shows its object data's `Icon`, which the build writes into the map, as
  the game gives no buff's icon while it runs. A buff with neither is left out, and so are the game's own buffs that
  have no Moonwell class. Eight to a line unless `Columns` says otherwise.

  ```csharp
  public sealed class Burning : Buff
  {
      public Burning()
      {
          Duration = 6f;
          HudIcon = @"ReplaceableTextures\CommandButtons\BTNImmolationOn.blp";
      }
  }
  ```

- `SelectionList.Follow(player)` shows one small `UnitFrame` (icon, name, life and mana) for each unit the player has
  selected, up to 24, in the order selected, one column down unless `Columns` is set. It changes with
  `OnSelectionChanged` and drops a unit that dies or goes at once. It is as high as the entries showing, so it is best
  placed by its top (`Anchor = FramePoint.TopLeft`). A click on an entry selects that unit alone, for the player who
  clicked; frames do not see Shift, so there is no Shift-click. `Entry = () => new PartyFrame()` makes a
  map's own `UnitFrame` class for each, set before `Follow`. The game's active subgroup (Tab) is not shown: no native
  reads it.

## Boards and dialogs

A multiboard is the game's table in the top right; a leaderboard its list of names and numbers there; a dialog its
popup of buttons. As with effects, each is in a scope, which ends it when it ends.

```csharp
var board = Board.Create("Kills", Player.Users, this);                  // a row for each player
board.Column("Player", p => p.ColoredName);
board.Column("Kills", p => kills[p].ToString(), width: 0.03f);
Every(0.5f, board.Refresh);                                              // read the rows again

var info = Board.Create("Round 3", columns: 2, rows: 2, this, shownTo: player);  // cells by hand
info[0, 0].Text = "Wave";
info[0, 1].Text = "3 of 10";
info[1, 0].Icon = @"ReplaceableTextures\CommandButtons\BTNFootman.blp";

var top = Leaderboard.Create("Team gold", teams, this);
top.Label(t => t.Name).Value(t => gold[t]);                              // sorted, highest first
Every(1f, top.Refresh);

var mode = Dialog.Create("Pick a mode", this);
mode.AddButton("Easy", who => votes[who] = Mode.Easy, hotkey: 'E');
mode.AddButton("Hard", who => votes[who] = Mode.Hard, hotkey: 'H');
mode.ShowTo(Player.Users);
```

- `Board.Create(title, rows, scope)` makes a row for each of the rows, any list (players, teams, heroes), and
  `Column(name, row => text)` a column, with `width:` (`Board.DefaultWidth`, 0.06, unless given) and `icon: row => path`.
  When any column has a name, the first row is the names, in the game's gold. `Refresh()` reads the rows again and
  writes only the cells that changed; it runs by itself in the instant after `Create`, so the columns added on the next
  lines show, and after that when the map calls it, as in `Every` above.
- `Board.Create(title, columns, rows, scope)` makes a board of empty cells. `board[row, column]`, from 0, has `Text`,
  `Icon` (none unless set), `Color` (white unless set) and `Width`; `Title`, `Rows` and `Columns` are read and set. A cell
  outside the board is reported. Colour codes work in a text.
- A player sees one multiboard, the newest shown to them; when it ends, or is set `Visible = false`, the one before
  shows again, and set `Visible` again it is the newest (seen). A board hides while the game's tooltip for a button
  shows, as it would cover it and no frame change puts it under (seen): `HidesForTooltips = false` keeps it.
- `Leaderboard.Create(title, rows, scope)` makes a leaderboard, `Label(row => text)` (a player's name unless given) and
  `Value(row => number)` its rows, sorted highest first, and `Refresh()` reads them again, as a board's does. The game
  keys each row to a player slot, and two rows on one slot crash it (seen), so Moonwell gives row 1 slot 1, row 2 slot 2,
  and a leaderboard shows at most `Leaderboard.MaxRows`, 24: the rest are left out and reported. A player sees one
  leaderboard, the newest shown to them, as with multiboards, and a leaderboard shows beside a multiboard (seen).
- `shownTo: player` makes a board or leaderboard for that player: it is made on every machine and shown on theirs
  (seen in single player; with two players it is not checked yet).
- `Dialog.Create(message, scope)` makes a dialog and `AddButton(text, who => ..., hotkey: 'E')` a button: its action
  runs with the clicking player, as a synced event, so it may change the game, and the click closes the dialog for that
  player. `ShowTo` takes a player or a list of them; `IsShownTo(player)` reads whether it is on their screen now.
- A player sees one dialog at a time: one shown while another is up waits, and shows once that one is answered or
  ended. A dialog shown from a button's action waits behind those waiting already. `End()` takes it off every screen.
- In single player a dialog pauses the game while it shows (seen), so a timer that waits for the answers waits too.
  14 buttons fit on the screen (seen); a development build reports more than `Dialog.MaxButtons`.
- Reads give what the map set, the same on every machine, so they are fine inside `Local.For`; setting, making and
  showing are not. `Handle` on each is the game's, for anything Moonwell does not do.

A vote is a dialog, a map and a timer:

```csharp
var votes = new OrderedMap<Player, Mode>();
var mode = Dialog.Create("Pick a mode", this);
mode.AddButton("Easy", who => votes[who] = Mode.Easy);
mode.AddButton("Hard", who => votes[who] = Mode.Hard);
mode.ShowTo(Player.Users);
After(15f, () =>
{
    mode.End();
    Start(MostVoted(votes)); // the map's own
});
```

## Colours and text

`Color` is a colour as the game takes it, each part 0 to 255: `new Color(255, 204, 0)`, `Color.FromHtml("#ffcc00")`,
the named ones (`Color.Red`, and `Color.TooltipGold` for the game's own gold) and `Color.Of(PlayerColor.Blue)`. `Lerp`,
`Darkened`, `Lightened`, `Inverted` and `Color.FromHsv` make one from another, as in Godot. `unit.Tint(color)` takes
one too.

`"Gold".Colored(Color.TooltipGold)` colours text, and `player.ColoredName` is a player's name in their colour. Letter by
letter: `Rainbow()` (a shift turns it round), `Gradient(a, b)` through two colours or more, `Alternate(a, b)` and
`Confetti()`; `Highlight("Stun", Color.Red)` colours a word wherever it appears.
`"Victory".Sweep(Color.Gold, Color.White, time)` draws a band of white in gold where it is at that time, moving 20
letters a second; `SweepShape.Wide`, `Sharp`, `Comet` and `Repeating` (a new band every second) change its shape. A
screen line shows 1,023 characters, and a colour code takes 10: `Rainbow` and `Gradient` share codes on a long text to
keep within them, while `Alternate` and `Confetti` fit about 90 letters. The game shows no alpha in text, and `|r` goes
back to white, not to an outer colour. `Text.Clock(125f)` writes "2:05", and `Text.TimeOfDay(6.5f)` "06:30".

A label or floating text moves its colours by itself with an `Effect`: `TextEffect.Rainbow()` turns the rainbow, its
colours flowing along the text, once round in 2 s unless `seconds:` is set, and `TextEffect.Sweep(main, sweep, shape)`
passes a band again and again, 20 letters a second unless `speed:` is set.

```csharp
var title = Screen.Add(new Label { Text = "Victory" }, this);
title.Effect = TextEffect.Rainbow();                                        // turns by itself
var sign = FloatingText.Create("Shop", x, y, this);
sign.Effect = TextEffect.Sweep(Color.TooltipGold, Color.White, SweepShape.Comet, speed: 10f);
title.Effect = null;                                                        // stands still again
```

Moonwell redraws every moving text 32 times a second on one timer, counting from when the effect is set, until the
`Effect` is set to null or the text ends. `Text` reads the words as set, and a new `Text` keeps the effect; `Color` is
not drawn while it runs (a floating text's alpha and fade are). A label's `ShowLocal` and `ShowNameOf` text take it too.
A redraw is mostly the colouring in Lua: about 0.1 ms for 13 letters and 0.2 to 0.4 ms for 53 (seen), so each moving
text of 13 letters takes about 3 ms of every game second, and one of 53 about 11.

## Random numbers

`Game.Random` draws from the game's own random generator, the one every machine shares: `Game.Random.Int(1, 6)` (both
ends included), `Real(0f, 1f)`, `Chance(0.25f)` (true one time in four), `Angle()` in degrees, `Pick(list)` and
`Shuffle(list)`. `new Dice(seed)` is a generator of its own with the same methods: the same seed gives the same numbers
in every game and in .NET tests, for a level built from a seed. Neither may be used inside `Local.For`, where only one
machine would draw (MW0001), and `new Random()` without a seed is refused everywhere (MW0038).

```csharp
var spawn = Game.Random.Pick(spawnPoints);
if (Game.Random.Chance(0.1f)) Game.Print("A rare creep appears.");

var level = new Dice(seed: 1234);
var rooms = level.Int(5, 9);
```

## Players

`Player.One` to `Player.TwentyFour` are the player slots, as the lobby numbers them; `Player.Get(slot)` takes the
slot from 0, so `Player.Get(0)` is `Player.One`. A slot is not a colour: a map can recolour its players. `Player.Red`
to `Player.Peanut` are whoever in the game has that colour, and `Player.ByColor(color)` the same for a colour in a
variable: the first in slot order, or null when nobody in the game has it, so check before using one.
`Player.NeutralAggressive`, `NeutralPassive`, `NeutralVictim` and `NeutralExtra` are the neutral players, and
`StartX` and `StartY` a player's start location.

`player.Print("Kills: 3")` shows a line to one player and `Game.Print("Wave 2")` to everyone; both take a time on
screen, `seconds: 10f`. The game's own kinds of message have their sounds: `player.Hint("Press F to open the shop")`
and `player.Warning("The boss wakes")` show as the game's hints and warnings, and `player.Error("Not enough mana.")`
like its errors: the same letters and colour, with the error sound, one line above the game's own error line (as low
as a message goes), and kept in the message log (F12). It is not allowed inside `Local.For`, as the sound is made on
every machine. `player.ClearMessages()` clears the player's screen. A team has `Print`, `Hint`, `Warning`, `Error` and
`ClearMessages` too, for each of its players. `Game.Log("wave 2 started")` writes a line to the log file instead (under The build). `Package.Version` is the version of Moonwell the map is built with. `Gold`, `Lumber`, `FoodUsed`, `FoodCap`, `FoodCapCeiling`, `GoldGathered`, `LumberGathered`,
`GoldUpkeepRate` and `LumberUpkeepRate` are the player's own: `player.Gold += 25`.

`Player.Playing` is every player in the game, people and computers, and `Player.Users` the people:
`foreach (var player in Player.Users)`. `player.SlotState` is `Empty`, `Playing` or `Left`, and `player.Controller`
says who plays it. `OnLeave(Player leavingPlayer)` runs on the game's components, then the leaving player's.

`Name` and `Color` are read and set, and `Race` read; setting `Color` also recolours the player's units. `Team` is the
player's team: the map's own (under The map's settings), or, in a map without `Slots`, one named after the game's team
number ("Team 1"). `player.Team == Arena.West` compares, and `team.Players` is the team's players in the game. Setting
`Team` moves the player: it becomes an ally of the new team's players, with the team's shared vision and control, and an
enemy of the old team's, both ways. A team no slot names is refused.

**Victory and defeat.** `player.Win()` and `player.Lose("Your hero has fallen.")` take one player out, as World Editor's
Victory and Defeat do, and the others play on. A person sees the victory or defeat box, which pauses a single-player
game. The victory box's Continue ends their game with the score screen: `Win(scores: false)` leaves the scores out, and
`Win(dialog: false)` ends it at once. The defeat box shows the message, or the game's own words without one, and offers
Restart in single player. A loser's slot turns `Left` and a winner's stays `Playing`; the units of both stay, and
neither runs `OnLeave`, which is for a player who quits. `Game.Over(Arena.West)` ends the game: the team's players in
the game win and everyone else in it loses, the losers first; `Game.Over(player)` has one winner. A player who has won
or lost already is left as they are, and a second `Win` or `Lose`, or one for a player not in the game, is refused. The
game and the players are not scopes to end: `Game.Instance.End()` is refused.

`Command("zoom", (player, rest) => ...)` on a scope hears a chat command: a line a person types that starts with `-zoom`,
in any case. `rest` is what follows, trimmed, so `-zoom 2000` gives `"2000"`. It runs on every machine, as chat reaches
them all, and ends with its scope, or when the `ChatCommand` it gives back ends. A chat line holds 127 characters. A
name that is empty, holds a space or starts with the dash is reported and refused in every build, and a development
build reports `debug`, `step`, `speed` and `stats`, the debug mode's own commands, since both would run there.

`a.MakeAllies(b)` and `a.MakeEnemies(b)` go both ways and replace what the two gave each other: `MakeEnemies` leaves
nothing between them, and `a.MakeAllies(b, sharedVision: true)` lets each see what the other sees. `a.ShowVisionTo(b)`
lets `b` see what `a` sees, `a.HideVisionFrom(b)` stops it and `a.ShowsVisionTo(b)` reads it: these go one way and leave
the alliance as it is. For anything finer, `a.SetAlliance(b, Alliance.Allied | Alliance.SharedControl)` sets what `a`
gives `b`, one way as the game keeps it, and `a.AllianceWith(b)`, `a.IsAlly(b)` and `a.IsEnemy(b)` read it.

`OnAllianceChanged(Player givingPlayer, Player receivingPlayer, Alliance before)` runs on the game's components, then
the giving player's, then the receiving player's, inside the change, once for each pair whose alliance changed, made by
code, a native or the game. `before` is what was given until then, and `givingPlayer.AllianceWith(receivingPlayer)` reads
what is given now. `MakeAllies` runs it once for each way that changed, and a team move for each pair it changes. A raw
native that sets the alliance one type at a time runs it once for each type.

**Selection.** `player.Select(unit)` selects only that unit for the player, as a click does; `AddToSelection(unit)`,
`Deselect(unit)` and `ClearSelection()` do the rest. `player.Selection` is the units the player has selected, in the
order selected, the same on every machine, so it is read anywhere, `Local.For` included:

```csharp
Command("kill", (player, rest) =>
{
    foreach (var unit in player.Selection)
    {
        unit.Kill();
    }
});

public sealed class Tavern : UnitComponent
{
    public override void OnSelected(Player selectingPlayer) => selectingPlayer.Print("Pick a hero.");
}
```

- `OnSelected(Player selectingPlayer)` and `OnDeselected(Player deselectingPlayer)` on a unit's class and its components,
  then `OnUnitSelected(Unit selectedUnit, Player selectingPlayer)` and `OnUnitDeselected` on the game's components and
  the selecting player's (whoever owns the unit), run as the game's events come. Inside them `Selection` already holds
  the unit, or no longer does.
- The events reach every machine a moment after a click or a `Select` (0.1 to 0.2 s in single player, the network's
  delay online), and `Selection` follows them, so it does not hold a unit in the instant `Select` is called.
- They run as the game fires them: a click on another unit, a drag or a control group deselects every unit, then
  selects the new ones, a unit already selected among them; a click on a selected unit selects it again; a double-click
  selects each unit of its type on screen.
- The game drops a unit from a selection with no event when it dies, is removed, hidden, gets Locust, or changes hands to
  a player who does not share control: `Selection` leaves it out and no `OnDeselected` runs, so what `OnSelected` set up
  is also undone in `OnDeath` or `OnLeave`. A unit hidden and shown again in one instant stays in `Selection`, though the
  game dropped it.
- `Select` and the rest change the selection on the player's own machine only, unit by unit, as the game takes it; they
  are changes, not for `Local.For`. A dead, hidden or Locust unit is not selected. Seen in single player; with two
  players it is not checked yet.
- `OnSelectionChanged(Player player)` on the game's components and that player's runs once when a player's selection
  has changed: after the instant's events are all in, so a drag over twelve units is one change, not 24, and only when
  the units or their order differ from the last time; read `player.Selection` inside. A unit the game drops with no
  event shows at the next change.
- A map listens for selections only when its code writes a selection reaction or reads `Selection`.

`LevelOf<T>()`, `SetLevel<T>(2)` and `AddLevel<T>()` take an upgrade class;
`SetMaxAllowed<T>(1)` a unit or upgrade class, where 0 forbids it and `Player.NoLimit` lifts a unit's limit; and
`SetAvailable<T>(false)` an ability class.

### Research

An upgrade class can run code for each level a player gains, and a building hears its own research:

```csharp
[ObjectId("R000")]
public sealed class SharpBlades : Human.Upgrades.IronForgedSwords
{
    public override void OnResearched(Player player, int level) => player.Print("Blades " + level);
}

[ObjectId("h000")]
public sealed class Smithy : Human.Units.Blacksmith
{
    public override void OnResearchStarted(Upgrade upgrade) { }
    public override void OnResearchCancelled(Upgrade upgrade) { }
    public override void OnResearchFinished(Upgrade upgrade) { }  // after the upgrade's OnResearched
}
```

- `OnResearched` runs on every machine, once per level, in order: when the game's research finishes, and inside
  `SetLevel` and `AddLevel` for each level they raise. Lowering a level runs nothing, nor does a level set with the
  natives. `AddLevel` stops at the top level (`MaxAllowed`).
- The building's three reactions run on it and on its components; `upgrade` is the upgrade class's object, or a plain
  `Upgrade` with its `TypeId` for an upgrade with no class.
- A requirement of the map's own ("needs 10 kills") is `SetMaxAllowed<T>`: 0 hides the button, the level count shows it.

### The 3.0 bag and equipment slots

A hero given the equipment slots (`AEqu`) and the bag (`AIni`) has nine equipment slots (head, chest, gloves, boots, two
rings, primary, offhand, trinket, in that order) and a bag of 30, with the game's own panel:

```csharp
hero.Equip(ring);               // from the ground, the six slots or the bag; false when its slots are full (it goes to the bag)
hero.Unequip(ring);             // into the bag
var worn = hero.Equipped;       // a new list each read, in slot order
var bagged = hero.Bag;

[ObjectId("I000")]
public sealed class Helm : Items.AncientBronzeHelmet
{
    public override void OnEquipped(Unit carrierUnit) { }    // after OnPickedUp, then the unit's OnItemEquipped
    public override void OnUnequipped(Unit carrierUnit) { }  // after OnDropped, then the unit's OnItemUnequipped
}
```

- Equipping and unequipping are a drop and a pick-up in the game; `OnEquipped` runs after the pick-up into the slots,
  `OnUnequipped` after the drop of an item Moonwell saw equipped. A player's own move in the game's panel fires neither,
  so Moonwell runs the same drop, pick-up and equip reactions for it, just after the move: there `Place` already reads
  where the item went, in `OnDropped` too. `Item.Carry` runs in the equipment slots, so a gift
  `heldBy: Item.Carry` holds while the item is worn and ends when it goes to the bag, where an item's abilities do
  nothing.
- `Equipped` and `Bag` take in items Moonwell has not seen, so they are not for `Local.For`. Items do not stack: the game
  never stacked any in the probes, the Max Stacks field set or not.

### Shops

A shop of the game's (a unit that sells items) runs reactions on itself and its components:

```csharp
[ObjectId("n000")]
public sealed class Goblin : Other.Units.GoblinMerchant
{
    public override void OnItemSold(Item item, Unit buyer) { }    // before the buyer's pick-up; the gold is already taken
    public override void OnItemPawned(Item item, Unit seller) { } // a unit sold it an item: before its drop and its gold
    public override void OnUnitSold(Unit soldUnit, Unit buyer) { }
}

market.AddStock<Items.PotionOfMana>(count: 2, max: 3);           // a shop with Sell Items (Asid), as the Marketplace has
market.RemoveStock<Items.PotionOfMana>();
```

- The reactions run for a shop Moonwell knows: one of a class of the map's, or one with components.
- `AddStock` works only on a shop with Sell Items, and only on goods it added: a good of the shop's own data (Items
  Sold) can't be changed at runtime. A development build reports a shop without Sell Items. A neutral passive shop takes
  a good sold out of its stock (Blizzard.j does, in every map), so an added good there does not restock.
- A good is not for sale before its stock start delay, counted from the start of the game, and a shop sells nothing in
  its first moment. Units can't be put on sale by code: the game sold none in the probes.
- For prices and stock of the map's own, or a shop window of its own look, use the kit's `ShopWindow`.

The `Natives` class has every native of the game for what Moonwell does not cover yet.

## Multiplayer

Code that differs between players' machines goes in `Local.For(player, () => ...)`, as Smite reads a unit's name,
which is in each player's language. The build rejects, as compile errors:

- A change to the game inside it (MW0001). The analyzer follows what the block runs: your methods and their
  overrides, the classes it makes or first touches (their field initializers and static constructors), operators, a
  `using`'s `Dispose`, a `foreach`'s enumerator, and the delegates it runs. A lambda you hand on through a parameter
  of your own (`ShowTo(p, () => ...)`, where `ShowTo` gives it to `Local.For`) is checked as part of the block; calling
  that parameter outside `Local.For` too is MW0040. A delegate it cannot follow (a list's element, a library's
  result) is MW0039: write the lambda in place.
- A library's method, property, constructor or operator inside it (MW0039 too): a library project of your own, or a
  NuGet package. The analyzer cannot see a library's code, so the library says what a block may call, with
  `[LocalSafe]` or `[MachineLocal]` (below). .NET's own classes need no mark.
- A machine-local read outside it (MW0002): the local player, the camera, the mouse's screen position, frame text,
  localised names, `RealTime.LocalNow`, `PlayerFile.Read`, and .NET's own clock and addresses (`DateTime.Now`, `Environment.TickCount`, a `Stopwatch`'s
  reading, `GetHashCode` of a handle or of a class without its own, a handle turned into text). A frame's parent and
  children (`BlzFrameGetParent`, `BlzFrameGetChild`, `BlzFrameGetChildrenCount`) are the same on every machine: read
  them outside it, as `BlzGetFrameByName` is.
- A change inside it to something every machine shares (MW0048), in the block or in what it calls: a field or
  auto-property of your map, a local or parameter from outside the block, an event of your map, or a collection held in
  one of those (`Scores.Add(x)`, `Scores[i] = x`, on .NET's collections, arrays, `StringBuilder`, `Random`, `OrderedMap`,
  `OrderedSet`, `JsonObject` and `JsonArray`). What the block makes is free: a constructor writing its own object,
  `new Panel { Count = 3 }`, a local that only holds a `new` of the block, and a struct in a local. A method called on a new object is still checked.
  A list assigned to a local later, given as a parameter, or returned by a method or a property with a body, is not seen.
- `new Random()` without a seed, anywhere (MW0038): it reseeds the game's own random generator with each machine's
  clock. Use `Game.Random`, or `new Dice(seed)` with a seed every machine shares (see Random numbers).
- `GetHandleId` (MW0003), looping over a `Dictionary` or `HashSet`, or one held as `IDictionary`, `ISet` or an
  `ICollection` (MW0004; use `OrderedMap` and `OrderedSet`), and `long`, `ulong`, `uint`, `double` and `decimal`
  (MW0005).
- A blizzard.j function that changes the game inside its own `GetLocalPlayer()` block, such as
  `SelectGroupForPlayerBJ`, which desyncs wherever it runs (MW0006).

`OrderedMap` and `OrderedSet` keep the order things were added in, the same on every machine.
`foreach (var pair in map)` gives each key and its value in that order (`pair.Key` and `pair.Value`, or
`foreach (var (key, value) in map)`), and LINQ works on both. A loop that adds or removes a key or an item throws: to
change one as you go, loop over `set.Snapshot()`, or over a map by index from the end. In the game each pair is a small
table, so code that runs every tick over many keys loops by index with `KeyAt` and `ValueAt`, which makes none.

`[MachineLocal]` on a method of your own makes it code of one machine: its body is checked as a `Local.For` block, and
calling it outside one is MW0002. Where the code runs on every machine anyway, on an override of Moonwell's
(`OnGameStart`) or on a lambda given to a scope's `Every`, the attribute is MW0040.

`[MachineLocal]` on a field or auto-property of your own makes its value one machine's: code of one machine writes it,
and any other read is MW0002, `+=`, `++` and `.Add` included. A plain `=` is allowed anywhere, as it reads nothing: a
reset in a constructor needs no block. A local or an event cannot be marked.

In a library project of your own, the marks also say what a map's `Local.For` may call. `[MachineLocal]` on a method
works as above: its body is checked when the library is built. `[LocalSafe]` goes on a method, a constructor, a
property (its getter only) or a class (every member), and says it only reads, or changes only what one player sees or
hears. Nothing checks that: the analyzer takes the library's word, so a wrong `[LocalSafe]` desyncs with no error.
A library you cannot mark, such as someone else's package, is called from a block marked `[SyncUnchecked]` (next).

`[SyncUnchecked]` on a method turns the checks off in its own body, except `decimal`; `Local.For` still follows a method
so marked, so a block whose changes are meant is marked itself: `Local.For(p, [SyncUnchecked] () => ...)`. That is the
way for MW0048's false alarms: a lazy `instance ??= new Hud()`, or a cache filled inside a block.
`Natives.BlzSendSyncData` inside `Local.For` sends a machine-local value to every machine.

A `#pragma warning disable`, `[SuppressMessage]`, `NoWarn` or `.editorconfig` on one of these rules removes its
protection. To turn the checks off where you mean it, use `[SyncUnchecked]`. Every build lists each place that turns a
Moonwell rule off, `[SyncUnchecked]` included, so nothing is silenced unseen (see The build).

## Files and sharing

`PlayerFile.Write(player, "settings.txt", text)` writes a file on that player's computer, in
`Documents\Warcraft III\CustomMapData\` and the map's save folder, replacing what it held; no other machine writes
anything. The folder is the map's `SaveFolder` (`SaveFolder = "MyRpg";` in `Map.cs`), else its name, in letters and
digits only. A file name has letters, digits, `-`, `_` and `.`, and ends in `.txt` or `.pld`, the files the game reads
back. A file holds about 40,000 characters, fewer with quotes, tildes, backslashes and line breaks; a name the game cannot use, or a
longer text, writes nothing, and a development build reports it.

`ReadFile(player, "settings.txt", text => ...)` on a scope reads the file on that player's machine and hands its text to
every machine once it has arrived, a moment later. The text is null when there is no file, when the player leaves first,
or when they are not a person playing. Like a timer, it belongs to its scope: nothing runs if the scope ends first.
Inside `Local.For`, `PlayerFile.Read(path)` reads this machine's file at once.

`Share(player, () => value, value => ...)` on a scope hands every machine what one machine knows, the same way: a text, a
whole number, a float or a bool. The first lambda runs on the player's machine only and is checked as a `Local.For`
block; the second runs on every machine. `Share(p, () => RealTime.LocalNow.Hour, hour => ...)` gives everyone that
player's hour.

A replay runs the map again, but writes no files, so watching one leaves the players' files as they were. A file written
in a game's first moments waits until Moonwell knows it is no replay.

The player can open and change their files, so check what you read. `ByteWriter` writes whole numbers, floats, bools and
texts as compact bytes, and `ByteReader` reads them back in the same order; a read past the end throws a
`FormatException`. `ToCode()` gives the bytes as letters easy to type, `X7KQ-M3PA`, and `ByteReader.FromCode(text)` reads
them back, null for a text that is no code; it reads lower case too, and O for 0 and I or L for 1. `ToArray()` gives
the bytes for .NET's `Convert.ToBase64String`.

The game gives a file back through an ability Moonwell adds to the map, with the id `AMWf`, so reading files needs the
`Moonwell.Objects` package, and no object of the map may have that id.

## Saves

A save keeps a player's progress for a later game: a class deriving from `SaveGame`, with `[Saved(n)]` on each field to
keep. Each field has a number of its own, so renaming and reordering fields is safe. A field an old save lacks keeps the
value the class gives it, and a field the class no longer has is skipped. Never give a removed field's number to another.

```csharp
public sealed class RpgSave : SaveGame
{
    [Saved(1)] public int HeroType;
    [Saved(2)] public int Level;
    [Saved(3)] public List<int> Items = new();
    [Saved(4)] public RealTime LastClaim;
}

public sealed class MyMap : Map
{
    public MyMap() { Name = "My RPG"; SaveKey = "a long private text, never changed"; }
}
```

A saved field is an `int`, `float`, `bool`, `string`, an enum, a `RealTime`, a `List` or array of those, or a part: a
class of the map's own with `[Saved]` fields of its own, such as a hero's gear. It is public, and a property has a public
`get` and `set`. A save class and a part have a public constructor without parameters and no `required` member, and are
not private, protected, file-local or generic, as the build's code names them. An override of a saved property is saved
under its base's number. Error MW0052 says when one doesn't hold, or when a number is used twice. A library of the map's
own may hold save classes too; they are public.

`SaveGame.Save(player, save)` runs on every machine: it sets `save.Code`, a code the player can type, such as
`X7KQ-M3PA-...`, and writes it to a file on the player's computer, in the map's save folder. `Load<RpgSave>(player, save
=> ...)` on a scope reads the file back and hands the save to every machine a moment later, null when there is none.
`SaveGame.FromCode<RpgSave>(player, text)` reads a code at once, null when it is no code. It reads lower case, spaces and
missing dashes, O as 0 and I or L as 1.

```csharp
public sealed class Saving : PlayerComponent
{
    public void SaveHero(Hero hero)
    {
        var save = new RpgSave { HeroType = hero.TypeId, Level = hero.Level };
        SaveGame.Save(Player, save, "paladin");
        Player.Print("Your code: " + save.Code);
    }

    public void LoadHero()
    {
        Load<RpgSave>(Player, "paladin", save =>
        {
            if (save == null) return;
            // make the hero
        });
    }
}
```

Slots keep several saves apart, one file each: `"paladin"` and `"mage"` are letters, digits, `-` and `_`, and with no
slot named a save goes in one default slot. A code holds one save and can be loaded into any slot.

The map hears `-load` with a chat command (under Players): `Command("load", (player, rest) => ...)` gives the code as
`rest` for `FromCode`. A chat line holds 127 characters, so after `-load ` a code has 121: about 60 bytes of
save, which a typical RPG save fits. A code longer than that drops its dashes. A development build reports a save
too long for a chat line; it still loads from its file.

A save is tied to the player's name and to the map's `SaveKey`. The check fails for anyone else, so a code is no use to
another player, and a changed key fails every save made before. Single player names player 1 "Local Player", so a save
made offline does not load online. A determined cheater can read the key from the map; the check stops casual edits,
not them. A file that fails its check loads as null and writes a line to the log, so "no file" and "changed" can be told
apart.

One save class may go without an id. Every other one has `[SaveId("settings")]`, never changed, so a code of one class
fails for another.

The build keeps a record of the saves in `moonwell-saves.txt` beside the project file: each class's numbers and types (a
part's or an enum's with its class), each save class's id, a fingerprint of the key and the save folder. Commit it with
the map. The build stops on a change old saves would not survive (a number used again after its field was removed, a
field whose type changed, a part or enum swapped for another, a changed id, a new key or folder), and names the line to
delete when the change is meant. A lost record loses only these checks.

## JSON

Add the `Moonwell.Json` package (the template's `.csproj` has the line, commented) to read and write JSON, as a tree with
the names of .NET's `System.Text.Json.Nodes`. A map that does not reference it carries none of its code.

```csharp
using Moonwell.Json;

var settings = new JsonObject { ["volume"] = 80, ["name"] = "Åsa", ["shown"] = new JsonArray { "gold", "kills" } };
PlayerFile.Write(player, "settings.txt", settings.ToJsonString()); // {"volume":80,"name":"Åsa","shown":["gold","kills"]}

ReadFile(player, "settings.txt", text =>
{
    var read = text == null ? null : JsonNode.Parse(text);
    var volume = read?["volume"] is { } found ? (int)found : 100;
});
```

A number is an `int` when it is whole and within `int`, else a `float`, as the game has no `double`: `(int)node`,
`(float)node`, `(bool)node`, `(string?)node` and `GetValue<T>()` read a value, and `node.AsValue().TryGetValue(out int
gold)` says whether it is one. A missing key reads as null. An object's keys keep the order they were added or read in,
so a loop over one goes the same way on every machine. `ToJsonString()` writes compact JSON with letters outside ASCII as
they are. A node is in one object or array at most, as in .NET.

`JsonNode.Parse` throws a `JsonException`, whose `Position` says where, for text that is no JSON or nests deeper than 64,
and a file a player edited can be either: catch it where you read one.

## Data files

CSV and JSON files in the project's `data` folder become typed C# the build writes before it compiles, so object
constructors and game code read them by name, and a wrong name is a compile error. The class is `Data`, with a class
for each file (`data\abilities.csv` is `Data.Abilities`). Run a build once after adding a file, so the editor sees it.

```
data\abilities.csv            data\waves.csv
Id,Damage:float,Cooldown      Wave,Unit,Count
Smite,75;150;225,9            1,Footman,10
                              2,Knight,4
```

```csharp
Damage = Levels(Data.Abilities.Smite.Damage);          // in Smite's constructor
foreach (var wave in Data.Waves.All) { ... }            // in game code: wave.Unit, wave.Count
```

- A CSV file's first line names its columns, and each line after it is a `Row`, in `All` in the file's order. When the
  first column holds names, not numbers, each row is also a member named by it, its words run together
  (`Big Bash` is `BigBash`). Values are separated by commas, decimals by points, and a value with a comma is quoted.
- A column is `int` when every value is a whole number, else `float` when every value is a number, else text. A value
  with a `;` makes the column a list: `75;150;225`. `Damage:float`, `:int` or `:text` in the header gives the type,
  as `Levels(...)` for a `float` field takes only `float`s. An empty value is 0, or the empty text or list.
- A JSON file is typed from its shape: an object is a class of its keys (`Data.Quests.Intro.Title`), the objects in a
  list share a class with every key they have (`StepsItem` for `Steps`), and a whole number is an `int`. A list at
  the top of the file is `All`.
- Names are made of the letters a to z, digits and `_`, starting with a letter: the build stops at a file, column,
  key or row name it can't make one from, at two that make the same, and at a CSV line with more values than columns.
  Only the files directly in `data` are read.

## Lua libraries

A Lua library from Hive or elsewhere goes in the project's `lua` folder as a `.lua` file. The build links every one
into the script before the map's code, in the order of their paths, and writes C# to call each global function and each
function of a global table, typed from the library's LuaLS comments:

```lua
---Pushes a unit away.
---@param target unit
---@param distance number
---@param angle? number
---@return boolean
function Knockback.push(target, distance, angle) ... end
```

```csharp
Lua.Knockback.push(hero, 300f);   // the class is the table's, or the file's (knock_back.lua is KnockBack) for a global function
```

| Comment | C# |
|---|---|
| `number`, `integer`, `string`, `boolean` | `float`, `int`, `string`, `bool` |
| `unit`, `item`, `destructable`, `player` | Moonwell's `Unit`, `Item`, `Destructable`, `Player`, turned into the game's handle and back |
| another of the game's types (`timer`, `effect`, `rect`) | that handle |
| `fun(u: unit, x: number): boolean` | `Func<Unit, float, bool>`: a lambda, given Moonwell's objects |
| `name?`, `type?` or `type\|nil` | optional, `null` for nil |
| anything else, or no comment | `object` |

- A method (`function T:f()`), a local function, a function marked `---@private` and a function's `...` are left out; the
  library can still use them.
- A global typed with `---@type` on the line above (`---@type integer` above `udg_Gold = 0`) is a property of the file's
  class, `Lua.OldMap.udg_Gold`, read and set; setting it inside `Local.For` is an error, as calling a function is.
- The analyzer cannot see into Lua, so a library function inside `Local.For` is an error (MW0001), unless its comments
  hold `---@localsafe`: add that line to the library when the function changes nothing in the game.
- A library runs only in the game, so the tests' fake world cannot call one.

## Old maps

`moonwell import <map> <name>` makes a project from an existing map, JASS or Lua, a `.w3x`, `.w3m` or a map saved as a
folder, so it plays as it did and its triggers move to C# one at a time:

- The map's files go into `map`, all but its script, and its settings stay World Editor's, as `Map.cs` sets none.
- Its object data becomes declared classes in `Objects/`, one file per tab: a new object with its `[ObjectId]`, a changed
  standard one `[Replaces]`, each named from its name in the editor, each field set as the standard classes have it, a
  value past World Editor's limits under `[BeyondLimits]`, a buff given a free carrier id. An object with a field the
  classes cannot say (a field Moonwell has no table of, an emptied reference, an empty choice) stays whole in the object
  files, which the build keeps beside the classes, as does one naming such an object; `import` lists each with why.
- The script becomes the Lua library `lua/old_map.lua`, a JASS script turned into Lua once. What Moonwell's own script
  makes is taken out: `main`, `config`, the placed units, items, regions, cameras and sounds, and the map's setup.
- `OldScript.cs` starts it when the game starts, as World Editor's `main` did: `InitGlobals`, `InitCustomTriggers`, then
  `RunInitializationTriggers`, so map initialization triggers run at the game's start, not while it loads.
- A JASS map's variables are typed properties (`Lua.OldMap.udg_Gold`), and its own functions outside triggers typed
  methods; a trigger's functions, and every function of a Lua map but the three above, are `---@private`.

To move a trigger, write it in C#, then delete its `Trig_` and `InitTrig_` functions and its line in
`InitCustomTriggers` from `old_map.lua`. A protected map, whose script is obfuscated or hidden, cannot be taken apart;
`import` says so. A map's own `Blizzard.j` or `common.j` does nothing in a Lua map.

## C# the game's Lua cannot run

Moonwell turns your C# into Lua with CSharp.lua, and the game runs the Lua. A few C# forms stop the compiler, write
Lua that does something else, or call a library function the Lua core does not have. The analyzer rejects most of them
as errors with their line, so the map does not build and then fail on the screen. `[SyncUnchecked]` does not silence
them, `decimal` included, because the Lua cannot run them anywhere; a `#pragma warning disable` does, and the map then
fails in the game (the build's list of what is turned off names it; see The build). The list comes from about 770
checks in 60 programs, run on .NET and in Lua with what they printed compared, and from later checks of the compiler
Moonwell builds with.

| Rule | Rejects | Write instead |
|---|---|---|
| MW0005 | `decimal` (`long`, `ulong`, `uint` and `double` are under Multiplayer) | `int` or `float` |
| MW0023 | `static abstract`; one class implementing `IG<int>` and `IG<string>` | a method; an ordinary class |
| MW0025 | a `ref` return, `ref int r = ref First(a)`, a ref reassignment `r = ref other` | a ref local of the element itself (`ref int r = ref a[i]` works), or a new ref local |
| MW0028 | a `char` or an enum given as an `object` to a text method other than `string`'s, `Console`'s and `StringBuilder`'s (a `TextWriter`'s `Write`), and a list of enums given to `string.Join` (shown as numbers) | `.ToString()` |
| MW0030 | a library member the Lua core lacks: threads (`Task`/`await`, `Thread`, `Monitor`, `Lock`), what is random on each machine (`Guid`, `Random.Shared`, LINQ `Shuffle`), reflection (`Exception.Source`, `Delegate.Target`, `GetUnderlyingType`), `FormattableString`, `string.Create` and `string.Normalize`, and the static methods of `int` and `float` but `Parse`, `TryParse` and `int`'s bit counts and rotations (`int.Clamp`, `float.Lerp`), and `DateTime`, `DateTimeOffset` and `TimeSpan`, whose 64-bit ticks the game's integers cannot hold | a `Timer`, `Natives.GetRandomInt`, `Math` (`Math.Clamp`), `RealTime` |
| MW0032 | `Enum.Parse(typeof(Color), text)` (gives the number) | `Enum.Parse<Color>(text)` |
| MW0035 | `data as unit`, `data is unit`, `case unit u` from an `object`, a `widget` or a `T`, a cast to a `T` limited to handles, `widgets.OfType<unit>()` and `widgets.Cast<unit>()`: a game handle fails every check for its type | a cast (`(unit)data` and `foreach (unit u in widgets)` work), a field or variable of the handle's type |
| MW0042 | `float.NaN` and `double.NaN` (1 in the game), `IsNaN` (never true), `PositiveInfinity`, `NegativeInfinity`, `IsInfinity` and `IsFinite` (`1 / 0` is 2^127 there) | a `bool` or a `float?` for no value, `float.MaxValue` for no limit, a test before dividing |
| MW0053 | a native as a method group, `xs.Select(Natives.Cos)` or `Func<float, float> f = Natives.Cos`: it is written `Moonwell.Natives.Cos`, which the game's Lua does not have, so the script stops there | a lambda that calls it, `x => Natives.Cos(x)` |
| MW0056 | `new Dictionary<Quest, int>()` or `new OrderedMap<Quest, int>()` where `Quest` is a class of the map's with static setup, a static field given a value that is no constant or a static constructor: the setup runs while the script loads, and the map can stop there with nothing on the screen (see The build) | the static field in another class or set on first use, or another key; a `HashSet` or an `OrderedSet` is fine |

MW0044 is about names, and the Lua is why: there a native is a global, and `Natives.Cos(x)` is written `Cos(x)`.

| It rejects | In the game | Write instead |
|---|---|---|
| `namespace Rect`: a first name that is a native or a function of Lua | the script stops while it loads | another name; `MyMap.Rect` is fine |
| a type in no namespace named like a native, or like `math`, `string` or another global | the same | a namespace |
| a type with the full name of one of Moonwell's, such as `Moonwell.Hero` | the build's Lua step or the script stops | another name or namespace |
| `Natives.Cos(x)` where the class, or a class around it, writes a method `Cos`, or a local, parameter or local function is called `Cos` | that runs in the native's place; `float Cos(float x) => Natives.Cos(x)` never returns, and the game freezes | another name; a local made from the call itself, `var Cos = Natives.Cos(a)`, is fine, as the Lua reads the call first |

A method of a base class, a field, a property and a type in a namespace pass.

The analyzer does not see these, so they build and then go wrong in the game:

| Form | In the game | Write instead |
|---|---|---|
| a struct of your own that can change, copied with its collection (`ToArray()`, `Clone()`, `[.. list]`) and then changed in place (`copy[0].X = 9`), changed by a method called on a `foreach` variable or straight on a List's element or on what a call gives back (`list[0].Move()`, `list.First().Move()`), or changed in a named method the collection calls (`list.ForEach(Move)`) | not copied: both hold the same struct, so a change to one is a change to the other | a `readonly struct`, made new to change it, or a class; a copy in a variable of its own (`var c = list[0];`) and a lambda (`list.ForEach(p => ...)`) are safe |
| a `null` after a number, `bool` or `char` in a `+` chain: `"a" + true + null` | the build stops at its line: "CSharp.lua cannot compile this code" | leave the `null` out |
| a later value in one `using` throwing while it is made (`using Res a = new Res(), b = Fail();`) | `a` is not disposed | a `using` of its own for each |
| an `abstract` property read by its bare name or through `this.` in the class that declares it (`abstract string Kind { get; }`, then `Kind` in that class) | nil: the class's own override is never asked | a `virtual` property with a body (`virtual string Kind => "";`) |
| an `out` variable declared after the first test of a `||` chain and read by a later test of it (`if (a == null || !map.TryGetValue(k, out var v) || v > 3)`) | the later test reads nil, so the condition is true | the tests before it in a condition of their own |

What works is most of the language: classes, structs, records, primary constructors, interfaces and default methods,
generics with constraints, enums as text, delegates, events, `switch` statements and expressions, patterns of every
kind, tuples, ranges and indexes, `??`, `?.`, collection expressions, `foreach` over every collection, `yield`, lambdas
and closures, `ref`/`out`/`in` parameters and ref locals, `params`, `checked`, `lock`, string interpolation, `List`,
`Dictionary`, `HashSet`, `Queue`, `Stack`, arrays and most of LINQ. `float` and `int` arithmetic is the game's, see
MW0005; its floats have no NaN or infinity to rely on (MW0042), and a raw `MathF.Sqrt` is off by up to 2 parts in
100,000 (Maths).

## Assets

A file in `assets/` goes into the map at the same path: `assets\war3mapImported\Fire.mdx` is
`war3mapImported\Fire.mdx` to object data and code. A file at one of the game's own paths replaces the game's.

- A PNG goes in as a BLP, the game's texture format, at the same path: code and object data name
  `war3mapImported\BTNFire.blp` for `assets\war3mapImported\BTNFire.png`. Each side must be a power of two (64 by 64
  for an icon), and a see-through part stays see-through. A PNG and a BLP of the same name are refused.
- An icon whose name starts with `BTN` or `PASBTN`, a PNG or a BLP, gets the grey copy a disabled button shows: the
  game reads it from `ReplaceableTextures\CommandButtonsDisabled\DIS` and the icon's file name, wherever the icon is,
  and without one shows the icon in colour (seen). The build makes it the way the game's HD copies are, unless
  `assets/` or the map has one there. Two icons with one file name in different folders share one copy, and the build
  warns of it.
- Pieces of a texture, colours, a round cut and a bar's steps are copies the build makes, listed in a `.looks.csv`
  beside the texture. The game tints and cuts a texture only in simple frames, a layer of their own that covers a label
  in `Screen.Add` and sits under every frame in `AddOver` (seen), so Moonwell's frames draw copies instead. A row is one
  copy, made at the texture's path with the row's name after it, and code names it through `Looks`, written before the
  compile as `Data` is ([Data files](#data-files)), so a wrong name is a compile error:

  ```
  assets\war3mapImported\UI-TargetingFrame.looks.csv      assets\war3mapImported\UI-StatusBar.looks.csv
  Name,Left,Top,Right,Bottom                              Name,Color,Bar
  PlayerFrame,0.855,0.0625,0.102,0.664                    Life,00ff00,yes
  ```

  ```csharp
  frame.Add(new Image { Texture = Looks.UITargetingFrame.PlayerFrame }, 0, 0, 193, 77);   // Screen.DesignHeight = 768
  frame.Add(new ProgressBar { Texture = Looks.UIStatusBar.Life, Value = 0.5f }, 106, 41, 119, 12);
  ```

  - `Left`, `Top`, `Right` and `Bottom` cut a piece, each a share of the texture from 0 to 1, as WoW's TexCoords; left
    greater than right mirrors it, top greater than bottom turns it over. The piece is stretched to the next power of two
    of its size in pixels, so an image sized as the piece shows it as drawn. With none of them, the copy is the whole
    texture.
  - `Color` multiplies the copy by a colour, as WoW's `BarColor` does a grey bar: `00ff00` is green.
  - `Round` set to `yes` cuts it round, a circle inside the square with a soft edge one pixel wide, for an icon in a ring.
  - `Bar` set to `yes` also makes 63 copies of its left part, in 64ths: a `ProgressBar` given that look shows the one
    nearest its share, so the texture is cut over the filled share, not squashed. Its path ends in `.Bar.blp`.
  - With no texture of its name in `assets/` or the game, each row is a flat colour, a `Name` and a `Color` only:
    `Flat.looks.csv` with the row `Backing,000000` gives `new Image { Texture = Looks.Flat.Backing, Alpha = 128 }`.
  - A `.looks.csv` at one of the game's icons copies the game's own, from its Classic files:
    `assets\ReplaceableTextures\CommandButtons\BTNFootman.looks.csv`. `Looks.Round(path)` gives the round copy of a
    listed icon, else the path as it is, for an icon read while the game runs, as a selected hero's.
  - A column the table does not have, a share outside 0 to 1, or a colour that is not six hex digits stops the build,
    naming the line. The table itself is not packed, and its texture is not listed as unused.
- A model has to be made for the graphics the map is played in. An HD (Reforged) model draws nothing in Classic
  graphics: the unit keeps its shadow, life bar and icon and has no body, and neither the build nor the game says so
  (seen). A Classic model shows in both. An HD model is told by its textures, a diffuse, a normal and an ORM map for
  each part; the MDX version does not tell them apart. A Classic model's own textures can be `.blp` or `.dds` (seen).
  A map set to Reforged only in World Editor's map options is drawn in Reforged graphics whatever the player has
  chosen (seen).
- A unit's voice comes from its sound set (`UnitSoundSet = "HeroTaurenChieftain"`), and a map cannot add a set: the
  game reads its sound tables from its own files only (seen). For a voice of the map's own, give the unit a set the
  map uses nowhere else and replace that set's files. `moonwell game find Units\Orc\HeroTaurenChieftain` lists them
  (`HeroTaurenChieftainWhat1.ogg` and so on), and a `.flac` of the same name in `assets/` at that path replaces the
  line (seen). Every unit with that set then speaks the new lines, and the set decides how many lines of each kind
  there are.
- The build warns of a path that object data or the map's code names (a texture, model or sound) that neither the game
  nor the map has, naming the class and field (`Sample.Arthas: uico`) or the type whose code has it. As in the game, a file
  under another extension of its kind counts: `.mdl` and `.mdx`; `.blp`, `.dds` and `.tga`; `.wav`, `.flac` and `.ogg`.
- It warns the same way of a texture that an `.mdx` model in the map names and neither the game nor the map has,
  naming the model: a model's textures must be in `assets/` at the exact paths the model has for them.
- The build also lists the textures, models and sounds in `assets/` that nothing in the built map names: not the
  code, object data, a model or a frame file. It leaves them in the map, as a path the code puts together while the
  game runs (`"BTN" + name + ".blp"`) is not seen. A file at one of the game's paths, a grey copy and a model's
  `_portrait` are not listed.
- The build names every file from `assets/`, and each BLP and grey copy it makes, in the built map's `war3map.imp`,
  World Editor's list of imported files, after the entries the map in `map/` has there. The game does not read the
  list. World Editor does: it shows a green icon for a file the list does not name, and its save drops the file (seen).
  So the built map can be opened in World Editor to look at, with its models and icons. A save there still writes the
  script anew from the triggers, without the map's code. For the map in `map/`, see `moonwell objects --to-map`
  ([The build](#the-build)).

## The build

`moonwell build` builds the map in the current folder into `build\MyMap.w3x`; `moonwell run` builds it and starts the
game with it. Both make a development build: it checks the values Moonwell receives and keeps the Lua readable.
`--release` makes the release build. `moonwell run --game <exe>` names the game when it is not in its usual place.
A second build of the same map started while one runs (a `watch` and a `run`, say) waits for the first to end and
says so.

| Command | What it does |
|---|---|
| `moonwell new <name>` | Makes a map project in a new folder of that name, the name being its namespace too |
| `moonwell watch` | Builds, then builds again each time a file of the project changes (the code, `assets/`, `map/`), until Ctrl+C; a failed build is shown and the watch goes on |
| `moonwell watch --run` | The same, starting the game with the map after each build, and closing the game it started before the next |
| `moonwell clean` | Deletes `build/` |
| `moonwell objects` | Lists the map's classes of object data, each with its id, kind and the standard object it is based on |
| `moonwell objects --to-map` | Builds, then writes the built object files, the files from `assets/` and the list that names them into `map/`, so World Editor lists the objects declared in code and shows their models and icons; close World Editor first |
| `moonwell game find <path>` | Says whether the game has a file, by the storage path it is read from (`moonwell game find Textures\HeroFarseer.blp`). A folder, or a path with `*`, lists the files there (`moonwell game find Units\Orc\HeroTaurenChieftain`) |
| `moonwell game extract <path>` | Writes the game's file into the current folder, under the name the game stores it by; `--out` names the file or the folder instead. A folder, or a path with `*`, writes every file there at its game path, so `--out assets` puts each where the map would replace it |

Each takes the project's folder after the command, as `build` does; `new` takes the name. The `game` commands need
no project. They take `--mode sd`, `hd` or `de` for the graphics the file is looked up in (Classic when not given),
and `--game <folder>` for an install that is not in its usual place. They end with 1 when the game has no such file.

The build also rejects `using static Moonwell.Natives` (MW0008; write `Natives.X`), which hides `Player` behind
`Natives.Player`, and a call to what only the code it writes calls (MW0045): `Runtime.Start`, a package's `Install` and
`ObjectTypes`' registrations, which the editor does not offer. `ObjectTypes.FourCC` and `IdOf` are a map's to use.

A `new Dictionary` or `new OrderedMap` keyed by a map's own class that has static setup is MW0056. Static setup is a
static field given a value that is no constant (`static readonly List<Quest> All = new()`), or a static constructor.
The game's Lua runs it while the script loads, when the dictionary's type is made, and the map can stop there with
nothing on the screen. Move the field to another class, set it on first use, or key by the handle. A constant
(`static int next = 1`) is no setup, a `HashSet` and an `OrderedSet` are fine, and Moonwell's classes, `Unit` and the
rest, are safe keys. The rule does not see a key class from a library, or a generic class of your own that makes a
dictionary keyed by its type parameter.

The build loads the script in Lua without running it, as the game does first, and stops with the C# file and line when
Lua refuses it. Lua allows 200 locals in a function: each parameter, local variable and object or collection
initializer is one, and so are some values the compiler keeps (`?.`, a switch expression), so a method that adds 200
`new Wave { ... }` to a list is too long. Split it into several methods.

The build checks the objects the map declares. A tooltip placeholder that names no object (`<A0O1,DataA1>`; ids are
case-sensitive) or a level its ability doesn't have (`<A001,DataA4>` of 3 levels) fails it, as the game shows 0
there; the key itself is not checked. Two warnings point at mistakes that can be meant:

- On a declared unit's command card, two buttons at one place or with one hotkey, or two abilities taking one order
  (two Storm Bolts: only one of them casts; a Channel's order is its Base Order ID). The card is the unit's abilities
  and what it trains, researches, upgrades to and sells; what a worker builds is checked the same way as its build
  menu. So three classes based on the Grunt in one `Trains` list are warned of once: they share the Grunt's place
  and hotkey until each sets `ButtonPositionX`, `ButtonPositionY` and `Hotkey`. The game shows them all: the first in
  the list keeps the place and the others move to free places, each still marked with the grid key of the place it
  was declared at. A card with more buttons than its 12 places is warned of too; the game's own Move, Stop and Attack
  are not counted. A build menu has 11, as Cancel holds (3, 2), and a building declared there is moved as well.
  Only things with an icon count, and for orders only castable abilities, with a hotkey. Buttons the game itself
  gave the standard unit are left alone among themselves (a Headhunter and its Berserker share a place). `[AllowClash]`
  on the unit keeps it quiet.
- A declared object nothing uses: no other object lists it, no code of another class names it, and the map places
  none. `[AllowUnused]` keeps it quiet, for an object kept for later or used where the build doesn't look (World
  Editor's own objects, item tables).

The build ends by listing each place that turns a Moonwell rule off, as `Hud.cs:12: [SyncUnchecked] on Hud.Refresh`,
and writes the same list to `build\silenced.txt`. Past five places it prints only the count and the file's name. A map
that turns nothing off gets no list and no file. What it lists:

- `[SyncUnchecked]` on a method, a lambda or a local function.
- `#pragma warning disable` that names an MW rule, or names no rule at all, which turns every rule off.
- `[SuppressMessage]` for an MW rule.
- `NoWarn` with an MW rule, and `RunAnalyzers` or `RunAnalyzersDuringBuild` set to false, in the project.
- A line of an `.editorconfig` or `.globalconfig` that sets an MW rule, one of Moonwell's categories or every analyzer
  rule to `none`, `silent` or `suggestion`.

The list is of this build: a `#pragma` inside `#if RELEASE` shows only in a release build. Of a library project of
your own it lists the `[SyncUnchecked]` members, by name with no line; the library's own `#pragma`s and project
settings are not read.

An error in a reaction shows on the screen with its C# file and line, and is written in full to
`Documents\Warcraft III\CustomMapData\Moonwell\log.txt`. A release build leaves the message off the screen and keeps
the file and line. An error that repeats shows and goes to the log at its 1st, 10th, 100th time and so on, with its
count ("failed 10 times"); past a hundred different errors, the new ones share one count. The log keeps its last 200
lines. A development build writes it at once on every line, so a crash to the desktop loses none of them; a
release build writes it at most once a second. Each write costs 6.5 to 23 ms, more as the log fills, so a map that
logs many lines a second sets `Game.LogAtOnce = false`, and a release build hunting a crash sets it `true`.

A loop that never ends freezes the game for every player, and nothing in the game stops it. A development build counts
the turns of your loops (`while`, `do`, `for`, `foreach`) and stops one with an error at its line, as any other error
in a reaction, once more than 10,000,000 turns have run in one instant: `more than 10000000 loop turns in one instant;
the loop was stopped`. The code after the loop does not run, and the next reaction does. An empty loop of that many
turns has frozen the game for about a second by then, so honest work that large belongs on a timer, a part at a time.
Only the map project's own code is counted, not Moonwell's packages, a library project or a Lua library. A release
build counts nothing, so there such a loop still freezes the game.

`Game.Log("text")` writes a map's own line to the same file, in order with the errors, inside `Local.For` too. A write
costs the game up to 23 ms, so the file is written at most once a second: the lines of the last second are lost if the
game closes first. A line longer than 253 characters is split.

To place objects declared in code in World Editor, close World Editor, run `moonwell objects --to-map` and open `map/`
again. The command builds the map and writes the built object files into `map/`; a build never writes into `map/`. It
also copies the files from `assets/` there, a PNG as its BLP, and names them in `map\war3map.imp`, so a placed unit
shows its model and icon in World Editor. Run it again when a new object should be placed or an asset has changed.
`assets/` stays the place to change a file: the build takes a file from `assets/` over its copy in `map/`. A file
deleted from `assets/` stays in `map/`, and in the built map, until it is deleted there too or in World Editor's Asset
Manager. World Editor reads a map's objects when it opens
the map, so one left open lists none of the new ones, and its next save puts its own objects back. The objects
Moonwell adds for itself, such as the dummy that casts for `CastOn`, are left out. The next build keeps the placed
units; code wins over any object it also declares. World Editor's own changes to an object get a warning at each
build: kept when no class declares the object, dropped, with the fields named, when one does.

## Debug mode

`moonwell run` builds a map you can look into while you play it. Type `-debug` in chat to turn the debug mode on or
off; a panel at the right edge shows the selected object, and lightning on the map shows its ranges, cells and area
outlines (one colour), its ticking pairs (another) and its waiting or paused pairs (a third).

| Input | Does |
|---|---|
| Ctrl + left click | selects the nearest object within 150; again near the same spot, the next nearest; on empty ground, nothing |
| Ctrl + H | halts Moonwell and holds every unit; again, resumes |
| Ctrl + Shift + H | halts Moonwell only; units go on walking; again, resumes |
| Ctrl + N | while halted, one step (`Interactions.Step`, else 1/32 s); held down, it repeats |
| Ctrl + `-` / Ctrl + `=` | slower / faster: ¼, ½, 1, 2, 4, 8 times |
| `-step 10`, `-speed 4`, `-stats` | in chat: that many steps while halted (1 to 320; `-step` alone, one), a speed (`-speed` alone lists them), the statistics shown or hidden |

Either Ctrl works. A halt and the speed change Moonwell's time only: its timers, interactions and areas. The game's
own attacks, buffs, cooldowns and timers made with the raw natives do not halt (Ctrl + H holds units, nothing more),
and the speed does not make units faster or slower. With units held, a step lets them go for one step, but they come
back to their orders slowly and move only about a quarter of a step. Units are always selectable; items, destructables and gizmos only
once the interactions grid tracks their kind. `-debug` off resumes at speed 1 and clears the selection.

A map writes nothing to have it. Two optional things: its own lines in the panel for a class or component that
implements `IDebugShown`, and `DebugMode` for code that speaks only about the selected object, or halts like a breakpoint.

```csharp
public sealed class Well : Gizmo, IDebugShown
{
    public int Healed;
    public void ShowDebug(DebugLines lines) { lines.Add("healed " + Healed); } // while a Well is selected
}

public override void OnTick(Pair pair) // in an interaction
{
    if (DebugMode.IsSelected(Holder)) { Game.Print("burns at " + pair.Distance); }
    if (pair.Distance < 32f) { DebugMode.Halt(); } // closer than it should get: stop here and look
}
```

- `DebugMode.Halt()` turns the mode on if it is off, then halts as Ctrl + H does; the code after the call still runs and
  what is due after it waits for a step or a resume. A breakpoint whose condition still holds halts again after a
  resume; `-debug` off escapes it, and `Halt()` does nothing until `-debug` turns the mode on again.
- `IsOn`, `Selected` and `IsSelected(actor)` read the mode; an object that has ended reads as not selected.
  `Select(actor)` selects as a click does while the mode is on, and does nothing while it is off.
- Development builds only. A release build leaves the mode out: `IsOn` and `IsSelected` read false, `Selected` is
  null, and the compiler takes out the calls to `Select` and `Halt`. An error in a `ShowDebug` shows as
  `Class.ShowDebug` and the panel goes on.
