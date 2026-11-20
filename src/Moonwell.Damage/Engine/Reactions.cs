using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Runs a stage's reactions on everything a hit or a heal involves, in decision 12's order: the source and its components, the target and its components, the game's components, the target's player's, then the source's player's.</summary>
    internal static class Reactions
    {
        /// <summary>Where each interface's methods start among the slots: a method's slot is this plus its stage.</summary>
        private const int SourceSlots = 0, TargetSlots = 5, WatchSlots = 10;

        /// <summary>Where the heal interfaces' methods start: two to an interface, the first stage then the second. The slots end at 20, within an int's 32 bits.</summary>
        private const int HealSourceSlots = 15, HealTargetSlots = 17, HealWatchSlots = 19;

        /// <summary>The reaction methods by slot, five to an interface in stage order (before armor, after armor, lethal, landed, burst); the target has no burst; then two to each heal interface. A slot's bit is 1 shifted by the slot, and a class's mask holds the bits of the methods it writes.</summary>
        private static readonly string?[] names =
        {
            "OnDealing", "OnDealt", "OnKilling", "OnLanded", "OnBurst",
            "OnTaking", "OnTaken", "OnDying", "OnHurt", null,
            "OnUnitTaking", "OnUnitTaken", "OnUnitDying", "OnUnitHurt", "OnUnitBurst",
            "OnHealing", "OnHealed", "OnReceiving", "OnReceived", "OnUnitReceiving", "OnUnitReceived",
        };

        /// <summary>The mask of each class seen, keyed by the class.</summary>
        private static readonly Dictionary<object, int> masks = new Dictionary<object, int>();

        /// <summary>The bits of every class seen, together: a stage none of them reacts to is skipped.</summary>
        private static int seen;

        /// <summary>The chain place of each hit or heal whose reactions are running, innermost last; a hit or heal made while they run belongs to the chain of the last.</summary>
        private static readonly List<int> running = new List<int>();

        /// <summary>Forgets every class's mask and the running reactions; Install calls it.</summary>
        internal static void Reset()
        {
            masks.Clear();
            seen = 0;
            running.Clear();
        }

        /// <summary>Works out the reactions of a class the runtime sees for the first time, so the stages it reacts to are run.</summary>
        internal static void Learn(object obj)
        {
            MaskOf(obj);
        }

        /// <summary>Whether any class seen reacts to a stage.</summary>
        internal static bool Wants(Stage stage)
        {
            var bits = (1 << (SourceSlots + (int)stage)) | (1 << (TargetSlots + (int)stage)) | (1 << (WatchSlots + (int)stage));
            return (seen & bits) != 0;
        }

        /// <summary>Whether any class seen reacts to a stage of a heal, 0 for the first and 1 for the second.</summary>
        private static bool WantsHeal(int stage)
        {
            var bits = (1 << (HealSourceSlots + stage)) | (1 << (HealTargetSlots + stage)) | (1 << (HealWatchSlots + stage));
            return (seen & bits) != 0;
        }

        /// <summary>The chain place of a hit or heal made now: 0, or one more than the one whose reactions are running.</summary>
        internal static int NextChain()
        {
            return running.Count == 0 ? 0 : running[running.Count - 1] + 1;
        }

        /// <summary>Runs core's OnDamaged family on a hit, as reactions to it, so damage they deal continues its chain.</summary>
        internal static void RunCore(Hit hit)
        {
            running.Add(hit.Chain);
            try
            {
                Inbound.Dispatch(hit, hit.Original == 0.0f && !hit.IsCode);
            }
            finally
            {
                running.RemoveAt(running.Count - 1);
            }
        }

        /// <summary>Runs a stage's reactions to a hit; a quiet hit has none, and a missed hit has only those of the before-armor stage it was missed in.</summary>
        internal static void Run(Hit hit, Stage stage)
        {
            if (hit.Quiet || !Wants(stage) || (hit.Missed && stage != Stage.BeforeArmor))
            {
                return;
            }

            Spread(hit.Source, hit.Target, hit, null, SourceSlots + (int)stage, TargetSlots + (int)stage, WatchSlots + (int)stage);
        }

        /// <summary>Runs a stage's reactions to a heal, 0 for the first stage and 1 for the second.</summary>
        internal static void RunHeal(Heal heal, int stage)
        {
            if (WantsHeal(stage))
            {
                Spread(heal.Source, heal.Target, null, heal, HealSourceSlots + stage, HealTargetSlots + stage, HealWatchSlots + stage);
            }
        }

        /// <summary>Runs the reactions of a source slot, a target slot and a watch slot in decision 12's order, for a hit or a heal.</summary>
        private static void Spread(Unit? source, Unit target, Hit? hit, Heal? heal, int sourceSlot, int targetSlot, int watchSlot)
        {
            var targetPlayer = target.Owner.Slot;
            var sourcePlayer = source == null ? -1 : source.Owner.Slot;
            if (source != null)
            {
                RunUnit(source, hit, null, heal, sourceSlot);
            }

            RunUnit(target, hit, null, heal, targetSlot);
            RunMap(Game.Instance, hit, null, heal, watchSlot);
            var first = Player.Existing(targetPlayer);
            if (first != null)
            {
                RunMap(first, hit, null, heal, watchSlot);
            }

            if (source != null && sourcePlayer != targetPlayer)
            {
                var second = Player.Existing(sourcePlayer);
                if (second != null)
                {
                    RunMap(second, hit, null, heal, watchSlot);
                }
            }
        }

        /// <summary>Runs the burst reactions: the source and its components, the game's components, then the source's player's.</summary>
        internal static void RunBurst(Burst burst)
        {
            var slot = burst.Source.Owner.Slot;
            RunUnit(burst.Source, null, burst, null, SourceSlots + (int)Stage.Burst);
            RunMap(Game.Instance, null, burst, null, WatchSlots + (int)Stage.Burst);
            var player = Player.Existing(slot);
            if (player != null)
            {
                RunMap(player, null, burst, null, WatchSlots + (int)Stage.Burst);
            }
        }

        /// <summary>Runs a unit's reaction in a slot, then its components', in add order; a unit or component that has ended is skipped.</summary>
        private static void RunUnit(Unit unit, Hit? hit, Burst? burst, Heal? heal, int slot)
        {
            var bit = 1 << slot;
            if (!unit.HasEnded && (MaskOf(unit) & bit) != 0 && !Spared(hit))
            {
                Call(unit, hit, burst, heal, slot);
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component != null && !component.HasEnded && (MaskOf(component) & bit) != 0 && !Spared(hit))
                {
                    Call(component, hit, burst, heal, slot);
                }
            }

            unit.EndDispatch();
        }

        /// <summary>Runs the map-wide reaction in a slot on an actor's components, in add order.</summary>
        private static void RunMap(Actor actor, Hit? hit, Burst? burst, Heal? heal, int slot)
        {
            var bit = 1 << slot;
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component != null && !component.HasEnded && (MaskOf(component) & bit) != 0 && !Spared(hit))
                {
                    Call(component, hit, burst, heal, slot);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Whether a hit in its lethal stage no longer kills, as an earlier reaction lowered it or healed the target: the later lethal reactions do not run. The life is read each time, as an earlier reaction may have dealt a hit.</summary>
        private static bool Spared(Hit? hit)
        {
            if (hit == null || hit.Stage != Stage.Lethal)
            {
                return false;
            }

            hit.TargetLife = Pipeline.World.Life(hit.Target.Handle);
            return !hit.Kills;
        }

        /// <summary>Calls the reaction in a slot; a failing reaction is reported.</summary>
        private static void Call(Scope obj, Hit? hit, Burst? burst, Heal? heal, int slot)
        {
            running.Add(heal != null ? heal.Chain : burst != null ? burst.Hits[0].Chain : hit!.Chain);
            try
            {
                switch (slot)
                {
                    case SourceSlots + (int)Stage.BeforeArmor: ((IDamageSource)obj).OnDealing(hit!); break;
                    case SourceSlots + (int)Stage.AfterArmor: ((IDamageSource)obj).OnDealt(hit!); break;
                    case SourceSlots + (int)Stage.Lethal: ((IDamageSource)obj).OnKilling(hit!); break;
                    case SourceSlots + (int)Stage.Landed: ((IDamageSource)obj).OnLanded(hit!); break;
                    case SourceSlots + (int)Stage.Burst: ((IDamageSource)obj).OnBurst(burst!); break;
                    case TargetSlots + (int)Stage.BeforeArmor: ((IDamageTarget)obj).OnTaking(hit!); break;
                    case TargetSlots + (int)Stage.AfterArmor: ((IDamageTarget)obj).OnTaken(hit!); break;
                    case TargetSlots + (int)Stage.Lethal: ((IDamageTarget)obj).OnDying(hit!); break;
                    case TargetSlots + (int)Stage.Landed: ((IDamageTarget)obj).OnHurt(hit!); break;
                    case WatchSlots + (int)Stage.BeforeArmor: ((IDamageWatch)obj).OnUnitTaking(hit!); break;
                    case WatchSlots + (int)Stage.AfterArmor: ((IDamageWatch)obj).OnUnitTaken(hit!); break;
                    case WatchSlots + (int)Stage.Lethal: ((IDamageWatch)obj).OnUnitDying(hit!); break;
                    case WatchSlots + (int)Stage.Landed: ((IDamageWatch)obj).OnUnitHurt(hit!); break;
                    case WatchSlots + (int)Stage.Burst: ((IDamageWatch)obj).OnUnitBurst(burst!); break;
                    case HealSourceSlots: ((IHealSource)obj).OnHealing(heal!); break;
                    case HealSourceSlots + 1: ((IHealSource)obj).OnHealed(heal!); break;
                    case HealTargetSlots: ((IHealTarget)obj).OnReceiving(heal!); break;
                    case HealTargetSlots + 1: ((IHealTarget)obj).OnReceived(heal!); break;
                    case HealWatchSlots: ((IHealWatch)obj).OnUnitReceiving(heal!); break;
                    case HealWatchSlots + 1: ((IHealWatch)obj).OnUnitReceived(heal!); break;
                }
            }
            catch (Exception error)
            {
                Errors.Report(obj, "." + names[slot], error);
            }

            running.RemoveAt(running.Count - 1);
        }

        /// <summary>The bits of the reaction methods an object's class writes, worked out once per class. Only methods the class writes count: in Lua an interface's empty defaults are not there.</summary>
        private static int MaskOf(object obj)
        {
            var key = Names.ClassOf(obj);
            if (masks.TryGetValue(key, out var mask))
            {
                return mask;
            }

            mask = 0;
            for (var slot = 0; slot < names.Length; slot++)
            {
                var name = names[slot];
                if (name != null && Implements(obj, slot) && Hooks.Overrides(obj, name, typeof(object)))
                {
                    mask |= 1 << slot;
                }
            }

            masks[key] = mask;
            seen |= mask;
            return mask;
        }

        /// <summary>Whether an object implements the interface a slot belongs to.</summary>
        private static bool Implements(object obj, int slot)
        {
            if (slot < TargetSlots)
            {
                return obj is IDamageSource;
            }

            if (slot < WatchSlots)
            {
                return obj is IDamageTarget;
            }

            if (slot < HealSourceSlots)
            {
                return obj is IDamageWatch;
            }

            if (slot < HealTargetSlots)
            {
                return obj is IHealSource;
            }

            return slot < HealWatchSlots ? obj is IHealTarget : obj is IHealWatch;
        }
    }
}
