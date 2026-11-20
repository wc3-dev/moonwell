using System;

namespace Moonwell
{
    /// <summary>
    /// The one game component of the stats package: it sees every hit and heal, after the reactions of the units and their components, and
    /// does what the ready-made stats say. A hit or heal the package made itself, or one that started at 0, gets none of it.
    /// </summary>
    internal sealed class StatEffects : GameComponent, IDamageWatch, IHealWatch
    {
        private const float RadiansPerDegree = 0.017453292f;

        /// <summary>How often a staggered hit's share is dealt, in seconds.</summary>
        internal const float StaggerPeriod = 0.5f;

        /// <summary>Before armor: the miss and the parry, the powers, the crit and the armor penetration.</summary>
        public void OnUnitTaking(Hit hit)
        {
            if (Skipped(hit))
            {
                return;
            }

            var source = hit.Source;
            if (source == null)
            {
                return;
            }

            var target = hit.Target;
            if (hit.IsAttack)
            {
                var miss = 100.0f - Reading.Of(source, Stat.HitChance) + Reading.Of(target, Stat.Evasion);
                if (Reading.Roll(miss))
                {
                    hit.Miss(StatsSystem.ShowTexts ? "miss" : "");
                    return;
                }

                // The front half of the target: the source is within 90 degrees of where the target faces.
                if (hit.IsMelee && InFront(target, source) && Reading.Roll(Reading.Of(target, Stat.Parry)))
                {
                    hit.Miss(StatsSystem.ShowTexts ? "parry" : "");
                    return;
                }

                hit.Amount += Reading.Of(source, Stat.AttackPower);
            }
            else if (hit.IsSpell)
            {
                hit.Amount += Reading.Of(source, Stat.SpellPower) * hit.Power;
            }

            if (Reading.Roll(Reading.Of(source, Stat.CritChance)))
            {
                hit.Amount = hit.Amount * Reading.Of(source, Stat.CritDamage) / 100.0f;
                hit.IsCrit = true;
            }

            var penetration = Reading.Of(source, Stat.ArmorPenetration);
            if (penetration > 0.0f && hit.Armor > 0.0f)
            {
                hit.Armor = hit.Armor > penetration ? hit.Armor - penetration : 0.0f;
            }
        }

        /// <summary>After armor: resistance, area damage reduction, block, barriers and stagger.</summary>
        public void OnUnitTaken(Hit hit)
        {
            if (Skipped(hit))
            {
                return;
            }

            var target = hit.Target;
            var resistance = Reading.Of(target, Stat.Resistance(hit.DamageKind));
            if (resistance > 0.0f && hit.Source != null)
            {
                resistance -= Reading.Of(hit.Source, Stat.SpellPenetration);
                if (resistance < 0.0f)
                {
                    resistance = 0.0f;
                }
            }

            if (resistance != 0.0f)
            {
                hit.Amount = hit.Amount * (1.0f - resistance / 100.0f);
            }

            if (hit.IsArea)
            {
                hit.Amount = hit.Amount * (1.0f - Reading.Of(target, Stat.AreaDamageReduction) / 100.0f);
            }

            if (hit.IsAttack && Reading.Roll(Reading.Of(target, Stat.BlockChance)))
            {
                var blocked = Reading.Of(target, Stat.BlockAmount);
                if (blocked > hit.Amount)
                {
                    blocked = hit.Amount;
                }

                hit.Amount -= blocked;
                hit.Blocked = blocked;
                if (hit.Amount <= 0.0f && StatsSystem.ShowTexts)
                {
                    FloatingText.Pop("block", target.X - Hit.MissLeft, target.Y, Color.Red, null, Hit.MissHeight);
                }
            }

            if (hit.Amount > 0.0f && Reading.Of(target, Stat.Barrier) > 0.0f)
            {
                var book = target.Get<BarrierBook>();
                if (book != null)
                {
                    hit.Absorbed = book.Absorb(hit.Amount);
                    hit.Amount -= hit.Absorbed;
                }
            }

            var stagger = Reading.Of(target, Stat.Stagger);
            var seconds = stagger > 0.0f ? Reading.Of(target, Stat.StaggerTime) : 0.0f;
            if (hit.Amount > 0.0f && seconds > 0.0f)
            {
                var later = hit.Amount * stagger / 100.0f;
                hit.Amount -= later;
                new Staggered(hit.Source ?? target, target, later, seconds).Begin();
            }
        }

        /// <summary>Landed: the crit's text, life steal and mana leech.</summary>
        public void OnUnitHurt(Hit hit)
        {
            if (Skipped(hit) || hit.Amount <= 0.0f)
            {
                return;
            }

            if (hit.IsCrit && StatsSystem.ShowTexts)
            {
                FloatingText.Pop(((int)MathF.Floor(hit.Amount + 0.5f)).ToString() + "!", hit.Target.X, hit.Target.Y, Color.Red);
            }

            var source = hit.Source;
            if (hit.IsAttack && source != null && source.IsAlive)
            {
                var steal = Reading.Of(source, Stat.LifeSteal);
                if (steal > 0.0f)
                {
                    source.Heal(source, hit.Amount * steal / 100.0f, StatsSystem.Own);
                }

                var leech = Reading.Of(source, Stat.ManaLeech);
                if (leech > 0.0f)
                {
                    source.Mana += hit.Amount * leech / 100.0f;
                }
            }
        }

        /// <summary>A heal's first stage: the source's healing bonus, then its crit.</summary>
        public void OnUnitReceiving(Heal heal)
        {
            if (heal.Original == 0.0f || ReferenceEquals(heal.Tag, StatsSystem.Own))
            {
                return;
            }

            var source = heal.Source;
            var bonus = Reading.Of(source, Stat.HealingBonus);
            if (bonus != 0.0f)
            {
                heal.Amount = heal.Amount * (1.0f + bonus / 100.0f);
            }

            if (Reading.Roll(Reading.Of(source, Stat.CritChance)))
            {
                heal.Amount = heal.Amount * Reading.Of(source, Stat.CritDamage) / 100.0f;
                heal.IsCrit = true;
            }
        }

        /// <summary>Whether a hit gets none of the effects: an earlier reaction missed it, the package made it, or it started at 0.</summary>
        private static bool Skipped(Hit hit)
        {
            return hit.Missed || hit.Original == 0.0f || ReferenceEquals(hit.Tag, StatsSystem.Own);
        }

        /// <summary>Whether the source stands in the front half of the target: within 90 degrees of where the target faces.</summary>
        private static bool InFront(Unit target, Unit source)
        {
            var facing = target.Facing * RadiansPerDegree;
            return MathF.Cos(facing) * (source.X - target.X) + MathF.Sin(facing) * (source.Y - target.Y) >= 0.0f;
        }
    }

    /// <summary>The part of a hit a stagger put off: dealt to the target in even ticks every half second, the last one taking what is left, as hits of a kind armor does not cut.</summary>
    internal sealed class Staggered
    {
        private readonly Unit source;

        private readonly Unit target;

        private readonly float each;

        private float left;

        private int ticks;

        internal Staggered(Unit source, Unit target, float amount, float seconds)
        {
            this.source = source;
            this.target = target;
            left = amount;
            ticks = (int)MathF.Ceiling(seconds / StatEffects.StaggerPeriod);
            if (ticks < 1)
            {
                ticks = 1;
            }

            each = amount / ticks;
        }

        /// <summary>Starts the ticks: the first comes after a period.</summary>
        internal void Begin()
        {
            target.OwnAfter(StatEffects.StaggerPeriod, Tick);
        }

        private void Tick()
        {
            if (!target.IsAlive)
            {
                return;
            }

            ticks--;
            var amount = ticks <= 0 ? left : each;
            left -= amount;
            var from = source.IsGone || source.HasEnded ? target : source;
            from.Damage(target, amount, DamageKind.Universal, tag: StatsSystem.Own);
            if (ticks > 0)
            {
                Begin();
            }
        }
    }
}
