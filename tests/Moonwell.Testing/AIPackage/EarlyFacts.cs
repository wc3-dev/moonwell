using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Facts declared as a map declares them, in a file whose name sorts before Fact.cs: the script makes their types while it loads, before Fact.cs's own imports are filled (K11).</summary>
    public static class EarlyFacts
    {
        /// <summary>A written flag.</summary>
        public static readonly Fact<bool> Ready = new Fact<bool>("ready");

        /// <summary>A written unit, which may be null.</summary>
        public static readonly Fact<Unit?> Target = new Fact<Unit?>("target");

        /// <summary>A worked-out number.</summary>
        public static readonly Fact<int> Seven = new Fact<int>("seven", keep: 1f, read: board => 7);
    }
}
