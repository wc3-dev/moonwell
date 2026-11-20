namespace Moonwell
{
    /// <summary>
    /// Healing from code: <c>healer.Heal(target, 120f)</c>. An extension, so a unit class whose declared field is named
    /// Heal still has it.
    /// </summary>
    public static class UnitHeal
    {
        /// <summary>
        /// Heals a unit from another through the package's two stages: the first stage's reactions (OnHealing, OnReceiving,
        /// OnUnitReceiving) may change the amount, then the life is set, never past the most life, and the second stage's
        /// (OnHealed, OnReceived, OnUnitReceived) see how much went in. The reactions run inside the call, in the order
        /// hits use, and share their chain limit: a heal or hit made from a reaction counts, and one that would make the
        /// chain longer than 8 throws, which the reaction reports. The game's own heals send no event and stay outside, and
        /// <c>unit.Life += x</c> stays raw, with no reaction.
        /// </summary>
        /// <param name="source">The unit that heals.</param>
        /// <param name="target">The unit that is healed.</param>
        /// <param name="amount">The amount to add.</param>
        /// <param name="tag">Anything of the map's own, which reactions read as the heal's Tag.</param>
        /// <returns>The heal; null for a target that is dead or gone, or an amount of 0 or less.</returns>
        public static Heal? Heal(this Unit source, Unit target, float amount, object? tag = null)
        {
            if (amount <= 0.0f || !target.IsAlive)
            {
                return null;
            }

            var chain = Reactions.NextChain();
            if (chain >= Pipeline.MaxChain)
            {
                throw new MoonwellException(Names.NameOf(source) + " to " + Names.NameOf(target) + ": not healed, a chain of " + Pipeline.MaxChain + " hits and heals made from reactions ends here");
            }

            var heal = new Heal(target, source, amount, tag, chain);
            Reactions.RunHeal(heal, 0);
            heal.Landed = true;
            var done = 0.0f;
            if (heal.Amount > 0.0f && target.IsAlive)
            {
                var life = target.Life;
                var room = target.MaxLife - life;
                done = room <= 0.0f ? 0.0f : (heal.Amount < room ? heal.Amount : room);
                if (done > 0.0f)
                {
                    target.Life = life + done;
                }
            }

            heal.Done = done;
            heal.Over = heal.Amount > 0.0f ? heal.Amount - done : 0.0f;
            Reactions.RunHeal(heal, 1);
            return heal;
        }
    }
}
