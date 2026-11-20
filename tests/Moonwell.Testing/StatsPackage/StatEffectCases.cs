using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A game component that holds modifiers and barriers, ended by the case.</summary>
    public sealed class StatHolder : GameComponent
    {
    }

    /// <summary>A unit component that logs each change of a stat on its unit, with the total before.</summary>
    public sealed class StatChangeLog : UnitComponent
    {
        /// <summary>The changes, as the stat's name, then the total before, then the total now.</summary>
        public List<string> Lines = new List<string>();

        /// <summary>Logs the change.</summary>
        public override void OnStatChanged(Stat stat, float before)
        {
            Lines.Add(stat.Name + " " + (int)before + " to " + (int)Unit.Get(stat));
        }
    }

    /// <summary>
    /// Cases for the stats package's effects on hits and heals, run on the damage package's fake: each ready-made stat alone with a
    /// chance of 0 or 100 or a seeded generator, the order of the steps, what the package's own hits and heals miss, and barriers.
    /// </summary>
    public static class StatEffectCases
    {
        /// <summary>The life the target starts with, large enough that the life lost is what a case reads, small enough that a float keeps its decimals.</summary>
        private const float Life = 1000.0f;

        private static FakeDamageWorld fake = null!;

        private static int Id(string code) => ObjectTypes.FourCC(code);

        /// <summary>Installs both packages on a fake; makes a melee source for player 0 and a target of 1000 life for player 1, with the texts on.</summary>
        private static void Arena(FakeWorld world, out Unit source, out Unit target)
        {
            fake = new FakeDamageWorld(world);
            DamageSystem.Install(fake);
            StatsSystem.Install();
            StatsSystem.ShowTexts = true;
            source = Unit.Of(world.NewUnit(Id("hfoo"), slot: 0))!;
            target = Unit.Of(world.NewUnit(Id("hfoo"), slot: 1))!;
            fake.Body(target.Handle).Life = Life;
            fake.Body(source.Handle).Melee = true;
        }

        /// <summary>The life the target has lost.</summary>
        private static float Lost(Unit target) => Life - fake.Body(target.Handle).Life;

        /// <summary>A hit as the game plays an attack of normal damage, landed.</summary>
        private static void Swing(Unit source, Unit target, float amount = 100.0f, int kind = 4)
        {
            fake.Hit(source.Handle, target.Handle, amount, attackType: 1, damageType: kind, isAttack: true);
            fake.RunZero();
        }

        /// <summary>A spell's hit from code, magic by default, landed.</summary>
        private static Hit Spell(Unit source, Unit target, float amount = 100.0f, DamageKind kind = DamageKind.Magic, bool area = false, float power = 1.0f)
        {
            var hit = (Hit?)source.Damage(target, amount, kind, area: area, power: power);
            fake.RunZero();
            return hit!;
        }

        /// <summary>An attack from code, of the kind, landed.</summary>
        private static Hit Attack(Unit source, Unit target, float amount = 100.0f, DamageKind kind = DamageKind.Normal, bool ranged = false)
        {
            var hit = (Hit?)source.Damage(target, amount, kind, AttackType.Normal, isAttack: true, ranged: ranged);
            fake.RunZero();
            return hit!;
        }

        /// <summary>A hit with no stat touched on either unit lands as it does without the package; no sheet of stats is made for either.</summary>
        public static void NoStatsNoEffect(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            Swing(source, target);
            Spell(source, target);
            Expect.Near(200.0f, Lost(target), "both hits landed whole");
            Expect.Equal(0, world.Texts.Count, "no text");
            Expect.True(source.stats == null && target.stats == null, "no sheet of stats was made for either unit");
        }

        /// <summary>A unit with no sheet reads a stat as Get does, from the stat's base, its limits and the base its type gives.</summary>
        public static void ReadingMatchesGet(FakeWorld world)
        {
            var plain = new Stat("Plain", 7.0f, 10.0f, 20.0f);
            var whole = new Stat("Whole", 2.4f, null, null, true);
            var typed = new Stat("Typed", 1.0f);
            typed.SetBase(Id("hpea"), 30.0f);
            var unit = Unit.Of(world.NewUnit(Id("hfoo")))!;
            var peasant = Unit.Of(world.NewUnit(Id("hpea")))!;
            Expect.Near(10.0f, Reading.Of(unit, plain), "a base under the least is held to it");
            Expect.Near(2.0f, Reading.Of(unit, whole), "a whole stat is rounded");
            Expect.Near(1.0f, Reading.Of(unit, typed), "no type base");
            Expect.Near(30.0f, Reading.Of(peasant, typed), "the type's base");
            Expect.True(unit.stats == null, "reading made no sheet for the unit");
            Expect.Near(unit.Get(plain), Reading.Of(unit, plain), "the same as Get, held");
            Expect.Near(peasant.Get(typed), Reading.Of(peasant, typed), "the same as Get, typed");
            var sheet = Unit.Of(world.NewUnit(Id("hfoo")))!;
            sheet.Modify(plain, 5.0f, heldBy: sheet);
            Expect.Near(12.0f, Reading.Of(sheet, plain), "a unit with a sheet reads its total: 7 and 5");
        }

        /// <summary>Hit chance and evasion: an attack misses at 100 - hit chance + evasion, held to 0 and 100; a spell never misses; the miss shows one red "miss".</summary>
        public static void MissAndHitChance(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.Evasion, 100.0f);
            Swing(source, target);
            Expect.Near(0.0f, Lost(target), "evasion 100 misses every attack");
            Expect.Equal(1, world.Texts.Count, "one text");
            Expect.Equal("miss", world.Texts[0].Words, "its words");
            Spell(source, target);
            Expect.Near(100.0f, Lost(target), "a spell is never missed");
            target.SetBase(Stat.Evasion, 20.0f);
            source.SetBase(Stat.HitChance, 150.0f);
            Swing(source, target);
            Expect.Near(200.0f, Lost(target), "100 - 150 + 20 is under 0: it hits");
            source.SetBase(Stat.HitChance, 0.0f);
            target.SetBase(Stat.Evasion, 0.0f);
            Swing(source, target);
            Expect.Near(200.0f, Lost(target), "hit chance 0 misses: 100 - 0 + 0");
            source.SetBase(Stat.HitChance, 100.0f);
            Swing(source, target);
            Expect.Near(300.0f, Lost(target), "hit chance 100 against no evasion hits");
        }

        /// <summary>With a seeded generator a unit with 50 evasion takes about half of 400 attacks.</summary>
        public static void MissRate(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var dice = new Dice(11);
            world.Pick = (low, high) => dice.Int(low, high);
            target.SetBase(Stat.Evasion, 50.0f);
            for (var i = 0; i < 400; i++)
            {
                Swing(source, target, 1.0f);
            }

            Expect.True(Lost(target) > 160.0f && Lost(target) < 240.0f, "about 200 of 400 landed, not " + Lost(target));
        }

        /// <summary>A melee attack from the front half of the target is parried, with the text "parry"; one from behind, a ranged one and a spell are not.</summary>
        public static void Parry(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.Parry, 100.0f);
            target.MoveTo(0.0f, 0.0f);
            target.FaceNow(0.0f);
            source.MoveTo(100.0f, 0.0f);
            Swing(source, target);
            Expect.Near(0.0f, Lost(target), "from the front it is parried");
            Expect.Equal("parry", world.Texts[0].Words, "the text");
            source.MoveTo(0.0f, 100.0f);
            Swing(source, target);
            Expect.Near(0.0f, Lost(target), "from the side, 90 degrees, still the front half");
            source.MoveTo(-100.0f, 0.0f);
            Swing(source, target);
            Expect.Near(100.0f, Lost(target), "from behind it lands");
            source.MoveTo(100.0f, 0.0f);
            fake.Body(source.Handle).Melee = false;
            fake.Body(source.Handle).Ranged = true;
            Swing(source, target);
            Expect.Near(200.0f, Lost(target), "a ranged attack is not parried");
            Spell(source, target);
            Expect.Near(300.0f, Lost(target), "nor a spell");
            target.SetBase(Stat.Parry, 0.0f);
            fake.Body(source.Handle).Melee = true;
            fake.Body(source.Handle).Ranged = false;
            Swing(source, target);
            Expect.Near(400.0f, Lost(target), "parry 0 parries nothing");
        }

        /// <summary>A missed hit gets nothing below it: no crit, no power, no text but the miss, no life steal.</summary>
        public static void MissedHitGetsNothing(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.SetBase(Stat.CritChance, 100.0f);
            source.SetBase(Stat.AttackPower, 50.0f);
            source.SetBase(Stat.LifeSteal, 50.0f);
            source.MaxLife = 100.0f;
            source.Life = 10.0f;
            target.SetBase(Stat.Evasion, 100.0f);
            var hit = Attack(source, target);
            Expect.True(hit.Missed && !hit.IsCrit, "missed, and never a crit");
            Expect.Near(0.0f, Lost(target), "no life lost");
            Expect.Equal(1, world.Texts.Count, "only the miss's text");
            Expect.Near(10.0f, source.Life, "no life stolen");
        }

        /// <summary>Attack power adds to an attack, and spell power to a spell hit times its power; neither touches the other's hits.</summary>
        public static void Power(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.SetBase(Stat.AttackPower, 15.0f);
            source.SetBase(Stat.SpellPower, 20.0f);
            Swing(source, target);
            Expect.Near(115.0f, Lost(target), "an attack of 100 and 15 attack power");
            var spell = Spell(source, target, 100.0f, power: 0.5f);
            Expect.Near(110.0f, spell.Amount, "a spell of 100, 20 spell power at power 0.5");
            Expect.Near(225.0f, Lost(target), "and what the target lost");
            Spell(source, target);
            Expect.Near(345.0f, Lost(target), "at power 1 the whole 20");
        }

        /// <summary>A crit at 100 multiplies by crit damage and marks the hit, with the landed amount in red and a "!" over the target; a spell crits too; a chance of 0 never does; texts off shows none.</summary>
        public static void Crit(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.SetBase(Stat.CritChance, 100.0f);
            source.SetBase(Stat.AttackPower, 10.0f);
            var hit = Attack(source, target);
            Expect.True(hit.IsCrit, "marked");
            Expect.Near(220.0f, hit.Amount, "(100 + 10 attack power) x 2, crit damage's base of 200");
            Expect.Near(220.0f, Lost(target), "what was lost");
            Expect.Equal(1, world.Texts.Count, "one text");
            Expect.Equal("220!", world.Texts[0].Words, "the amount and a !");
            Expect.True(world.Texts[0].Color![0] == 255 && world.Texts[0].Color![1] == 0 && world.Texts[0].Color![2] == 0, "red");
            source.SetBase(Stat.CritDamage, 150.0f);
            Spell(source, target);
            Expect.Near(370.0f, Lost(target), "a spell crits as well, x 1.5");
            source.SetBase(Stat.CritChance, 0.0f);
            Swing(source, target);
            Expect.Near(480.0f, Lost(target), "a chance of 0 never crits; the attack power is still added");
            Expect.Equal(2, world.Texts.Count, "no text for it");
            source.SetBase(Stat.CritChance, 100.0f);
            StatsSystem.ShowTexts = false;
            Swing(source, target);
            Expect.Near(480.0f + 165.0f, Lost(target), "a crit with the texts off still crits");
            Expect.Equal(2, world.Texts.Count, "and shows none");
            StatsSystem.ShowTexts = true;
        }

        /// <summary>Crit rolls at its chance: about a quarter of 400 hits of a seeded generator.</summary>
        public static void CritRate(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var dice = new Dice(5);
            world.Pick = (low, high) => dice.Int(low, high);
            StatsSystem.ShowTexts = false;
            source.SetBase(Stat.CritChance, 25.0f);
            var crits = 0;
            for (var i = 0; i < 400; i++)
            {
                if (Attack(source, target, 1.0f).IsCrit)
                {
                    crits++;
                }
            }

            StatsSystem.ShowTexts = true;
            Expect.True(crits > 70 && crits < 130, "about 100 crits in 400, not " + crits);
        }

        /// <summary>Armor penetration lowers the armor the hit meets, not below 0, and leaves an armor already below 0; the target has its own armor back.</summary>
        public static void ArmorPenetration(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var body = fake.Body(target.Handle);
            body.Armor = 10.0f;
            Swing(source, target);
            Expect.Near(62.5f, Lost(target), "100 on armor 10 loses 100 x (1 - 0.6 / 1.6)");
            source.SetBase(Stat.ArmorPenetration, 4.0f);
            Swing(source, target);
            Expect.Near(62.5f + 73.5294f, Lost(target), "armor 6 loses 100 x (1 - 0.36 / 1.36)");
            source.SetBase(Stat.ArmorPenetration, 25.0f);
            Swing(source, target);
            Expect.Near(62.5f + 73.5294f + 100.0f, Lost(target), "past the armor: 0, not below");
            Expect.Near(10.0f, body.Armor, "the target's own armor is back");
            body.Armor = -5.0f;
            Swing(source, target);
            Expect.Near(62.5f + 73.5294f + 100.0f + 126.6096f, Lost(target), "an armor below 0 is left alone: 100 x (2 - 0.94 to the 5th)");
        }

        /// <summary>Resistance takes a percent off a kind after armor; spell penetration lowers a positive one, never below 0; a negative one adds damage and penetration leaves it.</summary>
        public static void Resistance(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.Resistance(DamageKind.Fire), 25.0f);
            Spell(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(75.0f, Lost(target), "25 fire resistance");
            Spell(source, target, 100.0f, DamageKind.Cold);
            Expect.Near(175.0f, Lost(target), "none against cold");
            source.SetBase(Stat.SpellPenetration, 10.0f);
            Spell(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(260.0f, Lost(target), "15 after 10 penetration");
            source.SetBase(Stat.SpellPenetration, 40.0f);
            Spell(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(360.0f, Lost(target), "never below 0");
            target.SetBase(Stat.Resistance(DamageKind.Fire), -20.0f);
            Spell(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(480.0f, Lost(target), "a negative resistance adds damage, penetration leaving it");
            source.SetBase(Stat.SpellPenetration, 0.0f);
            Spell(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(600.0f, Lost(target), "and the same with none");
            target.SetBase(Stat.Resistance(DamageKind.Normal), 50.0f);
            Swing(source, target);
            Expect.Near(650.0f, Lost(target), "resistance to normal damage works on an attack, after armor");
        }

        /// <summary>Area damage reduction takes a percent off the hits marked as an area's, and no other.</summary>
        public static void AreaReduction(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.AreaDamageReduction, 30.0f);
            Spell(source, target, 100.0f, area: true);
            Expect.Near(70.0f, Lost(target), "an area's hit");
            Spell(source, target);
            Expect.Near(170.0f, Lost(target), "a single target's");
        }

        /// <summary>A block at a chance of 100 takes up to its amount off an attack and writes it to Blocked; "block" shows only when nothing is left; a spell is not blocked.</summary>
        public static void Block(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.BlockChance, 100.0f);
            target.SetBase(Stat.BlockAmount, 30.0f);
            var hit = Attack(source, target);
            Expect.Near(30.0f, hit.Blocked, "Blocked");
            Expect.Near(70.0f, Lost(target), "what landed");
            Expect.Equal(0, world.Texts.Count, "no text while something is left");
            hit = Attack(source, target, 20.0f);
            Expect.Near(20.0f, hit.Blocked, "no more than the hit had");
            Expect.Near(70.0f, Lost(target), "nothing landed");
            Expect.Equal(1, world.Texts.Count, "a text");
            Expect.Equal("block", world.Texts[0].Words, "its words");
            StatsSystem.ShowTexts = false;
            Attack(source, target, 20.0f);
            Expect.Equal(1, world.Texts.Count, "none with the texts off");
            StatsSystem.ShowTexts = true;
            Spell(source, target);
            Expect.Near(170.0f, Lost(target), "a spell is not blocked");
            target.SetBase(Stat.BlockChance, 0.0f);
            hit = Attack(source, target);
            Expect.Near(0.0f, hit.Blocked, "a chance of 0 blocks nothing");
        }

        /// <summary>A barrier absorbs after armor and writes what it took to Absorbed; a hit bigger than one barrier goes on to the next; the part no barrier takes lands.</summary>
        public static void BarrierAbsorbs(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var holder = Game.Instance.Add(new StatHolder());
            var lasting = target.AddBarrier(30.0f, holder);
            var timed = target.AddBarrier(30.0f, holder, seconds: 10.0f);
            var sooner = target.AddBarrier(10.0f, holder, seconds: 5.0f);
            Expect.Near(70.0f, target.Get(Stat.Barrier), "Stat.Barrier is the total");
            Expect.Near(30.0f, lasting.Left, "Left");
            var hit = Spell(source, target, 15.0f);
            Expect.Near(15.0f, hit.Absorbed, "10 from the one ending soonest, 5 from the next");
            Expect.True(sooner.HasEnded, "the first ended");
            Expect.Near(25.0f, timed.Left, "the second has 25 left");
            Expect.Near(30.0f, lasting.Left, "the one with no end is untouched");
            Expect.Near(0.0f, Lost(target), "nothing landed");
            Expect.Near(55.0f, target.Get(Stat.Barrier), "Stat.Barrier follows");
            hit = Spell(source, target, 100.0f);
            Expect.Near(55.0f, hit.Absorbed, "the rest of them");
            Expect.Near(45.0f, Lost(target), "and the part past them landed");
        }

        /// <summary>A barrier ends when nothing is left, after its seconds, or with its holder; each time Stat.Barrier follows.</summary>
        public static void BarrierEnds(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var holder = Game.Instance.Add(new StatHolder());
            var used = target.AddBarrier(40.0f, holder);
            Expect.Near(40.0f, target.Get(Stat.Barrier), "Stat.Barrier");
            var hit = Spell(source, target);
            Expect.Near(40.0f, hit.Absorbed, "all it had");
            Expect.Near(60.0f, Lost(target), "the rest landed");
            Expect.True(used.HasEnded && used.Left == 0.0f, "used up, it ended");
            Expect.Near(0.0f, target.Get(Stat.Barrier), "Stat.Barrier is 0");

            var timed = target.AddBarrier(50.0f, holder, seconds: 3.0f);
            world.Advance(2.0f);
            Expect.True(!timed.HasEnded && target.Get(Stat.Barrier) == 50.0f, "it lasts its seconds");
            world.Advance(1.5f);
            Expect.True(timed.HasEnded, "after its seconds it ended");
            Expect.Near(0.0f, target.Get(Stat.Barrier), "Stat.Barrier follows");

            var held = target.AddBarrier(25.0f, holder);
            var other = Game.Instance.Add(new StatHolder());
            var shared = target.AddBarrier(5.0f, other);
            Expect.Near(30.0f, target.Get(Stat.Barrier), "two holders' barriers add up");
            other.End();
            Expect.True(shared.HasEnded && !held.HasEnded, "its holder's end ended only its own");
            Expect.Near(25.0f, target.Get(Stat.Barrier), "Stat.Barrier follows");
            held.End();
            Expect.Near(0.0f, target.Get(Stat.Barrier), "End by hand too");
            var late = target.AddBarrier(10.0f, other);
            Expect.True(late.HasEnded, "a holder that has ended holds nothing");
            Expect.True(target.AddBarrier(0.0f, holder).HasEnded, "an amount of 0 is no barrier");
            Expect.Near(0.0f, target.Get(Stat.Barrier), "and the stat stays 0");
        }

        /// <summary>Barriers of one unit absorb in order, those that end by themselves soonest first and the rest oldest first, and the unit's change reaction hears the total move.</summary>
        public static void BarrierOrder(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var holder = Game.Instance.Add(new StatHolder());
            var log = target.Add(new StatChangeLog());
            var first = target.AddBarrier(30.0f, holder);
            var late = target.AddBarrier(30.0f, holder, seconds: 10.0f);
            var soon = target.AddBarrier(30.0f, holder, seconds: 4.0f);
            var second = target.AddBarrier(30.0f, holder);
            Expect.Equal("Barrier 0 to 30|Barrier 30 to 60|Barrier 60 to 90|Barrier 90 to 120", string.Join("|", log.Lines), "the total as barriers are added");
            var hit = Spell(source, target, 50.0f);
            Expect.Near(50.0f, hit.Absorbed, "all of the hit");
            Expect.True(soon.HasEnded && soon.Left == 0.0f, "the one ending in 4 s took 30 and ended");
            Expect.Near(10.0f, late.Left, "the one ending in 10 s took the other 20");
            Expect.Near(30.0f, first.Left, "the ones with no end are untouched");
            Expect.Near(30.0f, second.Left, "both");
            Expect.Near(70.0f, target.Get(Stat.Barrier), "Stat.Barrier");
            Expect.Near(0.0f, Lost(target), "nothing landed");
            Expect.Equal("Barrier 120 to 70", log.Lines[log.Lines.Count - 1], "the last change heard");
        }

        /// <summary>Stagger takes a share of what is left after resistance, block and barriers and deals it in even ticks every half second, the last taking the rest; the ticks are no one's crit, resistance or life steal.</summary>
        public static void Stagger(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            StatsSystem.ShowTexts = false;
            source.SetBase(Stat.CritChance, 100.0f);
            target.SetBase(Stat.Stagger, 50.0f);
            target.SetBase(Stat.StaggerTime, 2.0f);
            target.SetBase(Stat.Resistance(DamageKind.Universal), 50.0f);
            var hit = Spell(source, target, 100.0f);
            Expect.Near(100.0f, hit.Amount, "the hit of 100, doubled by the crit, 100 of it taken off");
            Expect.Near(100.0f, Lost(target), "half of 200 lands now");
            for (var i = 1; i <= 4; i++)
            {
                world.Advance(0.5f);
                fake.RunZero();
                Expect.Near(100.0f + 25.0f * i, Lost(target), "after tick " + i + " of 25");
            }

            world.Advance(5.0f);
            fake.RunZero();
            Expect.Near(200.0f, Lost(target), "no more after the four ticks");
            StatsSystem.ShowTexts = true;
        }

        /// <summary>Ticks of a staggered hit add up to the amount staggered whatever the time: three ticks for 1.3 s, the last taking the rest, and a unit that dies is dealt no more.</summary>
        public static void StaggerTicks(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            target.SetBase(Stat.Stagger, 100.0f);
            target.SetBase(Stat.StaggerTime, 1.3f);
            Spell(source, target, 100.0f);
            Expect.Near(0.0f, Lost(target), "all of it is put off");
            var seen = new List<float>();
            for (var i = 0; i < 6; i++)
            {
                world.Advance(0.5f);
                fake.RunZero();
                seen.Add(Lost(target));
            }

            Expect.Near(33.3333f, seen[0], "tick one");
            Expect.Near(66.6667f, seen[1], "tick two");
            Expect.Near(100.0f, seen[2], "tick three, the last, takes the rest");
            Expect.Near(100.0f, seen[5], "and no tick after it");

            target.SetBase(Stat.StaggerTime, 2.0f);
            Spell(source, target, 100.0f);
            fake.Body(target.Handle).Life = 10.0f;
            world.Advance(0.5f);
            fake.RunZero();
            Expect.True(!target.IsAlive, "a tick that lands on 10 life kills");
            world.Advance(3.0f);
            fake.RunZero();
            Expect.True(!target.IsAlive, "and nothing more is dealt to the dead");
        }

        /// <summary>The order after armor is resistance, block, barrier, stagger, each on what the one before left: 100 fire, 50 resistance, a block of 10, a barrier of 20, half staggered.</summary>
        public static void OrderAfterArmor(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            var holder = Game.Instance.Add(new StatHolder());
            target.SetBase(Stat.Resistance(DamageKind.Fire), 50.0f);
            target.SetBase(Stat.BlockChance, 100.0f);
            target.SetBase(Stat.BlockAmount, 10.0f);
            target.AddBarrier(20.0f, holder);
            target.SetBase(Stat.Stagger, 50.0f);
            var hit = Attack(source, target, 100.0f, DamageKind.Fire);
            Expect.Near(10.0f, hit.Blocked, "blocked 10 of the 50");
            Expect.Near(20.0f, hit.Absorbed, "absorbed 20 of the 40");
            Expect.Near(10.0f, hit.Amount, "half of the 20 left");
            Expect.Near(10.0f, Lost(target), "landed now");
            world.Advance(5.0f);
            fake.RunZero();
            Expect.Near(20.0f, Lost(target), "the other 10 over the four seconds");
        }

        /// <summary>Before armor the order is miss, parry, powers, crit, penetration: a crit's amount carries the attack power, and a hit that is missed is never a crit.</summary>
        public static void OrderBeforeArmor(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.SetBase(Stat.CritChance, 100.0f);
            source.SetBase(Stat.AttackPower, 50.0f);
            source.SetBase(Stat.ArmorPenetration, 5.0f);
            fake.Body(target.Handle).Armor = 5.0f;
            var hit = Attack(source, target, 100.0f, DamageKind.Normal);
            Expect.True(hit.IsCrit, "crit");
            Expect.Near(0.0f, hit.Armor, "the armor the hit met");
            Expect.Near(300.0f, hit.Amount, "(100 + 50) x 2, armor 0");
            Expect.Near(5.0f, fake.Body(target.Handle).Armor, "the target's own armor is back");
        }

        /// <summary>Life steal heals the source of an attack by a share of the landed amount, and mana leech gives a share as mana; a spell gives neither; the steal's own heal gets no healing bonus or crit.</summary>
        public static void StealAndLeech(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.MaxLife = 1000.0f;
            source.Life = 500.0f;
            source.MaxMana = 100.0f;
            source.Mana = 10.0f;
            source.SetBase(Stat.LifeSteal, 20.0f);
            source.SetBase(Stat.ManaLeech, 5.0f);
            source.SetBase(Stat.HealingBonus, 100.0f);
            source.SetBase(Stat.CritChance, 100.0f);
            var log = source.Add(new HealLog());
            Swing(source, target);
            Expect.Near(200.0f, Lost(target), "a crit of 200 landed");
            Expect.Equal(1, log.Count, "one heal");
            Expect.Near(40.0f, log.Done, "of 20 percent of 200, with no healing bonus and no crit on it");
            Expect.Near(540.0f, source.Life, "the life");
            Expect.Near(20.0f, source.Mana, "5 percent of 200 as mana");
            Spell(source, target);
            Expect.Near(540.0f, source.Life, "a spell steals nothing");
            Expect.Near(20.0f, source.Mana, "and leeches nothing");
            source.SetBase(Stat.LifeSteal, 0.0f);
            source.SetBase(Stat.ManaLeech, 0.0f);
            Swing(source, target);
            Expect.Near(540.0f, source.Life, "no life steal, no life");
        }

        /// <summary>Healing bonus raises the heals a unit gives, and a crit doubles them with the mark; a heal the package makes itself gets neither; a heal given with no stat is whole.</summary>
        public static void HealingBonusAndCrit(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            patient.MaxLife = 1000.0f;
            patient.Life = 100.0f;
            var plain = healer.Heal(patient, 50.0f);
            Expect.Near(50.0f, plain!.Amount, "no stat, no change");
            healer.SetBase(Stat.HealingBonus, 50.0f);
            var bonus = healer.Heal(patient, 50.0f);
            Expect.Near(75.0f, bonus!.Amount, "50 percent more");
            Expect.True(!bonus.IsCrit, "no crit");
            Expect.Near(225.0f, patient.Life, "the life");
            healer.SetBase(Stat.CritChance, 100.0f);
            var crit = healer.Heal(patient, 50.0f);
            Expect.Near(150.0f, crit!.Amount, "a crit of x 2 on 75");
            Expect.True(crit.IsCrit, "marked");
            healer.SetBase(Stat.HealingBonus, -100.0f);
            healer.SetBase(Stat.CritChance, 0.0f);
            Expect.Near(0.0f, healer.Heal(patient, 50.0f)!.Amount, "healing bonus -100 heals nothing");
        }

        /// <summary>A hit that starts at 0 gets none of the effects.</summary>
        public static void ZeroHitGetsNothing(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            source.SetBase(Stat.AttackPower, 40.0f);
            source.SetBase(Stat.CritChance, 100.0f);
            var hit = Attack(source, target, 0.0f);
            Expect.Near(0.0f, hit.Amount, "still 0");
            Expect.True(!hit.IsCrit, "no crit");
            Expect.Near(0.0f, Lost(target), "no life lost");
            Expect.Equal(0, world.Texts.Count, "no text");
        }

        /// <summary>With the texts off a miss still misses, with no text, and a block that leaves nothing shows none.</summary>
        public static void TextsOff(FakeWorld world)
        {
            Arena(world, out var source, out var target);
            StatsSystem.ShowTexts = false;
            target.SetBase(Stat.Evasion, 100.0f);
            var hit = Attack(source, target);
            Expect.True(hit.Missed, "it missed");
            Expect.Equal(0, world.Texts.Count, "with no text");
            target.SetBase(Stat.Evasion, 0.0f);
            target.SetBase(Stat.Parry, 100.0f);
            fake.Body(source.Handle).Melee = true;
            hit = Attack(source, target);
            Expect.True(hit.Missed, "a parry too, from where the unit stands");
            Expect.Equal(0, world.Texts.Count, "with no text");
            StatsSystem.ShowTexts = true;
        }
    }

    /// <summary>A unit component that adds up the heals its unit gives, to show what the package's own heals do.</summary>
    public sealed class HealLog : UnitComponent, IHealSource
    {
        /// <summary>How many heals the unit gave.</summary>
        public int Count;

        /// <summary>The life that went in, added up.</summary>
        public float Done;

        /// <summary>Counts the heal.</summary>
        public void OnHealed(Heal heal)
        {
            Count++;
            Done += heal.Done;
        }
    }
}
