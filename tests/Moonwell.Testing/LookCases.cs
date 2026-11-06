namespace Moonwell.Testing
{
    /// <summary>Cases that check weather, lightning, vision, pings and minimap icons: what the game is asked, and how each ends.</summary>
    public static class LookCases
    {
        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static Unit NewFootman(FakeWorld world, float x, float y)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, x, y);
            return Unit.Of(handle)!;
        }

        /// <summary>Weather with no rect covers the world's bounds, and is turned on for everyone.</summary>
        public static void WeatherWholeMap(FakeWorld world)
        {
            var rain = Weather.Create(WeatherKind.LordaeronHeavyRain, Game.Instance);
            Expect.Equal(1, world.Weathers.Count, "weather made");
            var made = world.Weathers[0];
            Expect.Equal("RLhr", made.Kind, "the id given to the game");
            Expect.Near(-4096f, made.Left, "the world's left");
            Expect.Near(4096f, made.Top, "the world's top");
            Expect.Equal(true, made.Shown, "turned on");
            Expect.Equal("RLhr", rain.Kind, "the kind reads the id");
            Expect.Near(-4096f, rain.Bottom, "the bottom reads the world's");
            Expect.Equal(true, rain.IsShownTo(Player.Two), "everyone sees it");
        }

        /// <summary>Weather over a rect ends with its scope, once, and weather made in an ended scope is never made.</summary>
        public static void WeatherRectEnds(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var snow = Weather.Create(WeatherKind.NorthrendLightSnow, -100f, -200f, 300f, 400f, holder);
            var made = world.Weathers[0];
            Expect.Near(-200f, made.Bottom, "the bottom given to the game");
            Expect.Near(300f, made.Right, "the right given to the game");
            holder.End();
            Expect.Equal(true, snow.HasEnded, "ended with its scope");
            Expect.Equal(true, made.Ended, "removed in the game");
            var late = Weather.Create(WeatherKind.NorthrendLightSnow, 0f, 0f, 1f, 1f, holder);
            Expect.Equal(true, late.HasEnded, "ended at once");
            Expect.Equal(1, world.Weathers.Count, "the game is not asked again");
            Expect.True(late.Handle == null, "no handle");
        }

        /// <summary>Weather shown to another player is made here too, and left off.</summary>
        public static void WeatherShownToOther(FakeWorld world)
        {
            var fog = Weather.Create(WeatherKind.DungeonBlueFogLight, Game.Instance, shownTo: Player.Two);
            Expect.Equal(1, world.Weathers.Count, "made at this machine too");
            Expect.Equal(false, world.Weathers[0].Shown, "left off here");
            Expect.Equal(true, fog.IsShownTo(Player.Two), "the player sees it");
            Expect.Equal(false, fog.IsShownTo(Player.One), "this player does not");
            Weather.Create(WeatherKind.DungeonBlueFogLight, Game.Instance, shownTo: Player.One);
            Expect.Equal(true, world.Weathers[1].Shown, "turned on for this machine's player");
        }

        /// <summary>A lightning between points is drawn 60 over the ground at each end, moves with MoveTo and its heights, and ends once.</summary>
        public static void LightningPoints(FakeWorld world)
        {
            world.Ground = (x, y) => x > 500f ? 100f : 0f;
            var bolt = Lightning.Create(LightningKind.AerialShackles, 0f, 0f, 600f, 0f, Game.Instance);
            var made = world.Bolts[0];
            Expect.Equal("LEAS", made.Kind, "the code given to the game");
            Expect.Near(60f, made.Z1, "the start's height");
            Expect.Near(160f, made.Z2, "the end's height over raised ground");
            bolt.MoveTo(10f, 20f, 30f, 40f);
            Expect.Near(20f, made.Y1, "moved start");
            Expect.Near(30f, made.X2, "moved end");
            Expect.Near(40f, bolt.Y2, "the end reads the move");
            bolt.FromHeight = 200f;
            Expect.Near(200f, made.Z1, "a height redraws it");
            bolt.End();
            Expect.Equal(true, made.Ended, "removed in the game");
            bolt.MoveTo(1f, 1f, 1f, 1f);
            bolt.ToHeight = 5f;
            Expect.Near(60f, bolt.ToHeight, "a set after the end does nothing");
        }

        /// <summary>A lightning between units follows them every 1/32 s, with a unit's flying height, and stops when it ends.</summary>
        public static void LightningFollows(FakeWorld world)
        {
            var a = NewFootman(world, 0f, 0f);
            var b = NewFootman(world, 300f, 0f);
            var bolt = Lightning.Between(LightningKind.DrainLife, a, b, Game.Instance);
            var made = world.Bolts[0];
            Expect.Near(300f, made.X2, "drawn to the second unit");
            Expect.Same(a, bolt.From, "the first unit");
            world.PlaceUnit(b.Handle, 500f, 100f);
            b.FlyHeight = 40f;
            world.Advance(0.04f);
            Expect.Near(500f, made.X2, "follows the second unit's x");
            Expect.Near(100f, made.Y2, "follows the second unit's y");
            Expect.Near(100f, made.Z2, "its flying height and 60");
            Expect.Near(500f, bolt.X2, "reads the unit's place");
            bolt.End();
            var moves = made.Moves;
            world.Advance(0.2f);
            Expect.Equal(moves, made.Moves, "no move after the end");
            Expect.Equal(true, made.Ended, "removed in the game");
        }

        /// <summary>A lightning between units ends when either unit ends, and one on a unit that is gone is never made.</summary>
        public static void LightningEndsWithUnit(FakeWorld world)
        {
            var a = NewFootman(world, 0f, 0f);
            var b = NewFootman(world, 300f, 0f);
            var bolt = Lightning.Between(LightningKind.DrainMana, a, b, Game.Instance);
            b.Remove();
            Expect.Equal(true, bolt.HasEnded, "ended with the unit");
            Expect.Equal(true, world.Bolts[0].Ended, "removed in the game");
            a.Remove();
            Expect.Equal(true, world.Bolts[0].Ended, "removed once: a second removal fails the case");
            var late = Lightning.Between(LightningKind.DrainMana, a, b, Game.Instance);
            Expect.Equal(true, late.HasEnded, "ended at once");
            Expect.Equal(1, world.Bolts.Count, "the game is not asked again");
        }

        /// <summary>PlayOnce draws a lightning and removes it 2 s later, and draws none for a unit that is gone.</summary>
        public static void LightningPlayOnce(FakeWorld world)
        {
            var a = NewFootman(world, 0f, 0f);
            var b = NewFootman(world, 300f, 0f);
            Lightning.PlayOnce(LightningKind.ChainLightningPrimary, a, b);
            Lightning.PlayOnce(LightningKind.ChainLightningSecondary, 0f, 0f, 10f, 10f);
            Expect.Equal(2, world.Bolts.Count, "drawn");
            Expect.Near(300f, world.Bolts[0].X2, "to the second unit");
            world.Advance(1.9f);
            Expect.Equal(false, world.Bolts[0].Ended, "still there before 2 s");
            world.Advance(0.2f);
            Expect.Equal(true, world.Bolts[0].Ended, "removed after 2 s");
            Expect.Equal(true, world.Bolts[1].Ended, "the one between points too");
            b.Remove();
            Lightning.PlayOnce(LightningKind.ChainLightningPrimary, a, b);
            Expect.Equal(2, world.Bolts.Count, "none for a unit that is gone");
        }

        /// <summary>A saved game's load drops the game's lightning (I36): the lasting ones are made again, where they are, and go on; a PlayOnce bolt from before the load is left to the game.</summary>
        public static void LightningAfterLoad(FakeWorld world)
        {
            var a = NewFootman(world, 0f, 0f);
            var b = NewFootman(world, 300f, 0f);
            var points = Lightning.Create(LightningKind.AerialShackles, 0f, 0f, 600f, 0f, Game.Instance);
            points.FromHeight = 200f;
            var link = Lightning.Between(LightningKind.DrainLife, a, b, Game.Instance);
            Lightning.PlayOnce(LightningKind.ChainLightningPrimary, 0f, 0f, 10f, 10f);
            var ended = Lightning.Create(LightningKind.DrainMana, 0f, 0f, 1f, 1f, Game.Instance);
            ended.End();
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Loaded), "a loaded game is listened for");

            Inbound.GameLoaded();

            Expect.Equal(6, world.Bolts.Count, "the two lasting ones made again, and only those");
            Expect.Equal("LEAS", world.Bolts[4].Kind, "the first, with its code");
            Expect.Near(200f, world.Bolts[4].Z1, "and its height");
            Expect.Near(300f, world.Bolts[5].X2, "the link, to its unit");
            world.PlaceUnit(b.Handle, 500f, 0f);
            world.Advance(2.1f);
            Expect.Near(500f, world.Bolts[5].X2, "the new link follows");
            Expect.Equal(false, world.Bolts[2].Ended, "the PlayOnce bolt from before the load is not removed");
            link.End();
            points.End();
            Expect.Equal(true, world.Bolts[5].Ended && world.Bolts[4].Ended, "End removes the new ones");
            Expect.Equal(false, world.Bolts[0].Ended, "and not the dropped ones");
        }

        /// <summary>Reveal and Hide start fog modifiers in their states, over a circle, a rect or the world, and end with their scope.</summary>
        public static void VisionModifiers(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var sight = Player.One.Reveal(100f, 200f, 600f, holder);
            var made = world.Visions[0];
            Expect.Same(Player.One.Handle, made.Player, "for the player");
            Expect.Equal(0, made.State, "visible");
            Expect.Near(600f, made.Radius, "the radius");
            Expect.Equal(false, made.OverUnits, "not over units");
            Expect.Same(Player.One, sight.Player, "reads the player");
            Player.Two.Reveal(holder);
            Expect.Near(-4096f, world.Visions[1].Left, "the whole world");
            Player.One.Hide(0f, 0f, 300f, holder);
            Expect.Equal(1, world.Visions[2].State, "fog unless black");
            Player.One.Hide(-10f, -20f, 30f, 40f, holder, black: true, overUnits: true);
            var dark = world.Visions[4];
            Expect.Equal(2, dark.State, "the black mask");
            Expect.Equal(true, dark.OverUnits, "over the player's units");
            Expect.Near(40f, dark.Top, "the rect's top");
            sight.End();
            Expect.Equal(true, world.Visions[6].Ended, "ended by End");
            holder.End();
            Expect.Equal(true, dark.Ended, "the rest end with the scope");
            var late = Player.One.Reveal(0f, 0f, 1f, holder);
            Expect.Equal(true, late.HasEnded, "ended at once in an ended scope");
            Expect.Equal(7, world.Visions.Count, "the game is not asked again");
        }

        /// <summary>The game keeps the oldest of a player's fog modifiers on a place (I35), so a new vision makes the player's older ones again after its own, newest first: a Hide in a Reveal holds.</summary>
        public static void VisionNewestWins(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var sight = Player.One.Reveal(-400f, -400f, 400f, 400f, holder);
            Player.Two.Reveal(holder);
            var first = sight.Handle;
            var fog = Player.One.Hide(0f, 0f, 300f, holder);
            Player.One.Hide(0f, 0f, 150f, holder, black: true);

            // 0 the reveal, 1 player two's; the fog hide: 2, then 3 the reveal again; the black hide: 4, then 5 the fog hide and 6 the reveal again.
            Expect.Equal(7, world.Visions.Count, "each Hide made first, then the older ones again, newest first");
            Expect.Equal(true, world.Visions[0].Ended && world.Visions[2].Ended && world.Visions[3].Ended, "the older fog modifiers ended");
            Expect.Equal(false, world.Visions[1].Ended, "another player's vision is left alone");
            Expect.Equal(2, world.Visions[4].State, "the black hide made first");
            Expect.Equal(1, world.Visions[5].State, "then the fog hide");
            Expect.Equal(0, world.Visions[6].State, "then the reveal");
            Expect.Near(400f, world.Visions[6].Top, "with its rect");
            Expect.True(sight.Handle != first, "the reveal's handle is the new one");
            fog.End();
            Expect.Equal(true, world.Visions[5].Ended, "End ends a hide's new one");
            Expect.Equal(false, world.Visions[4].Ended || world.Visions[6].Ended, "and leaves the others");
            sight.End();
            Expect.Equal(true, world.Visions[6].Ended, "End ends the reveal's new one");
        }

        /// <summary>Game.Fog and Game.BlackMask read and set the game's.</summary>
        public static void FogToggles(FakeWorld world)
        {
            Expect.Equal(true, Game.Fog, "fog on unless set");
            Game.Fog = false;
            Game.BlackMask = false;
            Expect.Equal(false, world.FogOn, "the game's fog");
            Expect.Equal(false, world.BlackMaskOn, "the game's mask");
            Expect.Equal(false, Game.BlackMask, "reads the mask");
        }

        /// <summary>Pings are white unless coloured, a pure red is kept from the warning ping, Attack is that ping, and a player's ping shows at its own machine alone.</summary>
        public static void Pings(FakeWorld world)
        {
            Game.Ping(10f, 20f);
            var plain = world.Pings[0];
            Expect.Equal(255, plain.Blue, "white");
            Expect.Near(3f, plain.Seconds, "3 s unless set");
            Expect.Equal(false, plain.Flashy, "simple");
            Game.Ping(0f, 0f, new Color(255, 0, 0));
            Expect.Equal(254, world.Pings[1].Red, "a pure red is drawn 254");
            Game.Ping(0f, 0f, Color.TooltipGold, 1f, PingStyle.Attack);
            Expect.Equal(255, world.Pings[2].Red, "the warning ping's red");
            Expect.Equal(0, world.Pings[2].Green, "whatever the colour");
            Player.Two.Ping(0f, 0f);
            Expect.Equal(3, world.Pings.Count, "another player's ping is not shown here");
            Player.One.Ping(5f, 6f, Color.Red, style: PingStyle.Flashy);
            Expect.Equal(true, world.Pings[3].Flashy, "this machine's player's, flashy");
        }

        /// <summary>An icon at a place has its model and colour, and ends with its scope; one shown to another player is hidden here.</summary>
        public static void IconAtPlace(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var icon = MinimapIcon.Create(MinimapIcon.QuestObjective, 100f, 200f, new Color(0, 255, 0), holder);
            var made = world.Icons[0];
            Expect.Equal(MinimapIcon.QuestObjective, made.Path, "the model given to the game");
            Expect.Equal(255, made.Green, "the colour given to the game");
            Expect.Equal(true, made.Shown, "shown");
            Expect.Near(200f, icon.Y, "reads its place");
            MinimapIcon.Create(MinimapIcon.Boss, 0f, 0f, Color.Red, holder, shownTo: Player.Two);
            Expect.Equal(false, world.Icons[1].Shown, "hidden at this machine");
            holder.End();
            Expect.Equal(true, made.Ended, "ended with its scope");
            Expect.Equal(true, icon.HasEnded, "ended");
        }

        /// <summary>An icon on a unit follows it and ends when the unit ends.</summary>
        public static void IconOnUnit(FakeWorld world)
        {
            var boss = NewFootman(world, 300f, 400f);
            var icon = boss.AddMinimapIcon(MinimapIcon.Boss, Color.Red, Game.Instance);
            var made = world.Icons[0];
            Expect.Same(boss.Handle, made.Unit, "on the unit");
            Expect.Same(boss, icon.Unit, "reads the unit");
            Expect.Near(400f, icon.Y, "reads the unit's place");
            boss.Remove();
            Expect.Equal(true, icon.HasEnded, "ended with the unit");
            Expect.Equal(true, made.Ended, "removed in the game");
            var late = boss.AddMinimapIcon(MinimapIcon.Boss, Color.Red, Game.Instance);
            Expect.Equal(true, late.HasEnded, "ended at once on a unit that is gone");
            Expect.Equal(1, world.Icons.Count, "the game is not asked again");
        }
    }
}
