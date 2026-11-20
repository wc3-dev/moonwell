using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>A unit component whose rules change the hits and heals its unit takes: a share of each hit kept after armor, a chance to miss, a life to spare, armor ignored, the damage type made magic, and a share of each heal kept. It logs what landed.</summary>
    public sealed class FightRules : UnitComponent, IDamageTarget, IHealTarget
    {
        /// <summary>The share of a hit kept after armor.</summary>
        public float Keep = 1.0f;

        /// <summary>The chance in percent that a hit misses, drawn from the map's own random numbers.</summary>
        public int MissPercent;

        /// <summary>Whether a hit that would kill spares one life.</summary>
        public bool SpareLast;

        /// <summary>Whether a hit ignores the unit's armor.</summary>
        public bool IgnoreArmor;

        /// <summary>Whether a hit is made magic damage, which armor does not cut.</summary>
        public bool MakeMagic;

        /// <summary>The share of each heal kept.</summary>
        public float HealKeep = 1.0f;

        /// <summary>The amount of each hit that landed, in order.</summary>
        public List<float> Landed = new List<float>();

        /// <summary>How many hits missed.</summary>
        public int Misses;

        /// <summary>How many hits it took after armor (the second stage).</summary>
        public int Taken;

        /// <summary>How much life the heals put in.</summary>
        public float Healed;

        /// <summary>Whether a landed hit killed.</summary>
        public bool Killed;

        /// <summary>Before armor: the armor, the damage type and the miss.</summary>
        public void OnTaking(Hit hit)
        {
            if (IgnoreArmor)
            {
                hit.Armor = 0.0f;
            }

            if (MakeMagic)
            {
                hit.DamageKind = DamageKind.Magic;
            }

            if (MissPercent > 0 && Game.Random.Int(1, 100) <= MissPercent)
            {
                Misses++;
                hit.Miss("");
            }
        }

        /// <summary>After armor: keeps a share of the amount.</summary>
        public void OnTaken(Hit hit)
        {
            Taken++;
            hit.Amount = hit.Amount * Keep;
        }

        /// <summary>The lethal stage: spares one life.</summary>
        public void OnDying(Hit hit)
        {
            if (SpareLast)
            {
                hit.Spare();
            }
        }

        /// <summary>Landed: logs the amount and whether it killed.</summary>
        public void OnHurt(Hit hit)
        {
            Landed.Add(hit.Amount);
            Killed = Killed || hit.Killed;
        }

        /// <summary>A heal's first stage: keeps a share of the amount.</summary>
        public void OnReceiving(Heal heal)
        {
            heal.Amount = heal.Amount * HealKeep;
        }

        /// <summary>A heal's second stage: adds what went in.</summary>
        public void OnReceived(Heal heal)
        {
            Healed += heal.Done;
        }
    }

    /// <summary>A map with nothing in it, so no reaction to damage anywhere.</summary>
    public sealed class QuietMap : Map
    {
    }

    /// <summary>Cases for the simulation's play of the damage package (hits, heals, misses, the lethal stage) and for a swing reading its unit's damage and attack speed as they are now. Each starts its own simulation; the numbers follow the play's rules: an order is stamped with the next step and stands the unit 8 steps from it, so a unit in range swings at step 9 and then every StepsOf(cooldown) steps.</summary>
    public static class SimulationHitCases
    {
        private static readonly int Hitter = ObjectTypes.FourCC("s0ha");
        private static readonly int Victim = ObjectTypes.FourCC("s0hv");
        private static readonly int Healer = ObjectTypes.FourCC("s0hh");
        private static readonly int ArmoredVictim = ObjectTypes.FourCC("s0hw");

        /// <summary>Runs the simulation one 0.02 s step at a time.</summary>
        private static void Steps(Simulation sim, int count)
        {
            for (var i = 0; i < count; i++)
            {
                sim.Run(0.02f);
            }
        }

        /// <summary>A hitter in range of 200, a swing of 10 every second (50 steps), and the types of the fights' victims.</summary>
        private static Simulation Start(int seed = 1, int damage = 10, int dice = 0)
        {
            var sim = Simulation.Start(seed);
            sim.SetNumbers(Hitter, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 1.0f, Damage = damage, DamageDice = dice, DamageSides = dice > 0 ? 6 : 0 });
            sim.SetNumbers(Victim, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            sim.SetNumbers(ArmoredVictim, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f, Armor = 2.0f });
            sim.SetNumbers(Healer, new UnitNumbers { Life = 100.0f, MoveSpeed = 200.0f });
            return sim;
        }

        /// <summary>A hitter of player one ordered to attack a victim of player two 100 away, which it does from step 9.</summary>
        private static Unit Attack(int victimType, out Unit victim, float y = 0.0f)
        {
            var hitter = Unit.Create(Hitter, Player.One, 0.0f, y, Game.Instance);
            victim = Unit.Create(victimType, Player.Two, 100.0f, y, Game.Instance);
            hitter.OrderOn("attack", victim);
            return hitter;
        }

        /// <summary>A component on the victim that halves each hit after armor changes a fight's length: the plain victim dies at its tenth swing, step 459, the halved one at its twentieth, step 959, and each landing is seen.</summary>
        public static void HalvingChangesTheFight(FakeWorld world)
        {
            var sim = Start();
            Attack(Victim, out var plain, 0.0f);
            Attack(Victim, out var halved, 1000.0f);
            var rules = halved.Add(new FightRules { Keep = 0.5f });
            var plainRules = plain.Add(new FightRules());
            Steps(sim, 9);
            Expect.Near(90.0f, plain.Life, "the plain victim takes 10");
            Expect.Near(95.0f, halved.Life, "the halved one 5");
            Expect.Equal(1, rules.Taken, "the second stage ran once");
            Expect.True(sim.RunUntil(() => !plain.IsAlive, 20.0f), "the plain victim dies");
            Expect.Near(9.18f, sim.Time, "at step 459"); // 9 + 50 x 9
            Expect.True(plainRules.Killed, "its last hit landed as a kill");
            Expect.Equal(10, plainRules.Landed.Count, "after ten hits");
            Expect.True(halved.IsAlive, "the halved one lives");
            Expect.True(sim.RunUntil(() => !halved.IsAlive, 20.0f), "the halved victim dies");
            Expect.Near(19.18f, sim.Time, "at step 959"); // 9 + 50 x 19
            Expect.Equal(20, rules.Landed.Count, "after twenty hits");
            Expect.Near(5.0f, rules.Landed[0], "each of 5");
        }

        /// <summary>A hit that misses takes no life and runs no core OnDamaged: 100 percent of the swings miss until the rule is lifted, then they land.</summary>
        public static void MissedSwings(FakeWorld world)
        {
            var sim = Start();
            Attack(Victim, out var victim);
            var rules = victim.Add(new FightRules { MissPercent = 100 });
            var log = victim.Add(new HitLog());
            Steps(sim, 109); // swings at 9, 59 and 109
            Expect.Equal(3, rules.Misses, "three swings missed");
            Expect.Near(100.0f, victim.Life, "no life lost");
            Expect.Equal(0, log.Amounts.Count, "core's OnDamaged did not run");
            Expect.Equal(0, rules.Taken, "nor the after-armor stage");
            rules.MissPercent = 0;
            Steps(sim, 50); // the swing at 159
            Expect.Equal(3, rules.Misses, "no more misses");
            Expect.Near(90.0f, victim.Life, "the next swing lands");
            Expect.Equal(1, log.Amounts.Count, "and runs OnDamaged");
        }

        /// <summary>A healer's heal during a fight goes through its two stages: a victim of 100 life takes 10 a second and is healed 15 every 2 s, and a target component keeps two thirds of each heal.</summary>
        public static void HealDuringAFight(FakeWorld world)
        {
            var sim = Start();
            Attack(Victim, out var victim);
            var healer = Unit.Create(Healer, Player.Two, 100.0f, 500.0f, Game.Instance);
            var rules = victim.Add(new FightRules());
            Game.Instance.Every(2.0f, () =>
            {
                healer.Heal(victim, 15.0f);
            });
            // Swings at 9, 59, ...; heals at 100, 200, 300, 400: 100 - 10 - 10 + 15 - 10 - 10 + 15 ... leaves 60 at step 459.
            Steps(sim, 459);
            Expect.Near(60.0f, victim.Life, "the victim's life");
            Expect.Near(60.0f, rules.Healed, "four heals of 15 went in");
            rules.HealKeep = 2.0f / 3.0f;
            Steps(sim, 45); // step 504: the heal at 500, kept at 10
            Expect.Near(10.0f, rules.Healed - 60.0f, "the first stage's reaction cut the heal to 10");
        }

        /// <summary>Spare in the lethal stage: a victim of 25 would die at the third swing and is left at 1 life instead, and again at each swing after.</summary>
        public static void SpareInTheLethalStage(FakeWorld world)
        {
            var sim = Start();
            Attack(Victim, out var spared, 0.0f);
            Attack(Victim, out var doomed, 1000.0f);
            spared.Life = 25.0f;
            doomed.Life = 25.0f;
            var rules = spared.Add(new FightRules { SpareLast = true });
            Steps(sim, 59); // swings at 9 and 59: 25 to 15 to 5
            Expect.Near(5.0f, spared.Life, "two swings in");
            Steps(sim, 50); // the third at 109 would kill
            Expect.True(spared.IsAlive, "it is spared");
            Expect.Near(1.0f, spared.Life, "at 1 life");
            Steps(sim, 100);
            Expect.True(spared.IsAlive && spared.Life == 1.0f, "and again at the swings after");
            Expect.True(!rules.Killed, "no hit landed as a kill");
            Expect.True(!doomed.IsAlive, "the same victim with no rule dies");
        }

        /// <summary>The same seed gives the same run through the package, a miss chance and dice among them; another seed gives another.</summary>
        public static void SameSeedSameRun(FakeWorld world)
        {
            var once = Fought(4);
            var again = Fought(4);
            var other = Fought(9);
            Expect.True(once.Landed.Count > 0, "some hits landed");
            Expect.Equal(once.Landed.Count, again.Landed.Count, "the same seed, the same hits");
            Expect.Equal(once.Misses, again.Misses, "and the same misses");
            for (var i = 0; i < once.Landed.Count; i++)
            {
                Expect.Near(once.Landed[i], again.Landed[i], "the same seed's hit " + i);
            }

            Expect.True(once.Misses != other.Misses || once.Landed.Count != other.Landed.Count || once.Landed[0] != other.Landed[0], "another seed gives another run");
        }

        /// <summary>A fight of 5 + 2d6 swings against 300 life where a third miss and the rest are halved, under a seed, until the death.</summary>
        private static FightRules Fought(int seed)
        {
            var sim = Start(seed, 5, 2);
            sim.SetNumbers(Victim, new UnitNumbers { Life = 300.0f, MoveSpeed = 200.0f });
            Attack(Victim, out var victim);
            var rules = victim.Add(new FightRules { Keep = 0.5f, MissPercent = 33 });
            Expect.True(sim.RunUntil(() => !victim.IsAlive, 400.0f), "the victim dies with seed " + seed);
            return rules;
        }

        /// <summary>A hit from code goes through the stages: a reaction halves it, armor cuts it as the game's does only when it is normal damage, and a before-armor reaction that zeroes the armor or makes it magic lifts the cut.</summary>
        public static void CodeHitsGoThroughTheStages(FakeWorld world)
        {
            Start();
            var source = Unit.Create(Hitter, Player.One, 0.0f, 0.0f, Game.Instance);
            var plain = Unit.Create(ArmoredVictim, Player.Two, 100.0f, 0.0f, Game.Instance);
            var halved = Unit.Create(ArmoredVictim, Player.Two, 100.0f, 0.0f, Game.Instance);
            var bare = Unit.Create(ArmoredVictim, Player.Two, 100.0f, 0.0f, Game.Instance);
            var magic = Unit.Create(ArmoredVictim, Player.Two, 100.0f, 0.0f, Game.Instance);
            halved.Add(new FightRules { Keep = 0.5f });
            bare.Add(new FightRules { IgnoreArmor = true });
            magic.Add(new FightRules { MakeMagic = true });
            var first = source.Damage(plain, 100.0f, DamageKind.Normal);
            Expect.Near(910.7143f, plain.Life, "100 normal on armor 2 loses 89.29"); // 1000 - 100 x (1 - 0.12 / 1.12)
            Expect.True(first is Hit, "the call returns the package's hit");
            Expect.Near(89.2857f, first!.Amount, "its amount is after armor");
            source.Damage(plain, 100.0f);
            Expect.Near(810.7143f, plain.Life, "100 magic is not cut by armor");
            source.Damage(halved, 100.0f, DamageKind.Normal);
            Expect.Near(955.3571f, halved.Life, "a reaction halves the 89.29 after armor"); // 1000 - 44.64
            source.Damage(bare, 100.0f, DamageKind.Normal);
            Expect.Near(900.0f, bare.Life, "a reaction that sets the armor to 0 lifts the cut");
            source.Damage(magic, 100.0f, DamageKind.Normal);
            Expect.Near(900.0f, magic.Life, "a reaction that makes it magic lifts it too");
            Expect.Near(2.0f, bare.Get(Stat.Armor), "and the unit's own armor is back");
        }

        /// <summary>A swing reads its unit's damage as it is: a modifier of +10 put on the hitter after two swings turns its swings of 10 into 20, so a victim of 100 dies at step 259 where it would at 459.</summary>
        public static void DamageModifierShortensTheFight(FakeWorld world)
        {
            var sim = Start();
            Attack(Victim, out var plainVictim, 0.0f);
            var strong = Attack(Victim, out var strongVictim, 1000.0f);
            Steps(sim, 100); // swings at 9 and 59: 80 left on both
            Expect.Near(80.0f, strongVictim.Life, "two swings of 10");
            strong.Modify(Stat.Damage, 10.0f, heldBy: strong);
            Expect.True(sim.RunUntil(() => !strongVictim.IsAlive, 20.0f), "the strengthened hitter's victim dies");
            Expect.Near(5.18f, sim.Time, "at step 259: swings of 20 at 109, 159, 209 and 259"); // 80 - 4 x 20
            Expect.Near(40.0f, plainVictim.Life, "the other hitter's victim has taken only swings of 10, six of them");
        }

        /// <summary>Gives a unit a bonus of attack speed straight to the fake's bonus ability, which the stats put theirs on, so the limits are tried whatever the stat holds.</summary>
        private static void GiveSpeed(FakeWorld world, Unit unit, float bonus)
        {
            var id = ObjectTypes.FourCC("AMWs");
            world.SetAbilityLevel(unit.Handle, id, 1);
            world.PutBonus(unit.Handle, id, ObjectTypes.FourCC("Isx1"), true, bonus);
        }

        /// <summary>A swing's cooldown is the base over 1 + the attack speed bonus now. In 10 s (500 steps) of swings every second there are 10 swings with none, 20 at +100 percent (a Stat.AttackSpeed modifier), 50 with a bonus past five times (held at five times: a swing every 10 steps) and 2 with one below a fifth (held at a fifth: every 250 steps).</summary>
        public static void AttackSpeedRaisesTheSwings(FakeWorld world)
        {
            var sim = Start(1, 1);
            sim.SetNumbers(Victim, new UnitNumbers { Life = 10000.0f, MoveSpeed = 200.0f });
            var swings = new List<HitLog>();
            var hitters = new List<Unit>();
            for (var i = 0; i < 4; i++)
            {
                hitters.Add(Attack(Victim, out var victim, 1000.0f * i));
                swings.Add(victim.Add(new HitLog()));
            }

            hitters[1].Modify(Stat.AttackSpeed, 100.0f, heldBy: hitters[1]);
            GiveSpeed(sim.World, hitters[2], 10.0f);
            GiveSpeed(sim.World, hitters[3], -0.9f);
            Steps(sim, 500);
            Expect.Equal(10, swings[0].Amounts.Count, "a swing every 50 steps: 9 to 459");
            Expect.Equal(20, swings[1].Amounts.Count, "at +100 percent every 25: 9 to 484");
            Expect.Equal(50, swings[2].Amounts.Count, "past five times, every 10: 9 to 499");
            Expect.Equal(2, swings[3].Amounts.Count, "below a fifth, every 250: 9 and 259");
        }

        /// <summary>A map with no reaction to damage plays as it did before the package ran on the simulation: swings of 100 on armor 2 and -2 deal the game's 89.29 and 111.64, and the game's damage event is not listened for.</summary>
        public static void QuietMapPlaysAsBefore(FakeWorld world)
        {
            var sim = Simulation.Start(new QuietMap());
            sim.SetNumbers(Hitter, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 2.0f, Damage = 100 });
            sim.SetNumbers(Victim, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f, Armor = 2.0f });
            sim.SetNumbers(ArmoredVictim, new UnitNumbers { Life = 1000.0f, MoveSpeed = 200.0f, Armor = -2.0f });
            Attack(Victim, out var armored, 0.0f);
            Attack(ArmoredVictim, out var bare, 1000.0f);
            Steps(sim, 9);
            Expect.Near(910.7143f, armored.Life, "100 on armor 2");
            Expect.Near(888.36f, bare.Life, "100 on armor -2");
            Expect.True(!sim.World.Listening.Contains(GameEvent.Damage), "core does not listen for the damage event");
            Steps(sim, 100);
            Expect.Near(910.7143f - 89.2857f, armored.Life, "one more swing at step 109, a cooldown of 2 s on");
        }
    }
}
