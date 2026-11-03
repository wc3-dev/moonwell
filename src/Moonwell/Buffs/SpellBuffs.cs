using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The watch for the buffs the game puts on (decision 152): the map's buff classes whose game buff an ability lists, which
    /// the build registers. When such a game buff comes on a unit, the class's object is made and its code runs; it ends when
    /// the game's buff goes. A unit is checked as decision 163 says: at a hit, before its reactions and just after; just after
    /// a spell of a listing ability takes effect on it or around it; every 0.1 s near the holder of a listing aura and while it
    /// has a live one; and every unit once a second.
    /// </summary>
    internal static class SpellBuffs
    {
        /// <summary>How long a cast stays a match for a buff with no hit, in seconds (the builder's call).</summary>
        private const float MatchWindow = 3.0f;

        /// <summary>How far past an area a unit still counts as in it, as the game reaches a unit's edge.</summary>
        private const float Slack = 64.0f;

        /// <summary>The game buffs watched, in the order registered.</summary>
        private static List<int> watched = new List<int>();

        /// <summary>The maker of each watched class, by its game buff.</summary>
        private static Dictionary<int, Func<Buff>> makers = new Dictionary<int, Func<Buff>>();

        /// <summary>The abilities that list each watched buff, by the game buff, in the order registered.</summary>
        private static Dictionary<int, List<int>> listers = new Dictionary<int, List<int>>();

        /// <summary>The abilities that are auras and list a watched buff, in the order registered.</summary>
        private static List<int> auras = new List<int>();

        /// <summary>The live objects the watch made, checked every 0.1 s.</summary>
        private static List<Buff> live = new List<Buff>();

        /// <summary>The units that hold a listing aura, found by the sweep.</summary>
        private static List<Unit> holders = new List<Unit>();

        /// <summary>The living holders' listing auras of the instant heldAt, so a step reads each once.</summary>
        private static List<HeldAura> held = new List<HeldAura>();

        /// <summary>The instant held was read at; -1 before the first.</summary>
        private static float heldAt = -1.0f;

        /// <summary>The casts of listing abilities in the last MatchWindow seconds, oldest first.</summary>
        private static List<Recent> recent = new List<Recent>();

        /// <summary>How many of the watch's own OnApply calls are running; a hit inside one gives nothing again.</summary>
        private static int giving;

        /// <summary>Whether the runtime has begun, so a registration from then on starts the watch at once.</summary>
        private static bool begun;

        /// <summary>Whether the watch has started.</summary>
        private static bool started;

        /// <summary>Whether any buff is watched.</summary>
        internal static bool Any => watched.Count > 0;

        /// <summary>Whether a game buff is watched: a class that shows it is a spell's, which Apply refuses.</summary>
        internal static bool Watches(int buffId)
        {
            return buffId != 0 && makers.ContainsKey(buffId);
        }

        /// <summary>Who gave a buff and at what level.</summary>
        private sealed class Giver
        {
            internal Unit? Source;
            internal int Level;
            internal bool ByCast;
            internal bool ByAura;
        }

        /// <summary>A living holder's listing aura, as read in one instant.</summary>
        private sealed class HeldAura
        {
            internal Unit Holder = null!;
            internal int Ability;
            internal int Level;
            internal float Area;
            internal float X;
            internal float Y;
        }

        /// <summary>A cast of a listing ability, kept for matching.</summary>
        private sealed class Recent
        {
            internal float Time;
            internal Unit Caster = null!;
            internal int Ability;
            internal int Level;
            internal Unit? Target;
            internal float X;
            internal float Y;
            internal float Area;
        }

        /// <summary>Registers a watched class by its game buff.</summary>
        internal static void Watch(int buffId, Func<Buff> make)
        {
            if (makers.ContainsKey(buffId))
            {
                throw new MoonwellException("ObjectTypes.WatchedBuff: buff " + buffId + " is registered twice");
            }

            makers[buffId] = make;
            watched.Add(buffId);
            listers[buffId] = new List<int>();
            if (begun)
            {
                Start();
            }
        }

        /// <summary>Registers an ability that lists a watched buff, and whether it is an aura.</summary>
        internal static void List(int abilityId, int buffId, bool aura)
        {
            if (!listers.TryGetValue(buffId, out var abilities))
            {
                throw new MoonwellException("ObjectTypes.ListsBuff: buff " + buffId + " is not watched");
            }

            if (!abilities.Contains(abilityId))
            {
                abilities.Add(abilityId);
            }

            if (aura && !auras.Contains(abilityId))
            {
                auras.Add(abilityId);
            }
        }

        /// <summary>Runs when the runtime has taken in the world: the watch starts if anything is watched.</summary>
        internal static void Begin()
        {
            begun = true;
            if (Any)
            {
                Start();
            }
        }

        /// <summary>Starts the watch once: every unit is taken in, hits, casts and deaths are listened for, and the checks run.</summary>
        private static void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            KnownUnits.TakeInEveryUnit();
            Events.Need(Hook.OnDamaged | Hook.OnCast | Hook.OnDeath);
            Game.Instance.OwnEvery(0.1f, Step);
            Game.Instance.OwnEvery(1.0f, Sweep);
        }

        /// <summary>
        /// A hit, before its reactions run: its target is checked now, so a reaction sees a buff that came on just before, made
        /// as its source's when the source has a listing ability. Only a hit that gives (a hit of 0 the game starts, as a spell's
        /// and an attack's buff comes, seen 11-03) gives a buff that is on again, and is checked once more just after, when the
        /// buff it puts on is on. Nothing gives again while a buff's own OnApply runs.
        /// </summary>
        internal static void Hit(Unit target, Unit? source, bool gives)
        {
            if (!started || target.IsGone)
            {
                return;
            }

            Check(target, buffId => HitGiver(source, buffId), gives && giving == 0);
            if (gives)
            {
                Game.Instance.OwnAfter(0.0f, () => Check(target, buffId => HitGiver(source, buffId), false));
            }
        }

        /// <summary>A spell takes effect: a cast of a listing ability is kept for matching, and just after, its target, or the units in its area around its point (around the caster for a spell with no target), are checked.</summary>
        internal static void Cast(Cast cast)
        {
            if (!started || !Lists(cast.Ability.Id))
            {
                return;
            }

            // A spell with no target gives the point (0, 0), and Thunder Clap a stray target too (seen 11-03).
            var caster = cast.Caster;
            var noPoint = cast.X == 0.0f && cast.Y == 0.0f
                && (cast.Target == null || Runtime.World.UnitX(cast.Target.Handle) != 0.0f || Runtime.World.UnitY(cast.Target.Handle) != 0.0f);
            var kept = new Recent
            {
                Time = Runtime.World.Now(),
                Caster = caster,
                Ability = cast.Ability.Id,
                Level = cast.Level,
                Target = noPoint ? null : cast.Target,
                X = noPoint ? Runtime.World.UnitX(caster.Handle) : cast.X,
                Y = noPoint ? Runtime.World.UnitY(caster.Handle) : cast.Y,
                Area = Runtime.World.AbilityArea(caster.Handle, cast.Ability.Id),
            };
            recent.Add(kept);
            Game.Instance.OwnAfter(0.0f, () => AfterCast(kept));
        }

        /// <summary>Checks a cast's target, or for a spell with no target its caster and the units in its area around it, as given by that cast.</summary>
        private static void AfterCast(Recent cast)
        {
            Func<int, Giver?> giver = buffId => Listed(cast.Ability, buffId) ? new Giver { Source = cast.Caster, Level = cast.Level, ByCast = true } : null;
            if (cast.Target != null)
            {
                Check(cast.Target, giver, true);
                return;
            }

            Check(cast.Caster, giver, true);
            if (cast.Area <= 0.0f)
            {
                return;
            }

            var found = Runtime.World.UnitsInRange(cast.X, cast.Y, cast.Area + Slack);
            for (var i = 0; i < found.Count; i++)
            {
                var unit = KnownUnits.Find(found[i]);
                if (unit != null)
                {
                    Check(unit, giver, true);
                }
            }
        }

        /// <summary>Every 0.1 s: the live objects end when their game buff has gone, an aura's follow the strongest holder in reach, and the units near each holder of a listing aura are checked.</summary>
        private static void Step()
        {
            for (var i = live.Count - 1; i >= 0; i--)
            {
                // A buff's own code may end others while this runs, so the list can be shorter than the index.
                if (i >= live.Count)
                {
                    continue;
                }

                var buff = live[i];
                if (buff.HasEnded)
                {
                    live.RemoveAt(i);
                    continue;
                }

                var unit = buff.Unit;
                if (unit.IsGone || Runtime.World.AbilityLevel(unit.Handle, buff.gameBuff) <= 0)
                {
                    live.RemoveAt(i);
                    buff.End();
                    continue;
                }

                if (buff.byAura)
                {
                    var giver = AuraGiver(unit, buff.gameBuff, buff.Source);
                    if (giver != null && (giver.Source != buff.Source || giver.Level != buff.Level))
                    {
                        buff.GiveAgain(giver.Source, giver.Level);
                    }
                }
            }

            var all = Held();
            for (var a = 0; a < all.Count; a++)
            {
                var aura = all[a];
                var found = Runtime.World.UnitsInRange(aura.X, aura.Y, aura.Area + Slack);
                for (var i = 0; i < found.Count; i++)
                {
                    var unit = KnownUnits.Find(found[i]);
                    if (unit != null)
                    {
                        Check(unit, buffId => AuraGiver(unit, buffId, null), false);
                    }
                }
            }
        }
        /// <summary>Every second: casts too old to match are dropped, every unit is checked, and the holders of listing auras are found.</summary>
        private static void Sweep()
        {
            var now = Runtime.World.Now();
            while (recent.Count > 0 && now - recent[0].Time > MatchWindow)
            {
                recent.RemoveAt(0);
            }

            // The holders first, so a buff made in this sweep in reach of one is that holder's.
            var units = KnownUnits.Live;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.HasEnded || unit.IsGone)
                {
                    continue;
                }

                var holds = HoldsAura(unit);
                if (holds && !holders.Contains(unit))
                {
                    holders.Add(unit);
                    heldAt = -1.0f;
                }
                else if (!holds && holders.Remove(unit))
                {
                    heldAt = -1.0f;
                }
            }

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.HasEnded && !unit.IsGone)
                {
                    Check(unit, null, false);
                }
            }
        }

        /// <summary>
        /// Checks a unit's watched game buffs: one on with no object gets one, given by the giver for it or else by the best
        /// match; one on with an object is given again when the giver says so and again is true; an object whose game buff
        /// has gone ends.
        /// </summary>
        private static void Check(Unit unit, Func<int, Giver?>? given, bool again)
        {
            if (unit.HasEnded || unit.IsGone)
            {
                return;
            }

            List<Buff>? objects = null;
            for (var i = 0; i < watched.Count; i++)
            {
                var buffId = watched[i];
                var on = Runtime.World.AbilityLevel(unit.Handle, buffId) > 0;
                objects ??= unit.GetAll<Buff>();
                var existing = ObjectOf(objects, buffId);
                if (on && existing == null)
                {
                    Make(unit, buffId, (given == null ? null : given(buffId)) ?? Match(unit, buffId));
                }
                else if (on && existing != null && again && given != null)
                {
                    var giver = given(buffId);
                    if (giver != null && !SameCast(existing, giver) && !(existing.givenBy == giver.Source && existing.givenAt == Runtime.World.Now()))
                    {
                        GiveAgain(existing, giver);
                    }
                }
                else if (!on && existing != null)
                {
                    live.Remove(existing);
                    existing.End();
                }
            }
        }

        /// <summary>Whether a hit is the hit of the cast that gave the buff again just before (a missile spell cast again on the unit): that cast already gave it.</summary>
        private static bool SameCast(Buff buff, Giver giver)
        {
            return !giver.ByCast && buff.castBy != null && buff.castBy == giver.Source && Runtime.World.Now() - buff.castAt <= MatchWindow;
        }

        /// <summary>Makes the object of a watched class for a game buff on a unit, with its giver, and starts it.</summary>
        private static void Make(Unit unit, int buffId, Giver? giver)
        {
            var buff = makers[buffId]();
            buff.gameBuff = buffId;
            buff.Source = giver?.Source;
            buff.Level = giver == null ? 0 : giver.Level;
            buff.byAura = giver == null ? AuraListed(buffId) : giver.ByAura;
            buff.givenBy = giver?.Source;
            buff.givenAt = Runtime.World.Now();
            unit.Add(buff);
            if (buff.HasEnded)
            {
                return;
            }

            live.Add(buff);
            giving++;
            try
            {
                buff.BeginSpell();
            }
            finally
            {
                giving--;
            }
        }

        /// <summary>Gives a live object again, from a giver.</summary>
        private static void GiveAgain(Buff buff, Giver giver)
        {
            buff.byAura = giver.ByAura;
            buff.givenBy = giver.Source;
            buff.givenAt = Runtime.World.Now();
            if (giver.ByCast)
            {
                buff.castBy = giver.Source;
                buff.castAt = Runtime.World.Now();
            }

            giving++;
            try
            {
                buff.GiveAgain(giver.Source, giver.Level);
            }
            finally
            {
                giving--;
            }
        }

        /// <summary>The live object for a game buff among a unit's buffs, or null.</summary>
        private static Buff? ObjectOf(List<Buff> found, int buffId)
        {
            for (var i = 0; i < found.Count; i++)
            {
                if (found[i].gameBuff == buffId && !found[i].HasEnded)
                {
                    return found[i];
                }
            }

            return null;
        }

        /// <summary>The giver of a hit: its source, when it has an ability that lists the buff, at that ability's level.</summary>
        private static Giver? HitGiver(Unit? source, int buffId)
        {
            if (source == null || source.IsGone)
            {
                return null;
            }

            var abilities = listers[buffId];
            for (var i = 0; i < abilities.Count; i++)
            {
                var level = Runtime.World.AbilityLevel(source.Handle, abilities[i]);
                if (level > 0)
                {
                    return new Giver { Source = source, Level = level };
                }
            }

            return null;
        }

        /// <summary>The best match for a buff with no giver of its own: the latest cast in the window aimed at the unit or reaching it, else the strongest aura holder in reach; null when nothing matches.</summary>
        private static Giver? Match(Unit unit, int buffId)
        {
            var now = Runtime.World.Now();
            for (var i = recent.Count - 1; i >= 0; i--)
            {
                var cast = recent[i];
                if (now - cast.Time > MatchWindow || !Listed(cast.Ability, buffId))
                {
                    continue;
                }

                if (cast.Target == unit || (cast.Target == null && (cast.Caster == unit || Within(unit, cast.X, cast.Y, cast.Area))))
                {
                    return new Giver { Source = cast.Caster, Level = cast.Level, ByCast = true };
                }
            }

            return AuraGiver(unit, buffId, null);
        }

        /// <summary>The living holders' listing auras, read once per instant: each holder's level, area and place.</summary>
        private static List<HeldAura> Held()
        {
            var now = Runtime.World.Now();
            if (now == heldAt)
            {
                return held;
            }

            heldAt = now;
            held = new List<HeldAura>();
            for (var h = holders.Count - 1; h >= 0; h--)
            {
                var holder = holders[h];
                if (holder.HasEnded || holder.IsGone)
                {
                    holders.RemoveAt(h);
                }
            }

            for (var h = 0; h < holders.Count; h++)
            {
                var holder = holders[h];
                if (!holder.IsAlive)
                {
                    continue;
                }

                for (var a = 0; a < auras.Count; a++)
                {
                    var level = Runtime.World.AbilityLevel(holder.Handle, auras[a]);
                    if (level > 0)
                    {
                        held.Add(new HeldAura
                        {
                            Holder = holder,
                            Ability = auras[a],
                            Level = level,
                            Area = Runtime.World.AbilityArea(holder.Handle, auras[a]),
                            X = Runtime.World.UnitX(holder.Handle),
                            Y = Runtime.World.UnitY(holder.Handle),
                        });
                    }
                }
            }

            return held;
        }

        /// <summary>The strongest living holder in reach of an aura that lists the buff; of equals, the one to keep if it is one of them, else the nearest. Null when none is.</summary>
        private static Giver? AuraGiver(Unit unit, int buffId, Unit? keep)
        {
            Giver? best = null;
            var bestDistance = 0.0f;
            var x = Runtime.World.UnitX(unit.Handle);
            var y = Runtime.World.UnitY(unit.Handle);
            var all = Held();
            for (var i = 0; i < all.Count; i++)
            {
                var aura = all[i];
                if (!Listed(aura.Ability, buffId))
                {
                    continue;
                }

                var dx = aura.X - x;
                var dy = aura.Y - y;
                var distance = dx * dx + dy * dy;
                if (distance > (aura.Area + Slack) * (aura.Area + Slack))
                {
                    continue;
                }

                var better = best == null || aura.Level > best.Level
                    || (aura.Level == best.Level && best.Source != keep && (aura.Holder == keep || distance < bestDistance));
                if (better)
                {
                    best = new Giver { Source = aura.Holder, Level = aura.Level, ByAura = true };
                    bestDistance = distance;
                }
            }

            return best;
        }
        /// <summary>Whether a unit holds a listing aura.</summary>
        private static bool HoldsAura(Unit unit)
        {
            for (var a = 0; a < auras.Count; a++)
            {
                if (Runtime.World.AbilityLevel(unit.Handle, auras[a]) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a unit is within an area, with the slack, of a place.</summary>
        private static bool Within(Unit unit, float x, float y, float area)
        {
            var dx = Runtime.World.UnitX(unit.Handle) - x;
            var dy = Runtime.World.UnitY(unit.Handle) - y;
            return area > 0.0f && dx * dx + dy * dy <= (area + Slack) * (area + Slack);
        }

        /// <summary>Whether an ability lists any watched buff.</summary>
        private static bool Lists(int abilityId)
        {
            for (var i = 0; i < watched.Count; i++)
            {
                if (listers[watched[i]].Contains(abilityId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether an aura lists a watched buff, so a buff made with no giver follows the strongest holder once one is found.</summary>
        private static bool AuraListed(int buffId)
        {
            for (var a = 0; a < auras.Count; a++)
            {
                if (Listed(auras[a], buffId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether an ability lists a watched buff.</summary>
        private static bool Listed(int abilityId, int buffId)
        {
            return listers.TryGetValue(buffId, out var abilities) && abilities.Contains(abilityId);
        }

        /// <summary>Forgets every registration and what the watch holds; Runtime.Reset calls it through ObjectTypes.</summary>
        internal static void Reset()
        {
            watched = new List<int>();
            makers = new Dictionary<int, Func<Buff>>();
            listers = new Dictionary<int, List<int>>();
            auras = new List<int>();
            live = new List<Buff>();
            holders = new List<Unit>();
            recent = new List<Recent>();
            held = new List<HeldAura>();
            heldAt = -1.0f;
            giving = 0;
            begun = false;
            started = false;
        }
    }
}
