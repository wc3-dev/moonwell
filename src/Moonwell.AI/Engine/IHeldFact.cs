namespace Moonwell.AI
{
    /// <summary>A fact of any type, as a blackboard lists the facts it holds, to forget them when it ends.</summary>
    internal interface IHeldFact
    {
        /// <summary>Forgets the value kept for a blackboard; false when there was none.</summary>
        bool Forget(int board);
    }
}
