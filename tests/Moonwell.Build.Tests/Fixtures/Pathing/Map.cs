using Moonwell;

namespace PathingFixture
{
    /// <summary>A map that asks a path and a fit when the game starts, a path by unit type, which makes the build write the unit types' table, and where a Farm fits, which makes it write the buildings' table.</summary>
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
            var footman = Paths.FindPathFor(ObjectTypes.FourCC("hfoo"), 0f, 0f, 256f, 0f);
            if (route.Reaches && footman.Reaches && Paths.CanStand(31f, 256f, 0f))
            {
                Player.One.Print("Reaches " + route.Length);
            }

            if (Paths.CanBuild<Moonwell.Objects.Human.Units.Farm>(512f, 0f, Player.One) && Paths.BuildSpots(ObjectTypes.FourCC("htow"), 0f, 0f, 2).Count > 0)
            {
                Player.One.Print("Builds at " + Paths.BuildPlace<Moonwell.Objects.Human.Units.Farm>(500f, 0f));
            }
        }
    }
}
