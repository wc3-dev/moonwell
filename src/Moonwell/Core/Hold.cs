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

        /// <summary>Lets go of the component, which ends when this was its last hold.</summary>
        internal override void Finish()
        {
            Held.Release(this);
        }
    }
}
