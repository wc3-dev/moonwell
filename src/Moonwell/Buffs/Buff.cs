using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>A unit component that lasts for a duration, ticks at an interval, and stacks with the buffs of its class.</summary>
    public abstract class Buff : UnitComponent
    {
        /// <summary>The timer that ends the buff; a refresh ends it and starts a new one. Null when the buff has no expiry.</summary>
        internal Timer? expiry;

        /// <summary>The seconds the buff lasts; 0, the default, lasts until it is removed. A refresh gives the buff there the duration of the new apply, from the refresh on.</summary>
        [LocalSafe]
        public float Duration { get; protected set; } = 0.0f;

        /// <summary>The seconds between ticks; 0 for no ticks.</summary>
        [LocalSafe]
        public float Tick { get; protected set; } = 0.0f;

        /// <summary>What a second apply of the buff does when the unit already has one of its class.</summary>
        [LocalSafe]
        public Stacking Stacking { get; protected set; } = Stacking.Refresh;

        /// <summary>How strong the buff is, which the class sets in its constructor for each apply; a strongest buff reads the strongest of its applies still on (decision 165).</summary>
        [LocalSafe]
        public float Strength { get; protected set; } = 0.0f;

        /// <summary>For a strongest buff, the apply it runs at.</summary>
        internal Hold? current;

        /// <summary>The game buff a spell, an aura or an attack put on, which this object stands for (decision 152); 0 for a buff put on by Apply.</summary>
        internal int gameBuff;

        /// <summary>Whether a game aura gave it last, so the watch follows the strongest holder in reach.</summary>
        internal bool byAura;

        /// <summary>The caster whose cast gave it again last, and when, so that cast's own hit does not give it a second time.</summary>
        internal Unit? castBy;

        /// <summary>When castBy gave it again.</summary>
        internal float castAt;

        /// <summary>The source that gave it last, and when, so one source gives it once in an instant (a spell's hit of 0 and its cast).</summary>
        internal Unit? givenBy;

        /// <summary>When givenBy gave it; -1 before.</summary>
        internal float givenAt = -1.0f;

        /// <summary>The level of the spell, attack or aura that put this buff on (decision 153): of the matched cast or hit's ability, or the strongest aura holder's; 0 when nothing matched, and for a buff put on by Apply.</summary>
        [LocalSafe]
        public int Level { get; internal set; }

        /// <summary>Whether the unit's death ends the buff, after the buff has seen it; true unless the class keeps it through death.</summary>
        [LocalSafe]
        public bool EndsOnDeath { get; protected set; } = true;

        /// <summary>The unit that applied the buff last; null when none did.</summary>
        [LocalSafe]
        public Unit? Source { get; internal set; }

        /// <summary>The seconds until the buff expires; 0 without an expiry. For a strongest buff, until the last of its applies with a duration ends.</summary>
        [LocalSafe]
        public float Remaining
        {
            get
            {
                if (Stacking != Stacking.Strongest)
                {
                    return expiry != null && !expiry.HasEnded ? expiry.Remaining : 0.0f;
                }

                var most = 0.0f;
                for (var i = 0; holds != null && i < holds.Count; i++)
                {
                    var timer = holds[i].Expiry;
                    if (timer != null && !timer.HasEnded && timer.Remaining > most)
                    {
                        most = timer.Remaining;
                    }
                }

                return most;
            }
        }

        /// <summary>Runs when the buff is applied, and again each time it is applied while it is on.</summary>
        public virtual void OnApply()
        {
        }

        /// <summary>Runs at each tick.</summary>
        public virtual void OnTick()
        {
        }

        /// <summary>Runs when the buff expires, before it ends.</summary>
        public virtual void OnExpire()
        {
        }

        /// <summary>How many times the buff was applied: its start, each refresh and each apply joined to it.</summary>
        internal int applies;

        /// <summary>How many times each buff was applied, for EndOnDeath.</summary>
        internal static List<int> AppliesOf(List<Buff> found)
        {
            var counts = new List<int>();
            for (var i = 0; i < found.Count; i++)
            {
                counts.Add(found[i].applies);
            }

            return counts;
        }

        /// <summary>Ends the buffs a death ends, newest first, of those that were on the unit as it died (Inbound takes them, with their AppliesOf, at the start of the death, and calls this after its reactions): a buff the reactions applied, or applied again, has not seen the death and stays.</summary>
        internal static void EndOnDeath(List<Buff> found, List<int> applied)
        {
            for (var i = found.Count - 1; i >= 0; i--)
            {
                if (found[i].EndsOnDeath && found[i].applies == applied[i])
                {
                    found[i].End();
                }
            }
        }

        /// <summary>Starts a buff that was just added to its unit: it shows when it is the first of its class, starts its ticks and expiry, listens for deaths when a death ends it, and runs OnApply. A strongest buff takes its first apply, with its holder, instead of an expiry.</summary>
        internal void Begin(Scope? heldBy = null)
        {
            applies++;
            if (Stacking == Stacking.Strongest)
            {
                current = Join(Strength, Source, heldBy, Duration);
                if (HasEnded)
                {
                    return;
                }
            }

            if (EndsOnDeath)
            {
                Events.Need(Hook.OnDeath);
            }

            if (!HasOtherLive())
            {
                Show();
            }

            if (Tick > 0.0f)
            {
                OwnEvery(Tick, () => CallTick());
            }

            if (Duration > 0.0f && Stacking != Stacking.Strongest)
            {
                StartExpiry();
            }

            CallApply();
        }

        /// <summary>Starts a buff the watch made for a game buff (decision 152): its ticks, and listening for deaths when a death ends it, then OnApply. The game's buff shows its icon and decides when it ends, so it has no carrier and no expiry.</summary>
        internal void BeginSpell()
        {
            if (EndsOnDeath)
            {
                Events.Need(Hook.OnDeath);
            }

            if (Tick > 0.0f)
            {
                OwnEvery(Tick, () => CallTick());
            }

            CallApply();
        }

        /// <summary>Gives a spell's buff again (decision 153): the new source and level, and OnApply again.</summary>
        internal void GiveAgain(Unit? source, int level)
        {
            Source = source;
            Level = level;
            CallApply();
        }

        /// <summary>Joins another apply to a strongest buff already on its unit, with its own strength, source, holder and duration, and runs at the strongest; the new buff is dropped.</summary>
        internal void Offer(Buff from, Unit? source, Scope? heldBy)
        {
            applies++;
            Join(from.Strength, source, heldBy, from.Duration);
            if (!HasEnded)
            {
                Choose();
            }
        }

        /// <summary>Lets go of an apply of a strongest buff: the buff ends with its last, and otherwise runs at the strongest left. Other buffs let go as components do.</summary>
        internal override void Release(Hold hold)
        {
            if (Stacking != Stacking.Strongest)
            {
                base.Release(hold);
                return;
            }

            if (holds == null || !holds.Remove(hold) || ending || HasEnded)
            {
                return;
            }

            if (hold.Expiry != null)
            {
                hold.Expiry.End();
            }

            if (holds.Count == 0)
            {
                End();
                return;
            }

            Choose();
        }

        /// <summary>Adds an apply to a strongest buff: a hold its holder adopts, or one of its own with no holder, and a timer that ends it after its seconds. A holder that holds it already has its own apply updated instead.</summary>
        private Hold Join(float strength, Unit? source, Scope? heldBy, float seconds)
        {
            var held = heldBy == null ? null : HeldBy(heldBy);
            if (held != null)
            {
                held.Renewed++;
                held.Strength = strength;
                held.Source = source;
                if (held.Expiry != null)
                {
                    held.Expiry.End();
                    held.Expiry = null;
                }

                if (seconds > 0.0f)
                {
                    held.Expiry = OwnAfter(seconds, () => ExpireHold(held));
                }

                return held;
            }

            var hold = new Hold(this);
            hold.Strength = strength;
            hold.Source = source;
            holds ??= new List<Hold>();
            holds.Add(hold);
            if (heldBy != null)
            {
                heldBy.Adopt(hold);
            }

            if (!hold.HasEnded && seconds > 0.0f)
            {
                hold.Expiry = OwnAfter(seconds, () => ExpireHold(hold));
            }

            return hold;
        }

        /// <summary>Runs the strongest apply: the one running stays on a tie; its source becomes the buff's, and OnApply runs when the strength changed.</summary>
        private void Choose()
        {
            Hold? best = null;
            for (var i = 0; i < holds!.Count; i++)
            {
                var hold = holds[i];
                if (best == null || hold.Strength > best.Strength || (hold.Strength == best.Strength && hold == current))
                {
                    best = hold;
                }
            }

            current = best;
            Source = best!.Source;
            if (best.Strength != Strength)
            {
                Strength = best.Strength;
                CallApply();
            }
        }

        /// <summary>Ends an apply of a strongest buff whose seconds ran out; the last one runs OnExpire first, which may apply the buff again.</summary>
        private void ExpireHold(Hold hold)
        {
            if (HasEnded || hold.HasEnded)
            {
                return;
            }

            hold.Expiry = null;
            if (holds != null && holds.Count == 1)
            {
                // OnExpire may apply it again from the same holder, which renews this apply.
                var renewed = hold.Renewed;
                CallExpire();
                if (HasEnded || hold.HasEnded || hold.Renewed != renewed)
                {
                    return;
                }
            }

            hold.End();
        }

        /// <summary>Refreshes a buff already on its unit with a new apply of its class (decision 66): it takes the new buff's duration and the source, starts its expiry again for that duration (a duration of 0 lasts until it is removed), and runs OnApply again. Its other values, and what it has counted, stay its own; the new buff is dropped.</summary>
        internal void Refresh(Buff from, Unit? source)
        {
            applies++;
            Source = source;
            Duration = from.Duration;
            if (Duration > 0.0f)
            {
                StartExpiry();
            }
            else if (expiry != null)
            {
                expiry.End();
                expiry = null;
            }

            CallApply();
        }

        /// <summary>Runs after the buff's children have ended: it removes its display from the unit when it was the last buff of its class, and the unit is still in the game.</summary>
        internal override void Finish()
        {
            base.Finish();
            if (actor == null || Unit.IsGone || HasOtherLive())
            {
                return;
            }

            var carrier = ObjectTypes.CarrierOf(this);
            if (carrier == 0)
            {
                return;
            }

            Runtime.World.RemoveAbility(Unit.Handle, carrier);
            Runtime.World.RemoveAbility(Unit.Handle, ObjectTypes.BuffIdOf(this));
        }

        /// <summary>Whether another buff of this class is live on the unit.</summary>
        private bool HasOtherLive()
        {
            var found = Unit.GetAll<Buff>();
            var key = Names.ClassOf(this);
            for (var i = 0; i < found.Count; i++)
            {
                var other = found[i];
                if (other != this && !other.HasEnded && Names.ClassOf(other) == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Shows the buff on its unit through its carrier ability, when a display is registered for its class. The carrier is made permanent, as a morph (Metamorphosis, Bear Form) takes away an ability code added that is not, and its icon with it (seen 10-26).</summary>
        private void Show()
        {
            var carrier = ObjectTypes.CarrierOf(this);
            if (carrier == 0)
            {
                return;
            }

            Runtime.World.AddAbility(Unit.Handle, carrier);
            Runtime.World.HideAbility(Unit.Handle, carrier);
            Runtime.World.MakeAbilityPermanent(Unit.Handle, carrier);
        }

        /// <summary>Ends the expiry timer the buff has, if any, and starts a new one for its duration.</summary>
        private void StartExpiry()
        {
            if (expiry != null)
            {
                expiry.End();
            }

            Timer? made = null;
            made = OwnAfter(Duration, () => Expire(made));
            expiry = made;
        }

        /// <summary>Runs OnExpire, then ends the buff unless OnExpire ended it or applied it again.</summary>
        private void Expire(Timer? made)
        {
            if (HasEnded)
            {
                return;
            }

            CallExpire();
            if (!HasEnded && expiry == made)
            {
                End();
            }
        }

        /// <summary>Runs OnApply as a reaction of the buff.</summary>
        private void CallApply()
        {
            if (HasEnded)
            {
                return;
            }

            try
            {
                OnApply();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".OnApply", error);
            }
        }

        /// <summary>Runs OnTick as a reaction of the buff.</summary>
        private void CallTick()
        {
            if (HasEnded)
            {
                return;
            }

            try
            {
                OnTick();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".OnTick", error);
            }
        }

        /// <summary>Runs OnExpire as a reaction of the buff.</summary>
        private void CallExpire()
        {
            if (HasEnded)
            {
                return;
            }

            try
            {
                OnExpire();
            }
            catch (Exception error)
            {
                Errors.Report(this, ".OnExpire", error);
            }
        }
    }
}
