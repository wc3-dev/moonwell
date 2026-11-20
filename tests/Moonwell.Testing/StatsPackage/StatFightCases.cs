namespace Moonwell.Testing
{
    /// <summary>Cases for the stats package in the simulation: fights played through the damage stages with a seed, where a stat changes how long a fight lasts or how many hits land.</summary>
    public static class StatFightCases
    {
        private static readonly int Hitter = ObjectTypes.FourCC("s1ha");
        private static readonly int Victim = ObjectTypes.FourCC("s1hv");

        /// <summary>A hitter in range of 200 with a swing of 10 every second, and a victim of 100 life; a map of nothing.</summary>
        private static Simulation Start(int seed, float victimLife = 100.0f)
        {
            var sim = Simulation.Start(seed);
            sim.SetNumbers(Hitter, new UnitNumbers { Life = 100.0f, MoveSpeed = 300.0f, AttackRange = 200.0f, AttackCooldown = 1.0f, Damage = 10 });
            sim.SetNumbers(Victim, new UnitNumbers { Life = victimLife, MoveSpeed = 200.0f });
            return sim;
        }

        /// <summary>A hitter of player one ordered to attack a victim of player two 100 away, the pair y apart from the others; the victim is given back with a log of what landed on it.</summary>
        private static Unit Fight(float y, out Unit victim, out HitLog log)
        {
            var hitter = Unit.Create(Hitter, Player.One, 0.0f, y, Game.Instance);
            victim = Unit.Create(Victim, Player.Two, 100.0f, y, Game.Instance);
            hitter.OrderOn("attack", victim);
            log = victim.Add(new HitLog());
            return hitter;
        }

        /// <summary>Over 400 swings of the same seed a victim with 50 evasion takes about half of them, and one with none takes all.</summary>
        public static void EvasionHalvesTheHits(FakeWorld world)
        {
            var sim = Start(3, 1000000.0f);
            StatsSystem.ShowTexts = false;
            Fight(0.0f, out var plain, out var plainLog);
            Fight(1000.0f, out var evasive, out var evasiveLog);
            evasive.SetBase(Stat.Evasion, 50.0f);
            sim.Run(400.0f);
            StatsSystem.ShowTexts = true;
            Expect.True(plainLog.Amounts.Count >= 399, "every swing of the plain victim landed, not " + plainLog.Amounts.Count);
            Expect.True(evasiveLog.Amounts.Count > 160 && evasiveLog.Amounts.Count < 240, "about 200 swings landed on the evasive one, not " + evasiveLog.Amounts.Count);
        }

        /// <summary>A hitter with a crit build kills a victim of 300 sooner than a plain one, under the same seed: 50 percent for x 3 averages 20 a swing against 10.</summary>
        public static void CritBuildKillsSooner(FakeWorld world)
        {
            var sim = Start(7, 300.0f);
            StatsSystem.ShowTexts = false;
            Fight(0.0f, out var plainVictim, out var plainLog);
            var crit = Fight(1000.0f, out var critVictim, out var critLog);
            crit.SetBase(Stat.CritChance, 50.0f);
            crit.SetBase(Stat.CritDamage, 300.0f);
            Expect.True(sim.RunUntil(() => !plainVictim.IsAlive, 100.0f), "the plain victim dies");
            var plainTime = sim.Time;
            Expect.True(!critVictim.IsAlive, "the crit build's victim is dead before it");
            StatsSystem.ShowTexts = true;
            Expect.Near(30.0f, (float)plainLog.Amounts.Count, "the plain victim took 30 swings of 10");
            Expect.True(critLog.Amounts.Count < 25, "the crit build needed fewer swings, not " + critLog.Amounts.Count);
            Expect.True(critLog.Amounts.Count >= 10, "but at least 10 of 30");
            Expect.True(plainTime > 29.0f, "the plain fight took 29 seconds or more");
        }

        /// <summary>A barrier of 100 lengthens a fight: a victim of 100 dies at its tenth swing, step 459, and with a barrier of 100 at its twentieth, step 959.</summary>
        public static void BarrierLengthensTheFight(FakeWorld world)
        {
            var sim = Start(1);
            Fight(0.0f, out var plain, out var plainLog);
            Fight(1000.0f, out var shielded, out var shieldedLog);
            shielded.AddBarrier(100.0f, Game.Instance);
            Expect.True(sim.RunUntil(() => !plain.IsAlive, 20.0f), "the plain victim dies");
            Expect.Near(9.18f, sim.Time, "at step 459");
            Expect.True(shielded.IsAlive, "the shielded one lives");
            Expect.Near(0.0f, shielded.Get(Stat.Barrier), "its barrier is used up by then");
            Expect.True(sim.RunUntil(() => !shielded.IsAlive, 20.0f), "the shielded victim dies");
            Expect.Near(19.18f, sim.Time, "at step 959");
            Expect.Equal(10, plainLog.Amounts.Count, "ten hits for the plain victim");
            Expect.Equal(20, shieldedLog.Amounts.Count, "twenty for the shielded one, ten of them taken by the barrier: core sees each before the package's stages");
        }
    }
}
