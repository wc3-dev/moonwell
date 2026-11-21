using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// What a unit's barrier has left to absorb: it takes what a hit has after armor, the barrier that ends soonest first, and ends with
    /// its holder, after its seconds, or when nothing is left. <c>unit.AddBarrier(200, heldBy: this, seconds: 8)</c> makes one.
    /// </summary>
    public sealed class Barrier : Scoped
    {
        /// <summary>The unit's barriers; null for a barrier that was never given to one.</summary>
        internal readonly BarrierBook? book;

        /// <summary>The seconds on the game's clock when it ends by itself; meaningless when it has no end.</summary>
        internal readonly float endsAt;

        /// <summary>Whether it ends by itself after some seconds.</summary>
        internal readonly bool timed;

        /// <summary>The timer that ends it; null for a barrier with no end.</summary>
        internal Timer? timer;

        private float left;

        internal Barrier(Unit unit, BarrierBook? book, float amount, float endsAt, bool timed)
        {
            Unit = unit;
            this.book = book;
            this.endsAt = endsAt;
            this.timed = timed;
            left = amount;
        }

        /// <summary>The unit the barrier protects.</summary>
        [LocalSafe]
        public Unit Unit { get; }

        /// <summary>How much it has left to absorb; 0 once it has ended.</summary>
        [LocalSafe]
        public float Left => left;

        /// <summary>Takes up to the amount, ends the barrier when nothing is left, and gives back what it took.</summary>
        internal float Take(float amount)
        {
            var taken = amount < left ? amount : left;
            left -= taken;
            return taken;
        }

        internal override void Finish()
        {
            left = 0.0f;
            if (timer != null)
            {
                timer.End();
            }

            if (book != null)
            {
                book.Drop(this);
            }
        }
    }

    /// <summary>Gives a unit a barrier: <c>unit.AddBarrier(200f, heldBy: this, seconds: 8f)</c>. An extension, so a unit class with a field of the name still has it.</summary>
    public static class UnitBarrier
    {
        /// <summary>
        /// Gives the unit a barrier of an amount, which absorbs what hits have left after armor (and block) until it is used up. It
        /// ends with the scope that holds it, after the seconds when given (0 for none), or when nothing is left. The unit's
        /// <see cref="Stat.Barrier"/> is the total its barriers have left, and its change reactions follow. A holder that has ended, or
        /// an amount of 0 or less, gives a barrier that has ended, with nothing published: <see cref="Stat.Barrier"/> is not raised and no
        /// change reaction runs. A holder that has ended is reported in a development build.
        /// </summary>
        /// <param name="unit">The unit the barrier protects.</param>
        /// <param name="amount">What it can absorb.</param>
        /// <param name="heldBy">The scope that holds it, which ends it when it ends.</param>
        /// <param name="seconds">How long it lasts; 0 for no end but its holder's or its use.</param>
        public static Barrier AddBarrier(this Unit unit, float amount, Scope heldBy, float seconds = 0.0f)
        {
            if (amount <= 0.0f || unit.IsGone || unit.HasEnded)
            {
                var none = new Barrier(unit, null, 0.0f, 0.0f, false);
                none.End();
                return none;
            }

            if (heldBy == null || heldBy.HasEnded || heldBy.childrenEnded)
            {
                Dev.Report("Unit.AddBarrier", "the barrier is held by " + (heldBy == null ? "nothing" : Names.NameOf(heldBy) + ", which has ended") + ", so it was not added");
                var none = new Barrier(unit, null, 0.0f, 0.0f, false);
                none.End();
                return none;
            }

            var book = unit.Get<BarrierBook>();
            if (book == null)
            {
                book = unit.Add(new BarrierBook());
            }

            var barrier = new Barrier(unit, book, amount, Runtime.World.Now() + seconds, seconds > 0.0f);
            book.Insert(barrier);
            if (seconds > 0.0f)
            {
                barrier.timer = book.OwnAfter(seconds, () => barrier.End());
            }

            heldBy.Adopt(barrier);
            return barrier;
        }
    }

    /// <summary>A unit's barriers, in the order they absorb: those that end by themselves, soonest first, then those that do not, oldest first. It keeps the unit's Stat.Barrier equal to what they have left.</summary>
    internal sealed class BarrierBook : UnitComponent
    {
        private readonly List<Barrier> barriers = new List<Barrier>();

        /// <summary>Whether the unit is ending: its barriers end with it, and the stat is not worked out for each.</summary>
        private bool closing;

        /// <summary>Adds a barrier in its place in the order and puts the total in the stat.</summary>
        internal void Insert(Barrier barrier)
        {
            var at = barriers.Count;
            if (barrier.timed)
            {
                at = 0;
                while (at < barriers.Count && barriers[at].timed && barriers[at].endsAt <= barrier.endsAt)
                {
                    at++;
                }
            }

            barriers.Insert(at, barrier);
            Sync();
        }

        /// <summary>Takes a barrier that has ended out and puts the total in the stat.</summary>
        internal void Drop(Barrier barrier)
        {
            if (barriers.Remove(barrier))
            {
                Sync();
            }
        }

        /// <summary>What the barriers have left, added up.</summary>
        internal float Total()
        {
            var total = 0.0f;
            for (var i = 0; i < barriers.Count; i++)
            {
                total += barriers[i].Left;
            }

            return total;
        }

        /// <summary>Lets the barriers absorb up to an amount, in their order, ends those used up, and gives back what they took.</summary>
        internal float Absorb(float amount)
        {
            var taken = 0.0f;
            var used = new List<Barrier>();
            for (var i = 0; i < barriers.Count && taken < amount; i++)
            {
                taken += barriers[i].Take(amount - taken);
                if (barriers[i].Left <= 0.0f)
                {
                    used.Add(barriers[i]);
                }
            }

            if (used.Count == 0)
            {
                Sync();
            }

            for (var i = 0; i < used.Count; i++)
            {
                used[i].End();
            }

            return taken;
        }

        /// <summary>The unit's own base of the stat is the total, so its change reactions and bars follow the barriers.</summary>
        private void Sync()
        {
            if (!closing && !Unit.IsGone)
            {
                Unit.SetBase(Stat.Barrier, Total());
            }
        }

        protected override void OnRemoved()
        {
            closing = true;
            var all = new List<Barrier>(barriers);
            for (var i = 0; i < all.Count; i++)
            {
                all[i].End();
            }
        }
    }
}
