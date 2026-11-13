using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>
    /// Facts a brain knows, each kept with the time it was written. Each brain has one, and each player and the game have one
    /// (<c>player.Blackboard()</c>, <c>Game.Instance.Blackboard()</c>). A read looks up a chain and the first blackboard
    /// holding the fact wins: a unit brain's, then that of the unit's owner now, then the game's; a player brain's, then its
    /// player's, then the game's; a game brain's, then the game's; a player's, then the game's. The same on every machine:
    /// read and write it outside <c>Local.For</c>.
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary>The kind of the game's blackboard.</summary>
        internal const int GameKind = 0;

        /// <summary>The kind of a player's blackboard.</summary>
        internal const int PlayerKind = 1;

        /// <summary>The kind of a brain's blackboard.</summary>
        internal const int BrainKind = 2;

        /// <summary>The number the facts keep this blackboard's values under; never another blackboard's.</summary>
        internal readonly int Number;

        /// <summary>The game's, a player's or a brain's.</summary>
        private readonly int kind;

        /// <summary>The facts this blackboard holds a value of, to forget when it ends.</summary>
        private readonly List<IHeldFact> held = new List<IHeldFact>();

        /// <summary>The unit, for a unit brain's blackboard once the brain is added; null otherwise.</summary>
        internal Unit? unit;

        /// <summary>The player, for a player's blackboard, or a player brain's once the brain is added; null otherwise.</summary>
        internal Player? player;

        /// <summary>Whether its brain has ended: it holds nothing more.</summary>
        internal bool ended;

        internal Blackboard(int kind, Player? player)
        {
            Number = Minds.NextBoard();
            this.kind = kind;
            this.player = player;
        }

        /// <summary>The unit of a unit brain's blackboard; null for any other. A worked-out fact's read function reads it.</summary>
        public Unit? Unit => unit;

        /// <summary>The player: for a unit brain's blackboard the unit's owner now, for a player brain's or a player's that player, and null for a game brain's or the game's. A worked-out fact's read function reads it.</summary>
        public Player? Player => unit != null ? unit.Owner : player;

        /// <summary>The value of a fact: a written one from the first blackboard up the chain that holds it, a worked-out one as its read function gives it for this blackboard; <c>default</c> (0, false or null) when nothing holds it.</summary>
        public T Read<T>(Fact<T> fact)
        {
            return fact.ReadFrom(this);
        }

        /// <summary>The seconds since the value a read would find was written or worked out; <c>float.MaxValue</c> when there is none. A worked-out fact is not worked out by this: only a value kept on this blackboard counts.</summary>
        public float Age<T>(Fact<T> fact)
        {
            return fact.AgeFrom(this);
        }

        /// <summary>Writes a fact on this blackboard with the time now, over what it held. A worked-out fact cannot be set: that is reported and refused, in every build. A blackboard whose brain has ended holds nothing more.</summary>
        public void Set<T>(Fact<T> fact, T value)
        {
            fact.SetOn(this, value);
        }

        /// <summary>Takes a fact off this blackboard, so that a read finds the next one up the chain holding it; a worked-out one is worked out again at its next read.</summary>
        public void Remove<T>(Fact<T> fact)
        {
            IHeldFact holding = fact;
            if (holding.Forget(Number))
            {
                held.Remove(holding);
            }
        }

        /// <summary>The next blackboard up the chain; null for the game's.</summary>
        internal Blackboard? Up()
        {
            if (kind == GameKind)
            {
                return null;
            }

            if (unit != null)
            {
                return Minds.Current().BoardOf(unit.Owner);
            }

            if (kind == BrainKind && player != null)
            {
                return Minds.Current().BoardOf(player);
            }

            return Minds.Current().GameBoard();
        }

        /// <summary>Notes a fact this blackboard now holds a value of.</summary>
        internal void Holds(IHeldFact fact)
        {
            held.Add(fact);
        }

        /// <summary>Ends a brain's blackboard: every fact forgets its value here.</summary>
        internal void End()
        {
            ended = true;
            for (var i = 0; i < held.Count; i++)
            {
                held[i].Forget(Number);
            }

            held.Clear();
        }
    }
}
