using System;

namespace Moonwell
{
    /// <summary>What a cinematic's scene waits for at a <c>yield return</c>: some seconds, a line a unit says, a fade.</summary>
    public abstract class Wait
    {
        internal Wait()
        {
        }

        /// <summary>Waits the seconds, on Moonwell's clock.</summary>
        public static Wait Seconds(float seconds)
        {
            return new Pause(seconds);
        }

        /// <summary>Runs resume when the wait is over, unless the scope ends first.</summary>
        internal abstract void Await(Scope scope, Action resume);

        private sealed class Pause : Wait
        {
            private readonly float seconds;

            internal Pause(float seconds)
            {
                Dev.Check(seconds >= 0.0f, "Wait.Seconds: seconds must be 0 or more");
                this.seconds = seconds < 0.0f ? 0.0f : seconds;
            }

            internal override void Await(Scope scope, Action resume)
            {
                scope.OwnAfter(seconds, resume);
            }
        }
    }
}
