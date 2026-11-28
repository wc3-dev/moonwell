using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check construction and training (J55): a building's construction reactions on the building, its components and the map's components, and OnTrained on the building that trained a unit.</summary>
    public static class ConstructionCases
    {
        private static readonly int HallId = ObjectTypes.FourCC("h010");
        private static readonly int BarracksId = ObjectTypes.FourCC("h011");
        private static readonly int RecruitId = ObjectTypes.FourCC("h012");

        /// <summary>A building class that logs its construction reactions.</summary>
        private sealed class Hall : Unit
        {
            public override void OnConstructionStarted()
            {
                TestLog.Lines.Add("started");
            }

            public override void OnConstructionCancelled()
            {
                TestLog.Lines.Add("cancelled");
            }

            public override void OnConstructionFinished()
            {
                TestLog.Lines.Add("finished");
            }
        }

        /// <summary>A unit component that logs the construction reactions.</summary>
        private sealed class Scaffold : UnitComponent
        {
            public override void OnConstructionStarted()
            {
                TestLog.Lines.Add("component started");
            }

            public override void OnConstructionCancelled()
            {
                TestLog.Lines.Add("component cancelled");
            }

            public override void OnConstructionFinished()
            {
                TestLog.Lines.Add("component finished");
            }
        }

        /// <summary>A game component that logs a unit's coming in and the construction reactions with the building's type.</summary>
        private sealed class Watch : GameComponent
        {
            public override void OnUnitCreated(Unit createdUnit)
            {
                TestLog.Lines.Add("created " + Names.IdText(createdUnit.TypeId));
            }

            public override void OnConstructionStarted(Unit building)
            {
                TestLog.Lines.Add("game started " + Names.IdText(building.TypeId));
            }

            public override void OnConstructionCancelled(Unit building)
            {
                TestLog.Lines.Add("game cancelled");
            }

            public override void OnConstructionFinished(Unit building)
            {
                TestLog.Lines.Add("game finished");
            }
        }

        /// <summary>A player component that logs its slot at a construction's finish.</summary>
        private sealed class PlayerWatch : PlayerComponent
        {
            public override void OnConstructionFinished(Unit building)
            {
                TestLog.Lines.Add("player " + Player.Slot);
            }
        }

        /// <summary>A building class that logs the unit it trained.</summary>
        private sealed class Barracks : Unit
        {
            public override void OnTrained(Unit trainedUnit)
            {
                TestLog.Lines.Add("trained " + Names.IdText(trainedUnit.TypeId) + " " + (trainedUnit is Recruit));
            }
        }

        /// <summary>A unit class that logs its coming in.</summary>
        private sealed class Recruit : Unit
        {
            protected override void OnCreated()
            {
                TestLog.Lines.Add("recruit created");
            }
        }

        /// <summary>A unit component that logs a trained unit.</summary>
        private sealed class Drill : UnitComponent
        {
            public override void OnTrained(Unit trainedUnit)
            {
                TestLog.Lines.Add("component trained");
            }
        }

        /// <summary>A construction's start, cancel and finish run on the building's class, then its components, then the game's components, then its owner's, not another player's. A building class that overrides one listens for construction.</summary>
        public static void ReactionOrder(FakeWorld world)
        {
            ObjectTypes.Unit(HallId, () => new Hall());
            Expect.True(world.Listening.Contains(GameEvent.Construction), "the building class listens for construction");
            Expect.True(!world.Listening.Contains(GameEvent.Train), "not for training");
            Game.Instance.Add(new Watch());
            Player.Get(0).Add(new PlayerWatch());
            Player.Get(1).Add(new PlayerWatch());
            var hall = Unit.Of(world.NewUnit(HallId, slot: 0))!;
            hall.Add(new Scaffold());
            TestLog.Lines.Clear();
            Constructing.FromGame(hall.Handle, Constructing.Started);
            Constructing.FromGame(hall.Handle, Constructing.Cancelled);
            Constructing.FromGame(hall.Handle, Constructing.Finished);
            Expect.Equal("started,component started,game started h010,cancelled,component cancelled,game cancelled,finished,component finished,game finished,player 0", string.Join(",", TestLog.Lines), "the reactions");
        }

        /// <summary>A building Moonwell does not hold yet is taken in at its construction's start, as coming into the game: the map's OnUnitCreated runs first, then the construction reactions; the same object afterwards.</summary>
        public static void PlainBuildingTakenIn(FakeWorld world)
        {
            Game.Instance.Add(new Watch());
            Expect.True(world.Listening.Contains(GameEvent.Construction), "a map component listens for construction");
            TestLog.Lines.Clear();
            var farm = world.NewUnit(ObjectTypes.FourCC("hhou"), slot: 2);
            Constructing.FromGame(farm, Constructing.Started);
            Expect.Equal("created hhou,game started hhou", string.Join(",", TestLog.Lines), "taken in, then the reaction");
            var held = Unit.Of(farm);
            TestLog.Lines.Clear();
            Constructing.FromGame(farm, Constructing.Finished);
            Expect.Same(held, Unit.Of(farm), "the same object");
            Expect.Equal("game finished", string.Join(",", TestLog.Lines), "the finish");
            Constructing.FromGame(null, Constructing.Finished);
            Expect.Equal(1, TestLog.Lines.Count, "no building, no reaction");
        }

        /// <summary>A reaction that throws is reported, and the next still runs.</summary>
        public static void ThrowingReactionIsReported(FakeWorld world)
        {
            Game.Instance.Add(new Throwing());
            Game.Instance.Add(new Watch());
            var farm = world.NewUnit(ObjectTypes.FourCC("hhou"), slot: 0);
            Constructing.FromGame(farm, Constructing.Finished);
            Expect.True(TestLog.Lines.Contains("game finished"), "the next component ran");
            var reported = false;
            for (var i = 0; i < world.Logged.Count; i++)
            {
                reported |= world.Logged[i].Contains("OnConstructionFinished");
            }

            Expect.True(reported, "the error is logged with the reaction's name");
        }

        /// <summary>A game component whose finish throws.</summary>
        private sealed class Throwing : GameComponent
        {
            public override void OnConstructionFinished(Unit building)
            {
                throw new MoonwellException("thrown");
            }
        }

        /// <summary>A finished training takes the trained unit in first (its OnCreated runs), then runs OnTrained on the building's class and its components with that unit. A building class that overrides it listens for training.</summary>
        public static void TrainedOnBuilding(FakeWorld world)
        {
            ObjectTypes.Unit(BarracksId, () => new Barracks());
            ObjectTypes.Unit(RecruitId, () => new Recruit());
            Expect.True(world.Listening.Contains(GameEvent.Train), "the building class listens for training");
            var barracks = Unit.Of(world.NewUnit(BarracksId, slot: 0))!;
            barracks.Add(new Drill());
            TestLog.Lines.Clear();
            var recruit = world.NewUnit(RecruitId, slot: 0);
            Constructing.Trained(barracks.Handle, recruit);
            Expect.Equal("recruit created,trained h012 True,component trained", string.Join(",", TestLog.Lines), "the reactions");
            Expect.True(Unit.Of(recruit) is Recruit, "the trained unit is held");
        }

        /// <summary>A training by a building Moonwell does not hold runs nothing; a component that overrides OnTrained listens for training.</summary>
        public static void TrainedByUnheldBuilding(FakeWorld world)
        {
            var plain = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hbar"), slot: 0))!;
            Expect.True(!world.Listening.Contains(GameEvent.Train), "nothing listens yet");
            plain.Add(new Drill());
            Expect.True(world.Listening.Contains(GameEvent.Train), "the component listens for training");
            TestLog.Lines.Clear();
            var unheld = world.NewUnit(ObjectTypes.FourCC("hbar"), slot: 0);
            Constructing.Trained(unheld, world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0));
            Expect.Equal(0, TestLog.Lines.Count, "no reaction");
            Constructing.Trained(plain.Handle, world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0));
            Expect.Equal("component trained", string.Join(",", TestLog.Lines), "the held building's component");
        }
    }
}
