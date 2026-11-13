using System;

namespace Moonwell.AI
{
    /// <summary>Gives Running until that many seconds, read at its start, have passed by Minds.Now; then Succeeded.</summary>
    internal sealed class WaitNode : Node
    {
        /// <summary>Gives the seconds to wait, read at each start.</summary>
        private readonly Func<float> seconds;

        /// <summary>The seconds read at its start.</summary>
        private float wait;

        /// <summary>The time it started, by Minds.Now.</summary>
        private float startedAt;

        /// <summary>A wait of the seconds a function gives at its start.</summary>
        internal WaitNode(Func<float> seconds)
        {
            this.seconds = seconds;
        }

        /// <summary>Reads the seconds and notes the time.</summary>
        protected override void Start()
        {
            wait = seconds();
            startedAt = Minds.Now();
        }

        /// <summary>Succeeded once the seconds have passed.</summary>
        protected override Status Run()
        {
            return Minds.Passed(startedAt, wait) ? Status.Succeeded : Status.Running;
        }
    }
}
