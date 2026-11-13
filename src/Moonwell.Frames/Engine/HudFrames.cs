using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The game's HUD frames, looked up on every machine when the package starts, as a frame's first lookup gives it a handle id (a claim); looked up at main they are the same at the game's start (seen 11-05).</summary>
    internal sealed class HudFrames
    {
        internal readonly framehandle ConsoleBackdrop;

        internal readonly framehandle Minimap;

        internal readonly framehandle Portrait;

        internal readonly framehandle HeroBar;

        internal readonly framehandle[] TopBar;

        internal readonly framehandle[] MinimapButtons;

        internal readonly framehandle[] HeroButtons;

        internal readonly framehandle[] CommandButtons;

        internal readonly framehandle[] ItemButtons;

        /// <summary>The SIMPLEBUTTONs the command buttons sit in, CommandButton_0 to _11: moving a button leaves its one behind, taking clicks (seen 11-13).</summary>
        internal readonly framehandle[] CommandHolders;

        /// <summary>The SIMPLEBUTTONs the item buttons sit in, InventoryButton_0 to _5; with the HUD hidden the item buttons show only once the first is shown (seen 11-12).</summary>
        internal readonly framehandle[] ItemHolders;

        /// <summary>ConsoleUI's child 5, which takes clicks at the command card's place with the HUD hidden (seen 11-13).</summary>
        internal readonly framehandle CardBlocker;

        /// <summary>The hero buttons' glows for unspent skill points, which stay with the HUD hidden (seen 11-12).</summary>
        internal readonly framehandle[] Glows;

        /// <summary>The selection panel: a group's icons, or one unit's details; the parent of SimpleInfoPanelUnitDetail.</summary>
        internal readonly framehandle SelectionPanel;

        /// <summary>SimpleInfoPanelUnitDetail, which SimpleNameValue sits in.</summary>
        internal readonly framehandle UnitDetail;

        /// <summary>SimpleInfoPanelUnitDetail's children: the damage, armour and stat boxes (seen 11-13).</summary>
        internal readonly framehandle[] UnitDetailParts;

        /// <summary>SimpleNameValue, a String: the game crashes on most natives given it (seen 11-13); its points and size are safe, and the unit's class line is tied to it.</summary>
        internal readonly framehandle UnitName;

        /// <summary>SimpleInfoPanelBuildingDetail, the selection panel's child 1, found at create context 1 from the game's start: a building's queue or research, with its name, "Training" line and bar (seen 11-13).</summary>
        internal readonly framehandle BuildingDetail;

        /// <summary>SimpleBuildingNameValue, a String in the building panel, "Anchor TOP" in its .fdf as SimpleNameValue is in the unit panel; looked up only, never touched but by its points.</summary>
        internal readonly framehandle BuildingName;

        /// <summary>InventoryText, a TEXT frame, not a String: it takes an alpha (seen 11-13).</summary>
        internal readonly framehandle InventoryText;

        /// <summary>SimpleInventoryCover, the SIMPLEFRAME whose texture covers the slots of a unit with no inventory; its texture, InventoryCoverTexture, is never looked up, as the game crashes reading it (seen 11-13).</summary>
        internal readonly framehandle InventoryCover;

        /// <summary>Looks them up: the minimap by its name, as its origin frame is nil (seen 11-05), the rest by the game's ORIGIN_FRAME_ numbers or by name.</summary>
        internal HudFrames(IFramesWorld world)
        {
            ConsoleBackdrop = world.GameFrame("ConsoleUIBackdrop")!;
            Minimap = world.GameFrame("MiniMapFrame")!;
            TopBar = new[] { world.GameFrame("ResourceBarFrame")!, world.GameFrame("UpperButtonBarFrame")! };
            Portrait = world.OriginFrame(16, 0)!;
            HeroBar = world.OriginFrame(2, 0)!;
            MinimapButtons = Origins(world, 9, 5);
            HeroButtons = Origins(world, 3, 7);
            CommandButtons = Origins(world, 1, 12);
            ItemButtons = Origins(world, 7, 6);
            CommandHolders = Named(world, "CommandButton_", 12);
            ItemHolders = Named(world, "InventoryButton_", 6);
            CardBlocker = world.ChildAt(world.GameFrame("ConsoleUI")!, 5);
            Glows = Origins(world, 6, 7);
            UnitDetail = world.GameFrame("SimpleInfoPanelUnitDetail")!;
            SelectionPanel = world.ParentOf(UnitDetail);
            UnitDetailParts = new framehandle[world.ChildCount(UnitDetail)];
            for (var i = 0; i < UnitDetailParts.Length; i++)
            {
                UnitDetailParts[i] = world.ChildAt(UnitDetail, i);
            }

            UnitName = world.GameFrame("SimpleNameValue")!;
            BuildingDetail = world.GameFrame("SimpleInfoPanelBuildingDetail", 1)!;
            BuildingName = world.GameFrame("SimpleBuildingNameValue", 1)!;
            InventoryText = world.GameFrame("InventoryText")!;
            InventoryCover = world.GameFrame("SimpleInventoryCover")!;
        }

        /// <summary>Puts a frame far above the screen, where the map that found these put them, on this machine, at no size when sized; the way that frees a game frame's clicks and holds where hiding does not (seen 11-12 and 13). A String is never sized here.</summary>
        internal static void Off(IFramesWorld world, framehandle frame, bool sized)
        {
            world.ClearFramePoints(frame);
            world.PinFrame(frame, (int)FramePoint.Center, 0.4f, 3.0f);
            if (sized)
            {
                world.SizeFrame(frame, 0.0001f, 0.0001f);
            }
        }

        private static framehandle[] Origins(IFramesWorld world, int kind, int count)
        {
            var found = new framehandle[count];
            for (var i = 0; i < count; i++)
            {
                found[i] = world.OriginFrame(kind, i)!;
            }

            return found;
        }

        private static framehandle[] Named(IFramesWorld world, string name, int count)
        {
            var found = new framehandle[count];
            for (var i = 0; i < count; i++)
            {
                found[i] = world.GameFrame(name + i)!;
            }

            return found;
        }
    }
}
