using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check research (decision 213): an upgrade class's OnResearched for each level gained, by code or by the game's research, and a building's research reactions.</summary>
    public static class ResearchCases
    {
        private static readonly int Swords = ObjectTypes.FourCC("R000");
        private static readonly int Smithy = ObjectTypes.FourCC("h000");

        /// <summary>An upgrade class that logs each level.</summary>
        private sealed class SharpSwords : Upgrade
        {
            public override void OnResearched(Player player, int level)
            {
                TestLog.Lines.Add("level " + level + " slot " + player.Slot);
            }
        }

        /// <summary>An upgrade class whose first level raises it to the third.</summary>
        private sealed class Leap : Upgrade
        {
            public override void OnResearched(Player player, int level)
            {
                TestLog.Lines.Add("level " + level);
                if (level == 1)
                {
                    player.SetLevel<Leap>(3);
                }
            }
        }

        /// <summary>An upgrade class whose first level takes the upgrade away.</summary>
        private sealed class Fickle : Upgrade
        {
            public override void OnResearched(Player player, int level)
            {
                TestLog.Lines.Add("level " + level);
                player.SetLevel<Fickle>(0);
            }
        }

        /// <summary>A building class that logs its research reactions.</summary>
        private sealed class Forge : Unit
        {
            public override void OnResearchStarted(Upgrade upgrade)
            {
                TestLog.Lines.Add("started " + Names.IdText(upgrade.TypeId));
            }

            public override void OnResearchCancelled(Upgrade upgrade)
            {
                TestLog.Lines.Add("cancelled");
            }

            public override void OnResearchFinished(Upgrade upgrade)
            {
                TestLog.Lines.Add("finished " + (upgrade is SharpSwords));
            }
        }

        /// <summary>A unit component that logs a finished research.</summary>
        private sealed class Ledger : UnitComponent
        {
            public override void OnResearchFinished(Upgrade upgrade)
            {
                TestLog.Lines.Add("component");
            }
        }

        /// <summary>SetLevel runs OnResearched for each level gained, in order, and lowering runs nothing; AddLevel adds one and stops at the top level.</summary>
        public static void LevelsInOrder(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Upgrade(Swords, () => new SharpSwords());
            var player = Player.Get(1);
            player.SetLevel<SharpSwords>(2);
            Expect.Equal("level 1 slot 1,level 2 slot 1", string.Join(",", TestLog.Lines), "two levels gained");
            TestLog.Lines.Clear();
            player.SetLevel<SharpSwords>(1);
            Expect.Equal(0, TestLog.Lines.Count, "lowering runs nothing");
            player.SetMaxAllowed<SharpSwords>(3);
            player.AddLevel<SharpSwords>();
            player.AddLevel<SharpSwords>();
            player.AddLevel<SharpSwords>();
            Expect.Equal("level 2 slot 1,level 3 slot 1", string.Join(",", TestLog.Lines), "AddLevel stops at the top level");
            Expect.Equal(3, player.LevelOf<SharpSwords>(), "the level");
        }

        /// <summary>A level set inside OnResearched runs the levels between once each, in order.</summary>
        public static void SetInsideRunsInOrder(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Upgrade(Swords, () => new Leap());
            Player.Get(0).SetLevel<Leap>(2);
            Expect.Equal("level 1,level 2,level 3", string.Join(",", TestLog.Lines), "each level once, in order");
            Expect.Equal(3, Player.Get(0).LevelOf<Leap>(), "the level");
        }

        /// <summary>A level a reaction lowers away runs nothing.</summary>
        public static void LoweredInsideStops(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Upgrade(Swords, () => new Fickle());
            Player.Get(0).SetLevel<Fickle>(3);
            Expect.Equal("level 1", string.Join(",", TestLog.Lines), "the levels above the lowered one run nothing");
        }

        /// <summary>The game's research: start and cancel run the building's reactions; a finish runs the upgrade's level, then the building's and its components'. A class overriding OnResearched listens for research.</summary>
        public static void GameResearch(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Upgrade(Swords, () => new SharpSwords());
            Expect.True(world.Listening.Contains(GameEvent.Research), "listens for research");
            ObjectTypes.Unit(Smithy, () => new Forge());
            var forge = Unit.Of(world.NewUnit(Smithy, slot: 0))!;
            forge.Add(new Ledger());
            var owner = Player.Get(0).Handle;
            Researching.FromGame(forge.Handle, owner, Swords, Researching.Started);
            Researching.FromGame(forge.Handle, owner, Swords, Researching.Cancelled);
            world.Research(owner, Swords, 1);
            Researching.FromGame(forge.Handle, owner, Swords, Researching.Finished);
            Expect.Equal("started R000,cancelled,level 1 slot 0,finished True,component", string.Join(",", TestLog.Lines), "the reactions");
        }

        /// <summary>A research of an upgrade with no class gives the building a plain Upgrade of its id, the same object each time.</summary>
        public static void PlainUpgrade(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Unit(Smithy, () => new Forge());
            var forge = Unit.Of(world.NewUnit(Smithy, slot: 0))!;
            var plain = ObjectTypes.FourCC("Rhme");
            Researching.FromGame(forge.Handle, Player.Get(0).Handle, plain, Researching.Started);
            Expect.Equal("started Rhme", string.Join(",", TestLog.Lines), "the building's reaction");
            Expect.Same(ObjectTypes.UpgradeOf(plain), ObjectTypes.UpgradeOf(plain), "one object per id");
            Expect.True(world.Listening.Contains(GameEvent.Research), "the building class listens for research");
        }
    }
}
