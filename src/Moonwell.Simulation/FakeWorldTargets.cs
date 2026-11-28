using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The fake world's target lists and the states they read: a unit's weapons and Targeted As, invulnerability, spell immunity and the ethereal state, and each ability's lists.</summary>
    public sealed partial class FakeWorld
    {
        /// <summary>A unit's two weapons: whether each is enabled, what it may attack and whether its attack is magic.</summary>
        private sealed class Weapons
        {
            internal readonly bool[] Enabled = new bool[2];
            internal readonly int[] Targets = new int[2];
            internal readonly bool[] Magic = new bool[2];
        }

        private readonly Dictionary<unit, Weapons> weapons = new Dictionary<unit, Weapons>();

        private readonly Dictionary<unit, int> targetedAs = new Dictionary<unit, int>();

        private readonly HashSet<unit> invulnerables = new HashSet<unit>();

        private readonly HashSet<unit> ethereals = new HashSet<unit>();

        private readonly Dictionary<int, int[]> abilityTargets = new Dictionary<int, int[]>();

        /// <summary>Gives a unit a weapon, 0 for its first and 1 for its second, enabled, with what it may attack as the object data writes it ("ground,structure,enemies"), and whether its attack type is magic.</summary>
        public void SetWeapon(unit unit, int weapon, string targets, bool magic = false)
        {
            if (!weapons.TryGetValue(unit, out var held))
            {
                held = new Weapons();
                weapons[unit] = held;
            }

            held.Enabled[weapon] = true;
            held.Targets[weapon] = TargetLists.Parse(targets);
            held.Magic[weapon] = magic;
        }

        /// <summary>Turns a unit's weapon off, as a unit with no attack has it.</summary>
        public void DisableWeapon(unit unit, int weapon)
        {
            if (weapons.TryGetValue(unit, out var held))
            {
                held.Enabled[weapon] = false;
            }
        }

        /// <summary>Sets what a unit is targeted as, as the object data writes it ("air"); until set, a structure is targeted as a structure, a unit whose type flies as air, and any other as ground.</summary>
        public void SetTargetedAs(unit unit, string kinds)
        {
            targetedAs[unit] = TargetLists.Parse(kinds);
        }

        /// <summary>Makes a unit invulnerable or not, as SetUnitInvulnerable does.</summary>
        public void MakeInvulnerable(unit unit, bool invulnerable)
        {
            if (invulnerable)
            {
                invulnerables.Add(unit);
            }
            else
            {
                invulnerables.Remove(unit);
            }
        }

        /// <summary>Makes a unit ethereal or not, as Banish does.</summary>
        public void MakeEthereal(unit unit, bool ethereal)
        {
            if (ethereal)
            {
                ethereals.Add(unit);
            }
            else
            {
                ethereals.Remove(unit);
            }
        }

        /// <summary>Sets what an ability may target at each level, from level 1, as the object data writes each list.</summary>
        public void SetAbilityTargets(int abilityId, params string[] levels)
        {
            var lists = new int[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                lists[i] = TargetLists.Parse(levels[i]);
            }

            abilityTargets[abilityId] = lists;
        }

        /// <summary>Whether a unit was made invulnerable.</summary>
        public bool IsInvulnerable(unit unit) => invulnerables.Contains(unit);

        /// <summary>Whether a unit is immune to spells: one in SpellImmune.</summary>
        public bool IsMagicImmune(unit unit) => SpellImmune.Contains(unit);

        /// <summary>Whether a unit was made ethereal.</summary>
        public bool IsEthereal(unit unit) => ethereals.Contains(unit);

        /// <summary>What a unit is targeted as: what was set or seeded, else a structure as a structure, a unit whose type flies as air, any other as ground.</summary>
        public int TargetedAsOf(unit unit)
        {
            if (targetedAs.TryGetValue(unit, out var kinds))
            {
                return kinds;
            }

            return structures.Contains(unit) ? TargetLists.Structure : CanFly(unit) ? TargetLists.Air : TargetLists.Ground;
        }

        /// <summary>Whether a unit's weapon was given and is enabled; none is until SetWeapon or a seeded type gives one.</summary>
        public bool AttackEnabledOf(unit unit, int weapon) => weapons.TryGetValue(unit, out var held) && weapon >= 0 && weapon < 2 && held.Enabled[weapon];

        /// <summary>What a unit's weapon may attack; 0 for one not given.</summary>
        public int AttackTargetsOf(unit unit, int weapon) => weapons.TryGetValue(unit, out var held) && weapon >= 0 && weapon < 2 ? held.Targets[weapon] : 0;

        /// <summary>A weapon's attack type as the natives number it: 4 (magic) for a magic weapon, else 1 (World Editor's normal).</summary>
        public int AttackTypeOf(unit unit, int weapon) => weapons.TryGetValue(unit, out var held) && weapon >= 0 && weapon < 2 && held.Magic[weapon] ? 4 : 1;

        /// <summary>What an ability may target at a level index: SetAbilityTargets's lists, else on .NET its object data's, else what the map registered; -1 for none.</summary>
        public int AbilityTargetsOf(int abilityId, int levelIndex)
        {
            if (abilityTargets.TryGetValue(abilityId, out var lists) && lists.Length > 0)
            {
                return lists[SpellNumbersIndex(lists.Length, levelIndex)];
            }

#if !MOONWELL_LUA
            var numbers = SpellOf(abilityId);
            if (numbers != null && numbers.Targets.Length > 0)
            {
                return numbers.Targets[SpellNumbers.Index(numbers.Targets.Length, levelIndex)];
            }
#endif
            return ObjectTypes.TargetsOf(abilityId, levelIndex);
        }

        private static int SpellNumbersIndex(int count, int levelIndex) => levelIndex < 0 ? 0 : levelIndex >= count ? count - 1 : levelIndex;

        /// <summary>Seeds a unit's weapons and Targeted As from its type's numbers.</summary>
        private void SeedTargets(unit made, UnitNumbers numbers)
        {
            if (numbers.TargetedAs != 0)
            {
                targetedAs[made] = numbers.TargetedAs;
            }

            var held = new Weapons();
            for (var weapon = 0; weapon < 2; weapon++)
            {
                held.Enabled[weapon] = (numbers.AttacksEnabled & (1 << weapon)) != 0;
                held.Targets[weapon] = numbers.AttackTargets[weapon];
                held.Magic[weapon] = numbers.MagicAttacks[weapon];
            }

            weapons[made] = held;
        }
    }
}
