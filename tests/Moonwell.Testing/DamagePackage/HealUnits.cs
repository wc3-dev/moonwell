namespace Moonwell.Testing
{
    /// <summary>A unit that logs both stages of the heals it gives; starts with a HealerComp. Its Healing and Healed actions, when set, run after the log.</summary>
    public class HealerUnit : Unit, IHealSource
    {
        /// <summary>Run in OnHealing after the log; null for nothing.</summary>
        public System.Action<Heal>? Healing;

        /// <summary>Run in OnHealed after the log; null for nothing.</summary>
        public System.Action<Heal>? Healed;

        /// <summary>Makes a healer with its component.</summary>
        public HealerUnit()
        {
            Components.Add(new HealerComp());
        }

        /// <summary>Logs the stage, then runs Healing.</summary>
        public void OnHealing(Heal heal)
        {
            TestLog.Lines.Add("healer healing");
            if (Healing != null)
            {
                Healing(heal);
            }
        }

        /// <summary>Logs the stage, then runs Healed.</summary>
        public void OnHealed(Heal heal)
        {
            TestLog.Lines.Add("healer healed");
            if (Healed != null)
            {
                Healed(heal);
            }
        }
    }

    /// <summary>A component that logs both stages of the heals its unit gives.</summary>
    public class HealerComp : UnitComponent, IHealSource
    {
        /// <summary>Logs the stage.</summary>
        public void OnHealing(Heal heal) => TestLog.Lines.Add("healer comp healing");

        /// <summary>Logs the stage.</summary>
        public void OnHealed(Heal heal) => TestLog.Lines.Add("healer comp healed");
    }

    /// <summary>A unit that logs both stages of the heals it receives and the hits it takes; starts with a PatientComp. Its actions, when set, run after the log.</summary>
    public class PatientUnit : Unit, IHealTarget, IDamageTarget
    {
        /// <summary>Run in OnReceiving after the log; null for nothing.</summary>
        public System.Action<Heal>? Receiving;

        /// <summary>Run in OnReceived after the log; null for nothing.</summary>
        public System.Action<Heal>? Received;

        /// <summary>Run in OnTaken after the log; null for nothing.</summary>
        public System.Action<Hit>? Taken;

        /// <summary>Makes a patient with its component.</summary>
        public PatientUnit()
        {
            Components.Add(new PatientComp());
        }

        /// <summary>Logs the stage, then runs Receiving.</summary>
        public void OnReceiving(Heal heal)
        {
            TestLog.Lines.Add("patient receiving");
            if (Receiving != null)
            {
                Receiving(heal);
            }
        }

        /// <summary>Logs the stage, then runs Received.</summary>
        public void OnReceived(Heal heal)
        {
            TestLog.Lines.Add("patient received");
            if (Received != null)
            {
                Received(heal);
            }
        }

        /// <summary>Logs the stage.</summary>
        public void OnTaking(Hit hit) => TestLog.Lines.Add("patient taking");

        /// <summary>Logs the stage, then runs Taken.</summary>
        public void OnTaken(Hit hit)
        {
            TestLog.Lines.Add("patient taken");
            if (Taken != null)
            {
                Taken(hit);
            }
        }

        /// <summary>Logs core's reaction.</summary>
        public override void OnDamaged(Damage damage) => TestLog.Lines.Add("patient core damaged");
    }

    /// <summary>A component that logs both stages of the heals its unit receives.</summary>
    public class PatientComp : UnitComponent, IHealTarget
    {
        /// <summary>Logs the stage.</summary>
        public void OnReceiving(Heal heal) => TestLog.Lines.Add("patient comp receiving");

        /// <summary>Logs the stage.</summary>
        public void OnReceived(Heal heal) => TestLog.Lines.Add("patient comp received");
    }

    /// <summary>A game component that logs both stages of every heal as "game".</summary>
    public class GameHealWatch : GameComponent, IHealWatch
    {
        /// <summary>Logs the stage.</summary>
        public void OnUnitReceiving(Heal heal) => TestLog.Lines.Add("game receiving");

        /// <summary>Logs the stage.</summary>
        public void OnUnitReceived(Heal heal) => TestLog.Lines.Add("game received");
    }

    /// <summary>A player component that logs both stages of the heals its player gives or receives, with its name.</summary>
    public class PlayerHealWatch : PlayerComponent, IHealWatch
    {
        /// <summary>The name the log lines start with.</summary>
        private readonly string name;

        /// <summary>Makes a watch that logs under a name.</summary>
        public PlayerHealWatch(string name)
        {
            this.name = name;
        }

        /// <summary>Logs the stage.</summary>
        public void OnUnitReceiving(Heal heal) => TestLog.Lines.Add(name + " receiving");

        /// <summary>Logs the stage.</summary>
        public void OnUnitReceived(Heal heal) => TestLog.Lines.Add(name + " received");
    }
}
