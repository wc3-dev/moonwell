namespace Moonwell.AI
{
    /// <summary>A fact of any type, as a blackboard lists the facts it holds, to forget them when it ends.</summary>
    internal interface IHeldFact
    {
        /// <summary>Forgets the value kept for a blackboard; false when there was none.</summary>
        bool Forget(int board);
#if DEBUG

        /// <summary>The fact's name, for the AI tools.</summary>
        string FactName();

        /// <summary>The value kept for a blackboard as the AI tools show it; "none" when there is none.</summary>
        string TextOn(int board);

        /// <summary>The time the value kept for a blackboard was written or worked out; float.MaxValue when there is none.</summary>
        float TimeOn(int board);

        /// <summary>-fact: sets the value a chat text gives on a blackboard; empty when set, otherwise why not.</summary>
        string SetFromText(Blackboard board, string text);
#endif
    }
}
