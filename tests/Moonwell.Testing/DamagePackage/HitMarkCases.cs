namespace Moonwell.Testing
{
    /// <summary>Cases that check a hit's miss and its plain marks, and the area and power Unit.Damage carries to a hit.</summary>
    public static class HitMarkCases
    {
        /// <summary>A hit missed before armor takes no life, makes no sound, shows one red text, and runs no later stage and none of core's reactions; the rest of its stage still runs.</summary>
        public static void MissTakesNothing(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            var watch = new StageWatch();
            Game.Instance.Add(watch);
            Game.Instance.Add(new GameHitWatch());
            var sound = -1;
            var amount = -1f;
            var hurt = false;
            target.Taking = hit => hit.Miss();
            target.Hurt = hit => hurt = true;
            fake.Hit(source.Handle, target.Handle, 500f, damageType: 4, weapon: 5, isAttack: true, inside: () =>
            {
                sound = fake.EventWeaponType();
                amount = fake.EventAmount();
            });
            fake.RunZero();
            Expect.Near(100f, fake.Body(target.Handle).Life, "the life after the missed hit of 500");
            Expect.True(!fake.Body(target.Handle).Dead, "the target is alive");
            Expect.Equal(0, sound, "the weapon sound the game was given");
            Expect.Near(0f, amount, "the amount the game was given");
            StageCases.ExpectLog(new string[] { "source dealing", "source comp dealing", "target taking", "target comp taking", "game before" }, "the log");
            Expect.True(!hurt, "no landed reaction");
            Expect.Equal(0, watch.Seen.Count, "no lethal or landed reaction on the game");
            Expect.Equal(1, world.Texts.Count, "one text");
            Expect.Equal("miss", world.Texts[0].Words, "its words");
            Expect.True(world.Texts[0].Color![0] == 255 && world.Texts[0].Color![1] == 0 && world.Texts[0].Color![2] == 0, "red");
        }

        /// <summary>Reactions after the miss in the same stage run, read Missed, and can set the amount to no effect; the burst lists the missed hit.</summary>
        public static void MissReadableLater(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            var reader = new MissReader();
            target.Add(reader);
            target.Taking = hit => hit.Miss();
            var bursts = 0;
            var listed = "";
            source.Bursted = burst =>
            {
                bursts++;
                listed = burst.Hits.Count + (burst.Hits[0].Missed ? " missed" : " hit");
            };
            fake.Hit(source.Handle, target.Handle, 40f, damageType: 4);
            fake.RunZero();
            Expect.Equal("missed", reader.Saw, "what the later reaction read");
            Expect.Near(0f, reader.AmountAfter, "the amount after the later reaction set it");
            Expect.Near(100f, fake.Body(target.Handle).Life, "no life lost");
            Expect.Equal(1, bursts, "one burst");
            Expect.Equal("1 missed", listed, "the burst's hits");
        }

        /// <summary>Miss after armor is reported in a development build and does nothing.</summary>
        public static void MissAfterArmorReported(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            var missed = true;
            target.Taken = hit => hit.Miss();
            target.Hurt = hit => missed = hit.Missed;
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            fake.RunZero();
#if DEBUG
            Expect.Equal(1, world.Logged.Count, "one error is logged in a development build");
            Expect.True(world.Logged[0].Contains("Hit.Miss can be changed only before armor"), "the report: " + world.Logged[0]);
#else
            Expect.Equal(0, world.Logged.Count, "a release build leaves the check out");
#endif
            Expect.True(!missed, "the hit did not miss");
            Expect.Near(90f, fake.Body(target.Handle).Life, "the hit took its life");
            Expect.Equal(0, world.Texts.Count, "no text");
        }

        /// <summary>An empty text shows nothing, and a given text shows once however many times Miss is called.</summary>
        public static void MissText(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            target.Taking = hit => hit.Miss("");
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            Expect.Equal(0, world.Texts.Count, "no text for an empty one");
            Expect.Near(100f, fake.Body(target.Handle).Life, "the empty miss is still a miss");
            target.Taking = hit =>
            {
                hit.Miss("dodge");
                hit.Miss("again");
            };
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            Expect.Equal(1, world.Texts.Count, "one text");
            Expect.Equal("dodge", world.Texts[0].Words, "the first call's words");
            Expect.True(world.Texts[0].X > source.X, "it starts right of the source, not over the target");
        }

        /// <summary>A miss's text starts over the source, a little right of it and low, as the game's own miss does; a hit with no source shows it over the target.</summary>
        public static void MissTextOverTheSource(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            source.MoveTo(100f, 200f);
            target.MoveTo(900f, -700f);
            target.Taking = hit => hit.Miss();
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            Expect.Equal(1, world.Texts.Count, "one text");
            Expect.Near(source.X + Hit.WordsRight, world.Texts[0].X, "right of the source's centre");
            Expect.Near(source.Y, world.Texts[0].Y, "at the source's y");
            Expect.Near(Hit.TextHeight, world.Texts[0].Height, "its height");
            fake.Hit(null, target.Handle, 10f, damageType: 4);
            Expect.Equal(2, world.Texts.Count, "a hit with no source shows one too");
            Expect.Near(target.X + Hit.WordsRight, world.Texts[1].X, "right of the target's centre");
            Expect.Near(target.Y, world.Texts[1].Y, "at the target's y");
        }

        /// <summary>The crit, blocked and absorbed marks set before armor are read at the landing; a hit starts with none.</summary>
        public static void Marks(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            var seen = new System.Collections.Generic.List<string>();
            target.Hurt = hit => seen.Add((hit.IsCrit ? "crit " : "plain ") + (int)hit.Blocked + " " + (int)hit.Absorbed);
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            target.Taking = hit =>
            {
                hit.IsCrit = true;
                hit.Blocked = 3f;
                hit.Absorbed = 4f;
            };
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            fake.RunZero();
            Expect.Equal("plain 0 0|crit 3 4", string.Join("|", seen), "the marks");
        }

        /// <summary>Unit.Damage's area and power reach the hit its reactions see and the call returns; a hit the game made, and a call with neither given, is no area's with a power of 1.</summary>
        public static void AreaAndPower(FakeWorld world)
        {
            StageCases.Arena(world, out var source, out var target);
            var fake = StageCases.Fake;
            var seen = new System.Collections.Generic.List<string>();
            target.Taken = hit => seen.Add((hit.IsArea ? "area " : "plain ") + (int)(hit.Power * 10.0f));
            var area = (Hit?)source.Damage(target, 10f, area: true, power: 0.5f);
            var plain = (Hit?)source.Damage(target, 10f);
            fake.Hit(source.Handle, target.Handle, 10f, damageType: 4);
            Expect.Equal("area 5|plain 10|plain 10", string.Join("|", seen), "what the reactions saw");
            Expect.True(area!.IsArea && !plain!.IsArea, "the returned hits' areas");
            Expect.Near(0.5f, area.Power, "the returned power");
            Expect.Near(1f, plain!.Power, "the default power");
        }
    }

    /// <summary>A target component that reads whether the hit missed in the before-armor stage, then tries to set its amount.</summary>
    public class MissReader : UnitComponent, IDamageTarget
    {
        /// <summary>"missed" or "hit", as read.</summary>
        public string Saw = "";

        /// <summary>The amount after it set 50.</summary>
        public float AmountAfter = -1f;

        /// <summary>Reads Missed and sets the amount.</summary>
        public void OnTaking(Hit hit)
        {
            Saw = hit.Missed ? "missed" : "hit";
            hit.Amount = 50f;
            AmountAfter = hit.Amount;
        }
    }
}
