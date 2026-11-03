using System;

namespace Moonwell
{
    /// <summary>The events the world delivers to the runtime, and what the runtime does with each.</summary>
    internal static partial class Inbound
    {
        /// <summary>The damage Unit.Damage is dealing, whose event keeps the damage it makes; null outside the call.</summary>
        private static Dealing? dealing;
        /// <summary>Runs OnGameStart on the game's components, then on each existing player's, in slot order.</summary>
        internal static void GameStarted()
        {
            StartComponents(Game.Instance);
            for (var slot = 0; slot < Player.MaxSlots; slot++)
            {
                var existing = Player.Existing(slot);
                if (existing != null)
                {
                    StartComponents(existing);
                }
            }
        }

        /// <summary>A unit enters the map: it is taken in when its type is registered, when a component wants to hear of entries, or once the interactions package tracks units (KnownUnits.EveryUnit). A hero is also taken in while a component wants OnHeroLevelUp, so its level is known before its first level-up.</summary>
        internal static void Entered(unit handle)
        {
            if (KnownUnits.Find(handle) != null)
            {
                return;
            }

            if (KnownUnits.EveryUnit || ObjectTypes.UnitMaker(Runtime.World.UnitTypeId(handle)) != null || Events.Wants(Hook.OnUnitCreated)
                || (Events.Wants(Hook.OnHeroLevelUp) && Runtime.World.IsHero(handle)))
            {
                KnownUnits.TakeIn(handle, null, true);
            }
        }

        /// <summary>A unit picks up an item: the item becomes the unit's player's, as the game leaves its owner alone (seen 11-02), every item, so one taken in later reads right; a new carry starts, unless the item went into the 3.0 bag (decision 166); then the item's OnPickedUp and components, then the unit's OnItemPickedUp and components.</summary>
        internal static void PickedUp(unit u, item i)
        {
            var carrierPlayer = Runtime.World.OwnerOf(u);
            if (carrierPlayer != null)
            {
                Runtime.World.SetItemOwner(i, carrierPlayer);
                var known = KnownItems.Find(i);
                if (known != null)
                {
                    known.OwningPlayer = carrierPlayer;
                }
            }

            if (KnownUnits.Find(u) == null && KnownItems.Find(i) == null && ObjectTypes.ItemMaker(Runtime.World.ItemTypeId(i)) == null)
            {
                return;
            }

            var by = Unit.Of(u);
            var picked = Item.Of(i);
            if (by == null || picked == null)
            {
                return;
            }

            picked.CarrierUnit = by;
            if (picked.carry != null)
            {
                picked.carry.End();
                picked.carry = null;
            }

            if (!Runtime.World.IsInBag(i))
            {
                var carry = new Carrying();
                by.Adopt(carry);
                picked.carry = carry;
            }

            ItemPickedUp(picked, by);
            UnitItemPickedUp(by, picked);
        }

        /// <summary>A unit drops an item: the item's OnDropped and components, then the unit's OnItemDropped and components, then the item's carry ends.</summary>
        internal static void Dropped(unit u, item i)
        {
            if (KnownUnits.Find(u) == null && KnownItems.Find(i) == null && ObjectTypes.ItemMaker(Runtime.World.ItemTypeId(i)) == null)
            {
                return;
            }

            var by = Unit.Of(u);
            var dropped = Item.Of(i);
            if (by == null || dropped == null)
            {
                return;
            }

            ItemDropped(dropped, by);
            UnitItemDropped(by, dropped);
            var carry = dropped.carry;
            if (carry != null)
            {
                dropped.carry = null;
                carry.End();
            }
        }

        /// <summary>A destructable dies: its type's OnDeath, then its components' OnDeath; one that is ending reacts to none, since the game fires the death event inside RemoveDestructable.</summary>
        internal static void DestructableDied(destructable d)
        {
            var dead = Destructable.Of(d);
            if (dead == null || dead.ending)
            {
                return;
            }

            if (!dead.HasEnded && (dead.Mask & Hook.OnDeath) != 0)
            {
                try
                {
                    dead.OnDeath();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(dead) + ".OnDeath", error);
                }
            }

            var count = dead.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = dead.ComponentAt(i) as DestructableComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnDeath) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDeath();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnDeath", error);
                }
            }

            dead.EndDispatch();
        }

        /// <summary>A unit takes damage: the source's type and components deal it, the target's take it, then the map's and the players' reactions; returns the amount after them.</summary>
        internal static float Damaged(unit target, unit? source, float amount, bool isAttack)
        {
            if (KnownUnits.Find(target) == null && (source == null || KnownUnits.Find(source) == null) && !Events.Wants(Hook.OnUnitDamaged))
            {
                return amount;
            }

            var hit = Unit.Of(target);
            if (hit == null)
            {
                return amount;
            }

            Unit? from = source == null ? null : Unit.Of(source);

            // The game's event says no attack for damage from code even when dealt as one, so Unit.Damage's own flag counts.
            var deal = dealing != null && dealing.Damage == null && dealing.Target == target && dealing.Source == source ? dealing : null;
            var damage = new Damage(hit, from, amount, deal != null ? deal.IsAttack : isAttack);
            if (deal != null)
            {
                deal.Damage = damage;
                damage.Tag = deal.Tag;
            }

            Dispatch(damage);
            return damage.Amount;
        }

        /// <summary>Deals damage from code through the world, and returns the damage the reactions saw; one made here when none ran, null when the game dealt nothing.</summary>
        internal static Damage? Deal(Unit source, Unit target, float amount, DamageKind kind, AttackType attackType, bool isAttack, bool ranged, WeaponSound sound, object? tag)
        {
            // The game answers true for a dead target, though it deals nothing and fires no event (seen 10-31).
            var alive = target.IsAlive;
            var previous = dealing;
            var deal = new Dealing(source.Handle, target.Handle, isAttack, tag);
            dealing = deal;
            bool dealt;
            try
            {
                dealt = Runtime.World.DealDamage(source.Handle, target.Handle, amount, isAttack, ranged, TypeCodes.AttackCode(attackType), (int)kind, (int)sound);
            }
            finally
            {
                dealing = previous;
            }

            if (!dealt || !alive)
            {
                return null;
            }

            var damage = deal.Damage ?? new Damage(target, source, amount, isAttack);
            damage.Tag = tag;
            return damage;
        }

        /// <summary>A damage Unit.Damage deals: its handles, and the damage its event made.</summary>
        private sealed class Dealing
        {
            internal Dealing(unit source, unit target, bool isAttack, object? tag)
            {
                Source = source;
                Target = target;
                IsAttack = isAttack;
                Tag = tag;
            }

            internal bool IsAttack { get; }

            internal object? Tag { get; }

            internal unit Source { get; }

            internal unit Target { get; }

            internal Damage? Damage { get; set; }
        }

        /// <summary>Runs a damage's reactions: the watch checks the target first (decision 163), then the source's type and components deal it, the target's take it, then the map's and the players' reactions; a damage package calls it with its own kind of damage.</summary>
        internal static void Dispatch(Damage damage)
        {
            var hit = damage.Target;
            var from = damage.Source;
            SpellBuffs.Hit(hit, from);
            var hitSlot = hit.Owner.Slot;
            var fromSlot = from == null ? -1 : from.Owner.Slot;
            if (from != null)
            {
                DealtDamage(from, damage);
            }

            TookDamage(hit, damage);
            MapDamaged(Game.Instance, damage);
            var first = Player.Existing(hitSlot);
            if (first != null)
            {
                MapDamaged(first, damage);
            }

            if (from != null)
            {
                var second = Player.Existing(fromSlot);
                if (second != null && fromSlot != hitSlot)
                {
                    MapDamaged(second, damage);
                }
            }
        }

        /// <summary>A unit dies: its type's OnDeath and components, then the killer's OnKill and components, then the map's and the players' reactions, then the buffs a death ends end, of those on the unit as it died; a killer equal to the dying unit counts as none.</summary>
        internal static void Died(unit dying, unit? killer)
        {
            unit? slain = killer == dying ? null : killer;
            if (KnownUnits.Find(dying) == null && (slain == null || KnownUnits.Find(slain) == null) && !Events.Wants(Hook.OnUnitDied))
            {
                return;
            }

            var dead = Unit.Of(dying);
            if (dead == null)
            {
                return;
            }

            // The buffs the death ends are those on the unit now, which see it; one its reactions apply stays.
            var buffs = dead.GetAll<Buff>();
            Unit? slayer = slain == null ? null : Unit.Of(slain);
            var deadSlot = dead.Owner.Slot;
            var slayerSlot = slayer == null ? -1 : slayer.Owner.Slot;
            Dying(dead, slayer);
            if (slayer != null)
            {
                Killing(slayer, dead);
            }

            MapDied(Game.Instance, dead, slayer);
            var first = Player.Existing(deadSlot);
            if (first != null)
            {
                MapDied(first, dead, slayer);
            }

            if (slayer != null)
            {
                var second = Player.Existing(slayerSlot);
                if (second != null && slayerSlot != deadSlot)
                {
                    MapDied(second, dead, slayer);
                }
            }

            Buff.EndOnDeath(buffs);
        }

        /// <summary>A unit's spell takes effect: its previous cast ends, the ability and the caster see the new cast, then the map's and the caster's player's reactions run.</summary>
        internal static void SpellEffect(unit caster, int abilityId, unit? target, float x, float y)
        {
            if (KnownUnits.Find(caster) == null && ObjectTypes.AbilityMaker(abilityId) == null && !Events.Wants(Hook.OnUnitCast))
            {
                return;
            }

            var by = Unit.Of(caster);
            if (by == null)
            {
                return;
            }

            SpellOrderDone(by);
            var casterSlot = by.Owner.Slot;
            var running = by.ActiveCast;
            if (running != null)
            {
                running.End();
            }

            var ability = by.AbilityOf(abilityId);
            var cast = new Cast(ability, by, target == null ? null : Unit.Of(target), x, y, Runtime.World.AbilityLevel(caster, abilityId));
            ability.Adopt(cast);
            by.ActiveCast = cast;
            SpellBuffs.Cast(cast);
            AbilityCast(ability, cast);
            CasterCast(by, cast);
            MapCast(Game.Instance, cast);
            var first = Player.Existing(casterSlot);
            if (first != null)
            {
                MapCast(first, cast);
            }
        }

        /// <summary>A unit's spell ends: the cast it is making ends, when that cast is live and is for this ability.</summary>
        internal static void SpellEnded(unit caster, int abilityId)
        {
            var ended = KnownUnits.Find(caster);
            if (ended == null)
            {
                return;
            }

            var cast = ended.ActiveCast;
            if (cast != null && !cast.HasEnded && cast.Ability.Id == abilityId)
            {
                cast.End();
            }
        }

        /// <summary>Runs OnPickedUp on an item, then on its components, in add order.</summary>
        private static void ItemPickedUp(Item item, Unit by)
        {
            if (!item.HasEnded && (item.Mask & Hook.OnPickedUp) != 0)
            {
                try
                {
                    item.OnPickedUp(by);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(item) + ".OnPickedUp", error);
                }
            }

            var count = item.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = item.ComponentAt(i) as ItemComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnPickedUp) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnPickedUp(by);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnPickedUp", error);
                }
            }

            item.EndDispatch();
        }

        /// <summary>Runs OnItemPickedUp on a unit, then on its components, in add order.</summary>
        private static void UnitItemPickedUp(Unit unit, Item item)
        {
            if (!unit.HasEnded && (unit.Mask & Hook.OnItemPickedUp) != 0)
            {
                try
                {
                    unit.OnItemPickedUp(item);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(unit) + ".OnItemPickedUp", error);
                }
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnItemPickedUp) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnItemPickedUp(item);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnItemPickedUp", error);
                }
            }

            unit.EndDispatch();
        }

        /// <summary>Runs OnDropped on an item, then on its components, in add order.</summary>
        private static void ItemDropped(Item item, Unit by)
        {
            if (!item.HasEnded && (item.Mask & Hook.OnDropped) != 0)
            {
                try
                {
                    item.OnDropped(by);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(item) + ".OnDropped", error);
                }
            }

            var count = item.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = item.ComponentAt(i) as ItemComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnDropped) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDropped(by);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnDropped", error);
                }
            }

            item.EndDispatch();
        }

        /// <summary>Runs OnItemDropped on a unit, then on its components, in add order.</summary>
        private static void UnitItemDropped(Unit unit, Item item)
        {
            if (!unit.HasEnded && (unit.Mask & Hook.OnItemDropped) != 0)
            {
                try
                {
                    unit.OnItemDropped(item);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(unit) + ".OnItemDropped", error);
                }
            }

            var count = unit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = unit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnItemDropped) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnItemDropped(item);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnItemDropped", error);
                }
            }

            unit.EndDispatch();
        }

        /// <summary>Runs OnDealtDamage on a unit, then on its components, in add order.</summary>
        private static void DealtDamage(Unit from, Damage damage)
        {
            if (!from.HasEnded && (from.Mask & Hook.OnDealtDamage) != 0)
            {
                try
                {
                    from.OnDealtDamage(damage);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(from) + ".OnDealtDamage", error);
                }
            }

            var count = from.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = from.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnDealtDamage) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDealtDamage(damage);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnDealtDamage", error);
                }
            }

            from.EndDispatch();
        }

        /// <summary>Runs OnDamaged on a unit, then on its components, in add order.</summary>
        private static void TookDamage(Unit hit, Damage damage)
        {
            if (!hit.HasEnded && (hit.Mask & Hook.OnDamaged) != 0)
            {
                try
                {
                    hit.OnDamaged(damage);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(hit) + ".OnDamaged", error);
                }
            }

            var count = hit.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = hit.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnDamaged) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDamaged(damage);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnDamaged", error);
                }
            }

            hit.EndDispatch();
        }

        /// <summary>Runs OnDeath on a unit, then on its components, in add order. Death is not an end: the unit and its corpse go on until it leaves the game, so what should outlast the corpse, such as a respawn timer, names another scope.</summary>
        private static void Dying(Unit dead, Unit? killer)
        {
            if (!dead.HasEnded && (dead.Mask & Hook.OnDeath) != 0)
            {
                try
                {
                    dead.OnDeath(killer);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(dead) + ".OnDeath", error);
                }
            }

            var count = dead.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = dead.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnDeath) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDeath(killer);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnDeath", error);
                }
            }

            dead.EndDispatch();
        }

        /// <summary>Runs OnKill on a unit, then on its components, in add order.</summary>
        private static void Killing(Unit slayer, Unit victim)
        {
            if (!slayer.HasEnded && (slayer.Mask & Hook.OnKill) != 0)
            {
                try
                {
                    slayer.OnKill(victim);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(slayer) + ".OnKill", error);
                }
            }

            var count = slayer.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = slayer.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnKill) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnKill(victim);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnKill", error);
                }
            }

            slayer.EndDispatch();
        }

        /// <summary>Runs OnUnitDamaged on the map components of an actor, in add order.</summary>
        private static void MapDamaged(Actor actor, Damage damage)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnUnitDamaged) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnUnitDamaged(damage);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnUnitDamaged", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Runs OnUnitDied on the map components of an actor, in add order.</summary>
        private static void MapDied(Actor actor, Unit dying, Unit? killer)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnUnitDied) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnUnitDied(dying, killer);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnUnitDied", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Runs OnCast on an ability, unless the ability has ended or does not override it: what it makes in its own scope runs on after the spell ends (decision 63), and work that stops with a channel names the cast.</summary>
        private static void AbilityCast(Ability ability, Cast cast)
        {
            if (!ability.HasEnded && (ability.Mask & Hook.OnCast) != 0)
            {
                try
                {
                    ability.OnCast(cast);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(ability) + ".OnCast", error);
                }
            }
        }

        /// <summary>Runs OnCast on a unit, then on its components, in add order.</summary>
        private static void CasterCast(Unit caster, Cast cast)
        {
            if (!caster.HasEnded && (caster.Mask & Hook.OnCast) != 0)
            {
                try
                {
                    caster.OnCast(cast);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(caster) + ".OnCast", error);
                }
            }

            var count = caster.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = caster.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnCast) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnCast(cast);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnCast", error);
                }
            }

            caster.EndDispatch();
        }

        /// <summary>A hero gains one level or more, as one event that reads the last: for each level gained, in order, the hero's OnLevelUp and its components', then the map's and the hero's player's OnHeroLevelUp. A hero first taken in here, or whose level was known as this one or higher (taken away by a native), runs them once, for the level reached. The levels after one whose reactions ended the hero run nothing.</summary>
        internal static void HeroLevel(unit handle)
        {
            if (KnownUnits.Find(handle) == null && !Events.Wants(Hook.OnHeroLevelUp))
            {
                return;
            }

            var hero = Unit.Of(handle) as Hero;
            if (hero == null)
            {
                return;
            }

            var reached = Runtime.World.LevelOf(handle);
            if (hero.leveledTo >= reached)
            {
                hero.leveledTo = reached - 1;
            }

            // One level at a time, so that a reaction that raises the hero again runs the new levels once, in order, inside
            // its call, and this loop then finds them done; one that lowers it ends the loop.
            var slot = hero.Owner.Slot;
            while (hero.leveledTo < reached && !hero.HasEnded)
            {
                var level = hero.leveledTo + 1;
                hero.leveledTo = level;
                HeroLeveled(hero, level);
                MapLeveled(Game.Instance, hero, level);
                var player = Player.Existing(slot);
                if (player != null)
                {
                    MapLeveled(player, hero, level);
                }

                if (hero.leveledTo < level)
                {
                    break;
                }
            }
        }

        /// <summary>A hero learns a skill or raises its level: the ability's OnLearned, the hero's OnSkillLearned and its components', then the map's and the hero's player's OnHeroSkillLearned. The game spends the skill point after the event (seen 11-02).</summary>
        internal static void HeroSkill(unit handle, int abilityId)
        {
            if (KnownUnits.Find(handle) == null && ObjectTypes.AbilityMaker(abilityId) == null && !Events.Wants(Hook.OnHeroSkillLearned))
            {
                return;
            }

            var hero = Unit.Of(handle) as Hero;
            if (hero == null)
            {
                return;
            }

            Learned(hero, hero.AbilityOf(abilityId));
        }

        /// <summary>Runs the learn reactions for a skill a hero learned or code gave it a level of: the ability's OnLearned, the hero's OnSkillLearned and its components', then the map's and the hero's player's OnHeroSkillLearned.</summary>
        internal static void Learned(Hero hero, Ability ability)
        {
            var slot = hero.Owner.Slot;
            AbilityLearned(ability);
            HeroLearned(hero, ability);
            MapLearned(Game.Instance, hero, ability);
            var player = Player.Existing(slot);
            if (player != null)
            {
                MapLearned(player, hero, ability);
            }
        }

        /// <summary>Runs OnLost on an ability RemoveAbility took from a hero whose skill it is.</summary>
        internal static void Lost(Ability ability)
        {
            if (!ability.HasEnded && (ability.Mask & Hook.OnLost) != 0)
            {
                try
                {
                    ability.OnLost();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(ability) + ".OnLost", error);
                }
            }
        }

        /// <summary>Runs OnLevelUp on a hero, then on its components, in add order.</summary>
        private static void HeroLeveled(Hero hero, int level)
        {
            if (!hero.HasEnded && (hero.Mask & Hook.OnLevelUp) != 0)
            {
                try
                {
                    hero.OnLevelUp(level);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(hero) + ".OnLevelUp", error);
                }
            }

            var count = hero.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = hero.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnLevelUp) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnLevelUp(level);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnLevelUp", error);
                }
            }

            hero.EndDispatch();
        }

        /// <summary>Runs OnHeroLevelUp on the map components of an actor, in add order.</summary>
        private static void MapLeveled(Actor actor, Hero hero, int level)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnHeroLevelUp) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnHeroLevelUp(hero, level);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnHeroLevelUp", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Runs OnLearned on an ability, unless it has ended or does not override it.</summary>
        private static void AbilityLearned(Ability ability)
        {
            if (!ability.HasEnded && (ability.Mask & Hook.OnSkillLearned) != 0)
            {
                try
                {
                    ability.OnLearned();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(ability) + ".OnLearned", error);
                }
            }
        }

        /// <summary>Runs OnSkillLearned on a hero, then on its components, in add order.</summary>
        private static void HeroLearned(Hero hero, Ability ability)
        {
            if (!hero.HasEnded && (hero.Mask & Hook.OnSkillLearned) != 0)
            {
                try
                {
                    hero.OnSkillLearned(ability);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(hero) + ".OnSkillLearned", error);
                }
            }

            var count = hero.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = hero.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnSkillLearned) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnSkillLearned(ability);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnSkillLearned", error);
                }
            }

            hero.EndDispatch();
        }

        /// <summary>Runs OnHeroSkillLearned on the map components of an actor, in add order.</summary>
        private static void MapLearned(Actor actor, Hero hero, Ability ability)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnHeroSkillLearned) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnHeroSkillLearned(hero, ability);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnHeroSkillLearned", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>A player leaves the game: the game's components react, then the leaving player's.</summary>
        internal static void Left(player p)
        {
            var leaving = Player.Of(p);
            if (leaving == null)
            {
                return;
            }

            MapLeft(Game.Instance, leaving);
            MapLeft(leaving, leaving);
        }

        /// <summary>Runs OnLeave on the map components of an actor, in add order.</summary>
        private static void MapLeft(Actor actor, Player leaving)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnLeave) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnLeave(leaving);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnLeave", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Runs OnUnitCast on the map components of an actor, in add order.</summary>
        private static void MapCast(Actor actor, Cast cast)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnUnitCast) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnUnitCast(cast);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnUnitCast", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>Runs OnGameStart on the map components of an actor, in add order.</summary>
        private static void StartComponents(Actor actor)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnGameStart) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnGameStart();
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + ".OnGameStart", error);
                }
            }

            actor.EndDispatch();
        }
    }
}
