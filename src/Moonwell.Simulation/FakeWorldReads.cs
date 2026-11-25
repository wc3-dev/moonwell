using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The fake world's unit and player reads a map asks of the game: a unit's food and a mine's gold, and a player's units.</summary>
    public sealed partial class FakeWorld
    {
        /// <summary>The food each unit uses, from its type's object data when it was made while a Simulation plays, else as SetFoodUsed set it.</summary>
        private readonly Dictionary<unit, int> foods = new Dictionary<unit, int>();

        /// <summary>The gold each mine holds, from its Gold Mine ability's Max Gold when it was made while a Simulation plays, else as SetResources set it.</summary>
        private readonly Dictionary<unit, int> resources = new Dictionary<unit, int>();

        /// <summary>The food a unit uses; 0 until set, or its type's food cost for a unit made while a Simulation plays.</summary>
        public int FoodUsedOf(unit unit)
        {
            return KeptWhole(foods, unit);
        }

        /// <summary>Sets the food a unit uses, as its type's food cost gives it in the game.</summary>
        public void SetFoodUsed(unit unit, int food)
        {
            foods[unit] = food;
        }

        /// <summary>The gold a mine holds; 0 until set, or its Gold Mine ability's Max Gold for a mine made while a Simulation plays.</summary>
        public int ResourcesOf(unit unit)
        {
            return KeptWhole(resources, unit);
        }

        /// <summary>Sets the gold a mine holds.</summary>
        public void SetResources(unit unit, int amount)
        {
            NoteGone(unit);
            resources[unit] = amount;
        }

        /// <summary>The units the world made that a player owns and the game has not removed, dead ones too, in the order made.</summary>
        public List<unit> UnitsOf(player player)
        {
            var slot = SlotOf(player);
            var found = new List<unit>();
            for (var i = 0; i < madeUnits.Count; i++)
            {
                var made = madeUnits[i];
                if (UnitSlot(made) == slot && !vanishedUnits.Contains(made) && !RemovedUnits.Contains(made) && !ownerless.Contains(made))
                {
                    found.Add(made);
                }
            }

            return found;
        }

        /// <summary>A whole number kept for a unit; 0 when none was set, and once the game has removed it.</summary>
        private int KeptWhole(Dictionary<unit, int> values, unit unit)
        {
            NoteGone(unit);
            if (vanishedUnits.Contains(unit))
            {
                return 0;
            }

            return values.TryGetValue(unit, out var value) ? value : 0;
        }
    }
}
