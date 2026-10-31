using Moonwell;

namespace NameClash
{
    /// <summary>A map that starts the Probe component its test writes beside this file.</summary>
    public sealed class ClashMap : Map
    {
        public ClashMap()
        {
            Name = "NameClash";
            Components.Add(new Probe());
        }
    }
}
