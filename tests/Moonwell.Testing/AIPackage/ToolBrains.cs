#if DEBUG
using Moonwell.AI;
using static Moonwell.AI.Nodes;

namespace Moonwell.Testing
{
    /// <summary>The facts and the tree the AI tools' cases use. Each name is made once in the cases: -fact finds a fact by its name, and refuses two of one name.</summary>
    public static class ToolFacts
    {
        /// <summary>A written number of 0..1, the fight option's input.</summary>
        public static readonly Fact<float> Morale = new Fact<float>("morale");

        /// <summary>A written flag, the guard's When.</summary>
        public static readonly Fact<bool> Hurt = new Fact<bool>("hurt");

        /// <summary>A written flag no case sets, the rest option's input: false, so rest scores its weight.</summary>
        public static readonly Fact<bool> Tired = new Fact<bool>("tired");

        /// <summary>A written count.</summary>
        public static readonly Fact<int> Ammo = new Fact<int>("ammo");

        /// <summary>A written text.</summary>
        public static readonly Fact<string> Shout = new Fact<string>("shout");

        /// <summary>A written count, set on a player's blackboard.</summary>
        public static readonly Fact<int> Rations = new Fact<int>("rations");

        /// <summary>A written flag, set on the game's blackboard.</summary>
        public static readonly Fact<bool> Omen = new Fact<bool>("omen");

        /// <summary>A worked-out count, always 3.</summary>
        public static readonly Fact<int> Tally = new Fact<int>("tally", 0f, board => 3);

        /// <summary>A written unit, of a type -fact cannot read.</summary>
        public static readonly Fact<Unit?> Quarry = new Fact<Unit?>("quarry");

        /// <summary>One of two facts named "twin".</summary>
        public static readonly Fact<int> TwinCount = new Fact<int>("twin");

        /// <summary>The other fact named "twin".</summary>
        public static readonly Fact<float> TwinNumber = new Fact<float>("twin");

        /// <summary>A written enum, which Lua keeps as a bare number.</summary>
        public static readonly Fact<ToolMood> Mood = new Fact<ToolMood>("mood");

        /// <summary>A written nullable count.</summary>
        public static readonly Fact<int?> Spare = new Fact<int?>("spare");

        /// <summary>The guard's tree of 7 nodes, numbered as the view counts them: 1 Select, 2 When hurt, 3 its child, 4 Sequence, 5 a child that succeeds, 6 one that runs, 7 the Select's last child, which runs.</summary>
        public static Node Guard()
        {
            return Select(
                When(Hurt, Do(() => Status.Running)),
                Sequence(Do(() => Status.Succeeded), Do(() => Status.Running)),
                Do(() => Status.Running));
        }
    }

    /// <summary>A unit brain thinking every step with the guard's tree and a choice of stance: the view numbers the tree's nodes 1 to 7, then the options 8 (fight by morale), 9 (flee at 0.3) and 10 (rest at 0.5 unless tired).</summary>
    public sealed class ToolBrain : UnitBrain
    {
        /// <summary>The name of the option run last.</summary>
        public string Stance = "";

        /// <summary>The guard's tree.</summary>
        private readonly Tree guard;

        /// <summary>Fight, flee or rest.</summary>
        private readonly Choice stances;

        /// <summary>Makes the brain, its tree and its choice.</summary>
        public ToolBrain()
        {
            guard = new Tree(this, ToolFacts.Guard());
            stances = new Choice
            {
                new Option("fight", Fight) { { ToolFacts.Morale, Curve.Rising } },
                new Option("flee", Flee, 0.3f),
                new Option("rest", Rest, 0.5f) { { ToolFacts.Tired, Curve.Falling } },
            };
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            guard.Run();
            stances.Run(Blackboard);
        }

        private void Fight()
        {
            Stance = "fight";
        }

        private void Flee()
        {
            Stance = "flee";
        }

        private void Rest()
        {
            Stance = "rest";
        }
    }

    /// <summary>A unit brain thinking every step with the guard's tree only.</summary>
    public sealed class GuardBrain : UnitBrain
    {
        /// <summary>The guard's tree.</summary>
        private readonly Tree guard;

        /// <summary>Makes the brain and its tree.</summary>
        public GuardBrain()
        {
            guard = new Tree(this, ToolFacts.Guard());
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            guard.Run();
        }
    }

    /// <summary>A unit brain thinking every step with a choice only: fight by morale, or rest at 0.5 unless tired. Morale at 0.9 fights and at 0.2 rests, the winner changing each time it flips.</summary>
    public sealed class StanceBrain : UnitBrain
    {
        /// <summary>Fight or rest.</summary>
        private readonly Choice stances;

        /// <summary>Makes the brain and its choice.</summary>
        public StanceBrain()
        {
            stances = new Choice
            {
                new Option("fight", Nothing) { { ToolFacts.Morale, Curve.Rising } },
                new Option("rest", Nothing, 0.5f) { { ToolFacts.Tired, Curve.Falling } },
            };
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            stances.Run(Blackboard);
        }

        private static void Nothing()
        {
        }
    }

    /// <summary>
    /// A unit brain thinking every step with a tree of 30 nodes, too many for the panel: 1 Select, 2 When hurt and 3 its
    /// child, 4 a Sequence whose first child (5) fails, with 12 children (5 to 16), 17 a Sequence whose first child (18)
    /// runs, with 12 children (18 to 29), and 30 the Select's last child.
    /// </summary>
    public sealed class BigTreeBrain : UnitBrain
    {
        /// <summary>The tree.</summary>
        private readonly Tree big;

        /// <summary>Makes the brain and its tree.</summary>
        public BigTreeBrain()
        {
            var failing = new Node[12];
            var running = new Node[12];
            failing[0] = Do(() => Status.Failed);
            running[0] = Do(() => Status.Running);
            for (var i = 1; i < 12; i++)
            {
                failing[i] = Do(() => Status.Succeeded);
                running[i] = Do(() => Status.Succeeded);
            }

            big = new Tree(this, Select(When(ToolFacts.Hurt, Do(() => Status.Running)), Sequence(failing), Sequence(running), Do(() => Status.Running)));
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            big.Run();
        }
    }

    /// <summary>A mood, for the enum fact.</summary>
    public enum ToolMood
    {
        /// <summary>The first, 0.</summary>
        Calm,

        /// <summary>The second, 1.</summary>
        Angry,
    }

    /// <summary>A unit brain thinking every step that reads the enum and the nullable facts, then picks fight or rest as StanceBrain does.</summary>
    public sealed class MoodBrain : UnitBrain
    {
        /// <summary>Fight or rest.</summary>
        private readonly Choice stances;

        /// <summary>Makes the brain and its choice.</summary>
        public MoodBrain()
        {
            stances = new Choice
            {
                new Option("fight", Nothing) { { ToolFacts.Morale, Curve.Rising } },
                new Option("rest", Nothing, 0.5f) { { ToolFacts.Tired, Curve.Falling } },
            };
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            Read(ToolFacts.Mood);
            Read(ToolFacts.Spare);
            stances.Run(Blackboard);
        }

        private static void Nothing()
        {
        }
    }

    /// <summary>A unit brain thinking every step with a Pick of two options: 1 Pick, 2 the first option (0.9) and 3 its child, 4 the second (0.2) and 5 its child, each child running.</summary>
    public sealed class PickBrain : UnitBrain
    {
        /// <summary>The tree.</summary>
        private readonly Tree pick;

        /// <summary>Makes the brain and its tree.</summary>
        public PickBrain()
        {
            pick = new Tree(this, Pick(Scored(() => 0.9f, Do(() => Status.Running)), Scored(() => 0.2f, Do(() => Status.Running))));
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            pick.Run();
        }
    }

    /// <summary>A unit brain thinking every step with the guard's tree, which ends itself after a pass once Quit is set.</summary>
    public sealed class QuitBrain : UnitBrain
    {
        /// <summary>Whether the next think ends the brain, after its pass.</summary>
        public bool Quit;

        /// <summary>The guard's tree.</summary>
        private readonly Tree guard;

        /// <summary>Makes the brain and its tree.</summary>
        public QuitBrain()
        {
            guard = new Tree(this, ToolFacts.Guard());
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            guard.Run();
            if (Quit)
            {
                End();
            }
        }
    }

    /// <summary>A player brain that thinks only when woken, and does nothing.</summary>
    public sealed class ToolPlayerBrain : PlayerBrain
    {
        protected override void Think()
        {
        }
    }

    /// <summary>A game brain that thinks only when woken, and does nothing.</summary>
    public sealed class ToolGameBrain : GameBrain
    {
        protected override void Think()
        {
        }
    }
}
#endif
