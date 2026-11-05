using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>How a fake spell is aimed: at a unit, at a place, or with no target.</summary>
    public enum SpellKind
    {
        /// <summary>At a unit, ordered with a target.</summary>
        Target,

        /// <summary>At a place, ordered at a point or at a unit, whose place it takes.</summary>
        Point,

        /// <summary>With no target, ordered as it is.</summary>
        None,
    }

    /// <summary>
    /// The spells a dummy casts in a fake world, as seen 11-03: an order of a spell's string by a unit that has it takes effect
    /// inside the order call, after the order's own event, and an instant spell's end-cast comes in the same call, a channel's
    /// at its end, at a stop or when the ability is removed. The game refuses an order of the wrong kind, a target the caster's
    /// player does not see (in fog, until a vision patch covers it) or one invisible to it, and what a case says to refuse. It
    /// also keeps what the runtime sets on a dummy's spell and reads of it. Kept apart from FakeWorld, at the Lua local limit.
    /// </summary>
    public sealed class FakeDummies
    {
        /// <summary>How far a vision patch reaches; the probe's was 128.</summary>
        private const float PatchReach = 128.0f;

        private readonly FakeWorld world;
        private readonly List<FakeSpell> spells = new List<FakeSpell>();
        private readonly Dictionary<unit, int> channels = new Dictionary<unit, int>();
        private readonly HashSet<int> refused = new HashSet<int>();
        private readonly HashSet<int> held = new HashSet<int>();
        private readonly Dictionary<player, HashSet<unit>> hidden = new Dictionary<player, HashSet<unit>>();
        private readonly HashSet<unit> invisible = new HashSet<unit>();
        private readonly Dictionary<player, Dictionary<int, int>> techs = new Dictionary<player, Dictionary<int, int>>();
        private readonly Dictionary<fogmodifier, FakePatch> patches = new Dictionary<fogmodifier, FakePatch>();
        private readonly Dictionary<int, float> durations = new Dictionary<int, float>();
        private readonly Dictionary<int, int> speeds = new Dictionary<int, int>();

        /// <summary>The dummies of a world.</summary>
        public FakeDummies(FakeWorld world)
        {
            this.world = world;
        }

        /// <summary>Each spell the runtime freed on a unit (range, mana and cooldown taken off its copy), in order, with its level.</summary>
        public List<(unit Unit, int Ability, int Level)> Fields { get; } = new List<(unit Unit, int Ability, int Level)>();

        /// <summary>Each vision patch made and stopped, in order: "made" and "stopped".</summary>
        public List<string> Patches { get; } = new List<string>();

        /// <summary>Defines a spell by its ability and order, in place of an earlier one of the ability: what it is aimed at, what its effect does (given the caster, the target and the place) and whether it is channelled.</summary>
        public void Spell(int abilityId, string order, SpellKind kind, Action<unit, unit?, float, float> effect, bool channel = false)
        {
            spells.RemoveAll(spell => spell.Ability == abilityId);
            spells.Add(new FakeSpell { Ability = abilityId, Order = order, Kind = kind, Effect = effect, Channel = channel });
        }

        /// <summary>Makes the game refuse every order of a spell, as for a target the spell does not allow.</summary>
        public void Refuse(int abilityId)
        {
            refused.Add(abilityId);
        }

        /// <summary>Makes the game take every order of a spell with no effect, as it took a dummy's standing on a target its player did not see (seen 11-03): the unit keeps the order until a stop.</summary>
        public void Hold(int abilityId)
        {
            held.Add(abilityId);
        }

        /// <summary>Hides a unit from a player, as fog does, until a vision patch of the player's covers it.</summary>
        public void Hide(player p, unit u)
        {
            if (!hidden.TryGetValue(p, out var units))
            {
                units = new HashSet<unit>();
                hidden[p] = units;
            }

            units.Add(u);
        }

        /// <summary>Makes a unit invisible to every player: a patch does not show it.</summary>
        public void Invisible(unit u)
        {
            invisible.Add(u);
        }

        /// <summary>Sets a player's count of a research or unit type, as GetPlayerTechCount reads it.</summary>
        public void Tech(player p, int techId, int count)
        {
            if (!techs.TryGetValue(p, out var counts))
            {
                counts = new Dictionary<int, int>();
                techs[p] = counts;
            }

            counts[techId] = count;
        }

        /// <summary>Sets a spell's durations, normal and hero.</summary>
        public void Durations(int abilityId, float normal, float hero)
        {
            durations[abilityId] = normal > hero ? normal : hero;
        }

        /// <summary>Sets a spell's missile speed; 0 until set, a spell with no missile.</summary>
        public void MissileSpeed(int abilityId, int speed)
        {
            speeds[abilityId] = speed;
        }

        /// <summary>Ends a unit's channel as its time runs out: its end-cast comes.</summary>
        public void EndChannel(unit u)
        {
            if (!channels.TryGetValue(u, out var abilityId))
            {
                return;
            }

            channels.Remove(u);
            if (world.Listening.Contains(GameEvent.Spell))
            {
                Inbound.SpellEnded(u, abilityId);
            }
        }

        /// <summary>Starts a vision patch of a player's at a place, and records "made".</summary>
        public fogmodifier Patch(player p, float x, float y)
        {
            var patch = new fogmodifier();
            patches[patch] = new FakePatch { Player = p, X = x, Y = y };
            Patches.Add("made");
            return patch;
        }

        /// <summary>Stops and ends a vision patch, and records "stopped".</summary>
        public void Unpatch(fogmodifier patch)
        {
            if (patches.Remove(patch))
            {
                Patches.Add("stopped");
            }
        }

        /// <summary>Whether a player sees a unit: one not hidden from it, or one a patch of its covers; never an invisible one.</summary>
        public bool Sees(player p, unit u)
        {
            if (invisible.Contains(u))
            {
                return false;
            }

            if (!hidden.TryGetValue(p, out var units) || !units.Contains(u))
            {
                return true;
            }

            var x = world.UnitX(u);
            var y = world.UnitY(u);
            foreach (var patch in patches.Values)
            {
                var dx = patch.X - x;
                var dy = patch.Y - y;
                if (patch.Player == p && dx * dx + dy * dy <= PatchReach * PatchReach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A player's count of a research or unit type; 0 until set.</summary>
        public int TechCount(player p, int techId)
        {
            return techs.TryGetValue(p, out var counts) && counts.TryGetValue(techId, out var count) ? count : 0;
        }

        /// <summary>The longer of a spell's durations; 0 until set.</summary>
        public float Lasts(int abilityId)
        {
            return durations.TryGetValue(abilityId, out var lasts) ? lasts : 0.0f;
        }

        /// <summary>A spell's missile speed; 0 until set.</summary>
        public int SpeedOf(int abilityId)
        {
            return speeds.TryGetValue(abilityId, out var speed) ? speed : 0;
        }

        /// <summary>Records that the runtime freed a unit's spell at a level.</summary>
        public void Freed(unit u, int abilityId, int level)
        {
            Fields.Add((u, abilityId, level));
        }

        /// <summary>The game's answer to an order that is a spell's: null for an order no spell of the unit has.</summary>
        internal bool? Answer(unit caster, string order, SpellKind kind, unit? target)
        {
            var spell = Find(caster, order);
            if (spell == null)
            {
                return null;
            }

            var accepted = spell.Kind == kind || (spell.Kind == SpellKind.Point && kind == SpellKind.Target);
            if (!accepted || refused.Contains(spell.Ability))
            {
                return false;
            }

            return held.Contains(spell.Ability) || target == null || Sees(world.OwnerOf(caster), target);
        }

        /// <summary>A spell's order taken: its effect inside the call, then an instant spell's end-cast; a channel goes on from before its effect, so a stop inside the effect ends it. A held spell's order, or one whose spell the order's reactions took away, takes no effect.</summary>
        internal void TakeEffect(unit caster, string order, unit? target, float x, float y)
        {
            var spell = Find(caster, order);
            if (spell == null || held.Contains(spell.Ability))
            {
                return;
            }

            if (spell.Channel)
            {
                channels[caster] = spell.Ability;
            }

            var listening = world.Listening.Contains(GameEvent.Spell);
            if (listening)
            {
                Inbound.SpellEffect(caster, spell.Ability, target, x, y);
            }

            spell.Effect(caster, target, x, y);
            if (!spell.Channel && listening)
            {
                Inbound.SpellEnded(caster, spell.Ability);
            }
        }

        /// <summary>A stop order ends a unit's channel.</summary>
        internal void Stopped(unit u)
        {
            EndChannel(u);
        }

        /// <summary>The ability's removal ends its channel; FakeWorld fires the end-cast.</summary>
        internal void AbilityRemoved(unit u, int abilityId)
        {
            if (channels.TryGetValue(u, out var channelled) && channelled == abilityId)
            {
                channels.Remove(u);
            }
        }

        private FakeSpell? Find(unit caster, string order)
        {
            for (var i = 0; i < spells.Count; i++)
            {
                if (spells[i].Order == order && world.AbilityLevel(caster, spells[i].Ability) > 0)
                {
                    return spells[i];
                }
            }

            return null;
        }

        /// <summary>A spell a case defines.</summary>
        private sealed class FakeSpell
        {
            public int Ability;
            public string Order = "";
            public SpellKind Kind;
            public Action<unit, unit?, float, float> Effect = null!;
            public bool Channel;
        }

        /// <summary>A vision patch of a player's at a place.</summary>
        private sealed class FakePatch
        {
            public player Player = null!;
            public float X;
            public float Y;
        }
    }
}
