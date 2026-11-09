namespace Moonwell
{
    /// <summary>
    /// A camera that follows a unit for one player, held in a scope: <see cref="ThirdPersonCamera"/> or
    /// <see cref="TopDownCamera"/> (decision 234). One per player: starting another ends the one before. While it runs, the
    /// player's own input cannot change the zoom, the rotation or the angle; when it ends, by End or its scope, the camera
    /// goes back to the game camera and the input to what the camera's own locks say. It pauses while its player watches a
    /// cinematic and goes on after (decision 235), and it ends when its unit leaves the game. The numbers can be set while it
    /// runs and the camera glides to them. Each change reaches that player's machine only; starting and ending one are game
    /// state, so neither goes inside Local.For.
    /// </summary>
    public abstract class FollowCamera : Scope
    {
        /// <summary>How often the camera is moved, in seconds; each move takes this long, so it glides.</summary>
        internal const float Tick = 0.03f;

        private Unit unit;
        private bool running;
        private bool paused;
        private bool turned;
        private float lastTurn;

        internal FollowCamera(Player player, Unit unit, float distance, float angle, float height)
        {
            Player = player;
            this.unit = unit;
            Distance = distance;
            Angle = angle;
            Height = height;
        }

        /// <summary>The player whose camera this is.</summary>
        [LocalSafe]
        public Player Player { get; }

        /// <summary>The unit followed; setting it moves the camera to another unit.</summary>
        public Unit Unit
        {
            [LocalSafe]
            get
            {
                return unit;
            }

            set
            {
                unit = value;
                if (running && !paused)
                {
                    InputSystem.World.FollowUnit(Player.Handle, unit.Handle, 0f, 0f);
                }
            }
        }

        /// <summary>The distance from the camera to the unit.</summary>
        public float Distance { [LocalSafe] get; set; }

        /// <summary>The angle of attack, in degrees: 270 looks straight down, 360 straight ahead.</summary>
        public float Angle { [LocalSafe] get; set; }

        /// <summary>How high above the unit the camera looks, on top of the unit's own fly height.</summary>
        public float Height { [LocalSafe] get; set; }

        /// <summary>The rotation wanted now, in degrees.</summary>
        internal abstract float Turn();

        /// <summary>Starts a camera made by Start: ends the player's camera before it, then holds it in the scope; in a scope that has ended it ends at once and changes nothing.</summary>
        internal static T Begin<T>(T made, Scope scope)
            where T : FollowCamera
        {
            var camera = made.Player.Camera();
            camera.following?.End();
            scope.Adopt(made);
            if (made.HasEnded)
            {
                return made;
            }

            camera.following = made;
            made.running = true;
            made.Take();
            made.OwnEvery(Tick, made.Step);
            return made;
        }

        /// <summary>Takes the fields from the player's input, locks the camera on the unit and puts it in place at once.</summary>
        private void Take()
        {
            var world = InputSystem.World;
            world.SetInputControlled(Player.Handle, CameraFields.Distance, false);
            world.SetInputControlled(Player.Handle, CameraFields.Rotation, false);
            world.SetInputControlled(Player.Handle, CameraFields.AngleOfAttack, false);
            world.FollowUnit(Player.Handle, unit.Handle, 0f, 0f);
            Move(0f);
        }

        private void Step()
        {
            if (unit.HasEnded || unit.IsGone)
            {
                End();
                return;
            }

            if (Cinematic.Of(Player) != null)
            {
                if (!paused)
                {
                    paused = true;
                    InputSystem.World.StopFollowing(Player.Handle);
                }

                return;
            }

            if (paused)
            {
                paused = false;
                Take();
                return;
            }

            Move(Tick);
        }

        // The game's lock keeps the target's z at the Z offset whatever the unit flies at (probe 11-09), so the fly height goes in it.
        private void Move(float seconds)
        {
            var world = InputSystem.World;
            world.SetField(Player.Handle, CameraFields.Rotation, Unwrapped(Turn()), seconds);
            world.SetField(Player.Handle, CameraFields.AngleOfAttack, Angle, seconds);
            world.SetField(Player.Handle, CameraFields.Distance, Distance, seconds);
            world.SetField(Player.Handle, CameraFields.ZOffset, unit.FlyHeight + Height, seconds);
        }

        // The turn nearest the last one, so a facing going from 359 to 1 turns 2 degrees, not 358 the other way.
        private float Unwrapped(float turn)
        {
            if (turned)
            {
                while (turn - lastTurn > 180f)
                {
                    turn -= 360f;
                }

                while (turn - lastTurn < -180f)
                {
                    turn += 360f;
                }
            }

            turned = true;
            lastTurn = turn;
            return turn;
        }

        /// <summary>Gives the camera back: the game camera, unless a cinematic has it now, and the player's input as the camera's own locks say.</summary>
        internal override void Finish()
        {
            if (!running)
            {
                return;
            }

            running = false;
            var camera = Player.Camera();
            if (camera.following == this)
            {
                camera.following = null;
            }

            if (!paused)
            {
                InputSystem.World.StopFollowing(Player.Handle);
                InputSystem.World.ResetCamera(Player.Handle, 0f);
            }

            camera.GiveInputBack();
        }
    }

    /// <summary>A camera behind a unit that turns with it, for one player (decision 234); see <see cref="FollowCamera"/>.</summary>
    public sealed class ThirdPersonCamera : FollowCamera
    {
        private ThirdPersonCamera(Player player, Unit unit, float distance, float angle, float height)
            : base(player, unit, distance, angle, height)
        {
        }

        /// <summary>Starts a camera behind a unit for a player, held in a scope; it ends the player's follow camera before it. The angle is the angle of attack in degrees (360 looks straight ahead).</summary>
        public static ThirdPersonCamera Start(Player player, Unit unit, Scope scope, float distance = 600f, float height = 150f, float angle = 345f)
        {
            return Begin(new ThirdPersonCamera(player, unit, distance, angle, height), scope);
        }

        internal override float Turn() => Unit.Facing;
    }

    /// <summary>A camera above a unit at a set rotation, for one player (decision 234); see <see cref="FollowCamera"/>.</summary>
    public sealed class TopDownCamera : FollowCamera
    {
        private TopDownCamera(Player player, Unit unit, float distance, float angle, float height, float rotation)
            : base(player, unit, distance, angle, height)
        {
            Rotation = rotation;
        }

        /// <summary>The rotation in degrees; 90 is the game's own, with north at the top.</summary>
        public float Rotation { [LocalSafe] get; set; }

        /// <summary>Starts a camera above a unit for a player, held in a scope; it ends the player's follow camera before it. The defaults are World Editor's camera (distance 1650, angle 304, rotation 90).</summary>
        public static TopDownCamera Start(Player player, Unit unit, Scope scope, float distance = 1650f, float angle = 304f, float rotation = 90f, float height = 0f)
        {
            return Begin(new TopDownCamera(player, unit, distance, angle, height, rotation), scope);
        }

        internal override float Turn() => Rotation;
    }
}
