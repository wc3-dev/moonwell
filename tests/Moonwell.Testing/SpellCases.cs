using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check the spell framework (decision 225): a cast checked and refused before anything is spent, a cast run to its end, and an ability's cooldown and mana from code.</summary>
    public static class SpellCases
    {
        private static readonly int Id = ObjectTypes.FourCC("A000");

        /// <summary>An ability that logs its reactions and refuses a cast when given a refusal.</summary>
        private sealed class Picky : Ability
        {
            /// <summary>The text to refuse a cast with; null lets it through.</summary>
            public string? Refusal;

            /// <summary>The casts each reaction was given; null when it has not run.</summary>
            public Cast? Casting;
            public Cast? Given;
            public Cast? Finished;

            /// <summary>Whether the cast had ended when OnFinished ran.</summary>
            public bool EndedInFinished;

            /// <summary>Whether OnCast refuses its cast, too late.</summary>
            public bool RefuseLate;

            public override void OnCasting(Cast cast)
            {
                TestLog.Lines.Add("casting");
                Casting = cast;
                if (Refusal != null)
                {
                    cast.Refuse(Refusal);
                }
            }

            public override void OnCast(Cast cast)
            {
                TestLog.Lines.Add("cast");
                Given = cast;
                if (RefuseLate)
                {
                    cast.Refuse("too late");
                }
            }

            public override void OnFinished(Cast cast)
            {
                TestLog.Lines.Add("finished");
                Finished = cast;
                EndedInFinished = cast.HasEnded;
            }
        }

        /// <summary>An ability whose only reaction is OnFinished.</summary>
        private sealed class FinishOnly : Ability
        {
            public override void OnFinished(Cast cast)
            {
            }
        }

        /// <summary>A footman of player 0 with the picky ability at level 1, and the ability.</summary>
        private static Picky Arena(FakeWorld world)
        {
            ObjectTypes.Ability(Id, () => new Picky());
            var caster = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            world.SetAbilityLevel(caster.Handle, Id, 1);
            TestLog.Lines.Clear();
            return caster.AbilityOf<Picky>();
        }

        /// <summary>OnCasting runs when the caster starts to cast, with the target and place, and OnCast gets the same cast when the spell takes effect.</summary>
        public static void CastingBeforeEffect(FakeWorld world)
        {
            var picky = Arena(world);
            var target = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1))!;
            world.StartCasting(picky.Unit.Handle, Id, target.Handle, 3f, 4f);
            Expect.Equal("casting", string.Join(",", TestLog.Lines), "the reactions when the cast starts");
            var casting = picky.Casting!;
            Expect.Same(target, casting.Target, "the target in OnCasting");
            Expect.Near(3f, casting.X, "the x in OnCasting");
            Expect.Equal(1, casting.Level, "the level in OnCasting");
            Expect.True(!casting.HasEnded, "the cast is live");
            Expect.True(world.TakeEffect(picky.Unit.Handle, Id, target.Handle, 3f, 4f), "the spell took effect");
            Expect.Equal("casting,cast", string.Join(",", TestLog.Lines), "the reactions after the effect");
            Expect.Same(casting, picky.Given, "OnCast gets the cast OnCasting had");
        }

        /// <summary>A refused cast ends, the caster is stopped, so the spell never takes effect, and the caster's owner sees the text as an error.</summary>
        public static void RefusedStopsAndShows(FakeWorld world)
        {
            var picky = Arena(world);
            picky.Refusal = "Target must be below half life";
            world.StartCasting(picky.Unit.Handle, Id);
            Expect.True(picky.Casting!.HasEnded, "the refused cast has ended");
            Expect.True(world.Orders.Contains("immediate stop"), "the caster was stopped");
            Expect.True(!world.TakeEffect(picky.Unit.Handle, Id), "the spell did not take effect");
            Expect.Equal("casting", string.Join(",", TestLog.Lines), "no OnCast ran");
            Expect.True(world.PrintedTexts.Contains("error: Target must be below half life"), "the error line");
            Expect.Equal(0, world.PrintedSlots[world.PrintedTexts.IndexOf("error: Target must be below half life")], "shown to the caster's owner");
        }

        /// <summary>A refusal with empty text stops the cast and shows no line.</summary>
        public static void RefusedEmptyShowsNothing(FakeWorld world)
        {
            var picky = Arena(world);
            picky.Refusal = "";
            var before = world.PrintedTexts.Count;
            world.StartCasting(picky.Unit.Handle, Id);
            Expect.True(picky.Casting!.HasEnded, "the refused cast has ended");
            Expect.True(world.Orders.Contains("immediate stop"), "the caster was stopped");
            Expect.Equal(before, world.PrintedTexts.Count, "no line shown");
        }

        /// <summary>A cast stopped after it started and before it took effect ends with no OnCast and no OnFinished.</summary>
        public static void StoppedBeforeEffect(FakeWorld world)
        {
            var picky = Arena(world);
            world.StartCasting(picky.Unit.Handle, Id);
            picky.Unit.Order("stop");
            Expect.True(picky.Casting!.HasEnded, "the cast has ended");
            Expect.Equal("casting", string.Join(",", TestLog.Lines), "only OnCasting ran");
        }

        /// <summary>OnFinished runs when the spell runs to its end, before its cast ends; the end then ends it.</summary>
        public static void FinishedBeforeEnd(FakeWorld world)
        {
            var picky = Arena(world);
            world.StartCasting(picky.Unit.Handle, Id);
            world.TakeEffect(picky.Unit.Handle, Id);
            Inbound.SpellFinished(picky.Unit.Handle, Id);
            Inbound.SpellEnded(picky.Unit.Handle, Id);
            Expect.Equal("casting,cast,finished", string.Join(",", TestLog.Lines), "the reactions");
            Expect.Same(picky.Given, picky.Finished, "OnFinished gets the cast");
            Expect.True(!picky.EndedInFinished, "the cast was live in OnFinished");
            Expect.True(picky.Given!.HasEnded, "the cast ended after");
        }

        /// <summary>A cast that ends without the finish event, stopped early, runs no OnFinished; another ability's finish does not either.</summary>
        public static void StoppedIsNotFinished(FakeWorld world)
        {
            var picky = Arena(world);
            world.StartCasting(picky.Unit.Handle, Id);
            world.TakeEffect(picky.Unit.Handle, Id);
            Inbound.SpellFinished(picky.Unit.Handle, ObjectTypes.FourCC("AHhb"));
            Inbound.SpellEnded(picky.Unit.Handle, Id);
            Inbound.SpellFinished(picky.Unit.Handle, Id);
            Expect.Equal("casting,cast", string.Join(",", TestLog.Lines), "no OnFinished ran");
        }

        /// <summary>An ability whose only reaction is OnFinished makes the world deliver spells.</summary>
        public static void FinishedListens(FakeWorld world)
        {
            Expect.True(!world.Listening.Contains(GameEvent.Spell), "Spell is not listened for before");
            ObjectTypes.Ability(Id, () => new FinishOnly());
            Expect.True(world.Listening.Contains(GameEvent.Spell), "Spell is listened for");
        }

        /// <summary>Cooldown and mana per unit: the full cooldown and mana cost at the level now, the cooldown left, starting it at any length, 0 making it ready, and resetting it.</summary>
        public static void CooldownAndMana(FakeWorld world)
        {
            var picky = Arena(world);
            picky.Cooldown = 9f;
            picky.ManaCost = 75;
            Expect.Near(9f, world.AbilityCooldown(picky.Unit.Handle, Id, 0), "the cooldown set at level 1");
            Expect.Near(9f, picky.Cooldown, "the cooldown read");
            Expect.Equal(75, picky.ManaCost, "the mana cost read");
            world.SetAbilityLevel(picky.Unit.Handle, Id, 2);
            Expect.Near(0f, picky.Cooldown, "level 2 has its own cooldown");
            world.SetAbilityLevel(picky.Unit.Handle, Id, 1);
            picky.StartCooldown(20f);
            Expect.Near(20f, picky.CooldownLeft, "started past the full cooldown");
            world.Advance(1f);
            Expect.Near(19f, picky.CooldownLeft, "a second later");
            picky.StartCooldown(0f);
            Expect.Near(0f, picky.CooldownLeft, "started at 0, it is ready");
            picky.StartCooldown(5f);
            picky.ResetCooldown();
            Expect.Near(0f, picky.CooldownLeft, "reset");
        }

        /// <summary>On a unit without the ability, cooldown and mana read 0 and setting changes nothing.</summary>
        public static void CooldownOfMissingAbility(FakeWorld world)
        {
            var picky = Arena(world);
            world.SetAbilityLevel(picky.Unit.Handle, Id, 0);
            picky.Cooldown = 9f;
            picky.StartCooldown(5f);
            Expect.Near(0f, picky.Cooldown, "the cooldown");
            Expect.Near(0f, picky.CooldownLeft, "the cooldown left");
            Expect.Equal(0, picky.ManaCost, "the mana cost");
            Expect.Near(0f, world.AbilityCooldown(picky.Unit.Handle, Id, 0), "nothing was set");
        }

#if DEBUG
        /// <summary>Refuse after the spell took effect is too late: the cast goes on, and a development build reports it.</summary>
        public static void RefuseLateReported(FakeWorld world)
        {
            var picky = Arena(world);
            picky.RefuseLate = true;
            var before = world.Printed.Count;
            world.StartCasting(picky.Unit.Handle, Id);
            world.TakeEffect(picky.Unit.Handle, Id);
            Expect.True(!picky.Given!.HasEnded, "the cast goes on");
            Expect.True(!world.Orders.Contains("immediate stop"), "the caster was not stopped");
            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("OnCasting"), "the report says where: " + world.Printed[before]);
        }

        /// <summary>A cooldown set on a unit without the ability is reported.</summary>
        public static void CooldownOfMissingReported(FakeWorld world)
        {
            var picky = Arena(world);
            world.SetAbilityLevel(picky.Unit.Handle, Id, 0);
            var before = world.Printed.Count;
            picky.ResetCooldown();
            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("does not have"), "the report says why: " + world.Printed[before]);
        }
#endif
    }
}
