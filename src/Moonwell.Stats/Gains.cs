using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The gain stats' effect at a kill. The game gives a kill's experience and bounty after the killing hit's lethal stage and before the
    /// death event, so the package notes what each hero and the killer's player have in the lethal stage and, at the start of the death event
    /// (before any reaction of Moonwell's runs, so before a map's own gift in OnKill), adds the percent of what the game gave.
    /// </summary>
    internal sealed class Gains
    {
        /// <summary>The heroes and players with an experience gain other than 0, kept current by the change reactions.</summary>
        private readonly HashSet<Actor> experience = new HashSet<Actor>();

        /// <summary>The units and players with a gold bonus other than 0, kept current by the change reactions.</summary>
        private readonly HashSet<Actor> gold = new HashSet<Actor>();

        /// <summary>The lethal hits noted and not yet settled, oldest first.</summary>
        private readonly List<Kill> kills = new List<Kill>();

        /// <summary>Whether anything has a gain to apply, so a map that uses neither stat does nothing at a kill.</summary>
        private bool Any => experience.Count > 0 || gold.Count > 0;

        /// <summary>Keeps the set of an actor whose gain stat changed; the first one asks for the death event.</summary>
        internal void Changed(Actor actor, Stat stat, float total)
        {
            var set = stat == Stat.ExperienceGain ? (actor is Hero || actor is Player ? experience : null) : stat == Stat.GoldBonus ? gold : null;
            if (set == null)
            {
                return;
            }

            if (total == 0.0f)
            {
                set.Remove(actor);
                return;
            }

            if (!Any)
            {
                Events.Listen(GameEvent.Death);
            }

            set.Add(actor);
        }

        /// <summary>The lethal stage of a hit: notes the experience of each hero with a gain and the gold of the killer's player when the killer has a bonus.</summary>
        internal void Dying(Hit hit)
        {
            if (!Any)
            {
                return;
            }

            var kill = new Kill(hit);
            if (experience.Count > 0)
            {
                var units = KnownUnits.Live.Snapshot();
                for (var i = 0; i < units.Count; i++)
                {
                    var hero = units[i] as Hero;
                    if (hero == null || hero.IsGone)
                    {
                        continue;
                    }

                    var percent = Held(Reading.Of(hero, Stat.ExperienceGain) + Reading.Of(hero.Owner, Stat.ExperienceGain));
                    if (percent != 0.0f)
                    {
                        kill.heroes.Add(hero);
                        kill.experience.Add(Runtime.World.ExperienceOf(hero.Handle));
                        kill.experiencePercent.Add(percent);
                    }
                }
            }

            var source = hit.Source;
            if (gold.Count > 0 && source != null && !source.IsGone)
            {
                var percent = Held(Reading.Of(source, Stat.GoldBonus) + Reading.Of(source.Owner, Stat.GoldBonus));
                if (percent != 0.0f)
                {
                    kill.player = source.Owner;
                    kill.gold = source.Owner.Gold;
                    kill.goldPercent = percent;
                }
            }

            if (kill.heroes.Count > 0 || kill.player != null)
            {
                kills.Add(kill);
            }
        }

        /// <summary>A hit has landed: what was noted for it is dropped, whether it killed or was spared, so nothing is left behind.</summary>
        internal void Landed(Hit hit)
        {
            for (var i = kills.Count - 1; i >= 0; i--)
            {
                if (kills[i].hit == hit)
                {
                    kills.RemoveAt(i);
                }
            }
        }

        /// <summary>A unit died, at the start of its death event: the game has given the experience and the bounty, so each noted hero and the player get their percent of it more, whole, round half up, down to all of it at -100.</summary>
        internal void Died(unit dying)
        {
            Kill? settled = null;
            for (var i = kills.Count - 1; i >= 0; i--)
            {
                if (kills[i].hit.Target.Handle == dying)
                {
                    settled = kills[i];
                    kills.RemoveAt(i);
                }
            }

            if (settled == null)
            {
                return;
            }

            for (var i = 0; i < settled.heroes.Count; i++)
            {
                var hero = settled.heroes[i];
                var extra = Extra(Runtime.World.ExperienceOf(hero.Handle) - settled.experience[i], settled.experiencePercent[i]);
                if (extra != 0 && !hero.IsGone)
                {
                    hero.AddExperience(extra);
                }
            }

            if (settled.player != null)
            {
                var extra = Extra(settled.player.Gold - settled.gold, settled.goldPercent);
                if (extra != 0)
                {
                    settled.player.Gold += extra;
                }
            }
        }

        /// <summary>A percent held to -100 or more, so that a gain is taken away at most whole.</summary>
        private static float Held(float percent)
        {
            return percent < -100.0f ? -100.0f : percent;
        }

        /// <summary>The percent of what the game gave, as a whole number, round half up.</summary>
        private static int Extra(int given, float percent)
        {
            return given <= 0 ? 0 : (int)MathF.Floor(given * percent / 100.0f + 0.5f);
        }

        /// <summary>What was noted in a lethal stage: the hit, each hero with its experience then and its percent, and the player with its gold then and its percent.</summary>
        private sealed class Kill
        {
            internal readonly Hit hit;

            internal readonly List<Hero> heroes = new List<Hero>();

            internal readonly List<int> experience = new List<int>();

            internal readonly List<float> experiencePercent = new List<float>();

            internal Player? player;

            internal int gold;

            internal float goldPercent;

            internal Kill(Hit hit)
            {
                this.hit = hit;
            }
        }
    }
}
