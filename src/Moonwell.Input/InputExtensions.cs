namespace Moonwell
{
    /// <summary>What a player gives for input.</summary>
    public static class InputExtensions
    {
        /// <summary>The player's mouse: one for each slot, from 0 to 27, made when the package starts. Throws InvalidOperationException before that.</summary>
        [LocalSafe]
        public static Mouse Mouse(this Player player)
        {
            return InputSystem.MouseOf(player.Slot);
        }

        /// <summary>The player's keyboard: one for each slot, from 0 to 27, made when the keyboard is installed, which the build does in a map whose code names Keyboard or IKeyWatch. Throws InvalidOperationException before that.</summary>
        [LocalSafe]
        public static Keyboard Keyboard(this Player player)
        {
            return InputSystem.KeyboardOf(player.Slot);
        }

        /// <summary>The player's camera: one for each slot, from 0 to 27, made when the package starts. Throws InvalidOperationException before that.</summary>
        [LocalSafe]
        public static Camera Camera(this Player player)
        {
            return InputSystem.CameraOf(player.Slot);
        }

        /// <summary>The cameras of a cinematic's watchers: a change goes to every person watching (decision 208).</summary>
        [LocalSafe]
        public static CinematicCamera Camera(this Cinematic cinematic)
        {
            return new CinematicCamera(cinematic);
        }
    }
}
