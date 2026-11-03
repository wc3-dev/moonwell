using Moonwell;

namespace FramesFixture
{
    /// <summary>A map that uses the frames package: a panel on the screen at the start.</summary>
    public sealed class FramesMap : Map
    {
        public FramesMap()
        {
            Name = "Frames";
            Components.Add(new Hud());
        }
    }

    /// <summary>Puts a panel on the screen when the game starts.</summary>
    public sealed class Hud : GameComponent
    {
        public override void OnGameStart()
        {
            Screen.Add(new Panel(), this);
        }
    }
}
