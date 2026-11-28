using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for Unit.CanAttack and Ability.CanTarget: the game's answers of probe-targets (11-28, 3.0.1.24371) replayed on the
    /// fake world. The game was given 17 targets, each list's answer a 1 or 0 for each in turn (a spell's also for its
    /// caster, after a space): its own player's footman, an ally's, an enemy's, Neutral Passive's, Neutral Hostile's, an air
    /// unit, a Farm, a spell-immune Serpent Ward, a hero, a Steam Tank, a sapper, an invulnerable footman, a corpse, a
    /// spell-immune footman, a Banished one, an Ancient Protector and a Neutral Passive sheep.
    /// </summary>
    public static class TargetCases
    {
        private static int F(string id) => ObjectTypes.FourCC(id);

        /// <summary>The 17 targets of the probe, made for player 1 (slot 0) to ask about.</summary>
        private static List<Unit> Targets(FakeWorld world)
        {
            var passive = world.NeutralPassiveSlot();
            var ally = world.PlayerAt(2);
            world.SetAllianceOf(world.PlayerAt(0), ally, (int)Alliance.Passive);
            world.SetAllianceOf(ally, world.PlayerAt(0), (int)Alliance.Passive);

            // The game reads Neutral Passive as an ally of player 1 (IsPlayerAlly), and its units as neutral all the same.
            world.SetAllianceOf(world.PlayerAt(0), world.PlayerAt(passive), (int)Alliance.Passive);

            var made = new List<Unit>();
            Add(world, made, "hfoo", 0);
            Add(world, made, "hfoo", 2);
            Add(world, made, "hfoo", 1);
            Add(world, made, "hfoo", passive);
            Add(world, made, "hfoo", world.NeutralAggressiveSlot());
            world.MakeFlying(Add(world, made, "hgry", 1));
            var farm = Add(world, made, "hhou", 1);
            world.MakeStructure(farm);
            world.MakeKind(farm, UnitKind.Mechanical);
            var ward = Add(world, made, "osp1", 1);
            world.SetTargetedAs(ward, "ward");
            world.SpellImmune.Add(ward);
            Add(world, made, "Hpal", 1, true);
            world.MakeKind(Add(world, made, "hmtt", 1), UnitKind.Mechanical);
            world.MakeKind(Add(world, made, "ngsp", 1), UnitKind.Suicidal);
            world.MakeInvulnerable(Add(world, made, "hfoo", 1), true);
            Unit.Of(Add(world, made, "hfoo", 1))!.Kill();
            world.SpellImmune.Add(Add(world, made, "hfoo", 1));
            world.MakeEthereal(Add(world, made, "hfoo", 1), true);
            var ancient = Add(world, made, "etrp", 1);
            world.MakeStructure(ancient);
            world.MakeKind(ancient, UnitKind.Ancient);
            Add(world, made, "nshe", passive);
            return made;
        }

        private static unit Add(FakeWorld world, List<Unit> made, string id, int slot, bool hero = false)
        {
            var handle = world.NewUnit(F(id), slot, hero);
            made.Add(Unit.Of(handle)!);
            return handle;
        }

        /// <summary>The answers in the probe's form; a '?' in what the game gave matches either.</summary>
        private static void Match(string what, string game, string mine)
        {
            var same = game.Length == mine.Length;
            for (var i = 0; same && i < game.Length; i++)
            {
                same = game[i] == '?' || game[i] == mine[i];
            }

            Expect.True(same, what + ": the game gave " + game + ", Moonwell " + mine);
        }

        private static string B(bool b) => b ? "1" : "0";

        /// <summary>
        /// A Storm Bolt copy with each list, from a footman of player 1, against the 17 targets and the caster, as the game took its
        /// order: kinds by Targeted As, an empty list for ground, air and structures, owner flags (neutral is Neutral Passive's,
        /// friend not self, notself every owner but the caster), the living and the vulnerable when neither is named, the pairs, and
        /// never a spell-immune unit.
        /// </summary>
        public static void SpellTargets(FakeWorld world)
        {
            var targets = Targets(world);
            var caster = Unit.Of(world.NewUnit(F("hfoo"), 0))!;
            var lists = new string[]
            {
                "ground",
                "ground,enemies",
                "ground,allies",
                "ground,player",
                "ground,self",
                "ground,friend",
                "ground,friend,self",
                "ground,neutral",
                "ground,notself",
                "ground,player,notself",
                "ground,vulnerable",
                "ground,invulnerable",
                "ground,vulnerable,invulnerable",
                "ground,dead",
                "ground,alive,dead",
                "ground,organic",
                "ground,mechanical",
                "ground,organic,mechanical",
                "ground,hero",
                "ground,nonhero",
                "ground,sapper",
                "ground,nonsapper",
                "air",
                "structure",
                "structure,ancient",
                "structure,nonancient",
                "ward",
                "ground,ward",
                "ground,structure",
                "",
                "none",
                "ground,structure,air,ward,enemies,vulnerable,invulnerable,alive,dead",
            };
            var games = new string[]
            {
                "11111000111000101 1",
                "00101000111000100 0",
                "01000000000000000 0",
                "10000000000000000 0",
                "00000000000000000 1",
                "11000000000000000 0",
                "11000000000000000 1",
                "00010000000000001 0",
                "11111000111000101 0",
                "11111000111000101 0",
                "11111000111000101 1",
                "00000000000100000 0",
                "11111000111100101 1",
                "00000000000010000 0",
                "11111000111010101 1",
                "11111000101000101 1",
                "00000000010000000 0",
                "11111000111000101 1",
                "00000000100000000 0",
                "11111000011000101 1",
                "00000000001000000 0",
                "11111000110000101 1",
                "00000100000000000 0",
                "00000010000000010 0",
                "00000000000000010 0",
                "00000010000000000 0",
                "00000000000000000 0",
                "11111000111000101 1",
                "11111010111000111 1",
                "11111110111000111 1",
                "00000000000000000 0",
                "00101110111110110 0",
            };

            for (var n = 0; n < lists.Length; n++)
            {
                var id = F("AM00") + n;
                world.SetAbilityTargets(id, lists[n]);
                var ability = caster.AbilityOf(id);
                var mine = "";
                foreach (var target in targets)
                {
                    mine += B(ability.CanTarget(target));
                }

                Match("\"" + lists[n] + "\"", games[n], mine + " " + B(ability.CanTarget(caster)));
            }
        }

        /// <summary>
        /// A footman copy with each attack list beside each of the 17 targets, as the game started the attack: kinds as for a
        /// spell, but any owner whatever the list names, never a corpse, a normal attack on the spell-immune and not the ethereal,
        /// a magic one the other way round, and no attack from a unit whose weapon is off.
        /// </summary>
        public static void AttackTargets(FakeWorld world)
        {
            var targets = Targets(world);
            var lists = new string[]
            {
                "ground",
                "ground,enemies",
                "ground,allies",
                "ground,player",
                "ground,friend",
                "ground,neutral",
                "ground,notself",
                "ground,invulnerable",
                "ground,vulnerable,invulnerable",
                "ground,dead",
                "ground,alive,dead",
                "ground,organic",
                "ground,mechanical",
                "ground,hero",
                "ground,nonhero",
                "ground,sapper",
                "air",
                "structure",
                "structure,ancient",
                "structure,nonancient",
                "ward",
                "ground,ward",
                "",
                "none",
                "ground,structure,air,ward,enemies,vulnerable,invulnerable,alive,dead",
                "ground,enemies",
            };
            var games = new string[]
            {
                "11111000111001001",
                "11111000111001001",
                "11111000111001001",
                "11111000111001001",
                "11111000111001001",
                "11111000111001001",
                "11111000111001001",
                "00000000000100000",
                "11111000111101001",
                "00000000000000000",
                "11111000111001001",
                "11111000101001001",
                "00000000010000000",
                "00000000100000000",
                "11111000011001001",
                "00000000001000000",
                "00000100000000000",
                "00000010000000010",
                "00000000000000010",
                "00000010000000000",
                "00000001000000000",
                "11111001111001001",

                // The sheep was not attacked in this round and the one after next; it wanders, and the game facts leave it open.
                "1111111011100101?",
                "00000000000000000",
                "1111111111110101?",
                "11111000111000101",
            };
            var magics = new bool[] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true };

            for (var n = 0; n < lists.Length; n++)
            {
                var list = lists[n];
                var magic = magics[n];
                var game = games[n];
                var attacker = Unit.Of(world.NewUnit(F("hfoo"), 0))!;
                world.SetWeapon(attacker.Handle, 0, list, magic);
                var mine = "";
                foreach (var target in targets)
                {
                    mine += B(attacker.CanAttack(target));
                }

                Match("\"" + list + "\"" + (magic ? " magic" : ""), game, mine);
                Expect.True(!attacker.CanAttack(attacker), "not itself");
            }

            var unarmed = Unit.Of(world.NewUnit(F("hfoo"), 0))!;
            world.SetWeapon(unarmed.Handle, 0, "ground,structure,debris,item,ward");
            world.DisableWeapon(unarmed.Handle, 0);
            Expect.True(!unarmed.CanAttack(targets[2]), "no attack with its weapon off");

            var banished = Unit.Of(world.NewUnit(F("hfoo"), 0))!;
            world.SetWeapon(banished.Handle, 0, "ground,structure,debris,item,ward");
            Expect.True(banished.CanAttack(targets[2]), "the enemy footman, before it is Banished");
            world.MakeEthereal(banished.Handle, true);
            Expect.True(!banished.CanAttack(targets[2]), "an ethereal unit attacks nothing");
        }

        /// <summary>A second weapon counts when the first refuses: a Gryphon Rider's air attack beside its ground one; a dead or removed attacker and a removed target answer false.</summary>
        public static void SecondWeaponAndGone(FakeWorld world)
        {
            var rider = Unit.Of(world.NewUnit(F("hgry"), 0))!;
            var flier = Unit.Of(world.NewUnit(F("hgry"), 1))!;
            world.MakeFlying(flier.Handle);
            var footman = Unit.Of(world.NewUnit(F("hfoo"), 1))!;
            world.SetWeapon(rider.Handle, 0, "ground,structure,debris,item,ward");
            Expect.True(!rider.CanAttack(flier) && rider.CanAttack(footman), "the first weapon takes ground only");
            world.SetWeapon(rider.Handle, 1, "air");
            Expect.True(rider.CanAttack(flier) && rider.CanAttack(footman), "the second takes the flier");
            footman.Remove();
            Expect.True(!rider.CanAttack(footman), "not a removed target");
            rider.Kill();
            Expect.True(!rider.CanAttack(flier), "not from a dead unit");
        }

        /// <summary>
        /// CanTarget reads the list of the ability's level on its unit, level 1's when the unit lacks it and the last past its
        /// lists; the build's registration (ObjectTypes.TargetsAllowed) gives the lists when the world has none, an ancient's top
        /// bit included, and the grouped registration of the standard abilities (ObjectTypes.StandardTargets); an ability with no
        /// lists answers false.
        /// </summary>
        public static void AbilityLevelsAndRegistry(FakeWorld world)
        {
            var caster = Unit.Of(world.NewUnit(F("hfoo"), 0))!;
            var air = Unit.Of(world.NewUnit(F("hgry"), 1))!;
            world.MakeFlying(air.Handle);
            var ancient = Unit.Of(world.NewUnit(F("etrp"), 1))!;
            world.MakeStructure(ancient.Handle);
            world.MakeKind(ancient.Handle, UnitKind.Ancient);

            var id = F("A0T1");
            ObjectTypes.TargetsAllowed(id, new[] { "ground", "air,ground" });
            var ability = caster.AbilityOf(id);
            Expect.True(!ability.CanTarget(air), "level 1's list, as the unit lacks the ability");
            caster.AddAbility(id);
            ability.Level = 2;
            Expect.True(ability.CanTarget(air), "level 2's list");

            var ancients = F("A0T2");
            ObjectTypes.TargetsAllowed(ancients, new[] { "structure,ancient,enemies" });
            Expect.True(caster.AbilityOf(ancients).CanTarget(ancient), "a list with the top bit");

            var unknown = caster.AbilityOf(F("A0T3"));
            Expect.True(!unknown.CanTarget(air) && !unknown.CanTarget(ancient), "no lists, no target");

            // The build's grouped registration of the standard abilities (J195): each id of a group takes its list, an empty list
            // answers as one, and an ability registered on its own keeps its list.
            ObjectTypes.TargetsAllowed(F("A0T5"), new[] { "air" });
            ObjectTypes.StandardTargets(new[] { "air,ground", "A0T4A0T5", "", "A0T6" });
            Expect.True(caster.AbilityOf(F("A0T4")).CanTarget(air) && caster.AbilityOf(F("A0T4")).CanTarget(caster), "a grouped ability's list");
            Expect.True(caster.AbilityOf(F("A0T6")).CanTarget(caster), "a grouped empty list takes ground");
            Expect.True(caster.AbilityOf(F("A0T5")).CanTarget(air) && !caster.AbilityOf(F("A0T5")).CanTarget(caster), "its own list kept");
        }
    }
}
