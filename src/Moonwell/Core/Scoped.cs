using System;

namespace Moonwell
{
    /// <summary>Something a scope holds, and ends when the scope ends or when it ends by itself.</summary>
    public abstract class Scoped
    {
        /// <summary>Internal, as a map's class derives from the ones Moonwell made for it: this, in a map, would have no scope to end it.</summary>
        internal Scoped()
        {
        }

        /// <summary>The scope this object was made in, which ends it when it ends; kept after the object ends.</summary>
        [LocalSafe]
        public Scope? Parent { get; internal set; }

        /// <summary>Whether this object has ended: false inside its OnEnd, OnRemoved and OnLeave, true once its End or removal returns.</summary>
        [LocalSafe]
        public bool HasEnded { get; private set; }

        /// <summary>The older sibling in its parent's list of children; null for the oldest.</summary>
        internal Scoped? before;

        /// <summary>The newer sibling in its parent's list of children; null for the newest.</summary>
        internal Scoped? after;

        /// <summary>Whether this object is in its parent's list of children.</summary>
        internal bool linked;

        /// <summary>Whether this object is ending; a second End while it runs does nothing.</summary>
        internal bool ending;

        /// <summary>Ends this object once: its own OnEnd, its children, then Finish. The game and the players last the whole game: ending one is refused and reported, in every build.</summary>
        public void End()
        {
            if (ending || HasEnded)
            {
                return;
            }

            if (this is Game || this is Player)
            {
                Errors.Refuse(Names.NameOf(this) + ".End", "the game and the players last the whole game: end the game with Game.Over, take a player out with Win or Lose, or end a scope of your own");
                return;
            }

            ending = true;
            try
            {
                OnEnd();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".OnEnd", error);
            }

            if (this is Scope scope)
            {
                // Set first: a child adopted while they end, from a child's OnRemoved, ends at once, and is not run.
                scope.childrenEnded = true;
                while (scope.newest != null)
                {
                    var child = scope.newest;
                    scope.Unlink(child);
                    child.End();
                }
            }

            try
            {
                Finish();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".Finish", error);
            }

            HasEnded = true;
            if (linked && Parent != null)
            {
                Parent.Unlink(this);
            }
#if DEBUG
            Leaks.Ended(this);
#endif
        }

        /// <summary>Runs when the object ends, before its children end.</summary>
        protected virtual void OnEnd()
        {
        }

        /// <summary>Runs after the children have ended; empty here.</summary>
        internal virtual void Finish()
        {
        }
    }
}
