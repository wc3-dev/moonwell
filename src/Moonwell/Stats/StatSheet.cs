using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A unit's or player's modifiers and bases, by stat, and what Moonwell's part of each of the game's stats is in the game now
    /// (decision 221). A game stat's base is the game's number less Moonwell's part, so the game's own changes (a level, an
    /// item) stay, until the map sets a base for the unit or its type: then Moonwell owns that stat on the unit, its base is
    /// the truth, and the total goes to the game (decision 387). Damage, armor and attack speed go through Moonwell's bonus
    /// abilities, which the game takes at once and shows green; the others, whose bonus abilities keep their first value, change
    /// the unit's own number (seen 11-08).
    /// </summary>
    internal sealed class StatSheet
    {
        /// <summary>The bonus abilities the build declares (Moonwell.Build's BonusAbilities): copies of AItg, AId1 and AIsx that give nothing until set.</summary>
        internal const string DamageId = "AMWd", ArmorId = "AMWa", AttackSpeedId = "AMWs";

        /// <summary>Whether an ability id is one of the three bonus abilities.</summary>
        internal static bool IsBonusAbility(int abilityId)
        {
            return abilityId == ObjectTypes.FourCC(DamageId) || abilityId == ObjectTypes.FourCC(ArmorId) || abilityId == ObjectTypes.FourCC(AttackSpeedId);
        }

        /// <summary>
        /// Changes nested deeper than this, each inside a reaction to the one before, are reported once and the innermost is not
        /// announced. 16 as the order and dummy guards are: each try in the compiled Lua takes one of the game's about 196 nested
        /// calls, and the game drops a deeper chain without a word (decision 276).
        /// </summary>
        internal const int MostNested = 16;

        /// <summary>How many announcements are running now, each inside a reaction to the one before; shared by every holder, as a reaction may change another holder's stats.</summary>
        private static int nested;

        /// <summary>Whether the chain running now has been reported.</summary>
        private static bool nestedReported;

        private readonly Actor actor;

        /// <summary>Reads a stat's total on this holder, as the formulas are given; made with the sheet, so a read stores nothing.</summary>
        private readonly Func<Stat, float> reader;

        private readonly Dictionary<Stat, List<Modifier>> held = new Dictionary<Stat, List<Modifier>>();

        private readonly Dictionary<Stat, float> applied = new Dictionary<Stat, float>();

        /// <summary>The total of each game stat as last put in the game, which a hero's level-up reads as the total before.</summary>
        private readonly Dictionary<Stat, float> last = new Dictionary<Stat, float>();

        /// <summary>The bases set for this holder itself.</summary>
        private Dictionary<Stat, float>? bases;

        /// <summary>While the total a stat had before a change is worked out: the total each stat changed so far had, which what reads them sees instead of their present total; null otherwise.</summary>
        private Dictionary<Stat, float>? olds;

        /// <summary>While changes are being announced on this holder: the total of each stat its listeners last heard, or the total it had when the first change not yet announced began; empty when no change is running.</summary>
        private Dictionary<Stat, float>? heard;

        /// <summary>How many changes of this holder are between starting to announce and having announced, each inside a reaction to the one before.</summary>
        private int announcing;

        /// <summary>The unit's acquisition range from before Moonwell raised it to its attack range; null while it has not.</summary>
        private float? acquireWas;

        /// <summary>Whether the hero's level-up is listened for, so its percents are put again.</summary>
        private bool levelWatch;

        /// <summary>Whether the hero's other stats are being put again after a level or an attribute, so a put of an attribute inside does not start another round.</summary>
        private bool following;

        /// <summary>The most life and the most mana as the game read them back when Moonwell last put an owned one; null while the stat is not owned or none was put.</summary>
        private float? mostLife, mostMana;

        internal StatSheet(Actor actor)
        {
            this.actor = actor;
            reader = Read;
        }

        /// <summary>Whether any class overrides a change reaction: nothing is worked out for the reactions until one does.</summary>
        private static bool Watching => Events.Wants2(Hook2.OnStatChanged);

        /// <summary>A stat's total as it is now, to read before something changes it, when a reaction will want it; 0 when none will.</summary>
        internal float Before(Stat stat)
        {
            return Watching ? Get(stat) : 0.0f;
        }

        /// <summary>Adds a modifier held by a scope, under its stacking rule, and puts the stat's new value in the game; refused, and given back ended, for a player's game stat or a holder that has ended.</summary>
        internal Modifier Add(Stat stat, float amount, float percent, float times, float late, Scope heldBy, object? group, Stacking? stacking, Unit? source, string label)
        {
            var modifier = new Modifier(this, stat, amount, percent, times, late, group, source);
            if (stat.Kind != StatKind.Own && !(actor is Unit))
            {
                Errors.Refuse(label, stat.Name + " is a unit's stat: a player takes only the map's own stats");
                modifier.End();
                return modifier;
            }

            if (heldBy == null || heldBy.HasEnded || heldBy.childrenEnded)
            {
                // An item's carry read where there is none (in the 3.0 bag) holds nothing, quietly, as Add does.
                if (!(heldBy is Carrying carrying && carrying.quiet))
                {
                    Dev.Report(label, "the modifier of " + stat.Name + " is held by " + (heldBy == null ? "nothing" : Names.NameOf(heldBy) + ", which has ended") + ", so it was not added");
                }

                modifier.End();
                return modifier;
            }

            if (group == null)
            {
                if (stacking != null)
                {
                    Dev.Report(label, "the modifier of " + stat.Name + " has a stacking rule but no group, so it adds up with the others: give it a group");
                }
            }
            else
            {
                modifier.rule = stacking ?? Stacking.Strongest;
                if (modifier.rule == Stacking.Refresh || modifier.rule == Stacking.PerSource)
                {
                    EndOlder(stat, group, modifier.rule == Stacking.PerSource, source);
                }
            }

            if (!held.TryGetValue(stat, out var list))
            {
                list = new List<Modifier>();
                held[stat] = list;
            }

            var before = Before(stat);
            list.Add(modifier);
            heldBy.Adopt(modifier);
            if (heldBy is Buff buff && buff.applying)
            {
                buff.madeInApply ??= new List<Modifier>();
                buff.madeInApply.Add(modifier);
            }

            Changed(stat, before);
            return modifier;
        }

        /// <summary>Takes a modifier off and puts the stat's new value in the game.</summary>
        internal void Remove(Modifier modifier)
        {
            if (held.TryGetValue(modifier.Stat, out var list) && list.Contains(modifier))
            {
                var before = Before(modifier.Stat);
                list.Remove(modifier);
                Changed(modifier.Stat, before);
            }
        }

        /// <summary>
        /// Works a stat out again after a modifier or a base changed, with before its total until then (read with Before), and puts it
        /// in the game: the one place a stat's change goes through. The stats that read it are worked out again and put too, and then
        /// the reactions run, once for each stat whose total changed.
        /// </summary>
        internal void Changed(Stat stat, float before)
        {
            if (held.TryGetValue(stat, out var list))
            {
                Recount(list);
            }

            PutFollowing(stat);
            if (!Watching)
            {
                Spread(stat, null, null);
                return;
            }

            if (Get(stat) == before)
            {
                return;
            }

            var batch = new List<Stat>();
            batch.Add(stat);
            var thens = new Dictionary<Stat, float>();
            thens[stat] = before;
            Spread(stat, thens, batch);

            // A change made inside a reaction to this one is announced first, so each stat is announced from the total its listeners last heard, and not again when that is already the total.
            heard ??= new Dictionary<Stat, float>();
            if (announcing == 0)
            {
                heard.Clear();
            }

            for (var i = 0; i < batch.Count; i++)
            {
                if (!heard.ContainsKey(batch[i]))
                {
                    heard[batch[i]] = thens[batch[i]];
                }
            }

            announcing++;
            for (var i = 0; i < batch.Count; i++)
            {
                var now = Get(batch[i]);
                var was = heard[batch[i]];
                if (now != was)
                {
                    heard[batch[i]] = now;
                    Announce(batch[i], was);
                }
            }

            announcing--;
        }

        /// <summary>Works out again, and puts in the game, every stat that reads the stat; while a change is watched (thens is not null), the total each had before is noted in thens, worked out as the stats changed so far were, and the stat is added to the batch.</summary>
        private void Spread(Stat stat, Dictionary<Stat, float>? thens, List<Stat>? batch)
        {
            var readers = stat.Readers;
            if (readers == null)
            {
                return;
            }

            for (var i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];
                if (thens != null && !thens.ContainsKey(reader))
                {
                    olds = thens;
                    var then = Get(reader);
                    olds = null;
                    thens[reader] = then;
                    batch!.Add(reader);
                }

                PutFollowing(reader);
                Spread(reader, thens, batch);
            }
        }

        /// <summary>A hero gained a level: the game's own number of each stat has moved, so every game stat that has a percent or a multiplier counting, is owned, or reads or is read by another stat is worked out again and put, before the map's reactions to the level run (I52). For an owned stat this overwrites what the level added.</summary>
        internal void LevelUp()
        {
            Refresh(null);
        }

        /// <summary>Puts a game stat, and when it is a hero's attribute puts the hero's other stats a level would put again: the game adds an attribute's life, damage and the rest on top of the number a set left (probe-stats-2).</summary>
        private void PutFollowing(Stat stat)
        {
            Put(stat);
            if (!following && actor is Hero && (stat.Kind == StatKind.Strength || stat.Kind == StatKind.Agility || stat.Kind == StatKind.Intelligence))
            {
                Refresh(stat);
            }
        }

        /// <summary>Works out again and puts each game stat of the holder that Reaches, but the one given; a put of an attribute inside starts no round of its own.</summary>
        private void Refresh(Stat? except)
        {
            var stats = new List<Stat>();
            foreach (var stat in last.Keys)
            {
                stats.Add(stat);
            }

            var was = following;
            following = true;
            for (var i = 0; i < stats.Count; i++)
            {
                if (stats[i] != except && Reaches(stats[i]))
                {
                    Changed(stats[i], last[stats[i]]);
                }
            }

            following = was;
        }

        /// <summary>Whether a level changes what a game stat comes to: a counting percent or multiplier works on a base the level moved, an owned stat is put over it, and a stat that reads or is read by another passes the move on.</summary>
        private bool Reaches(Stat stat)
        {
            if (stat.HasFormulas || stat.Readers != null || IsOwned(stat))
            {
                return true;
            }

            if (held.TryGetValue(stat, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].counts && (list[i].Percent != 0.0f || list[i].Times != 1.0f))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Puts, in the game, each of the game stats the unit's type or class has a base for, as the unit comes in.</summary>
        internal void PutTyped(List<Stat> stats)
        {
            for (var i = 0; i < stats.Count; i++)
            {
                Changed(stats[i], Before(stats[i]));
            }
        }

        /// <summary>Runs the change reactions of a stat whose total changed: the unit's, its components', the game's components' and the unit's player's, or for a player's stat the game's components' and the player's. A chain nested more than MostNested deep is reported once and not announced.</summary>
        private void Announce(Stat stat, float before)
        {
            var unit = actor as Unit;
            if (unit != null && (unit.HasEnded || unit.IsGone))
            {
                return;
            }

            if (nested >= MostNested)
            {
                if (!nestedReported)
                {
                    nestedReported = true;
                    Errors.Refuse("Stat.Changed", "stat changes nested more than " + MostNested + " deep, each made by a reaction to the one before, at " + stat.Name + (unit == null ? "" : " of " + Names.NameOf(unit)) + "; the innermost is not announced, so make the next from a timer (After(0))");
                }

                return;
            }

            // No try around the reactions: every reaction has its own, and a second one would take another of the game's nested calls at each level.
            nested++;
            if (unit != null)
            {
                UnitChanged(unit, stat, before);
                MapChanged(Game.Instance, unit, null, stat, before);
                var player = Player.Existing(unit.Owner.Slot);
                if (player != null)
                {
                    MapChanged(player, unit, null, stat, before);
                }
            }
            else if (actor is Player holder)
            {
                MapChanged(Game.Instance, null, holder, stat, before);
                MapChanged(holder, null, holder, stat, before);
            }

            nested--;
            if (nested <= 0)
            {
                ResetNesting();
            }
        }

        /// <summary>No change is being announced: Runtime.Reset calls it, which puts right a count that an error thrown past an announcement left behind.</summary>
        internal static void ResetNesting()
        {
            nested = 0;
            nestedReported = false;
        }

        /// <summary>Runs OnStatChanged on a unit, then on its components, in add order.</summary>
        private static void UnitChanged(Unit unit, Stat stat, float before)
        {
            if (!unit.HasEnded && (unit.Mask2 & Hook2.OnStatChanged) != 0)
            {
                try
                {
                    unit.OnStatChanged(stat, before);
                }
                catch (Exception error)
                {
                    Errors.Report(unit, ".OnStatChanged", error);
                }
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnStatChanged) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnStatChanged(stat, before);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnStatChanged", error);
                }
            }

            unit.EndDispatch();
        }

        /// <summary>Runs OnUnitStatChanged (for a unit) or OnPlayerStatChanged (for a player) on the map components of an actor, in add order.</summary>
        private static void MapChanged(Actor on, Unit? unit, Player? player, Stat stat, float before)
        {
            var count = on.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = on.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnStatChanged) == 0)
                {
                    continue;
                }

                try
                {
                    if (unit != null)
                    {
                        component.OnUnitStatChanged(unit, stat, before);
                    }
                    else
                    {
                        component.OnPlayerStatChanged(player!, stat, before);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(component, unit != null ? ".OnUnitStatChanged" : ".OnPlayerStatChanged", error);
                }
            }

            on.EndDispatch();
        }

        /// <summary>The modifiers of a stat, as a copy in the order they were made, those a rule leaves out included.</summary>
        internal IReadOnlyList<Modifier> Modifiers(Stat stat)
        {
            return held.TryGetValue(stat, out var list) ? new List<Modifier>(list) : new List<Modifier>();
        }

        /// <summary>Sets the base of a stat on this holder; for a game stat, Moonwell owns it from then on. Refused, and reported, for a player's game stat.</summary>
        internal void SetBase(Stat stat, float value, string label)
        {
            if (stat.Kind != StatKind.Own && !(actor is Unit))
            {
                Errors.Refuse(label, stat.Name + " is a unit's stat: a player takes only the map's own stats");
                return;
            }

            var before = Before(stat);
            bases ??= new Dictionary<Stat, float>();
            bases[stat] = value;
            Changed(stat, before);
        }

        /// <summary>Adds an amount to the base the stat has now, without what its formulas give: the unit's own, its class's or type's, the stat's, or for a game stat not yet owned the game's number less Moonwell's part.</summary>
        internal void AddBase(Stat stat, float amount, string label)
        {
            SetBase(stat, RawBase(stat) + amount, label);
        }

        /// <summary>Whether a base was set for this holder itself, not for its class or type, and what: what a save keeps.</summary>
        internal bool OwnBase(Stat stat, out float value)
        {
            if (bases != null && bases.TryGetValue(stat, out value))
            {
                return true;
            }

            value = 0.0f;
            return false;
        }

        /// <summary>Whether Moonwell owns a game stat on this holder: the holder's own base or its type's is set (decision 387).</summary>
        private bool IsOwned(Stat stat)
        {
            return Owned(stat, out var ignored);
        }

        /// <summary>Whether a base is set for this holder, its class or its type, and which; for a map's own stat this is its base, and for a game stat it is the number Moonwell works from.</summary>
        private bool Owned(Stat stat, out float value)
        {
            if (bases != null && bases.TryGetValue(stat, out value))
            {
                return true;
            }

            if (actor is Unit unit && stat.TypeBaseOf(unit, out value))
            {
                return true;
            }

            value = 0.0f;
            return false;
        }

        /// <summary>A stat's base before what its formulas give: for a map's own stat, the holder's own, else its type's, else the stat's; for a game stat, the same when Moonwell owns it, else the game's number less Moonwell's part; 0 for a game stat of a unit that is gone.</summary>
        internal float RawBase(Stat stat)
        {
            if (stat.Kind != StatKind.Own)
            {
                var gameUnit = actor as Unit;
                if (gameUnit == null || gameUnit.Handle == null || gameUnit.IsGone)
                {
                    return 0.0f;
                }

                return Owned(stat, out var ownedBase) ? ownedBase : BaseOf(gameUnit.Handle, stat);
            }

            return Owned(stat, out var own) ? own : stat.Base;
        }

        /// <summary>A stat's base, with what its From and Formula give.</summary>
        internal float GetBase(Stat stat)
        {
            if (stat.Kind != StatKind.Own)
            {
                var gameUnit = actor as Unit;
                if (gameUnit == null || gameUnit.Handle == null || gameUnit.IsGone)
                {
                    return 0.0f;
                }
            }

            return RawBase(stat) + Given(stat);
        }

        /// <summary>A stat's total on this holder, as the formulas read it: the total before the change being spread when the stat is one it changed, else its present total.</summary>
        private float Read(Stat stat)
        {
            return olds != null && olds.TryGetValue(stat, out var then) ? then : Get(stat);
        }

        private float Given(Stat stat)
        {
            if (!stat.HasFormulas)
            {
                return 0.0f;
            }

            return stat.Given(reader);
        }

        /// <summary>A stat's total, by the four stages. A game stat Moonwell owns is Moonwell's total. Another game stat's is what the game shows: the base as it is now and Moonwell's part as last put, so what the game added since (strength's life, a level) is outside the percents until the stat next changes or the hero levels up (I52).</summary>
        internal float Get(Stat stat)
        {
            if (stat.Kind == StatKind.Own)
            {
                return Value(stat, RawBase(stat) + Given(stat));
            }

            var unit = actor as Unit;
            if (unit == null || unit.Handle == null || unit.IsGone)
            {
                return 0.0f;
            }

            if (Owned(stat, out var ownedBase))
            {
                var baseValue = ownedBase + Given(stat);
                return Shown(unit.Handle, stat, baseValue, Value(stat, baseValue));
            }

            return BaseOf(unit.Handle, stat) + (applied.TryGetValue(stat, out var part) ? part : 0.0f);
        }

        // What the game has of an owned stat's total: damage and armor are a white number and a whole green bonus on it (Put gives the bonus as Whole(total - white)), so the total is that number. The other stats' totals are what Put gives the game, whole ones being rounded by Limited.
        private static float Shown(unit handle, Stat stat, float baseValue, float total)
        {
            if (stat.Kind == StatKind.Damage)
            {
                var white = Runtime.World.AverageDamageOf(handle);
                return white + Whole(total - white);
            }

            return stat.Kind == StatKind.Armor ? baseValue + Whole(total - baseValue) : total;
        }

        // (base + flat) × (1 + percents / 100) × each multiplier + late flat, then held to the limits.
        private float Value(Stat stat, float baseValue)
        {
            var flat = 0.0f;
            var percent = 0.0f;
            var times = 1.0f;
            var late = 0.0f;
            if (held.TryGetValue(stat, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (!list[i].counts)
                    {
                        continue;
                    }

                    flat += list[i].Amount;
                    percent += list[i].Percent;
                    times *= list[i].Times;
                    late += list[i].Late;
                }
            }

            return Limited(stat, (baseValue + flat) * (1.0f + percent / 100.0f) * times + late);
        }

        // The least, then the most (the stat's own and its cap's total, the smaller), then whole.
        private float Limited(Stat stat, float total)
        {
            return Limit(stat, total, stat.Cap != null ? Read(stat.Cap) : 0.0f);
        }

        // The same, with the cap's total given when the stat has a cap.
        private static float Limit(Stat stat, float total, float cap)
        {
            if (stat.Least != null && total < stat.Least.Value)
            {
                total = stat.Least.Value;
            }

            var most = stat.Most;
            if (stat.Cap != null)
            {
                if (most == null || cap < most.Value)
                {
                    most = cap;
                }
            }

            if (most != null && total > most.Value)
            {
                total = most.Value;
            }

            return stat.Whole ? Whole(total) : total;
        }

        /// <summary>The stacking rules: all modifiers count, then in each group of a Strongest rule only the farthest from no change does, the earlier of a tie.</summary>
        private static void Recount(List<Modifier> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                list[i].counts = true;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var one = list[i];
                if (!one.counts || one.rule != Stacking.Strongest)
                {
                    continue;
                }

                for (var j = i + 1; j < list.Count; j++)
                {
                    var other = list[j];
                    if (!other.counts || other.rule != Stacking.Strongest || !object.Equals(one.Group, other.Group))
                    {
                        continue;
                    }

                    // The number that says how far a modifier is from no change: its four numbers added, a multiplier in percent points.
                    if (Distance(other) > Distance(one))
                    {
                        one.counts = false;
                        break;
                    }

                    other.counts = false;
                }
            }
        }

        private static float Distance(Modifier modifier)
        {
            return Math.Abs(modifier.Amount) + Math.Abs(modifier.Percent) + Math.Abs(modifier.Late) + Math.Abs(modifier.Times - 1.0f) * 100.0f;
        }

        /// <summary>Ends the modifiers a new Refresh or PerSource one replaces: the stat's older ones of the group, and with PerSource those of the same source.</summary>
        private void EndOlder(Stat stat, object group, bool sameSource, Unit? source)
        {
            if (!held.TryGetValue(stat, out var list))
            {
                return;
            }

            var older = new List<Modifier>(list);
            for (var i = 0; i < older.Count; i++)
            {
                if (object.Equals(older[i].Group, group) && (!sameSource || older[i].Source == source))
                {
                    older[i].End();
                }
            }
        }

        // The game's number less Moonwell's part; damage's green bonus and attack speed are not in the number read.
        private float BaseOf(unit handle, Stat stat)
        {
            return GameBase(handle, stat, applied.TryGetValue(stat, out var part) ? part : 0.0f);
        }

        // The game's number of a stat less Moonwell's part of it.
        private static float GameBase(unit handle, Stat stat, float mine)
        {
            var world = Runtime.World;
            switch (stat.Kind)
            {
                case StatKind.Damage:
                    return world.AverageDamageOf(handle);
                case StatKind.Armor:
                    return world.ArmorOf(handle) - mine;
                case StatKind.MoveSpeed:
                    return world.MoveSpeedOf(handle) - mine;
                case StatKind.MaxLife:
                    return world.MaxLifeOf(handle) - mine;
                case StatKind.MaxMana:
                    return world.MaxManaOf(handle) - mine;
                case StatKind.LifeRegen:
                    return world.RegenOf(handle, false) - mine;
                case StatKind.ManaRegen:
                    return world.RegenOf(handle, true) - mine;
                case StatKind.Strength:
                    return world.StrengthOf(handle, false) - mine;
                case StatKind.Agility:
                    return world.AgilityOf(handle, false) - mine;
                case StatKind.Intelligence:
                    return world.IntelligenceOf(handle, false) - mine;
                case StatKind.AttackRange:
                    return world.AttackRangeOf(handle) - mine;
                default:
                    return 0.0f;
            }
        }

        // Puts a game stat's total in the game. A stat Moonwell does not own works on the game's number as it is now (a hero's level-up puts it again); one it owns sets the game's number from its own base.
        private void Put(Stat stat)
        {
            var unit = actor as Unit;
            if (stat.Kind == StatKind.Own || unit == null || unit.Handle == null || unit.IsGone)
            {
                return;
            }

            var world = Runtime.World;
            var handle = unit.Handle;
            var owned = Owned(stat, out var ownedBase);
            var raw = owned ? ownedBase : BaseOf(handle, stat);
            var baseValue = raw + Given(stat);
            var total = Value(stat, baseValue);
            last[stat] = total;
            if (!levelWatch && unit is Hero && Reaches(stat))
            {
                levelWatch = true;
                Events.Need(Hook.OnLevelUp);
            }

            switch (stat.Kind)
            {
                case StatKind.Damage:
                    if (owned)
                    {
                        // The dice stay, so the middle of the white damage is the base; the game keeps a whole number, so the green bonus makes up what it did not take.
                        world.SetAverageDamage(handle, baseValue);
                        raw = world.AverageDamageOf(handle);
                    }

                    applied[stat] = Bonus(handle, DamageId, "Iatt", false, Whole(total - raw));
                    break;
                case StatKind.Armor:
                    if (owned)
                    {
                        // Whether the game's setter takes the bonuses with it is not known, so Moonwell's own is taken off first and put on again after.
                        if (world.AbilityLevel(handle, ObjectTypes.FourCC(ArmorId)) != 0)
                        {
                            Bonus(handle, ArmorId, "Idef", false, 0.0f);
                        }

                        world.SetBaseArmor(handle, baseValue);
                        raw = baseValue;
                    }

                    applied[stat] = Bonus(handle, ArmorId, "Idef", false, Whole(total - raw));
                    break;
                case StatKind.AttackSpeed:
                    applied[stat] = Bonus(handle, AttackSpeedId, "Isx1", true, total / 100.0f) * 100.0f;
                    break;
                case StatKind.MoveSpeed:
                    world.SetMoveSpeed(handle, total);
                    applied[stat] = world.MoveSpeedOf(handle) - raw;
                    break;
                case StatKind.MaxLife:
                    PutMost(handle, false, Whole(total), owned);
                    applied[stat] = world.MaxLifeOf(handle) - raw;
                    break;
                case StatKind.MaxMana:
                    PutMost(handle, true, Whole(total), owned);
                    applied[stat] = world.MaxManaOf(handle) - raw;
                    break;
                case StatKind.LifeRegen:
                case StatKind.ManaRegen:
                    world.SetRegen(handle, stat.Kind == StatKind.ManaRegen, total);
                    applied[stat] = total - raw;
                    break;
                case StatKind.Strength:
                    world.SetStrength(handle, (int)Whole(total));
                    applied[stat] = world.StrengthOf(handle, false) - raw;
                    break;
                case StatKind.Agility:
                    world.SetAgility(handle, (int)Whole(total));
                    applied[stat] = world.AgilityOf(handle, false) - raw;
                    break;
                case StatKind.Intelligence:
                    world.SetIntelligence(handle, (int)Whole(total));
                    applied[stat] = world.IntelligenceOf(handle, false) - raw;
                    break;
                case StatKind.AttackRange:
                    PutRange(handle, total);
                    applied[stat] = world.AttackRangeOf(handle) - raw;
                    break;
            }

            // What Get now gives is what a later level-up counts as the total before, so a total the game's whole green bonus rounds does not read as a change.
            if (stat.Kind == StatKind.Damage || stat.Kind == StatKind.Armor)
            {
                last[stat] = Get(stat);
            }
        }

        // The game takes an attack range only as an add, so the difference to the range read is put. A unit picks no targets past its acquisition range, so one raised to the range goes up with it, and the unit's own comes back when the range drops under it.
        private void PutRange(unit handle, float total)
        {
            var world = Runtime.World;
            var delta = total - world.AttackRangeOf(handle);
            if (delta != 0.0f)
            {
                world.AddAttackRange(handle, delta);
            }

            var reached = world.AttackRangeOf(handle);
            if (acquireWas != null)
            {
                var own = acquireWas.Value;
                acquireWas = null;
                if (reached > own)
                {
                    acquireWas = own;
                    world.SetAcquireRange(handle, reached);
                }
                else
                {
                    world.SetAcquireRange(handle, own);
                }
            }
            else if (reached > world.AcquireRangeOf(handle))
            {
                acquireWas = world.AcquireRangeOf(handle);
                world.SetAcquireRange(handle, reached);
            }
        }

        // Sets a unit's most life or mana, and keeps its current life or mana at the same percent of the most (decision 391): 650 of 650 becomes 750 of 750, 325 of 650 becomes 375 of 750. A dead unit is left alone, and a most of 0 before or after is not divided by. A unit alive keeps at least 1 life, as the game kills at 0.405.
        // An owned most is Moonwell's own number, so when the game has moved it since Moonwell last put it (a level, an attribute), the game's shift is taken back off the current value first: the game adds a level's life to the current life as well as to the most (probe 11-21), and the unit keeps the life it had. A most that is not owned keeps the game's gift.
        private void PutMost(unit handle, bool mana, float most, bool owned)
        {
            var world = Runtime.World;
            var before = mana ? world.MaxManaOf(handle) : world.MaxLifeOf(handle);
            var amount = mana ? world.ManaOf(handle) : world.LifeOf(handle);
            var alive = world.IsAlive(handle);
            var put = mana ? mostMana : mostLife;
            var shifted = false;
            if (owned && put != null && alive && before != put.Value)
            {
                shifted = true;
                amount -= before - put.Value;
                if (amount < (mana ? 0.0f : 1.0f))
                {
                    amount = mana ? 0.0f : 1.0f;
                }

                before = put.Value;
            }

            if (mana)
            {
                world.SetMaxMana(handle, most);
            }
            else
            {
                world.SetMaxLife(handle, most);
            }

            var now = mana ? world.MaxManaOf(handle) : world.MaxLifeOf(handle);
            if (mana)
            {
                mostMana = owned ? now : (float?)null;
            }
            else
            {
                mostLife = owned ? now : (float?)null;
            }

            if (!alive || (amount <= 0.0f && !shifted) || before <= 0.0f || now <= 0.0f || (now == before && !shifted))
            {
                return;
            }

            var keep = amount >= before ? now : now * amount / before;
            if (mana)
            {
                world.SetMana(handle, keep);
            }
            else
            {
                world.SetLife(handle, keep < 1.0f ? 1.0f : keep);
            }
        }

        // Gives the unit the bonus ability once, kept through morphs, and sets its field.
        private static float Bonus(unit handle, string abilityId, string fieldId, bool real, float value)
        {
            var world = Runtime.World;
            var id = ObjectTypes.FourCC(abilityId);
            if (world.AbilityLevel(handle, id) == 0)
            {
                world.AddAbility(handle, id);
                world.MakeAbilityPermanent(handle, id);
            }

            world.PutBonus(handle, id, ObjectTypes.FourCC(fieldId), real, value);
            return value;
        }

        // A holder with no sheet has no modifiers, no base of its own and nothing put in the game, so its stats are worked out here from
        // the stat's own base, its type's, its formulas, its cap and its limits, as a sheet with nothing in it would, and with nothing made:
        // a read inside Local.For must leave nothing on one machine only, and the package reads about ten stats at each hit.
        // The holder whose formulas are being read; they are read by one function made once, so nothing is made for a read.
        private static Actor? bareActor;

        private static Func<Stat, float>? bareReader;

        private static float BareRead(Stat stat)
        {
            return BareGet(bareActor!, stat);
        }

        private static float BareGiven(Actor actor, Stat stat)
        {
            if (!stat.HasFormulas)
            {
                return 0.0f;
            }

            var before = bareActor;
            bareActor = actor;
            bareReader ??= new Func<Stat, float>(BareRead);
            var sum = stat.Given(bareReader);
            bareActor = before;
            return sum;
        }

        private static float BareLimited(Actor actor, Stat stat, float total)
        {
            return Limit(stat, total, stat.Cap != null ? BareGet(actor, stat.Cap) : 0.0f);
        }

        /// <summary>A stat's total on a holder with no sheet: what Get gives it, with nothing made.</summary>
        internal static float BareGet(Actor actor, Stat stat)
        {
            var unit = actor as Unit;
            if (stat.Kind == StatKind.Own)
            {
                var own = unit != null && stat.TypeBaseOf(unit, out var typedOwn) ? typedOwn : stat.Base;
                return BareLimited(actor, stat, own + BareGiven(actor, stat));
            }

            if (unit == null || unit.Handle == null || unit.IsGone)
            {
                return 0.0f;
            }

            if (stat.TypeBaseOf(unit, out var ownedBase))
            {
                var baseValue = ownedBase + BareGiven(actor, stat);
                return Shown(unit.Handle, stat, baseValue, BareLimited(actor, stat, baseValue));
            }

            return GameBase(unit.Handle, stat, 0.0f);
        }

        /// <summary>A stat's base on a holder with no sheet, with what its formulas give, as GetBase says it.</summary>
        internal static float BareGetBase(Actor actor, Stat stat)
        {
            var unit = actor as Unit;
            if (stat.Kind == StatKind.Own)
            {
                return (unit != null && stat.TypeBaseOf(unit, out var typedOwn) ? typedOwn : stat.Base) + BareGiven(actor, stat);
            }

            if (unit == null || unit.Handle == null || unit.IsGone)
            {
                return 0.0f;
            }

            return (stat.TypeBaseOf(unit, out var typed) ? typed : GameBase(unit.Handle, stat, 0.0f)) + BareGiven(actor, stat);
        }

        /// <summary>The modifiers of a holder with no sheet: none, the one list for every holder.</summary>
        internal static IReadOnlyList<Modifier> NoModifiers => noModifiers ??= new Modifier[0];

        private static Modifier[]? noModifiers;

        /// <summary>A number rounded half up. The half is found from what is left over the floor, which is exact: adding 0.5 first moves a value from 2^23 up, where a float has no halves.</summary>
        internal static float Whole(float value)
        {
            var floor = (float)Math.Floor(value);
            return value - floor >= 0.5f ? floor + 1.0f : floor;
        }
    }
}
