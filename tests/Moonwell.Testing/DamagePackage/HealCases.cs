namespace Moonwell.Testing
{
    /// <summary>Cases that check heals: their two stages and order, what went in and what did not fit, and the chain limit they share with hits.</summary>
    public static class HealCases
    {
        /// <summary>The fake damage world of the current case.</summary>
        private static FakeDamageWorld fake = null!;

        /// <summary>Installs the package on a fake, registers HealerUnit (H002) and PatientUnit (h003), and makes one of each: the healer for player 0, the patient for player 1 with 40 life of 100.</summary>
        private static void Arena(FakeWorld world, out HealerUnit healer, out PatientUnit patient)
        {
            fake = new FakeDamageWorld(world);
            DamageSystem.Install(fake);
            ObjectTypes.Unit(ObjectTypes.FourCC("H002"), () => new HealerUnit());
            ObjectTypes.Unit(ObjectTypes.FourCC("h003"), () => new PatientUnit());
            healer = (HealerUnit)Unit.Of(world.NewUnit(ObjectTypes.FourCC("H002"), slot: 0))!;
            patient = (PatientUnit)Unit.Of(world.NewUnit(ObjectTypes.FourCC("h003"), slot: 1))!;
            patient.MaxLife = 100f;
            patient.Life = 40f;
            TestLog.Lines.Clear();
        }

        /// <summary>A heal adds its amount to the life and returns the heal with its source, target, tag and what went in; a raw change to the life runs no reaction.</summary>
        public static void HealAddsLife(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            var heal = healer.Heal(patient, 30f, tag: "t");
            Expect.True(heal != null, "a heal is returned");
            Expect.Near(70f, patient.Life, "the life after the heal");
            Expect.Near(30f, heal!.Amount, "the amount");
            Expect.Near(30f, heal.Original, "the original amount");
            Expect.Near(30f, heal.Done, "what went in");
            Expect.Near(0f, heal.Over, "what did not fit");
            Expect.Same(healer, heal.Source, "the source");
            Expect.Same(patient, heal.Target, "the target");
            Expect.True((string?)heal.Tag == "t", "the tag");
            var lines = TestLog.Lines.Count;
            patient.Life += 5f;
            Expect.Near(75f, patient.Life, "the life after the raw change");
            Expect.Equal(lines, TestLog.Lines.Count, "the raw change ran no reaction");
        }

        /// <summary>Each stage runs the source and its components, the target and its components, the game's, the target's player's and the source's player's; the life is set between them.</summary>
        public static void HealOrder(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            Game.Instance.Add(new GameHealWatch());
            Player.Get(0).Add(new PlayerHealWatch("p0"));
            Player.Get(1).Add(new PlayerHealWatch("p1"));
            var first = -1f;
            var second = -1f;
            healer.Healing = heal => first = patient.Life;
            healer.Healed = heal => second = patient.Life;
            healer.Heal(patient, 10f);
            StageCases.ExpectLog(new string[]
            {
                "healer healing", "healer comp healing", "patient receiving", "patient comp receiving", "game receiving", "p1 receiving", "p0 receiving",
                "healer healed", "healer comp healed", "patient received", "patient comp received", "game received", "p1 received", "p0 received",
            }, "the log");
            Expect.Near(40f, first, "the life in the first stage");
            Expect.Near(50f, second, "the life in the second stage");
        }

        /// <summary>At full life nothing goes in and all is over; near full the part that fits goes in and the rest is over, and the life stops at the most. The second stage runs either way.</summary>
        public static void HealDoneAndOver(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            patient.Life = 100f;
            var full = healer.Heal(patient, 30f);
            Expect.Near(0f, full!.Done, "full: what went in");
            Expect.Near(30f, full.Over, "full: what did not fit");
            Expect.Near(100f, patient.Life, "full: the life");
            Expect.True(TestLog.Lines.Contains("patient received"), "full: the second stage ran");
            patient.Life = 90f;
            var near = healer.Heal(patient, 30f);
            Expect.Near(10f, near!.Done, "near full: what went in");
            Expect.Near(20f, near.Over, "near full: what did not fit");
            Expect.Near(100f, patient.Life, "near full: the life");
        }

        /// <summary>The first stage may change the amount, and the original stays; one that leaves 0 or less adds nothing; the second stage's set of the amount does nothing.</summary>
        public static void HealChangedAmount(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            patient.Receiving = heal => heal.Amount = heal.Amount * 2f;
            healer.Healed = heal => heal.Amount = 999f;
            var doubled = healer.Heal(patient, 10f);
            Expect.Near(20f, doubled!.Amount, "the amount after the stages");
            Expect.Near(10f, doubled.Original, "the original");
            Expect.Near(20f, doubled.Done, "what went in");
            Expect.Near(60f, patient.Life, "the life");
            patient.Receiving = heal => heal.Amount = 0f;
            var none = healer.Heal(patient, 10f);
            Expect.Near(0f, none!.Done, "none: what went in");
            Expect.Near(0f, none.Over, "none: what did not fit");
            Expect.Near(60f, patient.Life, "none: the life");
            patient.Receiving = heal => heal.Amount = -5f;
            var less = healer.Heal(patient, 10f);
            Expect.Near(0f, less!.Done, "less than none: what went in");
            Expect.Near(60f, patient.Life, "less than none: the life");
        }

        /// <summary>The crit mark a first-stage reaction sets is read in the second stage; a heal starts without it.</summary>
        public static void HealCrit(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            var seen = new System.Collections.Generic.List<string>();
            patient.Received = heal => seen.Add(heal.IsCrit ? "crit" : "plain");
            healer.Heal(patient, 5f);
            healer.Healing = heal => heal.IsCrit = true;
            healer.Heal(patient, 5f);
            Expect.Equal("plain|crit", string.Join("|", seen), "the marks");
        }

        /// <summary>A heal of 0 or less, or of a unit that is dead or gone, is not made: it returns null and runs no reaction.</summary>
        public static void HealRefused(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            Expect.True(healer.Heal(patient, 0f) == null, "0 gives null");
            Expect.True(healer.Heal(patient, -3f) == null, "less than 0 gives null");
            Expect.Near(40f, patient.Life, "the life");
            var gone = (PatientUnit)Unit.Of(world.NewUnit(ObjectTypes.FourCC("h003"), slot: 1))!;
            gone.Remove();
            Expect.True(healer.Heal(gone, 10f) == null, "a gone unit gives null");
            patient.Kill();
            Expect.True(healer.Heal(patient, 10f) == null, "a dead unit gives null");
            Expect.Equal(0, TestLog.Lines.Count, "no reaction ran");
        }

        /// <summary>A heal and a hit made from each other's reactions count toward one chain of 8; the ninth is refused with a report, and the chain ends.</summary>
        public static void HealChainSharedWithHits(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            var other = (PatientUnit)Unit.Of(world.NewUnit(ObjectTypes.FourCC("h003"), slot: 0))!;
            var steps = 0;
            patient.Taken = hit =>
            {
                steps++;
                patient.Heal(other, 1f);
            };
            other.Received = heal =>
            {
                steps++;
                other.Damage(patient, 1f);
            };
            fake.Hit(other.Handle, patient.Handle, 1f, damageType: 4);
            Expect.Equal(8, steps, "four hits and four heals");
            Expect.Equal(1, world.Logged.Count, "one error is reported");
            Expect.True(world.Logged[0].Contains("a chain of 8"), "the report: " + world.Logged[0]);
        }

        /// <summary>No hit reaction, and none of core's, runs for a heal, and no heal reaction runs for a hit.</summary>
        public static void HealIsNoHit(FakeWorld world)
        {
            Arena(world, out var healer, out var patient);
            healer.Heal(patient, 10f);
            StageCases.ExpectLog(new string[]
            {
                "healer healing", "healer comp healing", "patient receiving", "patient comp receiving",
                "healer healed", "healer comp healed", "patient received", "patient comp received",
            }, "the heal's log");
            TestLog.Lines.Clear();
            fake.Hit(healer.Handle, patient.Handle, 10f, damageType: 4);
            StageCases.ExpectLog(new string[] { "patient taking", "patient core damaged", "patient taken" }, "the hit's log");
        }
    }
}
