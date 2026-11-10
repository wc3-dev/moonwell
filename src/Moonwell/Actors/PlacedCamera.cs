namespace Moonwell
{
    /// <summary>A camera an editor placed in the map, by name, from <c>Placed.Cameras</c>. Where the input package is used it converts to <c>CameraSetup</c>.</summary>
    public readonly struct PlacedCamera
    {
        /// <summary>Its name in the editor.</summary>
        public readonly string Name;

        /// <summary>A camera by its name in the editor.</summary>
        [LocalSafe]
        public PlacedCamera(string name)
        {
            Name = name;
        }
    }
}
