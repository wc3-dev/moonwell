namespace Moonwell.AI
{
    /// <summary>A fact's value on one blackboard, and the time it was written or worked out; changed in place when written again.</summary>
    internal sealed class Kept<T>
    {
        internal T Value;

        internal float Time;

        internal Kept(T value, float time)
        {
            Value = value;
            Time = time;
        }
    }
}
