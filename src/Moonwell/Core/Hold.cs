namespace Moonwell
{
    /// <summary>A holder's hold on a component: the holder adopts it, so the holder's end lets go of the component (decision 164).</summary>
    internal sealed class Hold : Scoped
    {
        /// <summary>Makes a hold on a component.</summary>
        internal Hold(Component held)
        {
            Held = held;
        }

        /// <summary>The component held.</summary>
        internal Component Held;

        /// <summary>For an apply of a strongest buff, the strength it brings.</summary>
        internal float Strength;

        /// <summary>For an apply of a strongest buff, the unit that applied it.</summary>
        internal Unit? Source;

        /// <summary>For an apply of a strongest buff with a duration, the timer that ends this apply.</summary>
        internal Timer? Expiry;

        /// <summary>For an apply of a strongest buff, how many times its holder applied it again, which an expiry's OnExpire may do.</summary>
        internal int Renewed;

        /// <summary>Lets go of the component, which ends when this was its last hold.</summary>
        internal override void Finish()
        {
            Held.Release(this);
        }
    }
}
