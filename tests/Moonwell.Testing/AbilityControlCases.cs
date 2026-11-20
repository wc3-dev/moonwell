using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A game component that holds modifiers, ended by the case.</summary>
    public sealed class ControlHolder : GameComponent
    {
    }

    /// <summary>A buff that stuns for 10 seconds: crowd control.</summary>
    public sealed class StunBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public StunBuff()
        {
            Duration = 10.0f;
            IsCrowdControl = true;
        }
    }

    /// <summary>A crowd control buff that lasts until it is removed.</summary>
    public sealed class RootBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public RootBuff()
        {
            IsCrowdControl = true;
        }
    }

    /// <summary>A crowd control buff that keeps the strongest of its applies, for the strength and seconds given.</summary>
    public sealed class DazeBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public DazeBuff(float strength, float seconds)
        {
            Stacking = Stacking.Strongest;
            Strength = strength;
            Duration = seconds;
            IsCrowdControl = true;
        }
    }

    /// <summary>A harmful buff that is not crowd control, lasting 10 seconds.</summary>
    public sealed class RotBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public RotBuff()
        {
            Duration = 10.0f;
            IsHarmful = true;
        }
    }

    /// <summary>A buff that is neither harmful nor crowd control.</summary>
    public sealed class BlessBuff : Buff
    {
    }

    /// <summary>A buff with an icon, harmful only.</summary>
    public sealed class RotIconBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public RotIconBuff()
        {
            IsHarmful = true;
            HudIcon = @"ReplaceableTextures\CommandButtons\BTNPoisonCloud.blp";
        }
    }

    /// <summary>A buff with an icon, neither harmful nor crowd control.</summary>
    public sealed class BlessIconBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public BlessIconBuff()
        {
            HudIcon = @"ReplaceableTextures\CommandButtons\BTNHeal.blp";
        }
    }

    /// <summary>A crowd control buff with an icon.</summary>
    public sealed class StunIconBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public StunIconBuff()
        {
            IsCrowdControl = true;
            HudIcon = @"ReplaceableTextures\CommandButtons\BTNStun.blp";
        }
    }

    /// <summary>A crowd control buff a spell lists, which the game puts on and Moonwell watches for.</summary>
    public sealed class ChilledBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public ChilledBuff()
        {
            IsCrowdControl = true;
        }
    }

    /// <summary>Cases for the abilities of a unit, the live numbers an ability keeps a base for, attack range as a stat, an owned damage in a simulated swing, the buff marks and the crowd control reduction.</summary>
    public static class AbilityControlCases
    {
        private static readonly int Slow = ObjectTypes.FourCC("Aslo");
        private static readonly int Curse = ObjectTypes.FourCC("Acrs");
        private static readonly int Fireball = ObjectTypes.FourCC("Afbk");
        private static readonly int Chill = ObjectTypes.FourCC("B030");
        private static readonly int ChillCarrier = ObjectTypes.FourCC("A031");
        private static readonly int ChillSpell = ObjectTypes.FourCC("A030");
        private static readonly int QuietSpell = ObjectTypes.FourCC("A032");
        private static readonly int RotMark = ObjectTypes.FourCC("B0cb");
        private static readonly int RotCarrier = ObjectTypes.FourCC("A0cb");

        private static Unit Footman(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
        }

        /// <summary>Gives a unit an ability with a cooldown, a mana cost and a cast range for each of two levels, as the game's own copy has.</summary>
        private static Ability Given(FakeWorld world, Unit unit)
        {
            world.SetLevelCount(Slow, 3);
            world.SetAbilityLevel(unit.Handle, Slow, 1);
            world.SetAbilityCooldown(unit.Handle, Slow, 0, 10.0f);
            world.SetAbilityCooldown(unit.Handle, Slow, 1, 20.0f);
            world.SetAbilityManaCost(unit.Handle, Slow, 0, 50);
            world.SetAbilityManaCost(unit.Handle, Slow, 1, 80);
            world.SetAbilityRange(unit.Handle, Slow, 0, 600.0f);
            world.SetAbilityRange(unit.Handle, Slow, 1, 700.0f);
            return unit.AbilityOf(Slow);
        }

        /// <summary>A unit's abilities are the objects AbilityOf gives, read from the game: Moonwell's own helpers (the stat bonus abilities, a buff's carrier and game buff, a game buff a class watches) are left out, an ability given or taken shows at once, and a list read before is a copy.</summary>
        public static void AbilitiesList(FakeWorld world)
        {
            var unit = Footman(world);
            Expect.Equal(0, unit.LiveAbilities.Count, "a unit with none");
            world.SetAbilityLevel(unit.Handle, Slow, 1);
            Expect.Equal(1, unit.LiveAbilities.Count, "one the game gave");
            Expect.Equal(Slow, unit.LiveAbilities[0].Id, "its id");
            Expect.Same(unit.AbilityOf(Slow), unit.LiveAbilities[0], "the object AbilityOf gives");

            var holder = Game.Instance.Add(new ControlHolder());
            unit.Modify(Stat.Armor, 5.0f, heldBy: holder);
            unit.Modify(Stat.Damage, 5.0f, heldBy: holder);
            unit.Modify(Stat.AttackSpeed, 10.0f, heldBy: holder);
            Expect.Equal(1, world.AbilityLevel(unit.Handle, ObjectTypes.FourCC("AMWd")), "the damage bonus is on the unit");
            ObjectTypes.BuffDisplay<BlessBuff>(RotMark, RotCarrier);
            unit.Apply(new BlessBuff());
            world.SetAbilityLevel(unit.Handle, RotMark, 1);
            Expect.Equal(1, world.AbilityLevel(unit.Handle, RotCarrier), "the buff's carrier is on the unit");
            SpellBuffCases.Watch();
            world.SetAbilityLevel(unit.Handle, SpellBuffCases.Frozen, 1);
            Expect.Equal(1, unit.LiveAbilities.Count, "the bonus abilities, the carrier and the game buffs are left out");

            unit.AddAbility(Curse);
            Expect.Equal(2, unit.LiveAbilities.Count, "an ability added");
            Expect.Equal(Curse, unit.LiveAbilities[1].Id, "after the one the unit had");
            var before = unit.LiveAbilities;
            unit.RemoveAbility(Slow);
            unit.AddAbility(Fireball);
            Expect.Equal(2, before.Count, "a list read before does not change");
            Expect.Equal(Slow, before[0].Id, "it still has what it had");
            Expect.Equal(2, unit.LiveAbilities.Count, "one taken and one given");
            Expect.Equal(Curse, unit.LiveAbilities[0].Id, "the ability left");
            Expect.Equal(Fireball, unit.LiveAbilities[1].Id, "the one added");
            unit.Remove();
            Expect.Equal(0, unit.LiveAbilities.Count, "empty once the unit is gone");
        }

        /// <summary>The notice for a stats package: an ability's object first made, AddAbility, RemoveAbility, a level set, and a hero learning a skill each tell it which unit changed; reading again changes nothing.</summary>
        public static void AbilitiesNotice(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H0lv"), () => new LevelHero());
            ObjectTypes.Ability(ObjectTypes.FourCC("A0gv"), () => new GivenAbility());
            world.SetLevelCount(ObjectTypes.FourCC("A0gv"), 3);
            world.MarkHeroAbility(ObjectTypes.FourCC("A0gv"));
            GivenAbility.LowerAt = 0;
            LevelHero.RemoveAt = 0;
            LevelHero.RaiseAt = 0;
            var unit = Footman(world);
            var heard = new List<Unit>();
            Events.AbilitiesListener = changed => heard.Add(changed);
            unit.AbilityOf(Slow);
            Expect.Equal(1, heard.Count, "an object first made");
            Expect.Same(unit, heard[0], "for the unit");
            unit.AbilityOf(Slow);
            Expect.Equal(1, heard.Count, "not made again");
            heard.Clear();
            unit.AddAbility(Slow);
            Expect.Equal(1, heard.Count, "an ability added");
            heard.Clear();
            unit.AddAbility(Curse);
            Expect.True(heard.Count >= 1, "an ability added whose object is new");
            heard.Clear();
            var listed = unit.LiveAbilities;
            Expect.Equal(2, listed.Count, "both abilities listed");
            Expect.Equal(0, heard.Count, "a read of the list makes nothing new");
            unit.AbilityOf(Slow).Level = 2;
            Expect.Equal(1, heard.Count, "a level set");
            heard.Clear();
            unit.RemoveAbility(Slow);
            Expect.Equal(1, heard.Count, "an ability removed");
            heard.Clear();
            var hero = (LevelHero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("H0lv"), slot: 0, hero: true))!;
            hero.AbilityOf(ObjectTypes.FourCC("A0gv"));
            heard.Clear();
            world.SetSkillPoints(hero.Handle, 1);
            Expect.True(world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0gv")), "the hero learns the skill");
            Expect.Equal(1, heard.Count, "a hero learning a skill");
            Expect.Same(hero, heard[0], "for the hero");
        }

        /// <summary>The three live numbers keep the ability's own number for each level as a base: a scale multiplies the base into what the game gets, set again it does not compound, a map's own set becomes the base, and a level has its own.</summary>
        public static void LiveNumbersKeepABase(FakeWorld world)
        {
            var unit = Footman(world);
            var ability = Given(world, unit);
            Expect.Near(10.0f, ability.LiveCooldown, "the game's cooldown");
            Expect.Equal(50, ability.LiveManaCost, "the game's mana cost");
            Expect.Near(600.0f, ability.LiveRange, "the game's cast range");

            ability.CooldownScale = 0.8f;
            Expect.Near(8.0f, ability.LiveCooldown, "20 percent off");
            ability.CooldownScale = 0.8f;
            Expect.Near(8.0f, ability.LiveCooldown, "set again, it does not compound");
            ability.CooldownScale = 0.5f;
            Expect.Near(5.0f, ability.LiveCooldown, "a new scale works on the base");
            ability.ManaCostScale = 0.5f;
            Expect.Equal(25, ability.LiveManaCost, "half the mana");
            ability.ManaCostScale = 0.3f;
            Expect.Equal(15, ability.LiveManaCost, "a whole number");
            ability.RangeScale = 1.25f;
            Expect.Near(750.0f, ability.LiveRange, "a quarter more range");
            ability.RangeScale = 1.0f;
            Expect.Near(600.0f, ability.LiveRange, "and back to the base");

            ability.LiveCooldown = 12.0f;
            Expect.Near(6.0f, ability.LiveCooldown, "a set is the new base, still scaled");
            ability.CooldownScale = 1.0f;
            Expect.Near(12.0f, ability.LiveCooldown, "and it is what the scale starts from");
            ability.LiveRange = 900.0f;
            ability.LiveManaCost = 40;
            ability.RangeScale = 0.5f;
            ability.ManaCostScale = 0.5f;
            Expect.Near(450.0f, ability.LiveRange, "a set range is a base");
            Expect.Equal(20, ability.LiveManaCost, "a set mana cost is a base");

            ability.CooldownScale = 0.5f;
            ability.Level = 2;
            Expect.Near(20.0f, ability.LiveCooldown, "a level of its own, not scaled until the scale is put again");
            ability.CooldownScale = 0.5f;
            ability.RangeScale = 0.5f;
            ability.ManaCostScale = 0.5f;
            Expect.Near(10.0f, ability.LiveCooldown, "the new level scaled");
            Expect.Near(350.0f, ability.LiveRange, "its range");
            Expect.Equal(40, ability.LiveManaCost, "its mana cost");
            ability.Level = 1;
            Expect.Near(6.0f, ability.LiveCooldown, "the first level as it was left");
            ability.CooldownScale = 1.0f;
            Expect.Near(12.0f, ability.LiveCooldown, "scaled again from the base the map set");
        }

        /// <summary>A scale on an ability the unit lacks reaches nothing in the game, and one the unit is given after is not scaled until the scale is put again.</summary>
        public static void LiveNumbersNeedTheAbility(FakeWorld world)
        {
            var unit = Footman(world);
            var ability = unit.AbilityOf(Slow);
            ability.CooldownScale = 0.5f;
            ability.RangeScale = 0.5f;
            Expect.Near(0.0f, world.AbilityCooldown(unit.Handle, Slow, 0), "nothing set in the game");
            Expect.Near(0.0f, ability.LiveCooldown, "reads 0 without the ability");
            Expect.Near(0.0f, ability.LiveRange, "and so does the range");
            world.SetAbilityCooldown(unit.Handle, Slow, 0, 10.0f);
            unit.AddAbility(Slow);
            Expect.Near(10.0f, ability.LiveCooldown, "the game's own after it is given");
            var kept = ability.CooldownScale;
            ability.CooldownScale = kept;
            Expect.Near(5.0f, ability.LiveCooldown, "the kept scale put again");
        }

        /// <summary>Attack range as a stat: a flat and a percent raise it, a negative lowers it, taking them off puts it back, the acquisition range goes up with it and comes back, and it never goes below 0.</summary>
        public static void AttackRange(FakeWorld world)
        {
            var unit = Footman(world);
            world.SetAttackRange(unit.Handle, 100.0f);
            world.SetAcquireRange(unit.Handle, 200.0f);
            var holder = Game.Instance.Add(new ControlHolder());
            Expect.Equal("Attack range", Stat.AttackRange.Name, "its name");
            Expect.Same(Stat.AttackRange, Stat.Named("Attack range"), "found by name");
            Expect.Near(100.0f, unit.Get(Stat.AttackRange), "the weapon's range");

            var flat = unit.Modify(Stat.AttackRange, 300.0f, heldBy: holder);
            Expect.Near(400.0f, world.AttackRangeOf(unit.Handle), "the game's range");
            Expect.Near(400.0f, unit.Get(Stat.AttackRange), "the total");
            Expect.Near(100.0f, unit.GetBase(Stat.AttackRange), "the base is the weapon's range less Moonwell's part");
            Expect.Near(300.0f, unit.GetBonus(Stat.AttackRange), "the bonus");
            Expect.Near(400.0f, world.AcquireRangeOf(unit.Handle), "the acquisition range goes up to it");
            flat.End();
            Expect.Near(100.0f, world.AttackRangeOf(unit.Handle), "back to the weapon's");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "and the unit's own acquisition range is back");

            var percent = unit.Modify(Stat.AttackRange, holder, percent: 50.0f);
            Expect.Near(150.0f, world.AttackRangeOf(unit.Handle), "a percent");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "under the acquisition range, which stays");
            var more = unit.Modify(Stat.AttackRange, 100.0f, heldBy: holder);
            Expect.Near(300.0f, world.AttackRangeOf(unit.Handle), "flat and percent: (100 + 100) x 1.5");
            Expect.Near(300.0f, world.AcquireRangeOf(unit.Handle), "above it, which follows");
            percent.End();
            Expect.Near(200.0f, world.AttackRangeOf(unit.Handle), "the percent off");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "the acquisition range put back where it was");
            more.End();
            Expect.Near(100.0f, world.AttackRangeOf(unit.Handle), "the flat off");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "and still the unit's own");

            var less = unit.Modify(Stat.AttackRange, -30.0f, heldBy: holder);
            Expect.Near(70.0f, world.AttackRangeOf(unit.Handle), "a negative lowers it");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "the acquisition range is not lowered");
            less.End();
            unit.Modify(Stat.AttackRange, -500.0f, heldBy: holder);
            Expect.Near(0.0f, world.AttackRangeOf(unit.Handle), "never below 0");
            Expect.Near(0.0f, unit.Get(Stat.AttackRange), "in the total too");
        }

        /// <summary>A base set for the unit makes Moonwell own its attack range: the game gets the base and the modifiers whatever the weapon had, and a new base is put as a new total.</summary>
        public static void AttackRangeOwned(FakeWorld world)
        {
            var unit = Footman(world);
            world.SetAttackRange(unit.Handle, 100.0f);
            world.SetAcquireRange(unit.Handle, 200.0f);
            var holder = Game.Instance.Add(new ControlHolder());
            unit.SetBase(Stat.AttackRange, 500.0f);
            Expect.Near(500.0f, world.AttackRangeOf(unit.Handle), "the game has Moonwell's base");
            Expect.Near(500.0f, unit.Get(Stat.AttackRange), "Get is Moonwell's total");
            Expect.Near(500.0f, world.AcquireRangeOf(unit.Handle), "the acquisition range follows");
            unit.Modify(Stat.AttackRange, 50.0f, heldBy: holder);
            Expect.Near(550.0f, world.AttackRangeOf(unit.Handle), "with a modifier");
            Expect.Near(500.0f, unit.GetBase(Stat.AttackRange), "the base stays Moonwell's");
            unit.SetBase(Stat.AttackRange, 150.0f);
            Expect.Near(200.0f, world.AttackRangeOf(unit.Handle), "a new base");
            Expect.Near(200.0f, world.AcquireRangeOf(unit.Handle), "and the acquisition range comes back to the unit's own");
        }

        /// <summary>A played unit's reach follows the stat: a unit of range 100 given +200 stops its chase 300 from its target, and one idle with an acquisition range of 150 acquires an enemy 300 away once the range, and with it the acquisition range, are 400.</summary>
        public static void SimulationAttackRange(FakeWorld world)
        {
            var sim = Simulation.Start();
            var type = ObjectTypes.FourCC("s0ar");
            sim.SetNumbers(type, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 100.0f, AttackCooldown = 1.0f, Damage = 10, AcquisitionRange = 150.0f });
            var dummy = ObjectTypes.FourCC("s0av");
            sim.SetNumbers(dummy, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            var attacker = Unit.Create(type, Player.One, 0.0f, 0.0f, Game.Instance);
            var target = Unit.Create(dummy, Player.Two, 400.0f, 0.0f, Game.Instance);
            attacker.Modify(Stat.AttackRange, 200.0f, heldBy: attacker);
            attacker.OrderOn("attack", target);
            sim.Run(2.0f);
            Expect.Near(100.0f, attacker.X, "it stopped 300 from the target");
            Expect.True(target.Life < 100.0f, "and swung from there");

            var guard = Unit.Create(type, Player.One, 0.0f, 2000.0f, Game.Instance);
            var near = Unit.Create(dummy, Player.Two, 300.0f, 2000.0f, Game.Instance);
            sim.Run(2.0f);
            Expect.Near(100.0f, near.Life, "an enemy 300 away is past its acquisition range");
            guard.Modify(Stat.AttackRange, 300.0f, heldBy: guard);
            sim.Run(2.0f);
            Expect.True(near.Life < 100.0f, "with the range at 400 and the acquisition range with it, it attacks");
        }

        /// <summary>An owned damage reaches a simulated swing: a hitter of 10 given a base of 30 hits for 30, as Get says, and 40 with a modifier of 10, where an owned stat is the number the game has.</summary>
        public static void SimulationOwnedDamage(FakeWorld world)
        {
            var sim = Simulation.Start();
            var hitter = ObjectTypes.FourCC("s0oh");
            var victim = ObjectTypes.FourCC("s0ov");
            sim.SetNumbers(hitter, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 1.0f, Damage = 10 });
            sim.SetNumbers(victim, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f });
            var plain = Unit.Create(hitter, Player.One, 0.0f, 0.0f, Game.Instance);
            var plainVictim = Unit.Create(victim, Player.Two, 100.0f, 0.0f, Game.Instance);
            var owner = Unit.Create(hitter, Player.One, 0.0f, 1000.0f, Game.Instance);
            var ownedVictim = Unit.Create(victim, Player.Two, 100.0f, 1000.0f, Game.Instance);
            owner.SetBase(Stat.Damage, 30.0f);
            Expect.Near(30.0f, owner.Get(Stat.Damage), "Get is the base");
            plain.OrderOn("attack", plainVictim);
            owner.OrderOn("attack", ownedVictim);
            sim.Run(0.2f);
            Expect.Near(990.0f, plainVictim.Life, "the plain hitter swings for 10");
            Expect.Near(970.0f, ownedVictim.Life, "the owned one for 30, as Get says");
            owner.Modify(Stat.Damage, 10.0f, heldBy: owner);
            Expect.Near(40.0f, owner.Get(Stat.Damage), "a modifier on the base");
            sim.Run(1.0f);
            Expect.Near(930.0f, ownedVictim.Life, "the next swing is 40");
        }

        /// <summary>The buff marks are set in a class's constructor: harmful, crowd control (which reads harmful too) and neither.</summary>
        public static void BuffMarks(FakeWorld world)
        {
            var rot = new RotBuff();
            Expect.True(rot.IsHarmful && !rot.IsCrowdControl, "a harmful buff");
            var stun = new StunBuff();
            Expect.True(stun.IsCrowdControl && stun.IsHarmful, "crowd control reads harmful too");
            var bless = new BlessBuff();
            Expect.True(!bless.IsHarmful && !bless.IsCrowdControl, "neither");
        }

        /// <summary>A buff row shows every buff with an icon, or only the harmful ones, or only the others.</summary>
        public static void BuffRowHarmful(FakeWorld world)
        {
            FrameCases.Installed();
            var unit = Footman(world);
            var row = Screen.Add(new BuffRow(), Game.Instance);
            unit.Apply(new BlessIconBuff());
            unit.Apply(new RotIconBuff());
            unit.Apply(new StunIconBuff());
            row.Show(unit);
            Expect.Equal(3, row.slots.Count, "all three with no filter");
            Expect.True(row.slots[2].Buff != null, "the third is shown");
            row.Show(unit, true);
            Expect.True(row.slots[0].Buff is RotIconBuff, "the harmful first");
            Expect.True(row.slots[1].Buff is StunIconBuff, "crowd control is harmful");
            Expect.True(row.slots[2].Buff == null, "and no third");
            row.Show(unit, false);
            Expect.True(row.slots[0].Buff is BlessIconBuff, "the others");
            Expect.True(row.slots[1].Buff == null, "and nothing else");
            row.Show(unit);
            Expect.True(row.slots[2].Buff != null, "no filter shows all again");
        }

        /// <summary>A crowd control buff lasts the unit's reduction shorter from its apply, from a refresh with the reduction then, and not at all when it is not crowd control or lasts until removed.</summary>
        public static void CrowdControlOwnBuff(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new ControlHolder());
            var reduction = unit.Modify(Stat.CrowdControlReduction, 50.0f, heldBy: holder);
            var stun = unit.Apply(new StunBuff());
            Expect.Near(5.0f, stun.Remaining, "10 seconds, 50 percent shorter");
            Expect.Near(10.0f, stun.Duration, "its Duration is the class's");
            world.Advance(2.0f);
            reduction.Amount = 75.0f;
            unit.Apply(new StunBuff());
            Expect.Near(2.5f, stun.Remaining, "a refresh shortens by the reduction then");
            world.Advance(2.4f);
            Expect.True(!stun.HasEnded, "still on");
            world.Advance(0.2f);
            Expect.True(stun.HasEnded, "ended after 2.5");

            var root = unit.Apply(new RootBuff());
            world.Advance(100.0f);
            Expect.True(!root.HasEnded, "a duration of 0 stays until removed");
            Expect.Near(0.0f, root.Remaining, "with nothing left to tell");
            var rot = unit.Apply(new RotBuff());
            Expect.Near(10.0f, rot.Remaining, "a buff that is not crowd control is not shortened");

            reduction.Amount = 100.0f;
            var gone = unit.Apply(new StunBuff());
            world.Advance(0.1f);
            Expect.True(gone.HasEnded, "a reduction of 100 ends it at once");
            reduction.End();
            var whole = unit.Apply(new StunBuff());
            Expect.Near(10.0f, whole.Remaining, "no reduction, no change");
        }

        /// <summary>A strongest crowd control buff shortens each apply's own time: the first and one joined later, each by the reduction when it came.</summary>
        public static void CrowdControlStrongest(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new ControlHolder());
            var reduction = unit.Modify(Stat.CrowdControlReduction, 50.0f, heldBy: holder);
            var first = unit.Apply(new DazeBuff(1.0f, 10.0f));
            Expect.Near(5.0f, first.Remaining, "the first apply, 50 percent shorter");
            reduction.Amount = 25.0f;
            unit.Apply(new DazeBuff(0.5f, 20.0f));
            Expect.Near(15.0f, first.Remaining, "a weaker apply joined, 20 seconds less 25 percent");
            world.Advance(5.1f);
            Expect.True(!first.HasEnded, "the strongest's time is up, and the joined apply keeps it on");
            world.Advance(10.0f);
            Expect.True(first.HasEnded, "ended with its last apply");
        }

        /// <summary>Registers ChilledBuff as the build does for a class that two spells list, and begins the watch.</summary>
        private static void WatchChill()
        {
            ObjectTypes.BuffDisplay<ChilledBuff>(Chill, ChillCarrier);
            ObjectTypes.WatchedBuff<ChilledBuff>(Chill, () => new ChilledBuff());
            ObjectTypes.ListsBuff(ChillSpell, Chill, false);
            ObjectTypes.ListsBuff(QuietSpell, Chill, false);
            SpellBuffs.Begin();
        }

        /// <summary>A crowd control game buff a spell lists is taken off early when the unit has a reduction: at the spell's duration for the level, a hero's or another unit's, shortened by it; given again it starts anew; with no reduction, no duration read or no unit that gave it, the game's own end stands.</summary>
        public static void CrowdControlGameBuff(FakeWorld world)
        {
            WatchChill();
            ObjectTypes.Unit(ObjectTypes.FourCC("H0lv"), () => new LevelHero());
            world.Dummies.Durations(ChillSpell, 8.0f, 4.0f);
            var holder = Game.Instance.Add(new ControlHolder());
            var caster = Footman(world);
            world.SetAbilityLevel(caster.Handle, ChillSpell, 2);
            world.SetAbilityLevel(caster.Handle, QuietSpell, 1);
            var target = Footman(world);
            target.Modify(Stat.CrowdControlReduction, 50.0f, heldBy: holder);
            var bare = Footman(world);

            world.GameBuffs.Hit(caster.Handle, target.Handle, Chill);
            world.GameBuffs.Hit(caster.Handle, bare.Handle, Chill);
            world.Advance(0.01f);
            var buff = target.Get<ChilledBuff>()!;
            Expect.Equal(2, buff.Level, "the spell's level");
            Expect.True(buff.Remaining > 3.9f && buff.Remaining <= 4.0f, "the spell's 8 seconds, 50 percent shorter: " + buff.Remaining);
            world.Advance(3.5f);
            Expect.Equal(1, world.AbilityLevel(target.Handle, Chill), "the game's buff is still on");
            world.Advance(2.0f);
            Expect.Equal(0, world.AbilityLevel(target.Handle, Chill), "the game's buff is taken off at 4");
            Expect.True(buff.HasEnded, "and the object ended");
            Expect.True(bare.Has<ChilledBuff>(), "a unit with no reduction keeps it for the game to end");

            world.GameBuffs.Hit(caster.Handle, target.Handle, Chill);
            world.Advance(2.0f);
            world.GameBuffs.Hit(caster.Handle, target.Handle, Chill);
            world.Advance(3.9f);
            Expect.Equal(1, world.AbilityLevel(target.Handle, Chill), "given again, it starts anew");
            world.Advance(0.3f);
            Expect.Equal(0, world.AbilityLevel(target.Handle, Chill), "and ends 4 seconds after");

            var hero = Unit.Of(world.NewUnit(ObjectTypes.FourCC("H0lv"), slot: 0, hero: true))!;
            hero.Modify(Stat.CrowdControlReduction, 50.0f, heldBy: holder);
            world.GameBuffs.Hit(caster.Handle, hero.Handle, Chill);
            world.Advance(0.01f);
            var heroLeft = hero.Get<ChilledBuff>()!.Remaining;
            Expect.True(heroLeft > 1.9f && heroLeft <= 2.0f, "a hero's duration of 4, shortened: " + heroLeft);

            var silent = Footman(world);
            world.SetAbilityLevel(silent.Handle, QuietSpell, 1);
            var quiet = Footman(world);
            quiet.Modify(Stat.CrowdControlReduction, 50.0f, heldBy: holder);
            world.GameBuffs.Hit(silent.Handle, quiet.Handle, Chill);
            world.Advance(0.01f);
            world.Advance(30.0f);
            Expect.Equal(1, world.AbilityLevel(quiet.Handle, Chill), "a buff whose duration is not known is left to the game: the other spell has none set");
        }
    }
}
