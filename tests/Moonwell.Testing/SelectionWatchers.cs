namespace Moonwell.Testing
{
    /// <summary>A unit type that logs "unit selected 0" or "unit deselected 0" with the player's slot, and how many units that player's Selection holds then.</summary>
    public sealed class SelectedUnit : Unit
    {
        /// <summary>Logs the selection.</summary>
        public override void OnSelected(Player selectingPlayer)
        {
            TestLog.Lines.Add("unit selected " + selectingPlayer.Slot + " holding " + selectingPlayer.Selection.Count);
        }

        /// <summary>Logs the deselection.</summary>
        public override void OnDeselected(Player deselectingPlayer)
        {
            TestLog.Lines.Add("unit deselected " + deselectingPlayer.Slot + " holding " + deselectingPlayer.Selection.Count);
        }
    }

    /// <summary>A unit component that logs "component selected 0" or "component deselected 0".</summary>
    public sealed class SelectedComponent : UnitComponent
    {
        /// <summary>Logs the selection.</summary>
        public override void OnSelected(Player selectingPlayer)
        {
            TestLog.Lines.Add("component selected " + selectingPlayer.Slot);
        }

        /// <summary>Logs the deselection.</summary>
        public override void OnDeselected(Player deselectingPlayer)
        {
            TestLog.Lines.Add("component deselected " + deselectingPlayer.Slot);
        }
    }

    /// <summary>A game component that logs "game selected 0" or "game deselected 0".</summary>
    public sealed class SelectionGame : GameComponent
    {
        /// <summary>Logs the selection.</summary>
        public override void OnUnitSelected(Unit selectedUnit, Player selectingPlayer)
        {
            TestLog.Lines.Add("game selected " + selectingPlayer.Slot);
        }

        /// <summary>Logs the deselection.</summary>
        public override void OnUnitDeselected(Unit deselectedUnit, Player deselectingPlayer)
        {
            TestLog.Lines.Add("game deselected " + deselectingPlayer.Slot);
        }
    }

    /// <summary>A player component that logs "player 0 selected 0" or "player 0 deselected 0": its own player's slot, then the selecting player's.</summary>
    public sealed class SelectionPlayer : PlayerComponent
    {
        /// <summary>Logs the selection.</summary>
        public override void OnUnitSelected(Unit selectedUnit, Player selectingPlayer)
        {
            TestLog.Lines.Add("player " + Player.Slot + " selected " + selectingPlayer.Slot);
        }

        /// <summary>Logs the deselection.</summary>
        public override void OnUnitDeselected(Unit deselectedUnit, Player deselectingPlayer)
        {
            TestLog.Lines.Add("player " + Player.Slot + " deselected " + deselectingPlayer.Slot);
        }
    }
}
