namespace Moonwell
{
    /// <summary>
    /// How deep the game's events nest. An event the game fires inside a call a reaction makes runs its own reactions inside that
    /// call, and the game drops an event, with no error, once too many are open. Every event that runs a reaction comes in here,
    /// is counted, and goes on to its Handle method; a chain that nests past MaxNested is reported once (decision 276).
    /// </summary>
    internal static partial class Inbound
    {
        /// <summary>
        /// Events nested deeper than this are reported; none is stopped. The game dropped the 99th of a bare Lua map's nested
        /// events, and the 66th with a pcall in each (game facts), so it has room for about 196 nested calls, two for an event and
        /// one for a pcall. Every try in the compiled Lua is a pcall, and an event here holds two, around its entry and around its
        /// reaction: worked out from that, not probed, the game drops at 49 deep at best, at 39 through an order or Unit.Damage,
        /// and at 21 once an event holds nine calls. It is above the 16 of the order and dummy guards, so a chain of those alone is
        /// reported by its own guard.
        /// </summary>
        internal const int MaxNested = 20;

        /// <summary>How many of the game's events are running now, each inside a reaction to the one before.</summary>
        private static int nested;

        /// <summary>Whether the chain running now has been reported.</summary>
        private static bool nestedReported;

        /// <summary>
        /// One of the game's events comes in: it is counted, and the first of a chain to nest past MaxNested is reported, with the
        /// reaction it runs and the class of the unit it is about, when Moonwell knows the unit. Leave follows the event's Handle
        /// method with no try around them, which would take one more of the game's calls at every event.
        /// </summary>
        private static void Enter(string reaction, unit? about = null)
        {
            nested++;
            if (nested <= MaxNested || nestedReported)
            {
                return;
            }

            nestedReported = true;
            var known = about == null ? null : KnownUnits.Find(about);
            Errors.Refuse("Moonwell.Events", "game events nested more than " + MaxNested + " deep, each inside a reaction to the one before, at " + reaction + (known == null ? "" : " for " + Names.NameOf(known)) + "; a little deeper the game drops them without a word, so start the next from a timer (After(0))");
        }

        /// <summary>The event Enter counted is over; the end of the outermost starts a new chain.</summary>
        private static void Leave()
        {
            nested--;
            if (nested <= 0)
            {
                ResetNesting();
            }
        }

        /// <summary>No event is running: Runtime.Reset calls it, and Runtime.Wake, as a timer never runs inside an event, which puts right a count that an error thrown past Leave left behind.</summary>
        internal static void ResetNesting()
        {
            nested = 0;
            nestedReported = false;
        }

        /// <summary>A unit enters the map.</summary>
        internal static void Entered(unit handle)
        {
            Enter("OnUnitCreated", handle);
            HandleEntered(handle);
            Leave();
        }

        /// <summary>A unit picks up an item.</summary>
        internal static void PickedUp(unit u, item i)
        {
            Enter("OnItemPickedUp", u);
            HandlePickedUp(u, i);
            Leave();
        }

        /// <summary>A unit drops an item.</summary>
        internal static void Dropped(unit u, item i)
        {
            Enter("OnItemDropped", u);
            HandleDropped(u, i);
            Leave();
        }

        /// <summary>A destructable dies.</summary>
        internal static void DestructableDied(destructable d)
        {
            Enter("a destructable's OnDeath");
            HandleDestructableDied(d);
            Leave();
        }

        /// <summary>A unit takes damage; returns the amount after the reactions.</summary>
        internal static float Damaged(unit target, unit? source, float amount, bool isAttack)
        {
            Enter("OnDamaged", target);
            var left = HandleDamaged(target, source, amount, isAttack);
            Leave();
            return left;
        }

        /// <summary>A unit dies.</summary>
        internal static void Died(unit dying, unit? killer)
        {
            Enter("OnDeath", dying);
            HandleDied(dying, killer);
            Leave();
        }

        /// <summary>A unit's spell takes effect.</summary>
        internal static void SpellEffect(unit caster, int abilityId, unit? target, float x, float y)
        {
            Enter("OnCast", caster);
            HandleSpellEffect(caster, abilityId, target, x, y);
            Leave();
        }

        /// <summary>A unit starts to cast a spell.</summary>
        internal static void SpellChannel(unit caster, int abilityId, unit? target, float x, float y)
        {
            Enter("OnCasting", caster);
            HandleSpellChannel(caster, abilityId, target, x, y);
            Leave();
        }

        /// <summary>A unit's spell runs to its end.</summary>
        internal static void SpellFinished(unit caster, int abilityId)
        {
            Enter("OnFinished", caster);
            HandleSpellFinished(caster, abilityId);
            Leave();
        }

        /// <summary>A unit's spell ends, and with it the cast and what the cast holds.</summary>
        internal static void SpellEnded(unit caster, int abilityId)
        {
            Enter("the end of a cast", caster);
            HandleSpellEnded(caster, abilityId);
            Leave();
        }

        /// <summary>A hero gains one level or more.</summary>
        internal static void HeroLevel(unit handle)
        {
            Enter("OnLevelUp", handle);
            HandleHeroLevel(handle);
            Leave();
        }

        /// <summary>A hero learns a skill or raises its level.</summary>
        internal static void HeroSkill(unit handle, int abilityId)
        {
            Enter("OnSkillLearned", handle);
            HandleHeroSkill(handle, abilityId);
            Leave();
        }

        /// <summary>A unit is given an order.</summary>
        internal static void Ordered(unit handle, int id, OrderKind kind, float x, float y, unit? targetUnit, item? targetItem, destructable? targetDestructable, int queue)
        {
            Enter("OnOrdered", handle);
            HandleOrdered(handle, id, kind, x, y, targetUnit, targetItem, targetDestructable, queue);
            Leave();
        }

        /// <summary>A unit is loaded into a transport.</summary>
        internal static void Loaded(unit cargoHandle, unit transportHandle)
        {
            Enter("OnLoaded", cargoHandle);
            HandleLoaded(cargoHandle, transportHandle);
            Leave();
        }

        /// <summary>A player selects or deselects a unit.</summary>
        internal static void Selection(unit handle, player who, bool selected)
        {
            Enter(selected ? "OnSelected" : "OnDeselected", handle);
            HandleSelection(handle, who, selected);
            Leave();
        }

        /// <summary>The game says a player's alliances changed.</summary>
        internal static void AllianceChanged(player giver)
        {
            Enter("OnAllianceChanged");
            HandleAllianceChanged(giver);
            Leave();
        }

        /// <summary>A player leaves the game.</summary>
        internal static void Left(player p)
        {
            Enter("OnLeave");
            HandleLeft(p);
            Leave();
        }

        /// <summary>A game saved with the game's own Save Game was loaded.</summary>
        internal static void GameLoaded()
        {
            Enter("OnGameLoaded");
            HandleGameLoaded();
            Leave();
        }

        /// <summary>A person typed a chat line with a dash in it.</summary>
        internal static void ChatArrived(player p, string text)
        {
            Enter("a chat command");
            HandleChatArrived(p, text);
            Leave();
        }

        /// <summary>A person pressed Esc.</summary>
        internal static void EscArrived(player p)
        {
            Enter("a press of Esc");
            HandleEscArrived(p);
            Leave();
        }

        /// <summary>A sharing message arrived from a player.</summary>
        internal static void SyncArrived(player p, string data)
        {
            Enter("a shared message");
            HandleSyncArrived(p, data);
            Leave();
        }
    }
}
