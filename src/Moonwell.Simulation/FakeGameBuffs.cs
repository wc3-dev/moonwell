using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The game buffs of a fake world, as seen 11-03: a spell's, an aura's or an attack's buff reads level 1 while it is on,
    /// a spell's hit fires its damage event before the buff goes on, an aura gives its buff at once in reach and takes it
    /// 3 s after leaving, unless another aura still gives it, an aura of a dead or removed holder gives nothing, and a death
    /// takes every buff off before the death event. Kept apart from FakeWorld, whose class is at the Lua local limit
    /// (CSharp.lua moves the members past 200 to a table).
    /// </summary>
    public sealed class FakeGameBuffs
    {
        private readonly FakeWorld world;
        private readonly List<(unit Unit, int Buff, float End)> ends = new List<(unit Unit, int Buff, float End)>();
        private readonly Dictionary<unit, List<int>> given = new Dictionary<unit, List<int>>();
        private readonly List<FakeAura> auras = new List<FakeAura>();

        /// <summary>The game buffs of a world.</summary>
        public FakeGameBuffs(FakeWorld world)
        {
            this.world = world;
        }

        /// <summary>Puts a game buff on a unit: its level reads 1 from now; with seconds above 0 it runs out after them.</summary>
        public void Give(unit u, int buffId, float seconds = 0.0f)
        {
            world.SetAbilityLevel(u, buffId, 1);
            if (!given.TryGetValue(u, out var buffs))
            {
                buffs = new List<int>();
                given[u] = buffs;
            }

            if (!buffs.Contains(buffId))
            {
                buffs.Add(buffId);
            }

            ends.RemoveAll(entry => entry.Unit == u && entry.Buff == buffId);
            if (seconds > 0.0f)
            {
                ends.Add((u, buffId, world.Time + seconds));
            }
        }

        /// <summary>Takes a game buff off a unit, as the game does when it runs out or is dispelled.</summary>
        public void Take(unit u, int buffId)
        {
            world.SetAbilityLevel(u, buffId, 0);
            ends.RemoveAll(entry => entry.Unit == u && entry.Buff == buffId);
        }

        /// <summary>A spell's or an attack's hit that puts a game buff on: a hit of 0 from the source, whose damage event fires first, then the buff, in the same frame.</summary>
        public void Hit(unit source, unit target, int buffId, float seconds = 0.0f)
        {
            world.DealDamage(source, target, 0.0f, false, false, 0, 0, 0);
            Give(target, buffId, seconds);
        }

        /// <summary>Runs an aura of the game held by a unit, which it gets at level 1 unless it has a level already.</summary>
        public void Aura(unit holder, int abilityId, int buffId, float reach)
        {
            if (world.AbilityLevel(holder, abilityId) <= 0)
            {
                world.SetAbilityLevel(holder, abilityId, 1);
            }

            auras.Add(new FakeAura { Holder = holder, Buff = buffId, Reach = reach });
            Step(world.Time);
        }

        /// <summary>Runs out the buffs whose end has come, and steps the auras; Advance calls it at each wake and at its end.</summary>
        public void Step(float time)
        {
            for (var i = ends.Count - 1; i >= 0; i--)
            {
                if (ends[i].End <= time + 0.001f)
                {
                    var ended = ends[i];
                    ends.RemoveAt(i);
                    world.SetAbilityLevel(ended.Unit, ended.Buff, 0);
                }
            }

            foreach (var aura in auras)
            {
                var holds = world.UnitTypeId(aura.Holder) != 0 && !world.RemovedUnits.Contains(aura.Holder) && world.IsAlive(aura.Holder);
                var near = holds ? world.UnitsInRange(world.UnitX(aura.Holder), world.UnitY(aura.Holder), aura.Reach) : new List<unit>();
                foreach (var u in near)
                {
                    if (!world.IsAlive(u))
                    {
                        continue;
                    }

                    aura.LeftAt.Remove(u);
                    if (!aura.Given.Contains(u))
                    {
                        aura.Given.Add(u);
                    }

                    if (world.AbilityLevel(u, aura.Buff) == 0)
                    {
                        Give(u, aura.Buff);
                    }
                }

                for (var i = aura.Given.Count - 1; i >= 0; i--)
                {
                    var u = aura.Given[i];
                    if (near.Contains(u) && world.IsAlive(u))
                    {
                        continue;
                    }

                    if (!aura.LeftAt.TryGetValue(u, out var left))
                    {
                        aura.LeftAt[u] = time;
                    }
                    else if (left + 3.0f <= time + 0.001f)
                    {
                        aura.LeftAt.Remove(u);
                        aura.Given.RemoveAt(i);
                        if (!GivenByAnother(aura, u))
                        {
                            world.SetAbilityLevel(u, aura.Buff, 0);
                        }
                    }
                }
            }
        }

        /// <summary>Whether another aura of the same buff still gives it to the unit.</summary>
        private bool GivenByAnother(FakeAura aura, unit u)
        {
            foreach (var other in auras)
            {
                if (other != aura && other.Buff == aura.Buff && other.Given.Contains(u))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Takes a dying unit's game buffs off, as the game does before the death event; FakeWorld's death calls it.</summary>
        public void TakeAll(unit u)
        {
            if (given.TryGetValue(u, out var buffs))
            {
                foreach (var buffId in buffs)
                {
                    Take(u, buffId);
                }
            }
        }
    }
}
