namespace Moonwell.Testing
{
    /// <summary>Cases for a theme on every kit part (J64, J84, J63, J70, J110): each part's own Theme passed down to its pieces, the text sizes and font, the close button, a bar keeping a map's texture, and a list's theme taken when it is made.</summary>
    public static class KitThemeCases
    {
        private const int Click = 1;

        private sealed class Swing : Talent
        {
            public Swing()
            {
                Name = "Swing";
                MaxRank = 2;
            }
        }

        /// <summary>A theme of a map's own, as WoW's: other textures, a border, a font and three text sizes.</summary>
        private static Theme Wow()
        {
            return new Theme
            {
                Window = "WowWindow",
                Hud = "WowBorder",
                Backing = "wow-backing.blp",
                LifeBar = "wow-life.blp",
                ManaBar = "wow-mana.blp",
                CastBar = "wow-cast.blp",
                Title = new Color(255, 0, 0),
                Text = new Color(0, 255, 0),
                Font = @"war3mapImported\Wow.ttf",
                TitleSize = 0.015f,
                TextSize = 0.01f,
                BarTextSize = 0.008f,
            };
        }

        private static Unit Footman(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
        }

        private static string Text(int step)
        {
            return Label.TextTemplate(@"war3mapImported\Wow.ttf", step, false);
        }

        /// <summary>A unit frame given a theme of its own takes its border and passes it down: the bars' textures and text, the name's and level's colours, sizes and font, the buffs' times and the stats' rows; Kit.Theme is left as it was, and a part made after takes Kit.Theme.</summary>
        public static void PartsTakeTheirOwn(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var unit = Footman(world);
            var wow = Wow();
            var frame = Screen.Add(new UnitFrame { Theme = wow }, Game.Instance);
            frame.Show(unit);
            unit.Apply(new IconBuff());
            frame.Stats.Show(unit, Stat.Armor);
            world.Advance(0.06f);
            Expect.Same(wow, frame.Theme, "its own theme");
            Expect.Same(Theme.PlayersRace, Kit.Theme, "Kit.Theme left as it was");
            Expect.Equal("WowBorder", frames.FrameOf(frame.Handle!).Template, "the theme's Hud border");
            Expect.Same(wow, frame.Life.Theme, "passed down to the life bar");
            Expect.Equal("wow-life.blp", frame.Life.Bar.Texture, "the theme's life fill");
            Expect.Equal("wow-mana.blp", frame.Mana.Bar.Texture, "the theme's mana fill");
            Expect.Equal("wow-backing.blp", frame.Life.Backing.Texture, "the theme's backing");
            Expect.Equal(Text(16), frames.FrameOf(frame.Life.Label.Handle!).Template, "the bar text's size and font");
            Expect.Equal(Text(20), frames.FrameOf(frame.NameLabel.Handle!).Template, "the name's size and font");
            Expect.Equal(Text(20), frames.FrameOf(frame.LevelLabel.Handle!).Template, "the level's");
            Expect.Equal(255, frame.NameLabel.Color.R, "the name in the theme's title colour");
            Expect.Equal(255, frame.LevelLabel.Color.G, "the level in its text colour");
            Expect.Equal(Text(20), frames.FrameOf(frame.Buffs.slots[0].Time.Handle!).Template, "a buff's time");
            Expect.Equal(255, frame.Buffs.slots[0].Time.Color.G, "in the text colour");
            Expect.Equal(Text(20), frames.FrameOf(frame.Stats.rows[0].NameLabel.Handle!).Template, "a stat's row");
            Expect.Same(wow, frame.Stats.rows[0].tip.Theme, "and its tooltip");
            var plain = Screen.Add(new UnitFrame(), Game.Instance);
            Expect.Same(Theme.PlayersRace, plain.Theme, "a part made after takes Kit.Theme");
            Expect.Equal("", frames.FrameOf(plain.NameLabel.Handle!).Template, "at the game's text");
            Expect.Equal(0, world.Logged.Count, "nothing reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>The other parts take a theme of their own: a cast bar's border, fill and text; an action button's texts; a rich tooltip's template, colours and sizes; a window's title.</summary>
        public static void OtherParts(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var wow = Wow();
            var cast = Screen.Add(new CastBar { Theme = wow }, Game.Instance);
            cast.Start("Fireball", 1f);
            Expect.Equal("WowBorder", frames.FrameOf(cast.Handle!).Template, "the cast bar's border");
            Expect.Equal("wow-cast.blp", cast.Bar.Texture, "its fill");
            Expect.Equal(Text(16), frames.FrameOf(cast.Label.Handle!).Template, "its text at the bar text's size");
            var button = Screen.Add(new ActionButton { Theme = wow, Hotkey = "Q" }, Game.Instance);
            Expect.Equal(Text(20), frames.FrameOf(button.HotkeyLabel.Handle!).Template, "the hotkey's size and font");
            Expect.Equal(255, button.CountLabel.Color.G, "the count in the text colour");
            var tip = new RichTooltip { Theme = wow, Title = "Sword" };
            Expect.Equal("WowWindow", tip.Template, "the tooltip's template");
            Expect.Equal(255, tip.title.Color.R, "its title colour");
            Expect.Near(0.015f, tip.title.Size, "its title size");
            Expect.Near(0.01f, tip.words.Size, "its text size");
            Expect.Equal(@"war3mapImported\Wow.ttf", tip.cost.Font, "its font");
            var window = Screen.Add(new Window { Theme = wow, Title = "Bag" }, Game.Instance);
            Expect.Equal(Text(30), frames.FrameOf(window.title.Handle!).Template, "a window's title at the title size");
        }

        /// <summary>A bar keeps a fill texture the map set before Follow, through following life, mana and another theme; the backing too. One left to the theme follows what is followed.</summary>
        public static void BarKeepsMapTexture(FakeWorld world)
        {
            FrameCases.Installed();
            var unit = Footman(world);
            var bar = Screen.Add(new StatusBar(), Game.Instance);
            bar.Bar.Texture = "map-fill.blp";
            bar.Backing.Texture = "map-backing.blp";
            bar.Follow(unit);
            Expect.Equal("map-fill.blp", bar.Bar.Texture, "kept at the first Follow");
            bar.Follow(unit, Vital.Mana);
            Expect.Equal("map-fill.blp", bar.Bar.Texture, "kept following mana");
            bar.Theme = Wow();
            Expect.Equal("map-fill.blp", bar.Bar.Texture, "kept in another theme");
            Expect.Equal("map-backing.blp", bar.Backing.Texture, "the backing kept");
            var plain = Screen.Add(new StatusBar(), Game.Instance);
            plain.Follow(unit, Vital.Mana);
            Expect.Equal(Kit.Theme.ManaBar, plain.Bar.Texture, "the theme's mana fill");
            plain.Theme = Wow();
            Expect.Equal("wow-mana.blp", plain.Bar.Texture, "another theme's");
            plain.Follow(unit);
            Expect.Equal("wow-life.blp", plain.Bar.Texture, "its life fill once following life");
        }

        /// <summary>A window's close button: the game's dialog button with an X label inside it unless the theme names a Close texture, then an icon button of it at the theme's size and inset, which closes the window; Closable hides whichever it is.</summary>
        public static void CloseButton(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var plain = Screen.Add(new Window(), Game.Instance);
            var button = frames.FrameOf(plain.close.Handle!);
            Expect.Equal("ScriptDialogButton", button.Template, "the game's dialog button");
            Expect.Equal("", button.Text, "with no text of its own, which its edge cut");
            Expect.Equal("|cfffcd312X|r", frames.FrameOf(plain.closeMark.Handle!).Text, "an X label over it, in the title colour");
            Expect.Same(plain.close, plain.CloseButton, "the close button read");
            Expect.True(plain.closeIcon == null, "no icon button made");
            var theme = new Theme { Close = "wow-close.blp", CloseSize = 0.02f, CloseInset = 0.008f };
            var wow = Screen.Add(new Window { Theme = theme }, Game.Instance);
            var icon = wow.closeIcon!;
            Expect.Same(icon, wow.CloseButton, "the icon button read");
            Expect.Equal("wow-close.blp", icon.Icon, "the theme's texture");
            Expect.Near(0.02f, frames.FrameOf(icon.Handle!).Width, "its size");
            Expect.Near(-0.008f, frames.FrameOf(icon.Handle!).Points[0].X, "its inset");
            Expect.Equal(true, frames.FrameOf(icon.Handle!).Shown, "shown");
            Expect.Equal(false, frames.FrameOf(wow.close.Handle!).Shown, "the game's button hidden");
            var closedBy = -1;
            wow.Closed += who => closedBy = who.Slot;
            frames.Fire(frames.FrameOf(icon.MouseHandle!), Click, 1);
            Expect.Equal(1, closedBy, "it closes the window");
            Expect.Equal(false, wow.Visible, "hidden");
            wow.Visible = true;
            wow.Closable = false;
            Expect.Equal(false, frames.FrameOf(icon.Handle!).Shown, "Closable hides it");
            Expect.Equal(false, frames.FrameOf(wow.close.Handle!).Shown, "and the game's stays hidden");
            wow.Closable = true;
            Expect.Equal(true, frames.FrameOf(icon.Handle!).Shown, "shown again");
        }

        /// <summary>A selection list's own entries take the theme of the moment it was made, not of its first growth; Compact is a map's to call on its own entry.</summary>
        public static void ListThemeWhenMade(FakeWorld world)
        {
            FrameCases.Installed();
            var unit = Footman(world);
            var wow = Wow();
            Kit.Theme = wow;
            SelectionList list;
            SelectionList compact;
            try
            {
                list = Screen.Add(new SelectionList(), Game.Instance);
                compact = Screen.Add(new SelectionList(), Game.Instance);
            }
            finally
            {
                Kit.Theme = Theme.PlayersRace;
            }

            compact.Entry = () =>
            {
                var entry = new UnitFrame();
                entry.Compact();
                return entry;
            };
            list.Follow(Player.Get(0));
            compact.Follow(Player.Get(0));
            world.PlayerSelects(unit.Handle, 0);
            world.Advance(0.01f);
            Expect.Same(wow, list.Theme, "the list's theme, as it was made");
            Expect.Same(wow, list.Entries[0].Theme, "its entry in it");
            Expect.Equal("wow-life.blp", list.Entries[0].Life.Bar.Texture, "down to the bars");
            Expect.Near(0.14f, compact.Entries[0].Width, "a map's entry laid out small by Compact");
            Expect.Equal(false, compact.Entries[0].LevelLabel.Visible, "no level");
        }

        /// <summary>A talent window's tooltips and texts take the window's theme, set when it is made or after.</summary>
        public static void TalentTipsTakeWindowTheme(FakeWorld world)
        {
            FrameCases.Installed();
            var hero = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            var wow = Wow();
            var tree = new TalentWindow(hero) { Theme = wow };
            tree.Add(() => new Swing());
            Expect.Same(wow, ((RichTooltip)tree.buttons[0].Tooltip!).Theme, "the tooltip in the window's theme");
            Expect.Near(0.01f, tree.rankLabels[0].Size, "the rank at the text size");
            var orc = new Theme { Window = "OrcTip" };
            tree.Theme = orc;
            Expect.Equal("OrcTip", ((RichTooltip)tree.buttons[0].Tooltip!).Template, "passed on when set after");
            var shop = new ShopWindow { Theme = wow };
            shop.Sell(new ShopEntry { Name = "Sword" });
            Expect.Same(wow, ((RichTooltip)shop.buttons[0].Tooltip!).Theme, "a shop's goods in the shop's theme");
        }
    }
}
