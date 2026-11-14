namespace Moonwell.Testing
{
    /// <summary>A buff with a kit icon, lasting 6 seconds.</summary>
    public sealed class IconBuff : Buff
    {
        /// <summary>Makes it.</summary>
        public IconBuff()
        {
            Duration = 6f;
            HudIcon = @"ReplaceableTextures\CommandButtons\BTNImmolationOn.blp";
        }
    }

    /// <summary>A buff with no icon and no game buff, which a BuffRow leaves out.</summary>
    public sealed class PlainBuff : Buff
    {
    }

    /// <summary>A game component that logs "changed 0: 2" with the player's slot and how many units it has selected.</summary>
    public sealed class SelectionChangedGame : GameComponent
    {
        /// <summary>Logs the change.</summary>
        public override void OnSelectionChanged(Player player)
        {
            TestLog.Lines.Add("changed " + player.Slot + ": " + player.Selection.Count);
        }
    }

    /// <summary>A player component that logs "player 0 changed 0".</summary>
    public sealed class SelectionChangedPlayer : PlayerComponent
    {
        /// <summary>Logs the change.</summary>
        public override void OnSelectionChanged(Player player)
        {
            TestLog.Lines.Add("player " + Player.Slot + " changed " + player.Slot);
        }
    }

    /// <summary>Cases for the kit's HUD parts (decisions 334 to 342): bars, unit frames, action buttons, cast bars, buff rows and the selection list.</summary>
    public static class KitHudCases
    {
        private const int Click = 1;

        private static Unit Footman(FakeWorld world, int slot = 0)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: slot))!;
        }

        /// <summary>A bar follows a unit's life: the fill's range and value, and the text, read again 20 times a second; Percent and None change the text; mana takes the theme's mana texture; null empties it.</summary>
        public static void StatusBarFollows(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            world.SetMaxLife(unit.Handle, 450f);
            world.SetLife(unit.Handle, 300f);
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(unit);
            Expect.Near(450f, bar.Bar.Max, "the most");
            Expect.Near(300f, bar.Bar.Value, "the life");
            Expect.Equal("300 / 450", frames.FrameOf(bar.Label.Handle!).Text, "the text");
            Expect.Equal(Kit.Theme.LifeBar, bar.Bar.Texture, "the theme's life texture");
            world.SetLife(unit.Handle, 199.5f);
            world.Advance(0.06f);
            Expect.Near(199.5f, bar.Bar.Value, "read again");
            Expect.Equal("200 / 450", frames.FrameOf(bar.Label.Handle!).Text, "rounded up, as the game shows life");
            bar.Text = BarText.Percent;
            Expect.Equal("45%", frames.FrameOf(bar.Label.Handle!).Text, "the share");
            bar.Text = BarText.None;
            Expect.Equal("", frames.FrameOf(bar.Label.Handle!).Text, "no text");
            bar.Text = BarText.Values;
            world.SetMaxMana(unit.Handle, 100f);
            world.SetMana(unit.Handle, 25f);
            bar.Follow(unit, Vital.Mana);
            Expect.Equal(Kit.Theme.ManaBar, bar.Bar.Texture, "the theme's mana texture");
            Expect.Equal("25 / 100", frames.FrameOf(bar.Label.Handle!).Text, "the mana");
            bar.Follow(null);
            Expect.Near(0f, bar.Bar.Value, "null empties it");
            Expect.Equal("", frames.FrameOf(bar.Label.Handle!).Text, "with no text");
        }

        /// <summary>A bar redraws only on a change: its text is not given again while the life stands still; a unit with no mana shows no text.</summary>
        public static void StatusBarOnlyOnChange(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Follow(unit, Vital.Mana);
            Expect.Equal("", frames.FrameOf(bar.Label.Handle!).Text, "no mana, no text");
            bar.Follow(unit);
            var calls = frames.FrameOf(bar.Label.Handle!).Calls;
            world.Advance(0.5f);
            Expect.Equal(calls, frames.FrameOf(bar.Label.Handle!).Calls, "nothing given again in ten readings");
        }

        /// <summary>A unit frame is hidden until Show: then it shows the unit's icon (this machine's, Texture reading empty), name, level and bars; its pieces can be placed again; null hides it.</summary>
        public static void UnitFrameShows(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            world.SetIcon(unit.TypeId, @"ReplaceableTextures\CommandButtons\BTNFootman.blp");
            world.RenameUnit(unit.Handle, "Footman here");
            world.SetMaxLife(unit.Handle, 420f);
            world.SetLife(unit.Handle, 420f);
            var frame = Screen.Add(new UnitFrame(), Game.Instance);
            Expect.Equal(false, frames.FrameOf(frame.Handle!).Shown, "hidden until shown a unit");
            Expect.Equal("EscMenuControlBackdropTemplate", frames.FrameOf(frame.Handle!).Template, "the theme's Hud border");
            frame.Show(unit);
            Expect.Equal(true, frames.FrameOf(frame.Handle!).Shown, "shown");
            Expect.Equal(@"ReplaceableTextures\CommandButtons\BTNFootman.blp", frames.FrameOf(frame.Portrait.Handle!).Texture, "this machine's icon");
            Expect.Equal("", frame.Portrait.Texture, "Texture reads empty");
            Expect.Equal("|cfffcd312Footman here|r", frames.FrameOf(frame.NameLabel.Handle!).Text, "the name, in the theme's gold");
            Expect.Equal(unit.Level.ToString(), frames.FrameOf(frame.LevelLabel.Handle!).Text, "the level");
            Expect.Equal("420 / 420", frames.FrameOf(frame.Life.Label.Handle!).Text, "the life bar");
            Expect.Same(unit, frame.Buffs.Unit, "the buffs follow it");
            Screen.DesignHeight = 768f;
            try
            {
                frame.Add(frame.Life, 90f, 45f, 119f, 12f);
            }
            finally
            {
                Screen.DesignHeight = 0.6f;
            }

            var life = frames.FrameOf(frame.Life.Handle!);
            Expect.Near(119f * 0.6f / 768f, life.Width, "placed again by the map, in its own units");
            Expect.Near(90f * 0.6f / 768f, life.Points[0].X, "at its x");
            frame.Show(null);
            Expect.Equal(false, frames.FrameOf(frame.Handle!).Shown, "null hides it");
            Expect.True(frame.Life.Unit == null, "and the bars follow nothing");
        }

        /// <summary>A cooldown darkens the button whole and uncovers it in 64ths as it runs, then draws nothing; Hotkey and Count write their corners.</summary>
        public static void ActionButtonCooldown(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var button = Screen.Add(new ActionButton { Icon = @"ReplaceableTextures\CommandButtons\BTNFireBolt.blp", Hotkey = "Q", Count = 3 }, Game.Instance);
            Expect.Equal("Q", frames.FrameOf(button.HotkeyLabel.Handle!).Text, "the hotkey");
            Expect.Equal("3", frames.FrameOf(button.CountLabel.Handle!).Text, "the count");
            button.Count = null;
            Expect.Equal("", frames.FrameOf(button.CountLabel.Handle!).Text, "null shows no count");
            Expect.Equal(Image.Blank, frames.FrameOf(button.Sweep.Handle!).Texture, "no sweep before a cooldown");
            button.Cooldown(3f);
            Expect.Equal(@"MoonwellKit\Sweep64.blp", frames.FrameOf(button.Sweep.Handle!).Texture, "dark whole at first");
            world.Advance(1.56f);
            Expect.Equal(@"MoonwellKit\Sweep31.blp", frames.FrameOf(button.Sweep.Handle!).Texture, "31 ticks in, 1.45 s of 3 left: 31 64ths dark");
            Expect.True(button.CooldownLeft > 1.4f && button.CooldownLeft < 1.5f, "the time left");
            world.Advance(2f);
            Expect.Equal(Image.Blank, frames.FrameOf(button.Sweep.Handle!).Texture, "nothing once it is up");
            Expect.Near(0f, button.CooldownLeft, "none left");
            button.Cooldown(5f);
            button.Cooldown(0f);
            Expect.Equal(Image.Blank, frames.FrameOf(button.Sweep.Handle!).Texture, "0 ends one");
        }

        /// <summary>A cast bar is hidden until Start, fills over the seconds and hides; Interrupt fills it red, says Interrupted and fades it out; a Start meanwhile shows it again.</summary>
        public static void CastBarRuns(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var cast = Screen.Add(new CastBar(), Game.Instance);
            Expect.Equal(false, frames.FrameOf(cast.Handle!).Shown, "hidden until started");
            cast.Start("Fireball", 1f, @"ReplaceableTextures\CommandButtons\BTNFireBolt.blp");
            Expect.Equal(true, frames.FrameOf(cast.Handle!).Shown, "shown");
            Expect.Equal("Fireball", frames.FrameOf(cast.Label.Handle!).Text, "the name");
            Expect.Equal(true, frames.FrameOf(cast.Icon.Handle!).Shown, "the icon");
            world.Advance(0.51f);
            Expect.True(cast.Bar.Value > 0.45f && cast.Bar.Value < 0.55f, "half filled");
            world.Advance(0.6f);
            Expect.Equal(false, frames.FrameOf(cast.Handle!).Shown, "hidden at the end");
            Expect.Equal(false, cast.Casting, "no cast");
            cast.Start("Frost", 2f);
            Expect.Equal(false, frames.FrameOf(cast.Icon.Handle!).Shown, "no icon given, none shown");
            world.Advance(0.2f);
            cast.Interrupt();
            Expect.Equal(Kit.Theme.Interrupted, cast.Bar.Texture, "the theme's interrupted texture");
            Expect.Near(2f, cast.Bar.Value, "filled");
            Expect.Equal("Interrupted", frames.FrameOf(cast.Label.Handle!).Text, "says so");
            world.Advance(0.6f);
            Expect.Equal(false, frames.FrameOf(cast.Handle!).Shown, "faded out and hidden");
            cast.Start("Again", 1f);
            Expect.Equal(true, frames.FrameOf(cast.Handle!).Shown, "shown again");
            Expect.Equal(255, frames.FrameOf(cast.Handle!).Alpha, "not faded");
            Expect.Equal(Kit.Theme.CastBar, cast.Bar.Texture, "in the cast texture again");
        }

        /// <summary>A buff row shows the buffs with an icon and their time left, leaves out one with no icon, and hides a slot when its buff ends.</summary>
        public static void BuffRowShows(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            var row = Screen.Add(new BuffRow(), Game.Instance);
            row.Show(unit);
            unit.Apply(new PlainBuff());
            unit.Apply(new IconBuff());
            world.Advance(0.06f);
            Expect.Equal(1, row.slots.Count, "one slot: the plain buff has no icon");
            var slot = row.slots[0];
            Expect.Equal(true, frames.FrameOf(slot.Handle!).Shown, "shown");
            Expect.Equal(@"ReplaceableTextures\CommandButtons\BTNImmolationOn.blp", frames.FrameOf(slot.Icon.Handle!).Texture, "the buff's icon");
            Expect.Equal("6", frames.FrameOf(slot.Time.Handle!).Text, "its seconds left, rounded up");
            world.Advance(2f);
            Expect.Equal("4", frames.FrameOf(slot.Time.Handle!).Text, "counting down");
            world.Advance(4.1f);
            Expect.Equal(false, frames.FrameOf(slot.Handle!).Shown, "hidden once it ends");
            Expect.Equal("2m", BuffSlot.TimeLeft(61f), "minutes from a minute on");
            Expect.Equal("", BuffSlot.TimeLeft(0f), "nothing with no expiry");
        }

        /// <summary>A selection list shows one entry per unit selected, in order, changes when the selection does, drops a unit that dies, and a click on an entry selects that unit alone for the player who clicked.</summary>
        public static void SelectionListFollows(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var first = Footman(world);
            var second = Footman(world);
            var list = Screen.Add(new SelectionList(), Game.Instance, Player.Get(0));
            list.Follow(Player.Get(0));
            world.PlayerSelects(first.Handle, 0);
            world.PlayerSelects(second.Handle, 0);
            world.Advance(0.01f);
            Expect.Equal(2, list.Entries.Count, "two entries");
            Expect.Same(first, list.Entries[0].Unit, "in the order selected");
            Expect.Same(second, list.Entries[1].Unit, "the second");
            Expect.Near(0.14f, list.Entries[0].Width, "Kit's small unit frame");
            world.PlayerSelects(first.Handle, 0, false);
            world.Advance(0.01f);
            Expect.Equal(1, list.Entries.Count, "one left");
            Expect.Same(second, list.Entries[0].Unit, "the second, first now");
            Expect.Equal(false, frames.FrameOf(list.entries[1].Handle!).Shown, "the other entry hidden");
            world.SelectionCalls.Clear();
            frames.Fire(frames.FrameOf(list.clicks[0].MouseHandle!), Click, 0);
            Expect.Equal("0 clear, 0 select", string.Join(", ", world.SelectionCalls.ToArray()), "the click selects that unit alone");
            second.Kill();
            world.Advance(0.06f);
            Expect.Equal(0, list.Entries.Count, "a unit that died leaves at once");
        }

        /// <summary>Entry makes a map's own entry class.</summary>
        public static void SelectionListEntry(FakeWorld world)
        {
            FrameCases.Installed();
            var unit = Footman(world);
            var list = Screen.Add(new SelectionList { Entry = () => new UnitFrame { Width = 0.3f } }, Game.Instance);
            list.Follow(Player.Get(0));
            world.PlayerSelects(unit.Handle, 0);
            world.Advance(0.01f);
            Expect.Near(0.3f, list.Entries[0].Width, "the map's own entry");
            Expect.Near(0.3f, list.CellWidth, "the cells sized to it");
        }

        /// <summary>OnSelectionChanged runs once for a change of several units in one instant, for the game's components and then the player's own, not for another player's, and not when the selection is the same again.</summary>
        public static void SelectionChangedOnce(FakeWorld world)
        {
            var a = Footman(world);
            var b = Footman(world);
            Game.Instance.Add(new SelectionChangedGame());
            Player.Get(0).Add(new SelectionChangedPlayer());
            Player.Get(1).Add(new SelectionChangedPlayer());
            TestLog.Lines.Clear();
            world.PlayerSelects(a.Handle, 0);
            world.PlayerSelects(b.Handle, 0);
            Expect.Equal(0, TestLog.Lines.Count, "not inside the events");
            world.Advance(0.01f);
            Expect.Equal("changed 0: 2, player 0 changed 0", string.Join(", ", TestLog.Lines.ToArray()), "once, the game's then the player's own");
            TestLog.Lines.Clear();
            world.PlayerSelects(a.Handle, 0, false);
            world.PlayerSelects(b.Handle, 0, false);
            world.PlayerSelects(a.Handle, 0);
            world.PlayerSelects(b.Handle, 0);
            world.Advance(0.01f);
            Expect.Equal(0, TestLog.Lines.Count, "the same units in the same order: no change");
            world.PlayerSelects(a.Handle, 0, false);
            world.Advance(0.01f);
            Expect.Equal("changed 0: 1, player 0 changed 0", string.Join(", ", TestLog.Lines.ToArray()), "a deselect is a change");
        }
    }
}
