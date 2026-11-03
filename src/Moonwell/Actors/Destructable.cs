using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Moonwell
{
    /// <summary>A destructable of the game: an object of its declared class, which holds the components the class starts with.</summary>
    public class Destructable : Widget
    {
        /// <summary>The table behind DestructableHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? destructableHooks;

        /// <summary>The reactions of destructables; declared here, so that an override in a subclass is found.</summary>
        internal static HookTable DestructableHooks
        {
            get
            {
                if (destructableHooks == null)
                {
                    destructableHooks = new HookTable(typeof(Destructable), new string[] { "OnDeath", "OnRestored" }, new int[] { Hook.OnDeath, Hook.OnRestored });
                }

                return destructableHooks;
            }
        }

        /// <summary>The bit mask of the reactions this class overrides, worked out when the destructable is taken in.</summary>
        internal int Mask;

        /// <summary>Whether the world has been asked to deliver this destructable's death, once.</summary>
        internal bool watched;

        /// <summary>The life last read or set.</summary>
        internal float lastLife;

        /// <summary>The most life last read or set.</summary>
        internal float lastMaxLife;

        /// <summary>The components the class starts with, added in order when the destructable is taken in. Assigning a new list drops what a parent class listed.</summary>
        protected List<DestructableComponent> Components { get; set; } = new List<DestructableComponent>();

        /// <summary>The game's handle of the destructable.</summary>
        [LocalSafe]
        public destructable Handle { get; internal set; } = null!;

        /// <summary>The type id of the destructable, as the game read it when it was taken in.</summary>
        [LocalSafe]
        public int TypeId { get; internal set; }

        /// <summary>The x of the destructable, as the game read it when it was taken in; a destructable does not move, and the value stays once it is gone.</summary>
        [LocalSafe]
        public float X { get; internal set; }

        /// <summary>The y of the destructable, as the game read it when it was taken in; a destructable does not move, and the value stays once it is gone.</summary>
        [LocalSafe]
        public float Y { get; internal set; }

        /// <summary>The starting components, which the runtime adds in order when the destructable is taken in.</summary>
        internal List<DestructableComponent> StartComponents => Components;

        /// <summary>The id of the type whose declared values the destructable reads: its type id, else, while its constructor runs, the id its class is registered with, else 0.</summary>
        internal int DeclaredTypeId => TypeId != 0 ? TypeId : ObjectTypes.IdOfClass(this);

        /// <summary>The value the destructable's type declares for a field, which a shared member reads when the destructable has no handle (in its constructor). Here, the value the build wrote for the map; the standard classes read their own declared values on .NET.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected virtual T DeclaredValue<T>(string field)
        {
            // The build has no world; the classes it reads override this.
            if (Runtime.World == null)
            {
                return default(T)!;
            }

            var value = Runtime.World.TypeValue(DeclaredTypeId, field);
            return value == null ? default(T)! : (T)value;
        }

        /// <summary>Declares the value of a field for the destructable's class, which a shared member does when it is set with no handle. Here, nothing: in the game the values are the build's; the standard classes keep them on .NET.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected virtual void Declare(string field, object value)
        {
        }

        /// <summary>Whether a getter that read 0 read it from a destructable the game has removed: the game gives 0 for everything then, and the type id 0, before the sweep finds it. The getter then keeps the last value instead of the 0.</summary>
        internal bool RemovedByGame()
        {
            return Runtime.World.DestructableTypeId(Handle) == 0;
        }

        /// <summary>The destructable's life. Setting it keeps a fraction and stays at most the most life; 0.4 or less kills it (0.405 does not), and OnDeath runs inside the call. Setting it on a dead destructable does nothing, as the game would leave a stump that reads the life: Restore brings one back. Once the game has removed it, the last life read or set, and setting does nothing.</summary>
        [LocalSafe]
        public float Life
        {
            get
            {
                if (IsGone)
                {
                    return lastLife;
                }

                var now = Runtime.World.DestructableLifeOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastLife;
                }

                if (Local.Depth == 0)
                {
                    lastLife = now;
                }

                return now;
            }

            set
            {
                if (IsAlive)
                {
                    Runtime.World.PutDestructableLife(Handle, value);
                    lastLife = IsGone ? 0.0f : Runtime.World.DestructableLifeOf(Handle);
                }
            }
        }

        /// <summary>The most life the destructable can have, which the game keeps as a whole number (80.7 is 81). A full destructable stays full, and one above a lower most falls to it. Once the game has removed it, the last value read or set, and setting does nothing. In a declared class's constructor, the most life every destructable of the class starts with (bhps).</summary>
        [LocalSafe]
        public float MaxLife
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<float>("bhps");
                }

                if (IsGone)
                {
                    return lastMaxLife;
                }

                var now = Runtime.World.DestructableMaxLifeOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastMaxLife;
                }

                if (Local.Depth == 0)
                {
                    lastMaxLife = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("bhps", value);
                    return;
                }

                if (!IsGone)
                {
                    Runtime.World.PutDestructableMaxLife(Handle, value);
                    lastMaxLife = Runtime.World.DestructableMaxLifeOf(Handle);
                    lastLife = Runtime.World.DestructableLifeOf(Handle);
                }
            }
        }

        /// <summary>Whether the destructable is alive: its life is above 0. False once the game has removed it.</summary>
        [LocalSafe]
        public bool IsAlive => !IsGone && Runtime.World.DestructableLifeOf(Handle) > 0.0f;

        /// <summary>The facing of a destructable made without one, in degrees, as a unit's. A type with a fixed rotation keeps its own.</summary>
        public const float DefaultFacing = 270.0f;

        /// <summary>Creates a destructable of a type at a place, in a scope, which removes it when it ends, with a facing (270 degrees without one), a scale and a variation (its type's first model without one). The game puts it on its own grid, which X and Y read: a tree asked at 1000 stands at 960. Throws a MoonwellException naming the id when the game makes no destructable of the type.</summary>
        public static Destructable Create(int typeId, float x, float y, Scope scope, float facing = DefaultFacing, float scale = 1.0f, int variation = 0)
        {
            var handle = Runtime.World.SpawnDestructable(typeId, x, y, facing, scale, variation);
            if (handle == null)
            {
                throw new MoonwellException("Destructable.Create: the game made no destructable of type " + Names.IdText(typeId));
            }

            var made = KnownDestructables.TakeIn(handle, scope)!;
            if (made.Parent == null)
            {
                scope.Adopt(made);
            }

            return made;
        }

        /// <summary>Creates a destructable of a registered class at a place, in a scope, which removes it when it ends, with a facing (270 degrees without one), a scale and a variation.</summary>
        public static T Create<T>(float x, float y, Scope scope, float facing = DefaultFacing, float scale = 1.0f, int variation = 0)
            where T : Destructable
        {
            return (T)Create(ObjectTypes.IdOf<T>(), x, y, scope, facing, scale, variation);
        }

        /// <summary>Kills the destructable through the game (KillDestructable): OnDeath runs once, inside this call. It stays in the game, dead, until it is removed or restored. Does nothing to a dead destructable, and once it is gone.</summary>
        public void Kill()
        {
            if (IsAlive)
            {
                Runtime.World.SlayDestructable(Handle);
                lastLife = 0.0f;
            }
        }

        /// <summary>Brings a dead destructable back to life at its most life, with its birth animation when asked, and runs OnRestored and then its components' inside this call. Does nothing to a living destructable, and once it is gone. A destructable brought back with the native DestructableRestoreLife runs no OnRestored, as the game tells nothing.</summary>
        public void Restore(bool showBirth = false)
        {
            if (IsGone || IsAlive)
            {
                return;
            }

            Runtime.World.RestoreDestructable(Handle, MaxLife, showBirth);
            lastLife = Runtime.World.DestructableLifeOf(Handle);
            if (!HasEnded && (Mask & Hook.OnRestored) != 0)
            {
                try
                {
                    OnRestored();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(this) + ".OnRestored", error);
                }
            }

            var count = BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = ComponentAt(i) as DestructableComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnRestored) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnRestored();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnRestored", error);
                }
            }

            EndDispatch();
        }

        /// <summary>The object of a game destructable: made when it is first seen; null for null or for one the game has removed.</summary>
        public static Destructable? Of(destructable? handle)
        {
            if (handle == null)
            {
                return null;
            }

            return KnownDestructables.TakeIn(handle, null);
        }

        /// <summary>Adds a component to the destructable, and returns it.</summary>
        public T Add<T>(T component)
            where T : DestructableComponent
        {
            var added = AddComponent(component);
            WatchIfWanted(Mask | added.Mask);
            return added;
        }

        /// <summary>Removes the destructable from the game: it ends, which runs its OnLeave.</summary>
        public void Remove() => End();

        /// <summary>Runs once, when the destructable comes into the game, after its starting components are added.</summary>
        protected virtual void OnCreated()
        {
        }

        /// <summary>Runs when the destructable leaves the game, before its components end; HasEnded is still false here. It runs once for each OnCreated: one made in a scope that had already ended, which ends at once, runs neither.</summary>
        protected virtual void OnLeave()
        {
        }

        /// <summary>Runs OnCreated; Destructables calls it, since a map overrides the hook as protected.</summary>
        internal void CallEnter()
        {
            OnCreated();
        }

        /// <summary>Runs when the destructable dies, before the components see it.</summary>
        public virtual void OnDeath()
        {
        }

        /// <summary>Runs when Restore brings the destructable back to life, before the components see it.</summary>
        public virtual void OnRestored()
        {
        }

        /// <summary>Asks the world to deliver this destructable's death, once, when a mask has the death bit.</summary>
        internal void WatchIfWanted(int mask)
        {
            if ((mask & Hook.OnDeath) != 0 && !watched && !HasEnded)
            {
                Runtime.World.WatchDeath(Handle);
                watched = true;
            }
        }

        /// <summary>Tells the interactions package, if there is one; runs OnLeave; the components then end, and the destructable leaves the game. One that never came in, as one made in a scope that had ended, runs neither, as it ran no OnCreated.</summary>
        protected sealed override void OnEnd()
        {
            if (!entered)
            {
                return;
            }

            Hooks.CallEnding(this);
            try
            {
                OnLeave();
            }
            catch (Exception error)
            {
                Errors.Report(Names.NameOf(this) + ".OnLeave", error);
            }
        }

        /// <summary>Removes the destructable from the game unless the game has already, and forgets it.</summary>
        internal override void Finish()
        {
            if (!IsGone)
            {
                Runtime.World.DeleteDestructable(Handle);
                IsGone = true;
            }

            KnownDestructables.Forget(this);
        }
    }
}
