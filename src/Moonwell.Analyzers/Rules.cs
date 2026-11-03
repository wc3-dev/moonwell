using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Moonwell.Analyzers;

/// <summary>
/// The multiplayer mistakes the analyzer rejects (decision 13); <c>[SyncUnchecked]</c> on a method turns them off in its
/// own body, except <c>decimal</c>, and Local.For still follows the method. Then the C# CSharp.lua cannot compile, which
/// nothing turns off.
/// </summary>
public static class Rules
{
    private const string Category = "Multiplayer";

    private const string Silence = " [SyncUnchecked] on the method (or the lambda or local function) turns this check off in its own body; Local.For still follows a method so marked.";

    public static readonly DiagnosticDescriptor StateInLocal = new(
        "MW0001",
        "Changes the game inside Local.For",
        "{0}; inside Local.For it runs on one machine only, which desyncs the game",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Code inside Local.For runs on one player's machine. It may change only what that player sees or hears; creating handles or changing the game there desyncs the other machines." + Silence);

    public static readonly DiagnosticDescriptor LocalOutside = new(
        "MW0002",
        "Machine-local value outside Local.For",
        "'{0}' gives a different answer on each machine; use it only inside Local.For or a [MachineLocal] method{1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A machine-local value (the local player, the camera, the mouse, frame text, localised names) differs between machines, so game logic that uses it desyncs. So do .NET's clock (DateTime.Now, UtcNow and Today, Environment.TickCount, a Stopwatch's reading) and an object's address in the game's Lua: GetHashCode of a handle or of a class without its own, RuntimeHelpers.GetHashCode, and a handle turned into text." + Silence);

    public static readonly DiagnosticDescriptor HandleId = new(
        "MW0003",
        "GetHandleId",
        "Handle ids differ between machines; key by the handle itself, or by a number the map assigns",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each machine frees handle ids on its own, so the same object can have different ids." + Silence);

    public static readonly DiagnosticDescriptor HashLoop = new(
        "MW0004",
        "Loop over a Dictionary or HashSet",
        "{0} loops in no fixed order, which can differ between machines; use OrderedMap or OrderedSet",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "In the game's Lua a Dictionary or HashSet gives its items in no fixed order. Lookups are fine; loops are not: foreach, LINQ, CopyTo, a copy into a list, and a Dictionary or HashSet held as an IEnumerable, ICollection or IReadOnlyCollection. A foreach or LINQ through IDictionary, IReadOnlyDictionary, ISet or IReadOnlySet (or their Keys and Values) is one too, since in a map only a Dictionary or a HashSet implements them." + Silence);

    public static readonly DiagnosticDescriptor NumberType = new(
        "MW0005",
        "Number type the game cannot carry",
        "'{0}' {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The game's Lua has 32-bit integers and single-precision floats. [SyncUnchecked] on the method turns this check off in its own body, except for decimal, which is wrong in the game's Lua wherever it runs.");

    public static readonly DiagnosticDescriptor Desyncs = new(
        "MW0006",
        "Desyncs wherever it runs",
        "'{0}' changes the game inside its own GetLocalPlayer() block, so it desyncs wherever it runs",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A blizzard.j function that compares GetLocalPlayer() and then changes the game, such as SelectGroupForPlayerBJ, desyncs on every call. Do its local part inside Local.For with what the analyzer allows there." + Silence);

    public static readonly DiagnosticDescriptor UnseededRandom = new(
        "MW0038",
        "Random without a seed",
        "'{0}' seeds itself from this machine's clock and reseeds the game's random generator with it, which desyncs every random number after it, inside Local.For too; use Natives.GetRandomInt, or new Random(seed) with a seed every machine shares",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CSharp.lua's Random without a seed calls math.randomseed(os.time()) and then math.random. In the game math.random is the game's own synced generator (seen 2026-10-26), so each machine reseeds it with its own clock, and every GetRandomInt after it differs between machines, wherever the Random was made. new Random(seed) is computed in Lua alone, so it gives the same numbers on every machine when the seed is the same (one from Natives.GetRandomInt is)." + Silence);

    public static readonly DiagnosticDescriptor Unfollowed = new(
        "MW0039",
        "Code inside Local.For the analyzer cannot follow",
        "{0}; inside Local.For, write the lambda in place or call a method of the map",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The analyzer follows what Local.For runs: a lambda written in place, a method of the map, a delegate parameter whose callers' arguments it checks, and the delegates given to a variable, field, property or event of the map. A delegate from anywhere else (a list's element, a library's result, a lambda's parameter) runs code it cannot see, which may change the game on one machine only." + Silence);

    public static readonly DiagnosticDescriptor OneMachineCode = new(
        "MW0040",
        "Code of one machine where it runs on every machine",
        "'{0}' {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Code the analyzer checks as code of one machine must not also run on every machine, where its machine-local reads desync the game: a delegate parameter given to Local.For (whose callers' lambdas are checked as blocks) called outside it, and [MachineLocal] on an override or an interface implementation (Moonwell calls OnGameStart on every machine) or on a lambda given to a method or event that runs it (a scope's Every). [MachineLocal] on an override of a map method that has it too is right." + Silence);

    public static readonly DiagnosticDescriptor SharedWrite = new(
        "MW0048",
        "Shared value changed inside Local.For",
        "{0}; inside Local.For that happens on one machine only, so code every machine runs would see a different value, which desyncs the game; {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Code inside Local.For or a [MachineLocal] method, and the map's methods it calls, runs on one player's machine, so what it changes that lasts after it differs from the other machines. The rule rejects a write (=, +=, ??=, ++, ref, out, a tuple's assignment) to a field or auto-property of the map or to a local or parameter from outside the block, adding to an event of the map, and a call that changes one's value: an element set, and Add, Remove, Clear and the like on .NET's collections, an array, a StringBuilder, a Random, an OrderedMap or an OrderedSet. [MachineLocal] on a field or auto-property makes it one machine's: code of one machine writes it, and any read elsewhere is MW0002, though a plain = is allowed there. A local or an event cannot be marked. What is made in the block is free: a constructor writing its own object, a static constructor its own class, an object initializer, a local holding only a new from the block, and a struct in a local of the block. A method called on a new object is still checked, since a method is checked once and not for each object. A collection assigned to a local later, given as a parameter, or returned by a method or a property with a body is not seen." + Silence);

    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(StateInLocal, LocalOutside, HandleId, HashLoop, NumberType, Desyncs, UnseededRandom, Unfollowed, OneMachineCode, SharedWrite);

    private const string LuaCategory = "Lua";

    public static readonly DiagnosticDescriptor StaticNatives = new(
        "MW0008",
        "using static Moonwell.Natives",
        "using static Moonwell.Natives hides Moonwell.Player behind Natives.Player; write Natives.X",
        LuaCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Player.Get(0) fails with CS0119 because Natives.Player(int), imported by using static, wins over the type Moonwell.Player.");

    public static readonly DiagnosticDescriptor ChoiceNames = new(
        "MW0010",
        "Name of a Moonwell.Objects enum",
        "'{0}' needs the names of {1}, which the game's Lua does not have: there a choice is only its number (ArmorType.Metal shows as 1) and Enum.GetValues stops; compare with its members, or show (int) of it",
        LuaCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The enums of Moonwell.Objects (ArmorType, Targets, ...) are ignored by CSharp.lua: the game's Lua has their numbers only. ToString (also x?.ToString()), a string joined (+ or +=) or interpolated with one, a list or array of them given to string.Join or string.Concat, one given to an object parameter (string.Format, string.Concat, StringBuilder.Append; Equals and a declared object's Set are left alone), and Enum methods given one (GetValues, GetNames, GetName, IsDefined, Parse, TryParse, Format, ToObject) give the number where .NET gives the name, or stop. Comparing, |, & and HasFlag work.");

    public static readonly DiagnosticDescriptor HandleCheck = new(
        "MW0035",
        "Type check of a handle",
        "'{0}' checks that a value is a {1}, which the game's Lua cannot: there is, as or a pattern gives false or null, even for a {1}; cast it, or keep the handle in a field or variable of type {1}",
        LuaCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CSharp.lua checks is, as or a type pattern against the value's CSharp.lua class. A game handle is the game's own value (userdata), which has no such class, so a check for a handle type (unit, lightning, or a type parameter limited to one) never passes in the game: is, as and patterns give false or null. A cast to a handle type checks nothing since the fork's 2.0.4-moonwell.15, also the one in foreach (unit u in widgets). The same checks hide in OfType<unit>(), which tests each item with is, and in Cast<unit>() unless the items already are units. The Lua cases do not show it: there the fake world's handles are CSharp.lua objects.");

    /// <summary>The rules of C# that CSharp.lua cannot compile.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Lua { get; } = ImmutableArray.Create(StaticNatives, ChoiceNames, HandleCheck);

    private const string ObjectsCategory = "Objects";

    public static readonly DiagnosticDescriptor FieldRule = new(
        "MW0009",
        "Value World Editor does not allow",
        "{0}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant a class puts on a field of a standard object (an assignment, an argument of Levels, a Set by field id) is outside what World Editor allows: a number outside its range, a negative where none is allowed, an empty required name, a text or list that is too long, a choice that is no value of its enum, or a number list with a part that is no number. The message is the one the build gives. [BeyondLimits(nameof(Property))] on the class goes past a range or a length; it never allows a negative, an empty name, a wrong choice or a part that is no number. It also reports a [BeyondLimits] name that is no property, which lifts nothing.");

    public static readonly DiagnosticDescriptor SetById = new(
        "MW0049",
        "Set with a value its field does not hold",
        "{0}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Set(\"id\", value) takes any object, so the compiler lets through a value of another type than the field holds, and an id the class does not have; the build then stops on it. The message is the one the build gives. A value holds exactly the field's type: a whole number is no float, and a choice is its enum, never its number. A computed id, and a value typed object, are left to the build.");

    /// <summary>The rules for the values a map's classes put on object fields.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Objects { get; } = ImmutableArray.Create(FieldRule, SetById);

    public static readonly DiagnosticDescriptor MovedChoice = new(
        "MW0011",
        "Choice enum moved to namespace Moonwell",
        "{0} is Moonwell.{1} since Moonwell 0.2.1, which the damage package uses too: write Moonwell.{1}, or {1} with using Moonwell",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "AttackType and DefenseType left Moonwell.Objects for core Moonwell in 0.2.1, so that object data and the damage package share one of each. The compiler's own error does not name the new enum: CS0234 for Moonwell.Objects.AttackType, CS0103 for AttackType without using Moonwell, and CS0176 for AttackType.Magic in a standard class without using Moonwell, where AttackType is the property.");

    /// <summary>The rules for names that moved between Moonwell's namespaces.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Moved { get; } = ImmutableArray.Create(MovedChoice);

    public static readonly DiagnosticDescriptor BareDamage = new(
        "MW0050",
        "Damage without this in a unit class",
        "'Damage' here is the unit's field, attack 1's base damage: write this.Damage(...) to deal damage",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Damage(target, 10f) in a unit class does not compile: the declared field Damage is the name that binds, and C# finds an extension method only after a dot, so the compiler's error (CS1955) says only that the field is no method. this.Damage(target, 10f) deals the damage.");

    /// <summary>The rule for a bare Damage(...) in a unit class.</summary>
    public static ImmutableArray<DiagnosticDescriptor> BareDamages { get; } = ImmutableArray.Create(BareDamage);

    public static readonly DiagnosticDescriptor PlayerScope = new(
        "MW0043",
        "A player where a scope goes",
        "'{0}' is a player, which lasts the whole game, so it is no scope to make things in: write Game.Instance for what should last the game, or pass the player as the owner where the call takes one",
        "Scopes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "What code makes belongs to the scope named where it is made (decision 81). A player is a scope, as every actor is, but it never ends during a game, so Game.Instance does the same, and a player there is most often meant as the owner: Gizmo.Create(gizmo, x, y, scope, owner) and Missile.Create(missile, x, y, z, scope, owner) take the owning player after the scope, and the compiler cannot tell the two apart. The rule checks the scope argument of Moonwell's methods (Create, Fire, Interact) and what a scope's After and Every are called on; a player held in a variable typed Scope is not seen.");

    /// <summary>The rules for the scopes code makes things in.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Scopes { get; } = ImmutableArray.Create(PlayerScope);

    public static readonly DiagnosticDescriptor LuaName = new(
        "MW0044",
        "A name the game's Lua takes for another",
        "'{0}' {1}",
        LuaCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CSharp.lua keeps a map's names in Lua's own scopes, where the game's natives are globals of the same names. A namespace's first name is a global, so one named like a native or a function of Lua stops the script while it loads, and so does a type in no namespace named like any global of the game's Lua. The script holds one type of a full name, so a type with the full name of one of Moonwell's stops the Lua step or the script. And a class's methods, those of the classes around it, and a local, a parameter or a local function are Lua locals, so Natives.Cos(x) where one of them is called Cos runs that instead of the native: a method calling itself this way never returns. A method of a base class, a field, a property and a type in a namespace are not locals in the Lua, and pass.");

    /// <summary>The rule for a map's names in the game's Lua.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Names { get; } = ImmutableArray.Create(LuaName);

    public static readonly DiagnosticDescriptor UnseenReaction = new(
        "MW0036",
        "Reaction a package never calls",
        "'{0}' {1}",
        "Reactions",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The damage, input and physics packages' interfaces give every reaction an empty body, so the compiler accepts a class that only seems to write one. A package calls a reaction that is a public, non-static method of the interface's name and parameters: on a unit's class or its components (buffs too) for IDamageSource, IDamageTarget, IPushTarget and IMissileTarget, and on the game's and players' components for IDamageWatch, IMouseWatch and IKeyWatch. The rule rejects a method named like a reaction that is not one (not public, static, other parameters, an explicit implementation, or a reaction of an interface the class does not implement), a name renamed in Moonwell 0.3 (or IPushTarget's OnLanded, which is OnGrounded), a public method shaped like a reaction (a Hit, a Burst, an Impact or a Slam, or the parameter a physics reaction takes; a Mouse and a button, a Keyboard and a key) whose name is a letter off a reaction (one added, removed, changed, or two swapped) in a class with reactions, a reaction in a class that lacks its interface (an abstract class is left alone: its subclasses may list the interface), and an interface on a class the package never calls. For IMouseWatch and IKeyWatch an explicit implementation and a method with other parameters run in the game's Lua, which calls them by name, and not on .NET, so the two disagree. #pragma warning disable MW0036 keeps a helper's name.");

    public static readonly DiagnosticDescriptor HiddenReaction = new(
        "MW0041",
        "Reaction that never runs",
        "'{0}' {1}",
        "Reactions",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A reaction of a Moonwell class (a unit's OnDamaged, a component's OnKill, a game component's OnGameStart, a buff's OnTick, an area's OnUnitEntered) is a virtual method the map overrides. One written without override, or with new, hides it: the compiler only warns (CS0114), and Moonwell never calls it. A method named On... like a reaction but with other parameters never runs as the reaction either, and on .NET Moonwell's lookup of reactions by name fails on it when the class is added, while the game's Lua takes it.");

    public static readonly DiagnosticDescriptor CalledReaction = new(
        "MW0047",
        "Reaction called by hand",
        "'{0}' is a reaction, which Moonwell calls when its event happens, so calling it runs the code with no event behind it; move what both need into a method of your own",
        "Reactions",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A reaction (an On... method of a Moonwell class or of a package's interface, public or protected) belongs to Moonwell: a map overrides it, and Moonwell calls it. A call to one, or a method group of one (Every(1f, OnTick)), runs it with no event behind it, and ties the map to how Moonwell calls it. base.OnDeath(killerUnit) inside an override of OnDeath is right, and the only call allowed.");

    /// <summary>The rules for reactions: the packages' interfaces (damage, mouse, physics) and Moonwell's virtual methods.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Reactions { get; } = ImmutableArray.Create(UnseenReaction, HiddenReaction, CalledReaction);

    public static readonly DiagnosticDescriptor BuildOnlyMember = new(
        "MW0045",
        "A member only the build's code calls",
        "'{0}' is for the code Moonwell's build writes, and may change in any version, so a map does not call it",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The build writes code into a map that registers its classes and starts the game, and that code compiles as part of the map, so what it calls has to be public. Moonwell hides those members from the editor (ObjectTypes.Unit, Item, Destructable, Ability, Id and BuffDisplay, Runtime.Start, each package's Install, a declared class's Declare and DeclaredValue). The rule rejects a call to one, or a method group of one, anywhere but the namespace Moonwell.Generated, which is the build's. ObjectTypes.FourCC and IdOf are a map's to call.");

    public static readonly DiagnosticDescriptor NewOfGameObject = new(
        "MW0046",
        "new of a class whose objects the game has",
        "'{0}' makes an object with nothing in the game behind it: {1}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A class of a unit, an item, a destructable or an ability describes objects the game has, and a doodad's or an upgrade's only data for the build. Their constructors are public because the code the build writes needs them, so C# accepts new Footman(), which gives an object no game object stands behind: some of its members read the class's declared values and the rest reach the game with nothing to act on. Unit.Create<T> and Item.Create<T> make one in the game, and Unit.Default<T>() and Item.Default<T>() give a class's declared values. The namespace Moonwell.Generated, which is the build's, is left alone.");

    /// <summary>The rules for what only the build's code does.</summary>
    public static ImmutableArray<DiagnosticDescriptor> BuildOnly { get; } = ImmutableArray.Create(BuildOnlyMember, NewOfGameObject);

    public static readonly DiagnosticDescriptor LiveInDeclaration = new(
        "MW0012",
        "The game in a declared class's constructor",
        "'{0}' {1}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The build runs the constructor of a class that inherits a standard unit, hero, item or destructable to read its fields, before there is a game. A live value or action of Unit, Hero, Item or Destructable there (Life, X, Kill, Add) fails the build with only an exception's message. The live values a declared field shares (MaxLife, MaxMana, Mana, Level, Scale, FlyHeight, Name; a hero's Strength, Agility and Intelligence; an item's Charges; a destructable's MaxLife) are the class's declared values there, except a hero's Level (and Set(\"ulev\", ...)): a hero starts at level 1, so its level is not declared. A unit or destructable starts with full life, so Life has no declared value: set MaxLife. A component goes to Components.Add(...). The class's own default object (Default, or Unit.Default<T>() of the class) is what the constructor is making: read the field itself. Code in a lambda or a local function is left alone, since it may run later in the game.");

    public static readonly DiagnosticDescriptor LevelOutsideConstructor = new(
        "MW0015",
        "A unit's level set outside its declared class's constructor",
        "a unit's level is set only in its declared class's constructor",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The game has no setter for a unit's level, so Level is a unit's declared value: it is set in the constructor of the class that declares the unit (and in a local function there), and only read anywhere else; a lambda there may run later in the game, where the set does nothing. A hero's level is set with SetLevel; a hero's constructor does not declare it. The build rejects the same set.");

    public static readonly DiagnosticDescriptor DefaultKind = new(
        "MW0037",
        "Default<T>() of a class of another kind",
        "{0}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Default<T>() is declared on Widget, so C# accepts any widget class through Unit, Hero or Item: Item.Default<Guard>() compiles for a unit class, and a destructable class compiles and throws in the game. The rule rejects a class that is not of the kind the call is written on (a unit class through Item or Hero, an item class through Unit), and a destructable class anywhere, which has no default object.");

    /// <summary>The rules for the constructor of a declared object class, and for a class's default object.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Declared { get; } = ImmutableArray.Create(LiveInDeclaration, LevelOutsideConstructor, DefaultKind);

    public static readonly DiagnosticDescriptor NoId = new(
        "MW0013",
        "A declared object without its id",
        "{0} has {1}; moonwell build names a free one",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every class that declares an object of the map's own needs its id, as World Editor shows it, in [ObjectId(\"h001\")]; a [Replaces] class has its standard object's. A buff also gives the id of its carrier, the hidden ability the build makes to show its icon, so no class of the map declares that id: [ObjectId(\"B001\", Carrier = \"A001\")]. The build fails without them and names a free id to use.");

    public static readonly DiagnosticDescriptor BadId = new(
        "MW0014",
        "An object id the game cannot take",
        "{0} {1}",
        ObjectsCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An object id is four ASCII letters, digits or signs, but not a comma, which splits a list field that names the object. The game makes a unit whose id starts with an upper-case letter a hero, and only such a unit: a hero's id starts with one, another unit's does not.");

    /// <summary>The rules for a declared object's id.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Ids { get; } = ImmutableArray.Create(NoId, BadId);

    private static DiagnosticDescriptor Syntax(string id, string title, string message, string description) =>
        new(id, title, message, LuaCategory, DiagnosticSeverity.Error, isEnabledByDefault: true, description: description);

    public static readonly DiagnosticDescriptor FormWrong = Syntax(
        "MW0023",
        "Declaration CSharp.lua writes wrong",
        "{0}",
        "A static abstract or static virtual member of an interface, and a class that implements two constructions of one generic interface (IG<int> and IG<string>). A user-defined ++, --, operator true and operator false, a record that derives from a record, and with on an anonymous type work since the fork's 2.0.4-moonwell.15.");

    public static readonly DiagnosticDescriptor RefForm = Syntax(
        "MW0025",
        "ref return, ref local of a call, or ref reassignment",
        "{0}",
        "A ref return and ref int r = ref First(arr) give a copy, so writes through r are lost; a ref reassignment (r = ref other) writes other's value into what r named. ref parameters, out, in, and a ref local of a local, a field or an element of an array or Span work.");

    public static readonly DiagnosticDescriptor BoxedValue = Syntax(
        "MW0028",
        "char or enum given to an object parameter",
        "'{0}' {1}",
        "A char or an enum given to an object parameter of a library method that writes text, other than string's, Console's and StringBuilder's (which write it right since the fork's 2.0.4-moonwell.15), is shown as its number in the game's Lua; so is a list of enums given to string.Join. Joined with + or put in an interpolation hole it is right. Enums of Moonwell.Objects are MW0010.");

    public static readonly DiagnosticDescriptor LuaMember = Syntax(
        "MW0030",
        "Library member the game's Lua does not have",
        "'{0}' {1}",
        "The Lua core behind CSharp.lua has no such type or member, or gives another answer than .NET; the call fails when it runs, not when the map is built. The list: StringComparer, Monitor, Lock, Thread, Task, BitArray, BitOperations, HashCode, Guid, FormattableString, async/await, pointers; LINQ Append, Prepend, SkipLast, TakeLast, ToLookup, Empty, Zip, Chunk, ToHashSet, DefaultIfEmpty; List.ConvertAll; Span.ToArray/Fill/Clear; StringBuilder.Replace/Insert/Equals/AppendJoin; Exception.Source; Delegate.Target/Method; char.IsAscii/ToUpperInvariant; Environment.NewLine; Enum/Nullable.GetUnderlyingType; string.Create/Normalize/Intern/EnumerateRunes; Queue.TryPeek; Stack.TryPop/TryPeek; Dictionary.GetValueOrDefault; Convert.ToHexString; int.RotateLeft; Array.Sort(keys, items); HashSet.SetEquals (also through ISet and IReadOnlySet); Math.DivRem; Dictionary.Remove(key, out v); ToList/ToArray of a Dictionary (of a collection of the map's or Moonwell's own, such as OrderedMap, they work); Random.Shared.");

    public static readonly DiagnosticDescriptor EnumText = Syntax(
        "MW0032",
        "Enum shown as text",
        "{0}",
        "Enum.Parse(typeof(Color), text) gives an object, which in the game's Lua is the bare number. Enum.Parse<Color>, a [Flags] combination as text, the order of GetValues and GetNames, ToString(\"D\") and (\"X\"), and a constant cast joined to text work since the fork's 2.0.4-moonwell.15.");

    public static readonly DiagnosticDescriptor NotANumber = Syntax(
        "MW0042",
        "NaN or infinity, which the game's Lua does not have",
        "'{0}' {1}",
        "The game's own Lua has floats of its own (the 0.3 gate, 10-27): dividing makes no NaN and no infinity, and a product past the float's range gives 0 or nonsense. float.NaN and double.NaN, which CSharp.lua writes 0 / 0, are 1 there; 1 / 0 is 2^127 (1.7e38), and inf - inf and 0 * inf are 0. So float.IsNaN is never true, IsInfinity, IsPositiveInfinity, IsNegativeInfinity and IsFinite test for what never happens, and PositiveInfinity and NegativeInfinity turn into plain numbers in arithmetic. Mark no value with a bool or a float?, use float.MaxValue for no limit, and test what could go wrong before it does (a length of 0 before dividing by it).");

    /// <summary>The rules for syntax and library CSharp.lua or its Lua core cannot do.</summary>
    public static ImmutableArray<DiagnosticDescriptor> Syntaxes { get; } = ImmutableArray.Create(
        FormWrong, RefForm, BoxedValue, LuaMember, EnumText, NotANumber);
}
