using Moonwell;

namespace LibraryCode
{
    /// <summary>Uses each package that needs an install: a hit halved, the ground read, a region's shape made, a mouse's place read and a shot's speed worked out.</summary>
    public sealed class Opening : GameComponent, IMouseWatch
    {
        public override void OnGameStart()
        {
            var footman = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.One, 0f, 0f, Game.Instance);
            footman.Add(new Armor());
            footman.Damage(footman, Terrain.Height(0f, 0f), DamageKind.Fire);
            var shape = AreaShape.Circle(new Vector2(0f, 0f), 100f);
            Ballistics.Speed(new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), 45f, 1f, out var speed);
            Player.One.Print(shape.ToString() + speed);
        }

        public void OnMousePressed(Mouse mouse, MouseButton button)
        {
            Player.One.Print("mouse at " + mouse.X);
        }
    }

    /// <summary>Takes half of every hit.</summary>
    public sealed class Armor : UnitComponent, IDamageTarget
    {
        public void OnTaken(Hit hit)
        {
            hit.Amount = hit.Amount / 2f;
        }
    }
}
