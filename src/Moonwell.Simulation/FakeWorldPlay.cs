using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The fake world's play, which a Simulation turns on: in each 0.02 s step the units of a type with numbers walk, chase,
    /// swing and acquire, and each order stands its unit 8 steps first. Off, the fake is as it always was.
    /// </summary>
    public sealed partial class FakeWorld
    {
        /// <summary>Whether the play runs: only once a Simulation started it.</summary>
        private bool playing;

        /// <summary>The last play step run; 0 before the first.</summary>
        private int playStep;

        /// <summary>The play step an order given now is stamped with: the next one to run.</summary>
        private int nextPlay = 1;

        /// <summary>The play's own dice, which the damage of each swing is drawn from.</summary>
        private Dice playDice = null!;

        /// <summary>The numbers of each unit type, by type id.</summary>
        private readonly Dictionary<int, UnitNumbers> typeNumbers = new Dictionary<int, UnitNumbers>();

#if !MOONWELL_LUA
        /// <summary>The types the object data gave no numbers, so that each is looked up once.</summary>
        private readonly HashSet<int> numberless = new HashSet<int>();
#endif

        /// <summary>What the play keeps of each unit it plays.</summary>
        private readonly Dictionary<unit, PlayedUnit> played = new Dictionary<unit, PlayedUnit>();

        /// <summary>Turns the play on, with its dice from the seed and the map's random numbers (Pick) from the seed after it.</summary>
        internal void StartPlay(int seed)
        {
            playing = true;
            playDice = new Dice(seed);
            var mapDice = new Dice(seed + 1);
            Pick = (low, high) => mapDice.Int(low, high);
        }

        /// <summary>Gives a unit type its numbers, for the units of the type made from now on.</summary>
        internal void SetNumbers(int typeId, UnitNumbers numbers)
        {
            typeNumbers[typeId] = numbers;
        }

        /// <summary>What the play keeps of a unit; null for one it does not play.</summary>
        internal PlayedUnit? PlayedOf(unit unit)
        {
            return played.TryGetValue(unit, out var unitPlay) ? unitPlay : null;
        }

        /// <summary>How many units the world made that the slot owns, alive and not removed, Moonwell's dummies and helper units left out.</summary>
        internal int AliveOf(int slot)
        {
            var count = 0;
            for (var i = 0; i < madeUnits.Count; i++)
            {
                var made = madeUnits[i];
                if (!Gone(made) && !IsHelperUnit(made) && UnitSlot(made) == slot)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// One step: its time, with Moonwell's timers due by then (the brains' step among them), then the play over the units
        /// made before it, in the order made. An order given in the timers is stamped with this step, one given in the play
        /// with the next. A dead unit, a removed one and a paused one do nothing; one seen dead and alive again (a revived hero)
        /// comes back idle.
        /// </summary>
        internal void PlayOneStep()
        {
            playStep++;
            var k = playStep;
            nextPlay = k;
            AdvanceTo(k / 50.0f);
            nextPlay = k + 1;
            var count = madeUnits.Count;
            for (var i = 0; i < count; i++)
            {
                var made = madeUnits[i];
                if (!played.TryGetValue(made, out var unitPlay))
                {
                    continue;
                }

                if (dead.Contains(made))
                {
                    unitPlay.SeenDead = true;
                    continue;
                }

                if (Gone(made))
                {
                    continue;
                }

                if (unitPlay.SeenDead)
                {
                    unitPlay.SeenDead = false;
                    currentOrders[made] = 0;
                    unitPlay.Walking = false;
                    unitPlay.Target = null;
                    unitPlay.HasPoint = false;
                    unitPlay.Holding = false;
                }

                if (PausedUnits.Contains(made))
                {
                    continue;
                }

                if (k < unitPlay.ReadyStep)
                {
                    // An order given in this step's play is stamped with the next: its 8 steps start there.
                    if (k >= unitPlay.ReadyStep - 8)
                    {
                        unitPlay.Stood++;
                    }

                    continue;
                }

                Act(made, unitPlay, k);
            }
        }

        /// <summary>The armor's factor on damage, as the game's: 1 - 0.06a / (1 + 0.06a) for armor of 0 or more, 2 - 0.94^n below, n the armor's whole part toward zero, at most 20 (seen 11-06).</summary>
        internal static float ArmorFactor(float armor)
        {
            if (armor >= 0.0f)
            {
                return 1.0f - 0.06f * armor / (1.0f + 0.06f * armor);
            }

            return 2.0f - (float)Math.Pow(0.94, Math.Min(20, (int)-armor));
        }

        /// <summary>Seeds a unit made while the play runs, when its type has numbers, before Moonwell takes it in: its life, move speed, armor and average damage, and what the play keeps of it.</summary>
        private void Seed(unit made, int typeId)
        {
            if (!playing)
            {
                return;
            }

            var numbers = NumbersOf(typeId);
            if (numbers == null)
            {
                return;
            }

            maxLives[made] = numbers.Life;
            lives[made] = numbers.Life;
            moveSpeeds[made] = numbers.MoveSpeed;
            armors[made] = numbers.Armor;
            averageDamages[made] = numbers.Damage + numbers.DamageDice * (numbers.DamageSides + 1) / 2.0f;
            attackCooldowns[made] = numbers.AttackCooldown;
            acquireRanges[made] = numbers.AcquisitionRange;
            var unitPlay = new PlayedUnit();
            unitPlay.AttackRange = numbers.AttackRange;
            unitPlay.CooldownSteps = Simulation.StepsOf(numbers.AttackCooldown);
            unitPlay.Damage = numbers.Damage;
            unitPlay.DamageDice = numbers.DamageDice;
            unitPlay.DamageSides = numbers.DamageSides;
            unitPlay.AcquisitionRange = numbers.AcquisitionRange;
            played[made] = unitPlay;
        }

        /// <summary>A type's numbers: those set, else on .NET those of its object data; null for none.</summary>
        private UnitNumbers? NumbersOf(int typeId)
        {
            if (typeNumbers.TryGetValue(typeId, out var known))
            {
                return known;
            }

#if !MOONWELL_LUA
            if (!numberless.Contains(typeId))
            {
                var read = ObjectNumbers.Of(typeId);
                if (read != null)
                {
                    typeNumbers[typeId] = read;
                    return read;
                }

                numberless.Add(typeId);
            }
#endif
            return null;
        }

        /// <summary>Keeps an order given to a played unit: counted, standing it 8 steps from the step it is stamped with, a walk under way dropped. An order the fake does not know (id 0) is neither counted nor held.</summary>
        private void PlayOrdered(unit ordered, int id, OrderKind kind, float x, float y, unit? target)
        {
            if (id == 0)
            {
                return;
            }

            if (!played.TryGetValue(ordered, out var unitPlay))
            {
                return;
            }

            unitPlay.Orders++;
            unitPlay.ReadyStep = nextPlay + 8;
            unitPlay.Walking = false;
            unitPlay.Holding = id == 851993;
            unitPlay.Target = target;
            unitPlay.TargetIsEnemy = false;
            unitPlay.HasPoint = kind == OrderKind.Place;
            unitPlay.PointX = x;
            unitPlay.PointY = y;
            if (target != null)
            {
                unitPlay.HasPoint = true;
                unitPlay.PointX = XOf(target);
                unitPlay.PointY = YOf(target);
                unitPlay.TargetIsEnemy = IsEnemyOf(PlayerAt(UnitSlot(ordered)), PlayerAt(UnitSlot(target)));
            }
        }

        /// <summary>A ready unit's step, by its current order. An order the play does not carry out (a spell, attackground, one on an item) ends at the unit's first ready step; patrol stands. Hold position reads 0, as the game's does (seen 11-14), so the play keeps it apart from idle.</summary>
        private void Act(unit made, PlayedUnit unitPlay, int k)
        {
            var id = CurrentOrderOf(made);
            if (id == 851993 || (id == 0 && unitPlay.Holding))
            {
                Hold(made, unitPlay, k);
                return;
            }

            if (id == 0)
            {
                Acquire(made, unitPlay);
                return;
            }

            if (id == 851986 || id == 851971)
            {
                if (id == 851971 && unitPlay.Target != null && unitPlay.TargetIsEnemy)
                {
                    Fight(made, unitPlay, unitPlay.Target, k);
                }
                else if (unitPlay.HasPoint)
                {
                    Walk(made, unitPlay, unitPlay.PointX, unitPlay.PointY);
                }
                else
                {
                    currentOrders[made] = 0;
                }

                return;
            }

            if (id == 851983)
            {
                if (unitPlay.Target != null)
                {
                    Fight(made, unitPlay, unitPlay.Target, k);
                }
                else if (unitPlay.HasPoint)
                {
                    AttackMove(made, unitPlay, k);
                }
                else
                {
                    currentOrders[made] = 0;
                }

                return;
            }

            if (id != 851990 && id != 851972)
            {
                currentOrders[made] = 0;
            }
        }

        /// <summary>An idle unit that can attack takes the nearest enemy within its acquisition range as its attack, silently: no order event, not counted, not held. A watched unit's acquire event comes first, its order still reading 0, as the game's does; an order given inside it (full control's hold) is kept instead.</summary>
        private void Acquire(unit made, PlayedUnit unitPlay)
        {
            if (!CanAcquire(unitPlay))
            {
                return;
            }

            var found = Nearest(made, unitPlay.AcquisitionRange * unitPlay.AcquisitionRange);
            if (found == null)
            {
                return;
            }

            var orders = unitPlay.Orders;
            Acquire(made);
            if (unitPlay.Orders != orders)
            {
                return;
            }

            currentOrders[made] = 851983;
            unitPlay.Target = found;
            unitPlay.TargetIsEnemy = true;
            unitPlay.HasPoint = false;
            unitPlay.Walking = false;
        }

        /// <summary>An attack to a point: the nearest enemy within acquisition range is fought, else the unit walks on to the point.</summary>
        private void AttackMove(unit made, PlayedUnit unitPlay, int k)
        {
            unit? found = null;
            if (CanAcquire(unitPlay))
            {
                found = Nearest(made, unitPlay.AcquisitionRange * unitPlay.AcquisitionRange);
            }

            if (found != null)
            {
                unitPlay.Walking = false;
                Fight(made, unitPlay, found, k);
                return;
            }

            Walk(made, unitPlay, unitPlay.PointX, unitPlay.PointY);
        }

        /// <summary>Hold position: never moves, and swings at the nearest enemy in range when its cooldown is over.</summary>
        private void Hold(unit made, PlayedUnit unitPlay, int k)
        {
            if (unitPlay.Damage + unitPlay.DamageDice <= 0 || k < unitPlay.NextSwing)
            {
                return;
            }

            var found = Nearest(made, unitPlay.AttackRange * unitPlay.AttackRange + 1.0f);
            if (found != null)
            {
                Swing(made, unitPlay, found, k);
            }
        }

        /// <summary>A walking step toward a point: the plan is made at the first, from where the unit stands, in ceil(distance x 50 / speed) steps, and made again when the unit no longer stands where the play put it; at the last the unit stands exactly on the point, idle. A walk of 0 arrives at once; a unit with no speed stands.</summary>
        private void Walk(unit made, PlayedUnit unitPlay, float toX, float toY)
        {
            if (unitPlay.Walking && (XOf(made) != unitPlay.LastX || YOf(made) != unitPlay.LastY))
            {
                unitPlay.Walking = false;
            }

            if (!unitPlay.Walking)
            {
                var fromX = XOf(made);
                var fromY = YOf(made);
                var dx = toX - fromX;
                var dy = toY - fromY;
                var squared = dx * dx + dy * dy;
                if (squared == 0.0f)
                {
                    currentOrders[made] = 0;
                    return;
                }

                var speed = SpeedOf(made);
                if (!(speed > 0.0f))
                {
                    return;
                }

                unitPlay.Walking = true;
                unitPlay.FromX = fromX;
                unitPlay.FromY = fromY;
                unitPlay.Done = 0;

                // A hair over a whole number of steps counts as it, so that float and double give the same steps.
                var steps = Math.Sqrt(squared) * 50.0 / speed - 0.0001;
                unitPlay.Total = steps <= 1.0 ? 1 : (int)Math.Ceiling(steps);
            }

            unitPlay.Done++;
            if (unitPlay.Done >= unitPlay.Total)
            {
                Place(made, unitPlay, toX, toY);
                unitPlay.Walking = false;
                currentOrders[made] = 0;
                return;
            }

            Place(made, unitPlay, unitPlay.FromX + (toX - unitPlay.FromX) * unitPlay.Done / unitPlay.Total, unitPlay.FromY + (toY - unitPlay.FromY) * unitPlay.Done / unitPlay.Total);
        }

        /// <summary>Puts a played unit at a place, and keeps the place, so that a walk sees when something else moved the unit.</summary>
        private void Place(unit made, PlayedUnit unitPlay, float x, float y)
        {
            unitXs[made] = x;
            unitYs[made] = y;
            unitPlay.LastX = x;
            unitPlay.LastY = y;
        }

        /// <summary>An attack on a unit: ended when the target is dead or gone; in range, a swing when the cooldown is over; else a chase of speed / 50, stopping at the range.</summary>
        private void Fight(unit made, PlayedUnit unitPlay, unit target, int k)
        {
            if (Gone(target))
            {
                currentOrders[made] = 0;
                return;
            }

            var x = XOf(made);
            var y = YOf(made);
            var targetX = XOf(target);
            var targetY = YOf(target);
            var dx = targetX - x;
            var dy = targetY - y;
            var squared = dx * dx + dy * dy;
            var range = unitPlay.AttackRange;
            // The 1 is a margin for float: a chase stopped at the range must read as in range.
            if (squared <= range * range + 1.0f)
            {
                if (k >= unitPlay.NextSwing)
                {
                    Swing(made, unitPlay, target, k);
                }

                return;
            }

            var speed = SpeedOf(made);
            if (!(speed > 0.0f))
            {
                return;
            }

            var distance = (float)Math.Sqrt(squared);
            var step = speed / 50.0f;
            var towardX = dx / distance;
            var towardY = dy / distance;
            if (distance - step <= range)
            {
                Place(made, unitPlay, targetX - towardX * range, targetY - towardY * range);
            }
            else
            {
                Place(made, unitPlay, x + towardX * step, y + towardY * step);
            }
        }

        /// <summary>A swing: the damage plus a draw of the play's dice for each die, less the target's armor now, dealt as an attack, and the next swing a cooldown later.</summary>
        private void Swing(unit made, PlayedUnit unitPlay, unit target, int k)
        {
            var raw = unitPlay.Damage;
            if (unitPlay.DamageSides >= 1)
            {
                for (var i = 0; i < unitPlay.DamageDice; i++)
                {
                    raw += playDice.Int(1, unitPlay.DamageSides);
                }
            }

            unitPlay.NextSwing = k + unitPlay.CooldownSteps;
            Hurt(made, target, raw * ArmorFactor(ArmorAt(target)), true);
        }

        /// <summary>A unit's armor as ArmorOf reads it, its own and its abilities' Idef bonus fields, with no call counted.</summary>
        private float ArmorAt(unit unit)
        {
            var armor = armors.TryGetValue(unit, out var own) ? own : 0.0f;
            foreach (var id in armorBonuses)
            {
                if (AbilityLevel(unit, id) > 0)
                {
                    armor += BonusOf(unit, id);
                }
            }

            return armor;
        }

        /// <summary>The nearest living enemy of the unit within a squared distance, centre to centre, among the units the play plays, never Neutral Passive or Moonwell's dummies and helper units; of two as near, the one made first.</summary>
        private unit? Nearest(unit made, float reachSquared)
        {
            var x = XOf(made);
            var y = YOf(made);
            var owner = PlayerAt(UnitSlot(made));
            unit? nearest = null;
            var nearestSquared = 0.0f;
            for (var i = 0; i < madeUnits.Count; i++)
            {
                var other = madeUnits[i];
                if (other == made || !played.ContainsKey(other) || Gone(other) || IsHelperUnit(other))
                {
                    continue;
                }

                var slot = UnitSlot(other);
                if (slot == NeutralPassive || !IsEnemyOf(owner, PlayerAt(slot)))
                {
                    continue;
                }

                var dx = XOf(other) - x;
                var dy = YOf(other) - y;
                var squared = dx * dx + dy * dy;
                if (squared <= reachSquared && (nearest == null || squared < nearestSquared))
                {
                    nearest = other;
                    nearestSquared = squared;
                }
            }

            return nearest;
        }

        /// <summary>Whether a unit acquires enemies: it has an acquisition range and a damage.</summary>
        private static bool CanAcquire(PlayedUnit unitPlay)
        {
            return unitPlay.AcquisitionRange > 0.0f && unitPlay.Damage + unitPlay.DamageDice > 0;
        }

        /// <summary>Whether a unit is dead, removed or vanished: a removed unit is pending in its frame and vanished from the next.</summary>
        private bool Gone(unit unit)
        {
            return dead.Contains(unit) || vanishedUnits.Contains(unit) || pendingRemovals.Contains(unit);
        }

        /// <summary>Whether a unit is Moonwell's casting dummy or a package's helper unit, by the type the fake keeps.</summary>
        private bool IsHelperUnit(unit unit)
        {
            return unitTypes.TryGetValue(unit, out var typeId) && typeId != 0 && ObjectTypes.IsHelper(typeId);
        }

        /// <summary>The slot of the player owning a unit, read with no call counted; 0 for one the world did not make.</summary>
        private int UnitSlot(unit unit)
        {
            return unitSlots.TryGetValue(unit, out var slot) ? slot : 0;
        }

        /// <summary>The x of a unit, read with no call counted.</summary>
        private float XOf(unit unit)
        {
            return unitXs.TryGetValue(unit, out var x) ? x : 0.0f;
        }

        /// <summary>The y of a unit, read with no call counted.</summary>
        private float YOf(unit unit)
        {
            return unitYs.TryGetValue(unit, out var y) ? y : 0.0f;
        }

        /// <summary>The move speed of a unit now, read with no call counted.</summary>
        private float SpeedOf(unit unit)
        {
            return moveSpeeds.TryGetValue(unit, out var speed) ? speed : 0.0f;
        }
    }

    /// <summary>What the play keeps of one unit: its numbers, its counts, its order's point or target, and its walk.</summary>
    internal sealed class PlayedUnit
    {
        /// <summary>How far its attack reaches, centre to centre.</summary>
        public float AttackRange;

        /// <summary>The steps between its swings.</summary>
        public int CooldownSteps;

        /// <summary>The damage of a swing before the dice.</summary>
        public int Damage;

        /// <summary>How many dice a swing adds.</summary>
        public int DamageDice;

        /// <summary>The sides of each die.</summary>
        public int DamageSides;

        /// <summary>How far it looks for an enemy when idle.</summary>
        public float AcquisitionRange;

        /// <summary>The orders it was given.</summary>
        public int Orders;

        /// <summary>The steps it stood by its orders' holds.</summary>
        public int Stood;

        /// <summary>The first step it acts at after its last order.</summary>
        public int ReadyStep;

        /// <summary>The first step it may swing at again.</summary>
        public int NextSwing;

        /// <summary>The unit its order or its acquiring aims at; null for none.</summary>
        public unit? Target;

        /// <summary>Whether Target was an enemy when ordered, or acquired.</summary>
        public bool TargetIsEnemy;

        /// <summary>Whether its order has a point: the place ordered, or where the target stood.</summary>
        public bool HasPoint;

        /// <summary>The x of its order's point.</summary>
        public float PointX;

        /// <summary>The y of its order's point.</summary>
        public float PointY;

        /// <summary>Whether a walk is under way.</summary>
        public bool Walking;

        /// <summary>Whether its last order was hold position, which reads 0 as idle does.</summary>
        public bool Holding;

        /// <summary>The x the walk started from.</summary>
        public float FromX;

        /// <summary>The y the walk started from.</summary>
        public float FromY;

        /// <summary>The walk's steps in all.</summary>
        public int Total;

        /// <summary>The walk's steps done.</summary>
        public int Done;

        /// <summary>The x the play last put the unit at.</summary>
        public float LastX;

        /// <summary>The y the play last put the unit at.</summary>
        public float LastY;

        /// <summary>Whether the play saw the unit dead, so that it comes back idle if it lives again.</summary>
        public bool SeenDead;
    }
}
