using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// What one player sees of a place for a while: shown by player.Reveal, covered in fog or the black mask by
    /// player.Hide (a fog modifier). It is in the scope it was made in and ends when that scope ends, or by End; the
    /// ground stays explored after. Every machine makes it, as each one keeps every player's fog. The game keeps the
    /// oldest of a player's fog modifiers on a place (seen 11-06), so a new vision makes the player's older ones again
    /// after its own, and the newest wins.
    /// </summary>
    public sealed class Vision : Scoped
    {
        /// <summary>The fog states, as the world takes them.</summary>
        internal const int Visible = 0;

        internal const int Fogged = 1;

        internal const int Masked = 2;

        private readonly Player player;

        private readonly int state;

        private readonly bool rect;

        private readonly float x;

        private readonly float y;

        private readonly float radius;

        private readonly float right;

        private readonly float top;

        private readonly bool overUnits;

        private fogmodifier? handle;

        private Vision(Player player, int state, bool rect, float x, float y, float radius, float right, float top, bool overUnits)
        {
            this.player = player;
            this.state = state;
            this.rect = rect;
            this.x = x;
            this.y = y;
            this.radius = radius;
            this.right = right;
            this.top = top;
            this.overUnits = overUnits;
        }

        /// <summary>The player who sees it, or has it hidden.</summary>
        [LocalSafe]
        public Player Player => player;

        /// <summary>The game's fog modifier, for anything Moonwell does not do; null for a vision that ended before the game made it. A newer vision of the player makes a new one.</summary>
        [LocalSafe]
        public fogmodifier? Handle => handle;

        /// <summary>Starts a vision over a circle in a scope; in a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        internal static Vision Circle(Player player, int state, float x, float y, float radius, bool overUnits, Scope scope)
        {
            return Start(new Vision(player, state, false, x, y, radius, 0.0f, 0.0f, overUnits), scope);
        }

        /// <summary>Starts a vision over a rect in a scope, as Circle does.</summary>
        internal static Vision Rect(Player player, int state, float left, float bottom, float right, float top, bool overUnits, Scope scope)
        {
            return Start(new Vision(player, state, true, left, bottom, 0.0f, right, top, overUnits), scope);
        }

        // Ends the player's older fog modifiers, makes the new one, then makes the older ones again, newest first.
        private static Vision Start(Vision vision, Scope scope)
        {
            scope.Adopt(vision);
            if (vision.HasEnded)
            {
                return vision;
            }

            var older = new List<Vision>();
            foreach (var other in LiveVisions.All)
            {
                if (other.player == vision.player)
                {
                    older.Add(other);
                    Runtime.World.EndVision(other.handle!);
                }
            }

            vision.Make();
            for (var i = older.Count - 1; i >= 0; i--)
            {
                older[i].Make();
            }

            LiveVisions.All.Add(vision);
            return vision;
        }

        private void Make()
        {
            handle = rect
                ? Runtime.World.MakeVisionRect(player.Handle, state, x, y, right, top, overUnits)
                : Runtime.World.MakeVision(player.Handle, state, x, y, radius, overUnits);
        }

        /// <summary>Stops and removes the game's fog modifier.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndVision(handle);
                LiveVisions.All.Remove(this);
            }
        }
    }
}
