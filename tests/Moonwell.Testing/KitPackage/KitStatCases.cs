namespace Moonwell.Testing
{
    /// <summary>A game component that holds modifiers in the stat cases, named in a breakdown.</summary>
    public sealed class FrostAura : GameComponent
    {
    }

    /// <summary>A buff whose OnApply gives its unit a flat modifier of a stat, held by the buff.</summary>
    public sealed class StatBuff : Buff
    {
        private readonly Stat stat;

        /// <summary>Makes a buff that adds 7 to a stat.</summary>
        public StatBuff(Stat stat)
        {
            this.stat = stat;
        }

        /// <summary>Holds the modifier.</summary>
        public override void OnApply()
        {
            Unit.Modify(stat, 7.0f, heldBy: this);
        }
    }

    /// <summary>An item whose carry holds a modifier of its carrier's stat.</summary>
    public sealed class StatRing : Item
    {
    }

    /// <summary>An item component that adds 4 to its carrier's stat, held by the item's carry.</summary>
    public sealed class RingMod : ItemComponent
    {
        private readonly Stat stat;

        /// <summary>Makes it for a stat.</summary>
        public RingMod(Stat stat)
        {
            this.stat = stat;
        }

        /// <summary>Gives the carrier the modifier.</summary>
        public override void OnPickedUp(Unit carrierUnit)
        {
            carrierUnit.Modify(stat, 4.0f, heldBy: Item.Carry);
        }
    }

    /// <summary>Cases for the kit's parts that show stats: a bar for a stat out of another, a bar's overlay, the experience bar and the stat list.</summary>
    public static class KitStatCases
    {
        private static Unit Footman(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        private static string TextOf(FakeFramesWorld frames, Frame frame)
        {
            return frames.FrameOf(frame.Handle!).Text;
        }

        /// <summary>The width the fill of a progress bar draws.</summary>
        private static float FillOf(FakeFramesWorld frames, ProgressBar bar)
        {
            return frames.FrameOf(bar.fill!).Width;
        }

        /// <summary>A bar follows a stat out of another or a number: the value, the most and the text, redrawn at once when either stat changes, and not by the poll.</summary>
        public static void StatBarFollowsStats(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var energy = new Stat("Kit energy", 50.0f, 0.0f);
            var most = new Stat("Kit most energy", 100.0f);
            var unit = Footman(world);
            var holder = Game.Instance.Add(new FrostAura());
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(unit, energy, most);
            Expect.Near(50.0f, bar.Bar.Value, "the value");
            Expect.Near(100.0f, bar.Bar.Max, "the most");
            Expect.Equal("50 / 100", TextOf(frames, bar.Label), "the text");
            Expect.Equal(Kit.Theme.StatBar, bar.Bar.Texture, "the theme's stat texture");
            Expect.Equal(false, bar.ticking, "not read by the poll");
            unit.Modify(energy, 20.0f, heldBy: holder);
            Expect.Near(70.0f, bar.Bar.Value, "redrawn when the stat changes, with no time passing");
            Expect.Equal("70 / 100", TextOf(frames, bar.Label), "and its text");
            var raise = unit.Modify(most, 100.0f, heldBy: holder);
            Expect.Near(200.0f, bar.Bar.Max, "redrawn when the other stat changes");
            Expect.Equal("70 / 200", TextOf(frames, bar.Label), "and its text");
            bar.Text = BarText.Percent;
            Expect.Equal("35%", TextOf(frames, bar.Label), "the share");
            bar.Text = BarText.None;
            Expect.Equal("", TextOf(frames, bar.Label), "no text");
            bar.Text = BarText.Values;
            var calls = frames.FrameOf(bar.Label.Handle!).Calls;
            world.Advance(0.5f);
            Expect.Equal(calls, frames.FrameOf(bar.Label.Handle!).Calls, "nothing drawn again while time passes");
            Expect.Equal(false, bar.ticking, "still no poll");
            raise.End();
            Expect.Equal("70 / 100", TextOf(frames, bar.Label), "the other stat falling is a change too");
            bar.Follow(unit, energy, 80.0f);
            Expect.Equal("70 / 80", TextOf(frames, bar.Label), "out of a number");
            unit.Modify(energy, -30.0f, heldBy: holder);
            Expect.Equal("40 / 80", TextOf(frames, bar.Label), "the stat out of a number is redrawn on its change");
            var other = Footman(world);
            other.Modify(energy, 5.0f, heldBy: holder);
            Expect.Equal("40 / 80", TextOf(frames, bar.Label), "another unit's change is not this bar's");
        }

        /// <summary>Following something else drops the old following: a stat of another unit, a life, nothing; a life bar goes on by the poll.</summary>
        public static void StatBarFollowsOneThing(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var energy = new Stat("Kit energy two", 50.0f);
            var unit = Footman(world);
            var other = Footman(world);
            other.SetBase(energy, 10.0f);
            var holder = Game.Instance.Add(new FrostAura());
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(unit, Vital.Life);
            Expect.Equal(false, Game.Instance.Has<StatWatch>(), "no watcher for a life bar");
            bar.Follow(unit, energy, 100.0f);
            Expect.Equal(1, Game.Instance.GetAll<StatWatch>().Count, "one when a bar first follows a stat");
            bar.Follow(other, energy, 100.0f);
            Expect.Equal(1, Game.Instance.GetAll<StatWatch>().Count, "and only one");
            unit.Modify(energy, 20.0f, heldBy: holder);
            Expect.Equal("10 / 100", TextOf(frames, bar.Label), "the old unit's change is not drawn");
            other.Modify(energy, 5.0f, heldBy: holder);
            Expect.Equal("15 / 100", TextOf(frames, bar.Label), "the new unit's is");
            world.SetMaxLife(unit.Handle, 450.0f);
            world.SetLife(unit.Handle, 300.0f);
            bar.Follow(unit, Vital.Life);
            Expect.Equal(Kit.Theme.LifeBar, bar.Bar.Texture, "the life texture again");
            Expect.Equal("300 / 450", TextOf(frames, bar.Label), "the life");
            unit.Modify(energy, 1.0f, heldBy: holder);
            Expect.Equal("300 / 450", TextOf(frames, bar.Label), "a stat's change no longer draws");
            world.SetLife(unit.Handle, 100.0f);
            world.Advance(0.06f);
            Expect.Equal("100 / 450", TextOf(frames, bar.Label), "the life is read by the poll");
            bar.Follow(unit, energy, 100.0f);
            world.SetLife(unit.Handle, 200.0f);
            world.Advance(0.06f);
            Expect.Equal("71 / 100", TextOf(frames, bar.Label), "following a stat again, the poll no longer reads the life");
            bar.Follow(null, energy, 100.0f);
            Expect.Equal("", TextOf(frames, bar.Label), "null empties it");
            unit.Modify(energy, 1.0f, heldBy: holder);
            Expect.Near(0.0f, bar.Bar.Value, "and follows nothing");
        }

        /// <summary>A barrier over a life bar fills a share of the bar's most, held to its width, redrawn on the stat's change and on the most's, and off with null.</summary>
        public static void OverlayOnLife(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            world.SetMaxLife(unit.Handle, 800.0f);
            world.SetLife(unit.Handle, 800.0f);
            var holder = Game.Instance.Add(new FrostAura());
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(unit);
            Expect.Equal(false, frames.FrameOf(bar.OverlayBar.Handle!).Shown, "no overlay at first");
            Expect.Equal(Kit.Theme.OverlayBar, bar.OverlayBar.Texture, "the theme's overlay texture");
            Expect.Equal(Kit.Theme.OverlayAlpha, bar.OverlayBar.Alpha, "at the theme's alpha");
            bar.Overlay(Stat.Barrier);
            Expect.Equal(true, frames.FrameOf(bar.OverlayBar.Handle!).Shown, "shown with a stat");
            Expect.Near(0.0f, FillOf(frames, bar.OverlayBar), "empty with no barrier");
            var barrier = unit.Modify(Stat.Barrier, 200.0f, heldBy: holder);
            Expect.Near(0.03f, FillOf(frames, bar.OverlayBar), "200 of 800 fills a quarter of 0.12");
            barrier.Amount = 400.0f;
            Expect.Near(0.06f, FillOf(frames, bar.OverlayBar), "redrawn on the stat's change");
            unit.Modify(Stat.Barrier, 900.0f, heldBy: holder);
            Expect.Near(bar.OverlayBar.Max, bar.OverlayBar.Value, "held to the bar's most");
            Expect.Near(0.12f, FillOf(frames, bar.OverlayBar), "never wider than the bar");
            world.SetMaxLife(unit.Handle, 2600.0f);
            world.Advance(0.06f);
            Expect.Near(1300.0f / 2600.0f * 0.12f, FillOf(frames, bar.OverlayBar), "the bar's most changing redraws it");
            bar.Overlay(null);
            Expect.Equal(false, frames.FrameOf(bar.OverlayBar.Handle!).Shown, "null takes it off");
            unit.Modify(Stat.Barrier, 1.0f, heldBy: holder);
            Expect.Near(0.0f, FillOf(frames, bar.OverlayBar), "and it no longer follows the stat");
        }

        /// <summary>An overlay on a stat bar is a share of the stat's most; set before the bar follows, it draws once it does.</summary>
        public static void OverlayOnStatBar(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var energy = new Stat("Kit energy three", 50.0f);
            var shield = new Stat("Kit shield", 25.0f);
            var unit = Footman(world);
            var holder = Game.Instance.Add(new FrostAura());
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Overlay(shield);
            bar.Follow(unit, energy, 100.0f);
            Expect.Near(0.03f, FillOf(frames, bar.OverlayBar), "25 of 100");
            unit.Modify(shield, 150.0f, heldBy: holder);
            Expect.Near(0.12f, FillOf(frames, bar.OverlayBar), "held to the width");
            unit.Modify(energy, 10.0f, heldBy: holder);
            Expect.Equal("60 / 100", TextOf(frames, bar.Label), "the bar's own stat still draws");
            Expect.Near(0.12f, FillOf(frames, bar.OverlayBar), "and the overlay stays");
            bar.Overlay(null);
            Expect.Equal(false, frames.FrameOf(bar.OverlayBar.Handle!).Shown, "off");
        }

        /// <summary>The experience bar fills with a hero's progress to its next level, reads as its experience, and is empty for a unit that is no hero.</summary>
        public static void ExperienceBar(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var hero = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(hero, Vital.Experience);
            Expect.Equal(Kit.Theme.ExperienceBar, bar.Bar.Texture, "the theme's experience texture");
            Expect.Equal("0 / 200", TextOf(frames, bar.Label), "a new hero: none of the 200 level 2 needs");
            hero.SetLevel(5);
            hero.Experience = 1700;
            world.Advance(0.06f);
            Expect.Equal("300 / 600", TextOf(frames, bar.Label), "1700 is 300 past level 5's 1400, of the 600 to 2000");
            Expect.Near(hero.LevelProgress, bar.Bar.Value / bar.Bar.Max * 100.0f, "the fill is the hero's level progress");
            bar.Text = BarText.Percent;
            Expect.Equal("50%", TextOf(frames, bar.Label), "the share");
            hero.AddExperience(-1000);
            world.Advance(0.06f);
            Expect.Near(0.0f, bar.Bar.Value, "below its level's experience: none");
            hero.SetLevel(10);
            hero.Experience = 5400;
            bar.Text = BarText.Values;
            world.Advance(0.06f);
            Expect.Equal("1000 / 1000", TextOf(frames, bar.Label), "the top level is full");
            Expect.Near(bar.Bar.Max, bar.Bar.Value, "the whole bar");
            bar.Follow(Footman(world), Vital.Experience);
            Expect.Near(0.0f, bar.Bar.Value, "a unit that is no hero shows an empty bar");
            Expect.Equal("", TextOf(frames, bar.Label), "with no text");
        }

        private static StatRow RowOf(StatList list, int index)
        {
            return list.rows[index];
        }

        /// <summary>A list has a row for each stat, its name and value: a whole stat whole, another with at most one decimal; null or no stats empties it.</summary>
        public static void StatListRows(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var power = new Stat("Kit power", 10.0f);
            var charges = new Stat("Kit charges", 3.0f, 0.0f, 9.0f, true);
            var unit = Footman(world);
            var holder = Game.Instance.Add(new FrostAura());
            var list = Screen.Add(new StatList(), Game.Instance);
            list.Show(unit, power, charges);
            Expect.Same(unit, list.Unit, "the unit");
            Expect.Equal("|cfffcd312Kit power|r", TextOf(frames, RowOf(list, 0).NameLabel), "the first name");
            Expect.Equal("10", TextOf(frames, RowOf(list, 0).ValueLabel), "a value with no decimals shows none");
            Expect.Equal("3", TextOf(frames, RowOf(list, 1).ValueLabel), "a whole stat");
            Expect.Equal(true, frames.FrameOf(RowOf(list, 1).Handle!).Shown, "both rows shown");
            unit.Modify(power, percent: 25.0f, heldBy: holder);
            Expect.Equal("12.5", TextOf(frames, RowOf(list, 0).ValueLabel), "one decimal, redrawn on the stat's change");
            unit.Modify(power, 0.02f, heldBy: holder);
            Expect.Equal("12.5", TextOf(frames, RowOf(list, 0).ValueLabel), "no more than one");
            unit.Modify(charges, 2.4f, heldBy: holder);
            Expect.Equal("5", TextOf(frames, RowOf(list, 1).ValueLabel), "a whole stat is rounded by the engine");
            list.Show(unit, charges);
            Expect.Equal("|cfffcd312Kit charges|r", TextOf(frames, RowOf(list, 0).NameLabel), "shown again with one stat");
            Expect.Equal(false, frames.FrameOf(RowOf(list, 1).Handle!).Shown, "the second row is hidden");
            list.Show(unit);
            Expect.Equal(false, frames.FrameOf(RowOf(list, 0).Handle!).Shown, "no stats empties it");
            list.Show(unit, power);
            list.Show(null);
            Expect.Equal(false, frames.FrameOf(RowOf(list, 0).Handle!).Shown, "null empties it");
            unit.Modify(power, 1.0f, heldBy: holder);
            Expect.Equal(false, frames.FrameOf(RowOf(list, 0).Handle!).Shown, "and nothing draws again");
        }

        /// <summary>A row's tooltip gives the base, each modifier that counts with its numbers and its holder's name, then those a stacking rule leaves out; it is drawn again on a change.</summary>
        public static void StatListTooltip(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            ObjectTypes.Item(ObjectTypes.FourCC("I00S"), () => new StatRing());
            var power = new Stat("Kit tip power", 10.0f);
            var unit = Footman(world);
            var holder = Game.Instance.Add(new FrostAura());
            var list = Screen.Add(new StatList(), Game.Instance);
            list.Show(unit, power);
            var tip = RowOf(list, 0).tip;
            Expect.Equal("Kit tip power", tip.Title, "titled with the stat");
            Expect.Equal("Base 10", tip.Text, "the base alone");
            var flat = unit.Modify(power, 5.0f, heldBy: holder);
            unit.Modify(power, holder, percent: 20.0f);
            unit.Modify(power, holder, times: 1.5f);
            unit.Modify(power, holder, late: 10.0f);
            Expect.Equal("Base 10\n+5 from FrostAura\n+20% from FrostAura\nx1.5 from FrostAura\n+10 after from FrostAura", tip.Text, "each kind of modifier, in the order made");
            Expect.Equal("37", TextOf(frames, RowOf(list, 0).ValueLabel), "the value agrees: (10 + 5) x 1.2 x 1.5 + 10");
            flat.Amount = -2.5f;
            Expect.Equal("Base 10\n-2.5 from FrostAura\n+20% from FrostAura\nx1.5 from FrostAura\n+10 after from FrostAura", tip.Text, "redrawn when a number is set");
            flat.End();
            Expect.Equal("Base 10\n+20% from FrostAura\nx1.5 from FrostAura\n+10 after from FrostAura", tip.Text, "and when one ends");
            var both = unit.Modify(power, holder, percent: 5.0f, late: 1.0f);
            Expect.Equal("Base 10\n+20% from FrostAura\nx1.5 from FrostAura\n+10 after from FrostAura\n+5%, +1 after from FrostAura", tip.Text, "one modifier's numbers on one line");
            both.End();
            unit.Apply(new StatBuff(power));
            var ring = Item.Create(ObjectTypes.FourCC("I00S"), 0.0f, 0.0f, Game.Instance);
            ring.Add(new RingMod(power));
            unit.Give(ring);
            Expect.True(tip.Text.Contains("+7 from StatBuff"), "a buff is named by its class");
            Expect.True(tip.Text.Contains("+4 from StatRing"), "an item's carry is named by its item");
            var strong = unit.Modify(power, 8.0f, heldBy: holder, group: "ring");
            unit.Modify(power, 3.0f, heldBy: holder, group: "ring");
            world.Advance(0.06f);
            Expect.True(tip.Text.EndsWith("+8 from FrostAura\nLeft out:\n+3 from FrostAura"), "a weaker one of a strongest group is listed after as left out");
            strong.End();
            Expect.True(tip.Text.EndsWith("+3 from FrostAura") && !tip.Text.Contains("Left out"), "when the strong one ends the other counts");
        }

        /// <summary>A unit frame has an empty Stats list that draws nothing and moves nothing until the map shows stats in it; showing another unit empties it.</summary>
        public static void UnitFrameStats(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var power = new Stat("Kit frame power", 10.0f);
            var unit = Footman(world);
            var other = Footman(world);
            var frame = Screen.Add(new UnitFrame(), Game.Instance);
            Expect.Same(frame, frame.Stats.Parent, "a piece of the frame");
            Expect.Equal(0, frame.Stats.rows.Count, "no rows");
            var buffs = frames.FrameOf(frame.Buffs.Handle!);
            var life = frames.FrameOf(frame.Life.Handle!);
            var before = life.Points[0].Y;
            frame.Show(unit);
            Expect.Near(before, frames.FrameOf(frame.Life.Handle!).Points[0].Y, "the layout does not move");
            Expect.Equal(0, frame.Stats.rows.Count, "still empty after Show");
            Expect.True(frames.FrameOf(frame.Stats.Handle!).Points[0].Y < buffs.Points[0].Y - frame.Buffs.Height + 0.0001f, "below the buffs");
            frame.Stats.Show(unit, power);
            Expect.Equal("10", TextOf(frames, frame.Stats.rows[0].ValueLabel), "the map's stats show");
            frame.Show(unit);
            Expect.Equal(true, frames.FrameOf(frame.Stats.rows[0].Handle!).Shown, "the same unit again keeps them");
            frame.Show(other);
            Expect.Equal(false, frames.FrameOf(frame.Stats.rows[0].Handle!).Shown, "another unit empties them");
        }
    }
}
