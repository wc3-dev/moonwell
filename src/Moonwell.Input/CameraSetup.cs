using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>A camera to apply to a player's camera with <c>Camera.Apply</c>: one placed in World Editor, got by its name with <see cref="Named"/> (the build checks the names against the map's cameras), or one made in code with <see cref="Create"/>.</summary>
    public sealed class CameraSetup
    {
        /// <summary>The game's setup; null for one named before the package started, which is looked up by its name when used, and for one created before it started, until the package makes it.</summary>
        private camerasetup? handle;

        /// <summary>A setup created in code: its numbers as the build writes a World Editor camera's; null for a named one.</summary>
        private readonly float[]? numbers;

        /// <summary>Wraps a setup the game made.</summary>
        internal CameraSetup(string name, camerasetup handle)
        {
            Name = name;
            this.handle = handle;
        }

        /// <summary>Makes a setup named before the package started, looked up by its name when used.</summary>
        private CameraSetup(string name)
        {
            Name = name;
        }

        /// <summary>Makes a setup created in code, from its numbers.</summary>
        private CameraSetup(float[] numbers)
        {
            Name = "";
            this.numbers = numbers;
        }

        /// <summary>The camera's name in World Editor; empty for one created in code.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>The game's setup: for one named before the package started, the installed camera of its name, looked up at each use. Throws ArgumentException when the map has no camera of that name, and InvalidOperationException before the package starts.</summary>
        internal camerasetup Handle
        {
            get { return handle ?? InputSystem.SetupNamed(Name).Handle; }
        }

        /// <summary>A placed camera, as <see cref="Named"/> gives it, so <c>Placed.Cameras.Intro</c> goes where a setup does.</summary>
        [LocalSafe]
        public static implicit operator CameraSetup(PlacedCamera camera)
        {
            return Named(camera.Name);
        }

        /// <summary>
        /// The camera World Editor has under this name. Once the package has started it is the same object each time, and a
        /// name the map has no camera of throws ArgumentException. Named before the package starts (in a field of a component
        /// listed in the map's <c>Components</c>, which is made before the packages start), it gives a setup that is looked up
        /// when applied, and an unknown name throws ArgumentException there.
        /// </summary>
        [LocalSafe]
        public static CameraSetup Named(string name)
        {
            if (!InputSystem.Installed)
            {
                return new CameraSetup(name);
            }

            return InputSystem.SetupNamed(name);
        }

        /// <summary>
        /// Makes a camera in code, looking at (x, y), to apply with <c>Camera.Apply</c>: the fields given are set, the rest
        /// keep the game's. Angles are in degrees. The game's setup is made at once on every machine, or, created before the
        /// package starts (in a field of a component listed in the map's <c>Components</c>), when it starts; making it is game
        /// state, so it is called outside Local.For.
        /// </summary>
        public static CameraSetup Create(float x, float y, float? distance = null, float? angle = null, float? rotation = null, float? fieldOfView = null, float? roll = null, float? zOffset = null, float? farZ = null)
        {
            var values = new List<float> { x, y, 0f };
            Add(values, CameraFields.ZOffset, zOffset);
            Add(values, CameraFields.Rotation, rotation);
            Add(values, CameraFields.AngleOfAttack, angle);
            Add(values, CameraFields.Distance, distance);
            Add(values, CameraFields.Roll, roll);
            Add(values, CameraFields.FieldOfView, fieldOfView);
            Add(values, CameraFields.FarZ, farZ);
            values[2] = (values.Count - 3) / 2;
            values.Add(-1f);
            var made = new CameraSetup(values.ToArray());
            if (InputSystem.Installed)
            {
                made.Make();
            }
            else
            {
                InputSystem.MakeLater(made);
            }

            return made;
        }

        /// <summary>Makes the game's setup of one created in code.</summary>
        internal void Make()
        {
            handle = InputSystem.World.MakeSetup(numbers!, 0);
        }

        private static void Add(List<float> values, int field, float? value)
        {
            if (value != null)
            {
                values.Add(field);
                values.Add(value.Value);
            }
        }
    }
}
