using Moonwell;

namespace PathingFixture
{
    /// <summary>A map that asks a path and a fit when the game starts.</summary>
    public sealed class PathingMap : Map
    {
        public PathingMap()
        {
            Name = "Pathing";
            Components.Add(new Walker());
        }
    }

    public sealed class Walker : GameComponent
    {
        public override void OnGameStart()
        {
            var route = Paths.FindPath(31f, 0f, 0f, 256f, 0f);
            if (route.Reaches && Paths.CanStand(31f, 256f, 0f))
            {
                Player.One.Print("Reaches " + route.Length);
            }
        }
    }
}
