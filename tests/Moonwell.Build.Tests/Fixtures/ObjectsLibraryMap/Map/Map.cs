using Moonwell;

namespace ObjectsLibraryMapFixture
{
    /// <summary>A map that reaches Moonwell.Objects only through its library: its own code names only the library's spawner.</summary>
    public sealed class ObjectsLibraryMap : Map
    {
        public ObjectsLibraryMap()
        {
            Name = "ObjectsLibraryMap";
            Components.Add(new Start());
        }
    }

    /// <summary>Has the library make a footman when the game starts.</summary>
    public sealed class Start : GameComponent
    {
        public override void OnGameStart()
        {
            ObjectsLibraryCode.Spawner.Footman(Player.One);
        }
    }
}
