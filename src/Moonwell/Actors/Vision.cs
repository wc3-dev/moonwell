namespace Moonwell
{
    /// <summary>
    /// What one player sees of a place for a while: shown by player.Reveal, covered in fog or the black mask by
    /// player.Hide (a fog modifier). It is in the scope it was made in and ends when that scope ends, or by End; the
    /// ground stays explored after. Every machine makes it, as each one keeps every player's fog.
    /// </summary>
    public sealed class Vision : Scoped
    {
        /// <summary>The fog states, as the world takes them.</summary>
        internal const int Visible = 0;

        internal const int Fogged = 1;

        internal const int Masked = 2;

        private readonly Player player;

        private fogmodifier? handle;

        private Vision(Player player)
        {
            this.player = player;
        }

        /// <summary>The player who sees it, or has it hidden.</summary>
        [LocalSafe]
        public Player Player => player;

        /// <summary>The game's fog modifier, for anything Moonwell does not do; null for a vision that ended before the game made it.</summary>
        [LocalSafe]
        public fogmodifier? Handle => handle;

        /// <summary>Starts a vision over a circle in a scope; in a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        internal static Vision Circle(Player player, int state, float x, float y, float radius, bool overUnits, Scope scope)
        {
            var vision = new Vision(player);
            scope.Adopt(vision);
            if (!vision.HasEnded)
            {
                vision.handle = Runtime.World.MakeVision(player.Handle, state, x, y, radius, overUnits);
            }

            return vision;
        }

        /// <summary>Starts a vision over a rect in a scope, as Circle does.</summary>
        internal static Vision Rect(Player player, int state, float left, float bottom, float right, float top, bool overUnits, Scope scope)
        {
            var vision = new Vision(player);
            scope.Adopt(vision);
            if (!vision.HasEnded)
            {
                vision.handle = Runtime.World.MakeVisionRect(player.Handle, state, left, bottom, right, top, overUnits);
            }

            return vision;
        }

        /// <summary>Stops and removes the game's fog modifier.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndVision(handle);
            }
        }
    }
}
