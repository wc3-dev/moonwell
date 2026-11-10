using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>A part of an actor: it is in the actor's scope, and ends when it is removed or when the actor ends.</summary>
    public abstract class Component : Scope
    {
        /// <summary>The actor the component is on; null until it is added.</summary>
        internal Actor? actor;

        /// <summary>Whether the component has been added to its actor, so that its OnRemoved runs when it ends.</summary>
        internal bool added;

        /// <summary>The bit mask of the reactions this class overrides, worked out when the component is added.</summary>
        internal int Mask;

        /// <summary>The Hook2 bit mask of the reactions this class overrides, worked out with Mask.</summary>
        internal int Mask2;

        /// <summary>The holds of the scopes that hold the component; null when nothing has held it.</summary>
        internal List<Hold>? holds;

        /// <summary>Whether it was also put on with no holder, so it stays when its holders let go.</summary>
        internal bool unheld;

        /// <summary>The actor the component is on, from OnAdded on. Read before the component is added, in its constructor, a development build throws a MoonwellException that says so; a release build gives null, though the type does not say so (decision 78).</summary>
        [LocalSafe]
        public Actor Actor
        {
            get
            {
                Dev.Check(actor != null, "Component.Actor: read before " + Names.NameOf(this) + " was added to an actor; read it from OnAdded on, not in the constructor");
                return actor!;
            }
        }

        /// <summary>The reactions this kind of component declares.</summary>
        internal abstract HookTable Table { get; }

        /// <summary>Runs when the component is added to its actor.</summary>
        protected virtual void OnAdded()
        {
        }

        /// <summary>Runs when the component is removed or its actor ends, before it leaves its actor; HasEnded is still false here.</summary>
        protected virtual void OnRemoved()
        {
        }

        /// <summary>Runs OnAdded; Actor calls it, since a map overrides the hook as protected.</summary>
        internal void CallAdded()
        {
            OnAdded();
        }

        /// <summary>Puts a scope's hold on the component: the scope adopts the hold, so its end lets go. A scope that holds it already keeps its one hold, and a component that is ending takes none.</summary>
        internal void HoldBy(Scope holder)
        {
            if (ending || HasEnded || HeldBy(holder) != null)
            {
                return;
            }

            var hold = new Hold(this);
            holds ??= new List<Hold>();
            holds.Add(hold);
            holder.Adopt(hold);
        }

        /// <summary>The hold a scope has on the component, or null.</summary>
        internal Hold? HeldBy(Scope holder)
        {
            for (var i = 0; holds != null && i < holds.Count; i++)
            {
                if (holds[i].Parent == holder)
                {
                    return holds[i];
                }
            }

            return null;
        }

        /// <summary>Lets go of a hold: the component ends when that was its last and it was not also put on with no holder. While the component ends, its holds only go.</summary>
        internal virtual void Release(Hold hold)
        {
            if (holds == null || !holds.Remove(hold) || ending || HasEnded || unheld || holds.Count > 0)
            {
                return;
            }

            End();
        }

        /// <summary>Runs OnRemoved, then takes the component off its actor; a component never added does nothing.</summary>
        protected sealed override void OnEnd()
        {
            if (!added)
            {
                return;
            }

            try
            {
                OnRemoved();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".OnRemoved", error);
            }

            actor!.Detach(this);
            if (holds != null)
            {
                var left = new List<Hold>(holds);
                for (var i = 0; i < left.Count; i++)
                {
                    left[i].End();
                }
            }
        }
    }
}
