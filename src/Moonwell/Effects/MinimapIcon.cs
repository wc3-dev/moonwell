namespace Moonwell
{
    /// <summary>
    /// An icon on the minimap, at a place or on a unit, which it follows; it shows over the fog and the black mask, as the
    /// campaign's quest icons do. It is in the scope it was made in and ends when that scope ends, or by End; one on a
    /// unit also ends when the unit ends. Given shownTo, only that player sees it: it is made on every machine and hidden
    /// at the others. Every read gives what was made, the same on every machine.
    /// </summary>
    public sealed class MinimapIcon : Scoped
    {
        /// <summary>The quest giver's icon, an exclamation mark.</summary>
        public const string QuestGiver = "UI\\Minimap\\MiniMap-QuestGiver.mdl";

        /// <summary>A main quest's objective.</summary>
        public const string QuestObjective = "UI\\Minimap\\Minimap-QuestObjectivePrimary.mdl";

        /// <summary>An optional quest's objective.</summary>
        public const string QuestBonus = "UI\\Minimap\\Minimap-QuestObjectiveBonus.mdl";

        /// <summary>Where a quest is turned in.</summary>
        public const string QuestTurnIn = "UI\\Minimap\\Minimap-QuestTurnIn.mdl";

        /// <summary>A boss, a skull.</summary>
        public const string Boss = "UI\\Minimap\\MiniMap-Boss.mdl";

        /// <summary>A control point.</summary>
        public const string ControlPoint = "UI\\Minimap\\MiniMap-ControlPoint.mdl";

        private readonly string path;

        private readonly Color color;

        private readonly Unit? carrier;

        private readonly float x;

        private readonly float y;

        private readonly Player? shownTo;

        private minimapicon? handle;

        private MinimapIcon(string path, Color color, Unit? carrier, float x, float y, Player? shownTo)
        {
            this.path = path;
            this.color = color;
            this.carrier = carrier;
            this.x = x;
            this.y = y;
            this.shownTo = shownTo;
        }

        /// <summary>The icon's model, as it was made: one of the constants here, or any model path.</summary>
        [LocalSafe]
        public string Path => path;

        /// <summary>The colour the icon was made in; its alpha is not used.</summary>
        [LocalSafe]
        public Color Color => color;

        /// <summary>The unit the icon is on; null for one at a place.</summary>
        [LocalSafe]
        public Unit? Unit => carrier;

        /// <summary>The icon's place: where it was made, or its unit's place.</summary>
        [LocalSafe]
        public float X => carrier != null ? carrier.X : x;

        /// <summary>The icon's place: where it was made, or its unit's place.</summary>
        [LocalSafe]
        public float Y => carrier != null ? carrier.Y : y;

        /// <summary>The game's minimap icon, for anything Moonwell does not do; null for one that ended before the game made it.</summary>
        [LocalSafe]
        public minimapicon? Handle => handle;

        /// <summary>Puts an icon of a model on the minimap at a place, in a colour, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it. In a scope that has ended it ends at once, and the game is not asked to make it.</summary>
        public static MinimapIcon Create(string path, float x, float y, Color color, Scope scope, Player? shownTo = null)
        {
            var icon = new MinimapIcon(path, color, null, x, y, shownTo);
            scope.Adopt(icon);
            if (!icon.HasEnded)
            {
                icon.handle = Runtime.World.MakeIcon(path, x, y, color.R, color.G, color.B);
                icon.Hide();
            }

            return icon;
        }

        /// <summary>Whether a player sees the icon: the one it was shown to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Puts an icon on a unit's place on the minimap, which follows it, and lists it on the unit. On a unit that is gone, or in a scope that has ended, it ends at once and the game is not asked to make it.</summary>
        internal static MinimapIcon On(Unit unit, string path, Color color, Scope scope, Player? shownTo)
        {
            var icon = new MinimapIcon(path, color, unit, 0.0f, 0.0f, shownTo);
            scope.Adopt(icon);
            if (!icon.HasEnded && unit.IsGone)
            {
                icon.End();
            }

            if (!icon.HasEnded)
            {
                icon.handle = Runtime.World.MakeIconOn(path, unit.Handle, color.R, color.G, color.B);
                icon.Hide();
                unit.Attach(icon);
            }

            return icon;
        }

        /// <summary>Hides the icon at the machines whose player it is not shown to.</summary>
        private void Hide()
        {
            if (shownTo != null && !Runtime.World.IsLocal(shownTo.Handle))
            {
                Runtime.World.ShowIcon(handle!, false);
            }
        }

        /// <summary>Removes the game's icon and takes it off its unit.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndIcon(handle);
            }

            carrier?.Detach(this);
        }
    }
}
