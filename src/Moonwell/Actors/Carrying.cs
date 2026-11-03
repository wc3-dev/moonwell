namespace Moonwell
{
    /// <summary>An item's carry: a scope from a pick-up into the inventory or an equipment slot to the drop, held by its carrier (decision 166).</summary>
    internal sealed class Carrying : Scope
    {
        /// <summary>Whether this stands for no carry, as Item.Carry gives outside one: it has ended, and what is held by it is not added, with no report.</summary>
        internal bool quiet;

        /// <summary>A carry that has ended and stands for none.</summary>
        internal static Carrying None()
        {
            var none = new Carrying();
            none.quiet = true;
            none.End();
            return none;
        }
    }
}
