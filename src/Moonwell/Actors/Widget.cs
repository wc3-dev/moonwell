using System.Diagnostics;

namespace Moonwell
{
    /// <summary>A game object that holds components and is removed from the game: a unit, an item or a destructable.</summary>
    public abstract class Widget : Actor
    {
        /// <summary>Internal, as the game's widgets are units, items and destructables.</summary>
        internal Widget()
        {
        }

        /// <summary>Whether the object has been removed from the game, by the runtime or found removed by the game.</summary>
        [LocalSafe]
        public bool IsGone { get; internal set; }

        /// <summary>Whether the object came into the game for the runtime: set at its take-in, before its OnCreated, unless it had ended already. Only one that came in runs OnLeave and the ending listeners when it ends. Internal, not private, for the reason Scope gives.</summary>
        internal bool entered;

#if DEBUG
        /// <summary>The Kept bits of the members Moonwell holds a value for: each one read outside Local.For, or set, while the object was in the game.</summary>
        internal int kept;
#endif

        /// <summary>Marks, in a development build, the members (Kept bits) whose last value has just been stored.</summary>
        [Conditional("DEBUG")]
        internal void Keep(int members)
        {
#if DEBUG
            kept |= members;
#endif
        }

        /// <summary>Reports, in a development build, a read that gives the last value of a member (a Kept bit) when none was ever stored: what it gives is then the field's empty value, as (0, 0) for a place, with nothing to tell it from a real one (decision 270). An object that never came into the game, as a default object, reports nothing.</summary>
        [Conditional("DEBUG")]
        internal void CheckKept(int member, string name)
        {
#if DEBUG
            if (entered && (kept & member) == 0)
            {
                Dev.Report(Names.NameOf(this) + "." + name, "read after it left the game, with no value kept from before, so what it gives is not its own. Read it into a variable while it is in the game, and use that");
            }
#endif
        }

        /// <summary>The default object of a unit or item class, as <c>Unit.Default&lt;Champion&gt;()</c>: its declared values, the same object the Default of each of its units gives. One per class, made on first use by the class's maker, with no handle; it is gone, so its live actions do nothing. Throws for a destructable class. Declared here and not on Unit and Item, as C# allows no method named like the Default property beside it.</summary>
        [LocalSafe]
        public static T Default<T>()
            where T : Widget, new()
        {
            return (T)ObjectTypes.DefaultOf<T>();
        }
    }
}
