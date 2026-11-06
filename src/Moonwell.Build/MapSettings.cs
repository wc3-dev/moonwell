namespace Moonwell.Build
{
    /// <summary>What the map's <c>Moonwell.Map</c> class declares, read on .NET.</summary>
    /// <param name="TypeName">The full name of the map class, usable after <c>global::</c> in generated C#.</param>
    /// <param name="Name">The map's name; empty keeps World Editor's value.</param>
    /// <param name="Author">The map's author; empty keeps World Editor's value.</param>
    /// <param name="Description">The map's description; empty keeps World Editor's value.</param>
    /// <param name="RecommendedPlayers">The recommended players; empty keeps World Editor's value.</param>
    /// <param name="DataSet">The map's <c>Moonwell.DataSet</c> as its number: 0 Default, 1 Custom, 2 Melee.</param>
    /// <param name="DataVersion">The map's <c>Moonwell.DataVersion</c> as its number: 0 ReignOfChaos, 1 FrozenThrone, 2 ForsakenKingdom.</param>
    /// <param name="SaveFolder">The folder of CustomMapData for the map's player files; empty takes the map's name.</param>
    /// <param name="SaveKey">The private text every save's check covers; required when the map has a save class.</param>
    /// <param name="Slots">The slots the map class sets up, in its order; none keeps World Editor's players and forces.</param>
    /// <param name="Teams">The teams the slots name, numbered in the order the slots first name them.</param>
    public sealed record MapSettings(string TypeName, string Name, string Author, string Description, string RecommendedPlayers, int DataSet = 0, int DataVersion = 2, string SaveFolder = "", string SaveKey = "", IReadOnlyList<SlotSetting>? Slots = null, IReadOnlyList<TeamSetting>? Teams = null);

    /// <summary>A slot the map class sets up, read on .NET (decision 198).</summary>
    /// <param name="Number">The slot, from 0.</param>
    /// <param name="Team">The index of its team in the map's teams.</param>
    /// <param name="Controller">The slot's <c>Moonwell.Controller</c> as its number: 0 User, 1 Computer, 2 Rescuable, 3 Neutral.</param>
    /// <param name="Race">The slot's <c>Moonwell.Race</c> as its number, 0 Human to 3 NightElf; -1 for the player's pick.</param>
    /// <param name="Name">The slot's name; empty is "Player" and its number from 1.</param>
    public sealed record SlotSetting(int Number, int Team, int Controller, int Race, string Name);

    /// <summary>A team of the map class's slots, read on .NET.</summary>
    /// <param name="Name">The team's name.</param>
    /// <param name="SharedVision">Whether its players see what each other sees.</param>
    /// <param name="SharedControl">Whether its players control each other's units.</param>
    public sealed record TeamSetting(string Name, bool SharedVision, bool SharedControl);
}
