using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The map a project declares: its settings, and the components the game and its players start with; a project has one class deriving from it.</summary>
    public abstract class Map
    {
        /// <summary>The name of the map; empty keeps the value World Editor set.</summary>
        public string Name { get; protected set; } = "";

        /// <summary>The author of the map; empty keeps the value World Editor set.</summary>
        public string Author { get; protected set; } = "";

        /// <summary>The description of the map; empty keeps the value World Editor set.</summary>
        public string Description { get; protected set; } = "";

        /// <summary>The players the map recommends, as free text such as "Any" or "2-4"; empty keeps the value World Editor set.</summary>
        public string RecommendedPlayers { get; protected set; } = "";

        /// <summary>The game data set the map plays with; Default when the map sets none.</summary>
        public DataSet DataSet { get; protected set; } = DataSet.Default;

        /// <summary>The folder of CustomMapData the map's player files go in, letters and digits; empty takes the map's name, so set it to keep players' files when the name changes.</summary>
        public string SaveFolder { get; protected set; } = "";

        /// <summary>
        /// The map's own private text that every save's check covers, so a save loads only in this map: required when the map
        /// has a save class, and never changed, as a changed key fails every save made before (decision 183).
        /// </summary>
        public string SaveKey { get; protected set; } = "";

        /// <summary>The game data version the map plays with; ForsakenKingdom, World Editor's default, when the map sets none.</summary>
        public DataVersion DataVersion { get; protected set; } = DataVersion.ForsakenKingdom;

        /// <summary>The components the game starts with, added in list order when the game starts; the constructor lists them, or assigns a new list.</summary>
        protected List<GameComponent> Components { get; set; } = new List<GameComponent>();

        /// <summary>What makes the components each player in the game starts with, people and computers: when the game starts each maker runs once for each player, in list order, before the units already in the map come in. The constructor lists them, or assigns a new list.</summary>
        protected List<Func<PlayerComponent>> PlayerComponents { get; set; } = new List<Func<PlayerComponent>>();

        /// <summary>The starting components, in the order they were listed.</summary>
        internal List<GameComponent> StartComponents => Components;

        /// <summary>The makers of the starting player components, in the order they were listed.</summary>
        internal List<Func<PlayerComponent>> StartPlayerComponents => PlayerComponents;
    }
}
