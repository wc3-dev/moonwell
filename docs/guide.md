# Making a map with Moonwell

This guide describes Moonwell 0.7.1; the guide for 0.7.0 is at the
[`v0.7.0` tag](https://github.com/wc3-dev/moonwell/blob/v0.7.0/docs/guide.md).

## Start a map

Moonwell needs the .NET 10 SDK and Warcraft III 3.0, whose `common.j` and `Blizzard.j` the build reads from the
install. Name an install outside `C:\Program Files (x86)\Warcraft III` with the `MOONWELL_WAR3` environment variable.
Install the template and the `moonwell` command once:

```
dotnet new install Moonwell.Templates
dotnet tool install --global Moonwell.Cli
```

Then make a map and play it:

```
dotnet new moonwell -n MyMap
cd MyMap
moonwell run
```

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
| `assets/` | Files copied into the map at the same paths (models, icons, sounds), named with ASCII characters only; make it when you need it |
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
  `OnKill`). The names are `MaxLife`, `MaxMana`, `Mana` (what it starts with), `Scale`, `FlyHeight` and `Name` on a unit
  and hero, `Level` on a unit that is not a hero, `Strength`, `Agility` and `Intelligence` on a hero, `Charges` on an
  item, and `MaxLife` on a destructable. They were `HitPoints`, `ManaPoints`, `ManaInitialAmount`, `ScalingValue`,
  `Height`, `UnitLevel`, `StartingStrength`, `StartingAgility`, `StartingIntelligence`, `NumberOfCharges` and a
  destructable's `HitPoints`, which are gone. Outside the constructor the old names read the declared value; write
  `Default.MaxLife` (`Default.Name`) for that on a unit or item. A destructable has no `Default`, so its declared most
  life is read only in its constructor. An item's `Level`, `Scale` and `HitPoints` and a doodad's `Scale` keep their
  names, and six ability fields are
  named apart: `Inferno.InfernalDuration`, `Taunt.PreferHostiles` and `PreferFriendlies`, `WindWalk.BackstabDamage` and
  `Backstab`, `HealingModifier.HealingPercent`.
- A field with no live value (`Model`, `Tooltip`, `GoldCost`) keeps World Editor's name, is set only in the constructor,
  and reads the declared value anywhere. A set anywhere else fails the build, which names the class and method but no
  line: `MyMap.Heroes.Champion.OnKill sets GoldCost; declared fields are set only in the constructor`.
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
- The tooltips are World Editor's four: `Tooltip` (Tooltip - Normal), `TooltipExtended` (Tooltip - Normal - Extended),
  `TooltipLearn` (Tooltip - Learn) and `TooltipLearnExtended` (Tooltip - Learn - Extended). A tooltip shows an
  object's values with World Editor's placeholders: `<A000,Dur1>` an ability's at a level, `<h000,realHP>` a unit's,
  `<I000,uses>` an item's charges. The game rounds a value down to a whole number, so a decimal one takes `,.` (one
  decimal, `<A000,DataA1,.>`) or `,%` (times 100). The game shows a map's own buff's extended tooltip only up to its
  first comma, quoted or not, so a comma in it fails the build, and placeholders, which hold one, cannot go in it. A new
  buff that keeps a standard text with a comma (Flame Strike's) fails too: set `TooltipExtended`. Write `‚` (U+201A)
  for a comma there: the game draws it as one and does not cut the text at it.
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
value read or set (0 when never read, as in `OnLeave` after the game removed it), and setting does nothing. In the
quarter second before Moonwell notices a removal, a read that gives 0 on a removed unit keeps the last value too. A read is safe in
`Local.For`, a change is not.

| Unit | |
|---|---|
| `Life`, `MaxLife`, `Mana`, `MaxMana` | read and set; a `Life` of 0.405 or less kills, above the most it clamps; the most are whole numbers |
| `Owner` | read and set; the player the unit belongs to; setting gives the unit the new owner's colour |
| `X`, `Y` | read; `MoveTo(x, y)` through the game's pathing, `PlaceAt(x, y)` with none, not even the map's edge |
| `Facing` | read and set (the unit turns, so a read at once gives the old one); `FaceNow(degrees)` turns at once, a walking unit turns back |
| `Level` | read; a hero's hero level; a unit's level is set only in its declared class's constructor |
| `IsAlive`, `Kill()` | whether it lives; kills it, with `OnDeath` run |
| `IsIllusion` | an illusion (Mirror Image, a Wand of Illusion) has its hero's type but is a plain unit, not the hero's class: no starting components, no `OnCreated`; map-wide reactions still see it |
| `IsPaused`, `Scale`, `FlyHeight` | read and set; a paused unit takes no orders but still animates; `Scale` reads its type's scale until set; `FlyHeight` also lifts a ground unit |
| `Name` | read inside `Local.For` only (each player's language), set anywhere outside it |
| `Tint(red, green, blue, alpha)` | colours the model, each value clamped to 0-255 |
| `PlayAnimation(name)`, `QueueAnimation(name)` | as an effect's ([below](#special-effects)); it plays once, then an idle unit stands; a walking unit keeps walking and only stutters; a name the model lacks plays its stand, and a name picks no variant (`"attack 2"` is any attack) |
| `PlayAnimation(index)` | by the animation's index in the model, from 0: the one way to pick a variant; indices differ between models |
| `AnimationSpeed` | read and set, 1 unless set; 0.5 plays at half speed, 0 holds the unit still mid-animation |
| `AddAnimationTag(tag)`, `RemoveAnimationTag(tag)` | `AnimationTag.Defend`, `Alternate`, `Gold`, `Lumber`, `Work`, or any tag the game takes (`"upgrade first"`): the unit's animations play with it until it is removed |
| `Order(order)`, `OrderAt(order, x, y)`, `OrderOn(order, target)`, `Stop()` | orders by the game's order string, `Orders.Move` and the other constants, or a spell's (`"holybolt"`) |
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

A destructable brought back by the native `DestructableRestoreLife` runs no `OnRestored`, as the game tells nothing.

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
| `PlayAnimation(name)`, `QueueAnimation(name)` | by name: `Animation.Stand`, `Animation.Attack`, or any name the model has (`"attack slam"`) |
| `PlaysDeath` | true unless set; false makes it vanish at once instead of playing its death |
| `Model`, `Unit`, `AttachPoint`, `IsShownTo(player)` | read |
| `Handle` | the game's effect, for anything Moonwell does not do: a player's team colour on the model, stretching by axis, the sub-animations |

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
```

| FloatingText | |
|---|---|
| `Text`, `Size`, `Color` | read and set; `Size` is World Editor's font size, 10 unless set; `Color`'s alpha is the opacity a fade lowers from |
| `Height`, `PlaceAt(x, y)` | read and set; above the ground at a place, above the health bar on a unit (0 is just over it); `PlaceAt` does nothing on a unit |
| `Speed`, `Angle` | read and set; how fast it moves by itself, a second, and which way in degrees (90, the default, is up the screen) |
| `Duration`, `FadeOut` | read and set; the seconds from when it was made to when it ends by itself (0, the default, lasts until ended), and the seconds before that end over which it fades |
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
- A file of the map's own goes in `assets/`. A path to one of the game's sounds works with `.wav`, `.flac` or `.ogg`
  alike.

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

## Areas

Add the `Moonwell.Areas` package (the template's `.csproj` has the line, commented) for parts of the map that units enter,
stay in and leave. A map that does not reference it carries none of its code.

```csharp
public sealed class ShopFloor : Area // a game component: Components.Add(new ShopFloor()) in the map, or Game.Instance.Add
{
    public ShopFloor() { Shape = AreaShape.Region("Shop"); Tick = 1f; } // the region's name in World Editor
    public override bool Accepts(Unit unit) => unit is Hero;
    public override void OnUnitEntered(Unit enteringUnit) { enteringUnit.Owner.Print("Welcome."); }
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
- A timer an area's reaction makes with `After` is the area's and ends with it, so one made in `OnUnitLeft` while the
  area ends is ended at once; a buff or component added to a unit is the unit's. Make it on `Game.Instance` to outlive
  the area.
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
  `Alignment` says (a `FramePoint`: `Left` for the middle of the left edge; `TopLeft` unless set); `Scale` makes it
  bigger, as the game takes no other font or height for it (seen). `Image` shows a `Texture`. A map's own panel
  inherits `Panel` and keeps its frames in fields.
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
  gives a size from points too late to read); a label's `Color` colours its text, white unless set, as a colour code
  around it: a label lets clicks through by being turned off, and the game draws a turned-off label's text colour grey
  (seen). Codes in the text draw over it, and its alpha is not drawn, so fade a label with `Alpha`.
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
  player sees; `Text` keeps the shared text, and setting it shows that everywhere again.
- `End()` hides a frame with the frames added to it. Nothing is destroyed, as destroying a frame can desync (a claim):
  the next frame of its class and template is given the hidden one, with none of its old values. One sized on a
  template goes only to a frame given a size. One that a frame still showing is tied to is not given out again, so the
  tied frame stays where it was.
- A template the game does not have, as one from an `.fdf` whose TOC is not loaded, is reported in every build, and the
  frame is made by its type instead.
- `player.GameUi()` is the player's own view of the game's HUD: what is set on it changes only that player's screen,
  and lasts until set again, as a camera's does. `Visible = false` hides the whole HUD: the console's art goes, clicks
  reach the world down to the window's bottom, the map's frames still draw, and the command buttons and life bars stay,
  as the game leaves them. Its pieces, `Minimap`, `MinimapButtons`, `Portrait`, `HeroBar`, `HeroButtons`, `TopBar`,
  `CommandButtons` and `ItemButtons`, each have `Visible`; with the whole HUD hidden, a piece set `Visible` shows again,
  but the item buttons do not come back (seen). The minimap, the command buttons and the item buttons also move,
  `Attach(point, frame, framePoint)` to a map's frame, and size by `Scale`. A map's frame ties itself to a piece with
  `Attach` too: `label.Attach(FramePoint.Bottom, player.GameUi().Minimap, FramePoint.Top)`, with the label added by
  `Screen.AddOver`, as the console's art covers a frame beneath the HUD there. The info panel, the chat and the game's
  messages are not offered, nor moving the portrait or the hero icons, as the game crashes or draws them wrong (seen).
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
- Events run as frames' do: synced, with the player, and not inside `Local.For`.

## Colours and text

`Color` is a colour as the game takes it, each part 0 to 255: `new Color(255, 204, 0)`, `Color.FromHtml("#ffcc00")`,
the named ones (`Color.Red`, and `Color.TooltipGold` for the game's own gold) and `Color.Of(PlayerColor.Blue)`. `Lerp`,
`Darkened`, `Lightened`, `Inverted` and `Color.FromHsv` make one from another, as in Godot. `unit.Tint(color)` takes
one too.

`"Gold".Colored(Color.TooltipGold)` colours text, and `player.ColoredName` is a player's name in their colour. Letter by
letter: `Rainbow()` (a shift turns it round, for text that is redrawn), `Gradient(a, b)` through two colours or more,
`Alternate(a, b)` and `Confetti()`; `Highlight("Stun", Color.Red)` colours a word wherever it appears.
`"Victory".Sweep(Color.Gold, Color.White, Game.Time)` moves a band of white through gold, 20 letters a second, in text
that is redrawn, such as floating text set every 0.05 s; `SweepShape.Wide`, `Sharp`, `Comet` and `Repeating` (a new
band every second) change its shape. A screen line shows 1,023 characters, and a colour code takes 10: `Rainbow` and
`Gradient` share codes on a long text to keep within them, while `Alternate` and `Confetti` fit about 90 letters. The
game shows no alpha in text, and `|r` goes back to white, not to an outer colour. `Text.Clock(125f)` writes "2:05", and
`Text.TimeOfDay(6.5f)` "06:30".

## Players

`Player.One` to `Player.TwentyFour` are the player slots, as the lobby numbers them; `Player.Get(slot)` takes the
slot from 0, so `Player.Get(0)` is `Player.One`. A slot is not a colour: a map can recolour its players. `Player.Red`
to `Player.Peanut` are whoever in the game has that colour, and `Player.ByColor(color)` the same for a colour in a
variable: the first in slot order, or null when nobody in the game has it, so check before using one.
`Player.NeutralAggressive`, `NeutralPassive`, `NeutralVictim` and `NeutralExtra` are the neutral players, and
`StartX` and `StartY` a player's start location.

`player.Print("Kills: 3")` shows a line to one player and `Game.Print("Wave 2")` to everyone; both take a time on
screen, `seconds: 10f`. `Game.Log("wave 2 started")` writes a line to the log file instead (under The build). `Package.Version` is the version of Moonwell the map is built with. `Gold`, `Lumber`, `FoodUsed`, `FoodCap`, `FoodCapCeiling`, `GoldGathered`, `LumberGathered`,
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
- A map listens for selections only when its code writes a selection reaction or reads `Selection`.

`LevelOf<T>()` and `SetLevel<T>(2)` take an upgrade class;
`SetMaxAllowed<T>(1)` a unit or upgrade class, where 0 forbids it and `Player.NoLimit` lifts a unit's limit; and
`SetAvailable<T>(false)` an ability class.

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
- A machine-local read outside it (MW0002): the local player, the camera, the mouse's screen position, frame text,
  localised names, `RealTime.LocalNow`, `PlayerFile.Read`, and .NET's own clock and addresses (`DateTime.Now`, `Environment.TickCount`, a `Stopwatch`'s
  reading, `GetHashCode` of a handle or of a class without its own, a handle turned into text).
- A change inside it to something every machine shares (MW0048), in the block or in what it calls: a field or
  auto-property of your map, a local or parameter from outside the block, an event of your map, or a collection held in
  one of those (`Scores.Add(x)`, `Scores[i] = x`, on .NET's collections, arrays, `StringBuilder`, `Random`, `OrderedMap`,
  `OrderedSet`, `JsonObject` and `JsonArray`). What the block makes is free: a constructor writing its own object,
  `new Panel { Count = 3 }`, a local that only holds a `new` of the block, and a struct in a local. A method called on a new object is still checked.
  A list assigned to a local later, given as a parameter, or returned by a method or a property with a body, is not seen.
- `new Random()` without a seed, anywhere (MW0038): it reseeds the game's own random generator with each machine's
  clock. Use `Natives.GetRandomInt`, or `new Random(seed)` with a seed every machine shares.
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

`[SyncUnchecked]` on a method turns the checks off in its own body, except `decimal`; `Local.For` still follows a method
so marked, so a block whose changes are meant is marked itself: `Local.For(p, [SyncUnchecked] () => ...)`. That is the
way for MW0048's false alarms: a lazy `instance ??= new Hud()`, or a cache filled inside a block.
`Natives.BlzSendSyncData` inside `Local.For` sends a machine-local value to every machine.

A `#pragma warning disable`, `NoWarn` or `.editorconfig` on one of these rules removes its protection, and nothing says
so. To turn the checks off where you mean it, use `[SyncUnchecked]`.

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

## C# the game's Lua cannot run

Moonwell turns your C# into Lua with CSharp.lua, and the game runs the Lua. A few C# forms stop the compiler, write
Lua that does something else, or call a library function the Lua core does not have. The analyzer rejects most of them
as errors with their line, so the map does not build and then fail on the screen. `[SyncUnchecked]` does not silence
them, `decimal` included, because the Lua cannot run them anywhere; a `#pragma warning disable` does, and the map then
fails in the game. The list comes from about 770 checks in 60 programs, run on .NET and in Lua with what they printed
compared, and from later checks of the compiler Moonwell builds with.

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
| a struct of your own that can change, put into a collection by an initializer, a collection expression or an indexer (`new List<P> { a }`, `[a]`, `new P[] { a }`, `list[0] = a`, `dict[k] = a`), or changed through a lambda's parameter (`p => p.X = 9`, `list.ForEach(...)`) | not copied: both hold the same struct, so a change to one is a change to the other | a `readonly struct`, made new to change it, or a class; `Add` and `Insert` copy |
| a `where T : struct` or `unmanaged` method that copies its value (`var y = x;`, `foreach (var x in e) return x;`), given an `int`, a `float` or a `bool` | fails: "attempt to index a number value" | the method without the constraint |
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

## The build

`moonwell build` builds the map in the current folder into `build\MyMap.w3x`; `moonwell run` builds it and starts the
game with it. Both make a development build: it checks the values Moonwell receives and keeps the Lua readable.
`--release` makes the release build. `moonwell run --game <exe>` names the game when it is not in its usual place.

The build also rejects `using static Moonwell.Natives` (MW0008; write `Natives.X`), which hides `Player` behind
`Natives.Player`, and a call to what only the code it writes calls (MW0045): `Runtime.Start`, a package's `Install` and
`ObjectTypes`' registrations, which the editor does not offer. `ObjectTypes.FourCC` and `IdOf` are a map's to use. The
build does not catch a
`Dictionary` keyed by a map's own class that has a static field with a value: the map can stop while it loads, with
nothing on the screen. Key by the handle, or set the field on first use. Moonwell's classes, `Unit` and the rest, are
safe keys.

The build loads the script in Lua without running it, as the game does first, and stops with the C# file and line when
Lua refuses it. Lua allows 200 locals in a function: each parameter, local variable and object or collection
initializer is one, and so are some values the compiler keeps (`?.`, a switch expression), so a method that adds 200
`new Wave { ... }` to a list is too long. Split it into several methods.

An error in a reaction shows on the screen with its C# file and line, and is written in full to
`Documents\Warcraft III\CustomMapData\Moonwell\log.txt`. A release build leaves the message off the screen and keeps
the file and line. An error that repeats shows and goes to the log at its 1st, 10th, 100th time and so on, with its
count ("failed 10 times"); past a hundred different errors, the new ones share one count. The log keeps its last 200
lines.

`Game.Log("text")` writes a map's own line to the same file, in order with the errors, inside `Local.For` too. A write
costs the game up to 23 ms, so the file is written at most once a second: the lines of the last second are lost if the
game closes first. A line longer than 253 characters is split.

To place objects declared in code in World Editor, open `build\MyMap.w3x` there and Save Map As a folder beside `map/`:
World Editor will not save over `map/` and names the new folder `map2.w3m`. Delete `map/`, rename `map2.w3m` to `map`,
and open `map/` in World Editor again, so that later saves go there. The next build keeps the placed units; code wins
over any object it also declares. World Editor's own changes to an object get a warning at each build: kept when no
class declares the object, dropped, with the fields named, when one does.

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
