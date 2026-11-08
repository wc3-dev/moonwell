using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A unit's or player's modifiers, by stat, and what Moonwell's part of each of the game's stats is in the game now
    /// (decision 221). A game stat's base is the game's number less Moonwell's part, so the game's own changes (a level, an
    /// item) stay. Damage, armor and attack speed go through Moonwell's bonus abilities, which the game takes at once and
    /// shows green; the others, whose bonus abilities keep their first value, change the unit's own number (seen 11-08).
    /// </summary>
    internal sealed class StatSheet
    {
        /// <summary>The bonus abilities the build declares (Moonwell.Build's BonusAbilities): copies of AItg, AId1 and AIsx that give nothing until set.</summary>
        internal const string DamageId = "AMWd", ArmorId = "AMWa", AttackSpeedId = "AMWs";

        private readonly Actor actor;

        private readonly Dictionary<Stat, List<Modifier>> held = new Dictionary<Stat, List<Modifier>>();

        private readonly Dictionary<Stat, float> applied = new Dictionary<Stat, float>();

        internal StatSheet(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>Adds a modifier held by a scope and puts the stat's new value in the game; refused, and given back ended, for a player's game stat or a holder that has ended.</summary>
        internal Modifier Add(Stat stat, float amount, float percent, Scope heldBy, string label)
        {
            var modifier = new Modifier(this, stat, amount, percent);
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

            if (!held.TryGetValue(stat, out var list))
            {
                list = new List<Modifier>();
                held[stat] = list;
            }

            list.Add(modifier);
            heldBy.Adopt(modifier);
            Put(stat);
            return modifier;
        }

        /// <summary>Takes a modifier off and puts the stat's new value in the game.</summary>
        internal void Remove(Modifier modifier)
        {
            if (held.TryGetValue(modifier.Stat, out var list) && list.Remove(modifier))
            {
                Put(modifier.Stat);
            }
        }

        /// <summary>A stat's value: (base + flat) × (1 + percents / 100); a game stat is never below 0, the most life never below 1.</summary>
        internal float Get(Stat stat)
        {
            var kind = stat.Kind;
            if (kind == StatKind.Own)
            {
                return Total(stat, stat.Base);
            }

            var unit = actor as Unit;
            if (unit == null || unit.Handle == null || unit.IsGone)
            {
                return 0.0f;
            }

            return Total(stat, BaseOf(unit.Handle, stat));
        }

        private float Total(Stat stat, float baseValue)
        {
            var flat = 0.0f;
            var percent = 0.0f;
            if (held.TryGetValue(stat, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    flat += list[i].Amount;
                    percent += list[i].Percent;
                }
            }

            var total = (baseValue + flat) * (1.0f + percent / 100.0f);
            return stat.Kind == StatKind.Own ? total : Math.Max(stat.Kind == StatKind.MaxLife ? 1.0f : 0.0f, total);
        }

        // The game's number less Moonwell's part; damage's green bonus and attack speed are not in the number read.
        private float BaseOf(unit handle, Stat stat)
        {
            var world = Runtime.World;
            var mine = applied.TryGetValue(stat, out var part) ? part : 0.0f;
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
                default:
                    return 0.0f;
            }
        }

        // shortcut: a percent of a base the game changes (a level) is put again only at the stat's next change; follow levels if maps need it.
        private void Put(Stat stat)
        {
            var unit = actor as Unit;
            if (stat.Kind == StatKind.Own || unit == null || unit.Handle == null || unit.IsGone)
            {
                return;
            }

            var world = Runtime.World;
            var handle = unit.Handle;
            var baseValue = BaseOf(handle, stat);
            var total = Total(stat, baseValue);
            switch (stat.Kind)
            {
                case StatKind.Damage:
                    applied[stat] = Bonus(handle, DamageId, "Iatt", false, Whole(total - baseValue));
                    break;
                case StatKind.Armor:
                    applied[stat] = Bonus(handle, ArmorId, "Idef", false, Whole(total - baseValue));
                    break;
                case StatKind.AttackSpeed:
                    applied[stat] = Bonus(handle, AttackSpeedId, "Isx1", true, total / 100.0f) * 100.0f;
                    break;
                case StatKind.MoveSpeed:
                    world.SetMoveSpeed(handle, total);
                    applied[stat] = world.MoveSpeedOf(handle) - baseValue;
                    break;
                case StatKind.MaxLife:
                    world.SetMaxLife(handle, Whole(total));
                    applied[stat] = world.MaxLifeOf(handle) - baseValue;
                    break;
                case StatKind.MaxMana:
                    world.SetMaxMana(handle, Whole(total));
                    applied[stat] = world.MaxManaOf(handle) - baseValue;
                    break;
                case StatKind.LifeRegen:
                case StatKind.ManaRegen:
                    world.SetRegen(handle, stat.Kind == StatKind.ManaRegen, total);
                    applied[stat] = total - baseValue;
                    break;
                case StatKind.Strength:
                    world.SetStrength(handle, (int)Whole(total));
                    applied[stat] = world.StrengthOf(handle, false) - baseValue;
                    break;
                case StatKind.Agility:
                    world.SetAgility(handle, (int)Whole(total));
                    applied[stat] = world.AgilityOf(handle, false) - baseValue;
                    break;
                case StatKind.Intelligence:
                    world.SetIntelligence(handle, (int)Whole(total));
                    applied[stat] = world.IntelligenceOf(handle, false) - baseValue;
                    break;
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

        private static float Whole(float value)
        {
            return (float)Math.Floor(value + 0.5f);
        }
    }
}
