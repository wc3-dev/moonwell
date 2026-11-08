namespace Moonwell
{
    /// <summary>An ability of a unit: one object per unit and ability id, a scope that holds the casts of the ability.</summary>
    public class Ability : Scope
    {
        /// <summary>The table behind AbilityHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? abilityHooks;

        /// <summary>The reactions of abilities; declared here, so that an override in a subclass is found.</summary>
        internal static HookTable AbilityHooks
        {
            get
            {
                if (abilityHooks == null)
                {
                    abilityHooks = new HookTable(typeof(Ability), new string[] { "OnCast", "OnCasting", "OnFinished", "OnLearned", "OnLost" }, new int[] { Hook.OnCast, Hook.OnCast, Hook.OnCast, Hook.OnSkillLearned, Hook.OnLost });
                }

                return abilityHooks;
            }
        }

        /// <summary>The bit mask of the reactions this class overrides, worked out when the ability is made for its unit.</summary>
        internal int Mask;

        /// <summary>The unit that has the ability.</summary>
        [LocalSafe]
        public Unit Unit { get; internal set; } = null!;

        /// <summary>The id of the ability.</summary>
        [LocalSafe]
        public int Id { get; internal set; }

        /// <summary>The level last read or set, kept for when the unit is gone.</summary>
        internal int lastLevel;

        /// <summary>The level of the ability on its unit, as the game reads it now; 0 when the unit lacks it. Setting it keeps it within the ability's levels, as the game does (5 on a three-level ability gives 3, 0 gives 1), and does nothing on a unit without the ability; a development build reports both. A hero skill raised on a hero runs the learn reactions once for each level gained, in order, each with Level reading that level, as learning does, and a reaction that changes the level ends the levels still to come; lowering runs nothing. Once the unit is gone, the last level read or set, and setting does nothing.</summary>
        [LocalSafe]
        public int Level
        {
            get
            {
                if (Unit.IsGone)
                {
                    return lastLevel;
                }

                var now = Runtime.World.AbilityLevel(Unit.Handle, Id);
                if (now == 0 && Unit.RemovedByGame())
                {
                    return lastLevel;
                }

                if (Local.Depth == 0)
                {
                    lastLevel = now;
                }

                return now;
            }

            set
            {
                if (Unit.IsGone)
                {
                    return;
                }

                var now = Runtime.World.AbilityLevel(Unit.Handle, Id);
                if (now <= 0)
                {
                    Dev.Report(Names.NameOf(this) + ".Level", "the unit does not have the ability, so level " + value + " was not set");
                    return;
                }

                var hero = Unit as Hero;
                if (hero == null || value <= now || !Runtime.World.IsHeroAbility(Unit.Handle, Id))
                {
                    SetLevelInGame(value, value);
                    return;
                }

                // One level at a time, as learning gives them, so that each reaction reads its own level.
                for (var level = now + 1; level <= value; level++)
                {
                    if (!SetLevelInGame(level, value))
                    {
                        return;
                    }

                    Inbound.Learned(hero, this);
                    if (Unit.IsGone || Runtime.World.AbilityLevel(Unit.Handle, Id) != level)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>Sets the level in the game and keeps the level it gave; whether that is the level asked for. When it is not, a development build reports the level the map asked for and the one the game gave.</summary>
        internal bool SetLevelInGame(int level, int asked)
        {
            var given = Runtime.World.ChangeAbilityLevel(Unit.Handle, Id, level);
            lastLevel = given;
            if (given == level)
            {
                return true;
            }

            Dev.Report(Names.NameOf(this) + ".Level", "set to level " + asked + ", and the game gave " + given + ": an ability's level goes from 1 to its last");
            return false;
        }

        /// <summary>The full cooldown of the ability at its level now, on this unit, in seconds; 0 when the unit lacks it or is gone. Setting it changes this unit's cooldown at this level only, from its next cast on (a level gained later has its own), and does nothing on a unit without the ability; a development build reports that.</summary>
        [LocalSafe]
        public float Cooldown
        {
            get => Has() ? Runtime.World.AbilityCooldown(Unit.Handle, Id, Level - 1) : 0.0f;
            set
            {
                if (Usable("Cooldown"))
                {
                    Runtime.World.SetAbilityCooldown(Unit.Handle, Id, Level - 1, value);
                }
            }
        }

        /// <summary>The seconds left of the ability's cooldown on this unit; 0 when it is ready, the unit lacks it or is gone.</summary>
        [LocalSafe]
        public float CooldownLeft => Has() ? Runtime.World.AbilityCooldownLeft(Unit.Handle, Id) : 0.0f;

        /// <summary>The mana a cast of the ability costs at its level now, on this unit; 0 when the unit lacks it or is gone. Setting it changes this unit's cost at this level only, from its next cast on, and does nothing on a unit without the ability; a development build reports that.</summary>
        [LocalSafe]
        public int ManaCost
        {
            get => Has() ? Runtime.World.AbilityManaCost(Unit.Handle, Id, Level - 1) : 0;
            set
            {
                if (Usable("ManaCost"))
                {
                    Runtime.World.SetAbilityManaCost(Unit.Handle, Id, Level - 1, value);
                }
            }
        }

        /// <summary>Makes the ability ready at once on this unit, ending its cooldown.</summary>
        public void ResetCooldown()
        {
            if (Usable("ResetCooldown"))
            {
                Runtime.World.EndAbilityCooldown(Unit.Handle, Id);
            }
        }

        /// <summary>Starts the ability's cooldown on this unit for so many seconds, shown on its button, in place of what is left; any length, the full cooldown is not changed. 0 or less makes it ready, as the game does nothing for 0 (seen 11-08).</summary>
        public void StartCooldown(float seconds)
        {
            if (!Usable("StartCooldown"))
            {
                return;
            }

            if (seconds <= 0.0f)
            {
                Runtime.World.EndAbilityCooldown(Unit.Handle, Id);
                return;
            }

            Runtime.World.StartAbilityCooldown(Unit.Handle, Id, seconds);
        }

        /// <summary>Whether the unit is in the game and has the ability.</summary>
        private bool Has()
        {
            return !Unit.IsGone && Runtime.World.AbilityLevel(Unit.Handle, Id) > 0;
        }

        /// <summary>Whether a change can be made: the unit is in the game and has the ability; when it lacks it, a development build reports the member.</summary>
        private bool Usable(string member)
        {
            if (Unit.IsGone)
            {
                return false;
            }

            if (Runtime.World.AbilityLevel(Unit.Handle, Id) > 0)
            {
                return true;
            }

            Dev.Report(Names.NameOf(this) + "." + member, "the unit does not have the ability, so nothing was changed");
            return false;
        }

        /// <summary>Runs when the unit begins a cast of the ability, before anything is spent: before its mana, its cooldown and its effect, while the caster starts to cast (the game's channel event). cast.Refuse(text) stops the cast, shows the text to the caster's owner as the game shows its errors, and nothing is spent. The cast passed here is the one OnCast gets when the spell takes effect; a cast stopped before that ends without an OnCast.</summary>
        public virtual void OnCasting(Cast cast)
        {
        }

        /// <summary>Runs when a cast of the ability runs to its end (the game's finish event), before the cast ends: a channel its whole time, another spell its casting animation. A cast stopped early, by a stun, an order or a death, ends without it.</summary>
        public virtual void OnFinished(Cast cast)
        {
        }

        /// <summary>Runs when a cast of the ability takes effect (the game's effect event), after OnCasting let it through. This ability object is a scope that lasts as long as the caster is in the game, so a delayed effect made here with After runs after the spell has ended; work that should stop with a channel is made on the cast: cast.Every(0.5f, Pulse).</summary>
        public virtual void OnCast(Cast cast)
        {
        }

        /// <summary>Runs when a hero learns the ability or raises its level, before the hero's OnSkillLearned; Level reads the new level. The skill point is not spent yet: the hero's SkillPoints reads one more than after (seen 11-02).</summary>
        public virtual void OnLearned()
        {
        }

        /// <summary>Runs when RemoveAbility takes the ability from a hero whose skill it is, after the removal, so Level reads 0 and a cast of it has ended. The object stays, with what it made: undo here what OnLearned set up. The game fires nothing when an ability goes, so an ability that leaves with an item, or one a native removes, runs nothing.</summary>
        public virtual void OnLost()
        {
        }
    }
}
