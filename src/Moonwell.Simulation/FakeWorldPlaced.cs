using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The fake world's placed things: the units, items, destructables and sounds an editor placed, by World Editor's names without gg_.</summary>
    public sealed partial class FakeWorld
    {
        private readonly Dictionary<string, unit> placedUnits = new Dictionary<string, unit>();

        private readonly Dictionary<string, item> placedItems = new Dictionary<string, item>();

        private readonly Dictionary<string, destructable> placedDestructables = new Dictionary<string, destructable>();

        private readonly Dictionary<string, sound> placedSounds = new Dictionary<string, sound>();

        /// <summary>Makes a unit as the map's script makes a placed one, under its name.</summary>
        public unit PlaceUnit(string name, int typeId)
        {
            var made = NewUnit(typeId);
            placedUnits[name] = made;
            return made;
        }

        /// <summary>Makes an item as the map's script makes a placed one, under its name.</summary>
        public item PlaceItem(string name, int typeId)
        {
            var made = NewItem(typeId);
            placedItems[name] = made;
            return made;
        }

        /// <summary>Makes a destructable as the game makes a placed one, under its name.</summary>
        public destructable PlaceDestructable(string name, int typeId)
        {
            var made = NewDestructable(typeId);
            placedDestructables[name] = made;
            return made;
        }

        /// <summary>Makes a sound of the Sound Editor under its name; its fake's path is the name.</summary>
        public FakeSound AddPlacedSound(string name)
        {
            var made = MakeSound(name, false, true);
            placedSounds[name] = made;
            return SoundOf(made);
        }

        /// <summary>The placed unit of the name, as its gg_unit_ global holds it.</summary>
        public unit? PlacedUnit(string name)
        {
            return placedUnits.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>The placed item of the name, as its gg_item_ global holds it.</summary>
        public item? PlacedItem(string name)
        {
            return placedItems.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>The placed destructable of the name, as the search near its place finds it.</summary>
        public destructable? PlacedDestructable(string name)
        {
            return placedDestructables.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>The sound of the name, as its gg_snd_ global holds it.</summary>
        public sound? PlacedSound(string name)
        {
            return placedSounds.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>Stops a sound of the Sound Editor, which may play again.</summary>
        public void StopPlacedSound(sound sound)
        {
            LiveSound(sound).Halts++;
        }
    }
}
