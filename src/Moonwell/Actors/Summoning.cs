using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Summons (decision 227): units summoned by code or by the game's own spells, linked to their summoner, with their limits, their death with the summoner and their reactions.</summary>
    internal static class Summoning
    {
        /// <summary>The list a unit that never summoned gives.</summary>
        private static readonly List<Unit> none = new List<Unit>();

        /// <summary>Summons a unit of a type for the summoner's player at a place: summoned, with timed life for seconds above 0, linked to the summoner, held by the scope, which kills it when it ends; the summoner's oldest of the type die past the limit; then the summon reactions run.</summary>
        internal static Unit Make(Unit summoner, int typeId, float x, float y, float seconds, Scope scope, int limit, bool diesWithSummoner, float facing)
        {
            if (summoner.IsGone)
            {
                throw new MoonwellException("Unit.Summon: the summoner has left the game");
            }

            var handle = Runtime.World.SpawnUnit(summoner.Owner.Handle, typeId, x, y, facing);
            if (handle == null)
            {
                throw new MoonwellException("Unit.Summon: the game made no unit of type " + Names.IdText(typeId));
            }

            Runtime.World.MakeSummoned(handle);
            if (seconds > 0.0f)
            {
                Runtime.World.ApplyTimedLife(handle, seconds);
            }

            Unit made;
            try
            {
                made = KnownUnits.TakeIn(handle, null, true)!;
            }
            catch
            {
                Runtime.World.DeleteUnit(handle);
                throw;
            }

            if (diesWithSummoner)
            {
                made.diesWithSummoner = true;
                Events.Need(Hook.OnDeath);
            }

            Link(summoner, made);
            var hold = new SummonHold(made);
            made.Attach(hold);
            scope.Adopt(hold);
            if (limit > 0)
            {
                Trim(summoner, typeId, limit);
            }

            if (made.IsAlive)
            {
                Summoned(summoner, made);
            }

            return made;
        }

        /// <summary>The game summoned a unit (its summon event): the unit is taken in as it comes into the game, linked to its summoner (for a dummy's spell, the unit the dummy cast for), and the summon reactions run. An event with no summoned unit is let go: Mirror Image puts the hero in one image's place and removes that image, and its event names none (probe, 11-09).</summary>
        internal static void FromGame(unit? summoning, unit? summonedHandle)
        {
            if (summoning == null || summonedHandle == null)
            {
                return;
            }

            var summoner = Dummies.IsDummy(summoning) ? Dummies.CastFor(summoning) : Unit.Of(summoning);
            if (summoner == null)
            {
                return;
            }

            var summoned = KnownUnits.Find(summonedHandle) ?? KnownUnits.TakeIn(summonedHandle, null, true);
            if (summoned == null || summoned.HasEnded || summoned.Summoner != null)
            {
                return;
            }

            Link(summoner, summoned);
            Summoned(summoner, summoned);
        }

        /// <summary>The living summons of a unit, oldest first: the dead and the gone are let go first.</summary>
        internal static List<Unit> LivingOf(Unit summoner)
        {
            var list = summoner.summoned;
            if (list == null)
            {
                return none;
            }

            list.RemoveAll(summon => summon.HasEnded || !summon.IsAlive);
            return list;
        }

        /// <summary>A summoner dies or leaves the game: the summons made to die with it die.</summary>
        internal static void SummonerEnds(Unit summoner)
        {
            if (summoner.summoned == null)
            {
                return;
            }

            var living = new List<Unit>(LivingOf(summoner));
            for (var i = 0; i < living.Count; i++)
            {
                if (living[i].diesWithSummoner && !living[i].ending)
                {
                    living[i].Kill();
                }
            }
        }

        /// <summary>Links a summon to its summoner.</summary>
        private static void Link(Unit summoner, Unit summon)
        {
            summon.Summoner = summoner;
            if (summoner.summoned == null)
            {
                summoner.summoned = new List<Unit>();
            }

            summoner.summoned.Add(summon);
        }

        /// <summary>Kills the summoner's oldest living summons of a type until no more than the limit are left.</summary>
        private static void Trim(Unit summoner, int typeId, int limit)
        {
            var living = LivingOf(summoner);
            var count = 0;
            for (var i = 0; i < living.Count; i++)
            {
                if (living[i].TypeId == typeId)
                {
                    count++;
                }
            }

            var oldest = new List<Unit>();
            for (var i = 0; i < living.Count && count - oldest.Count > limit; i++)
            {
                if (living[i].TypeId == typeId)
                {
                    oldest.Add(living[i]);
                }
            }

            for (var i = 0; i < oldest.Count; i++)
            {
                oldest[i].Kill();
            }
        }

        /// <summary>Runs OnSummon on the summoner, then on its components, then OnUnitSummoned on the game's components, then on the summoner's player's.</summary>
        private static void Summoned(Unit summoner, Unit summon)
        {
            var slot = summoner.Owner.Slot;
            if (!summoner.HasEnded && (summoner.Mask & Hook.OnSummon) != 0)
            {
                try
                {
                    summoner.OnSummon(summon);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(summoner) + ".OnSummon", error);
                }
            }

            var count = summoner.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = summoner.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnSummon) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnSummon(summon);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnSummon", error);
                }
            }

            summoner.EndDispatch();
            MapSummoned(Game.Instance, summoner, summon);
            var player = Player.Existing(slot);
            if (player != null)
            {
                MapSummoned(player, summoner, summon);
            }
        }

        /// <summary>Runs OnUnitSummoned on the map components of an actor, in add order.</summary>
        private static void MapSummoned(Actor actor, Unit summoner, Unit summon)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnSummon) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnUnitSummoned(summoner, summon);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnUnitSummoned", error);
                }
            }

            actor.EndDispatch();
        }
    }

    /// <summary>What a summon's scope holds of it: when the scope ends, the summon dies, as at the end of its time (agreed with Sven, 11-08); when the summon leaves the game first, it only lets go.</summary>
    internal sealed class SummonHold : Scoped
    {
        /// <summary>The summon held.</summary>
        private readonly Unit summon;

        /// <summary>Makes the hold of a summon.</summary>
        internal SummonHold(Unit summon)
        {
            this.summon = summon;
        }

        /// <summary>Kills the summon unless it is leaving the game or dead already.</summary>
        internal override void Finish()
        {
            summon.Detach(this);
            if (!summon.ending && !summon.HasEnded && summon.IsAlive)
            {
                summon.Kill();
            }
        }
    }
}
