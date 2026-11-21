namespace Moonwell.Testing
{
    /// <summary>Cases that check placed things: what an editor placed in the map, reached by World Editor's names.</summary>
    public static class PlacedCases
    {
        /// <summary>A placed unit comes back by its name as the object of its class, the same one its handle gives; an unknown name gives null.</summary>
        public static void UnitByName(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H001"), () => new TestHero());
            var handle = world.PlaceUnit("H001_0003", ObjectTypes.FourCC("H001"));

            var found = Unit.Named("H001_0003");

            Expect.True(found is TestHero, "the unit is a TestHero");
            Expect.Same(Unit.Of(handle), found, "the same object as its handle's");
            Expect.Same(found, Unit.Named("H001_0003"), "the same object the second time");
            Expect.Same(null, Unit.Named("hfoo_0001"), "an unknown name");
        }

        /// <summary>A placed unit that has left the game gives null.</summary>
        public static void UnitGone(FakeWorld world)
        {
            world.PlaceUnit("hfoo_0001", ObjectTypes.FourCC("hfoo"));
            Unit.Named("hfoo_0001")!.Remove();

            Expect.Same(null, Unit.Named("hfoo_0001"), "a removed unit");
        }

        /// <summary>A placed item comes back by its name as the object of its class; an unknown name gives null.</summary>
        public static void ItemByName(FakeWorld world)
        {
            ObjectTypes.Item(ObjectTypes.FourCC("I001"), () => new TestOrb());
            var handle = world.PlaceItem("I001_0005", ObjectTypes.FourCC("I001"));

            var found = Item.Named("I001_0005");

            Expect.True(found is TestOrb, "the item is a TestOrb");
            Expect.Same(Item.Of(handle), found, "the same object as its handle's");
            Expect.Same(null, Item.Named("ratc_0001"), "an unknown name");
        }

        /// <summary>A placed destructable comes back by its name as the object of its class; an unknown name gives null.</summary>
        public static void DestructableByName(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var handle = world.PlaceDestructable("B001_0002", ObjectTypes.FourCC("B001"));

            var found = Destructable.Named("B001_0002");

            Expect.True(found is TestGate, "the destructable is a TestGate");
            Expect.Same(Destructable.Of(handle), found, "the same object as its handle's");
            Expect.Same(null, Destructable.Named("LTlt_0012"), "an unknown name");
        }

        /// <summary>
        /// Placed destructables are matched at the start to the nearest of their type to their place, though the game moved them,
        /// each taken once; one removed later gives null, not a neighbour of its type, and one killed still comes back (J142).
        /// </summary>
        public static void DestructableGone(FakeWorld world)
        {
            var tree = ObjectTypes.FourCC("LTlt");
            var first = world.PlaceDestructable("LTlt_0001", tree, 0.0f, 0.0f);
            var second = world.PlaceDestructable("LTlt_0002", tree, 64.0f, 0.0f);
            world.PlaceDestructable(first, 32.0f, 0.0f);
            world.PlaceDestructable(second, 96.0f, 0.0f);
            Runtime.Begin(() => { });

            Expect.Same(Destructable.Of(first), Destructable.Named("LTlt_0001"), "the first");
            Expect.Same(Destructable.Of(second), Destructable.Named("LTlt_0002"), "the second, though the first is as near its place");

            Destructable.Of(first)!.Remove();
            Expect.Same(null, Destructable.Named("LTlt_0001"), "the first once removed");

            Destructable.Of(second)!.Kill();
            Expect.Same(Destructable.Of(second), Destructable.Named("LTlt_0002"), "the second once killed");
        }

        /// <summary>A sound of the Sound Editor plays, at a place and on a unit, and stops without being freed, so it plays again.</summary>
        public static void Sound(FakeWorld world)
        {
            var fake = world.AddPlacedSound("Intro");
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;

            var intro = PlacedSound.Named("Intro")!;
            intro.Play();
            intro.PlayAt(64f, -32f);
            intro.Stop();
            intro.PlayOn(unit);

            Expect.Equal("Intro", intro.Name, "its name");
            Expect.Equal(3, fake.Starts, "started three times");
            Expect.Equal(1, fake.Halts, "stopped once");
            Expect.Near(64f, fake.X, "placed at x");
            Expect.Near(-32f, fake.Y, "placed at y");
            Expect.Same(unit.Handle, fake.Unit, "put on the unit");
            Expect.Equal(false, fake.FreedWhenDone, "never freed");
            Expect.Same(null, PlacedSound.Named("Outro"), "an unknown name");
        }

        /// <summary>A placed region's centre is the middle of its rect.</summary>
        public static void RegionCentre(FakeWorld world)
        {
            var region = new PlacedRegion("Spawn Left", -256f, -128f, 64f, 32f);

            Expect.Near(-96f, region.CenterX, "centre x");
            Expect.Near(-48f, region.CenterY, "centre y");
            Expect.Equal("Spawn Left", region.Name, "its name");
        }
    }
}
