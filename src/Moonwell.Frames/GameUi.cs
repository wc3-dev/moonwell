using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A player's own view of the game's HUD, from player.GameUi(): each piece set here changes only that player's screen,
    /// and lasts until set again, as a camera's does. Reads give what was set, the same on every machine; setting is not
    /// allowed inside Local.For. What the game shows or places again by itself is set again every second on the player's
    /// machine. The chat, the game's messages and moving the portrait or the hero buttons are left out, as the game crashes
    /// or draws them wrong (seen 10-15 and 11-05).
    /// </summary>
    public sealed class GameUi
    {
        private readonly Player player;

        private HudFrames hud;

        private readonly List<GamePiece> pieces = new List<GamePiece>();

        private bool visible = true;

        /// <summary>Whether this machine has the command card's click blocker hidden.</summary>
        private bool blockerOff;

        /// <summary>Whether this machine has each hero button's glow off the screen.</summary>
        private readonly bool[] glowsOff;

        internal GameUi(Player player, HudFrames hud)
        {
            this.player = player;
            this.hud = hud;
            glowsOff = new bool[hud.Glows.Length];
            Minimap = Keep(new MovablePiece(player, hud.Minimap, false, hud.ConsoleBackdrop));
            MinimapButtons = Plain(hud.MinimapButtons);
            Portrait = Keep(new GamePiece(player, new[] { hud.Portrait }));
            HeroBar = Keep(new GamePiece(player, new[] { hud.HeroBar }));
            HeroButtons = Plain(hud.HeroButtons);
            TopBar = Keep(new GamePiece(player, hud.TopBar));
            CommandButtons = Movable(hud.CommandButtons, hud.CommandHolders, true);
            ItemButtons = Movable(hud.ItemButtons, hud.ItemHolders, false);
            SelectionIcons = Keep(new MovablePiece(player, hud.SelectionPanel, false, null, null, true));
            UnitDetails = Keep(new HeldPiece(player, hud.UnitDetailParts, HeldPiece.Way.Scale, null));
            UnitName = Keep(new HeldPiece(player, new[] { hud.UnitName }, HeldPiece.Way.Points, hud.UnitDetail));
            InventoryText = Keep(new HeldPiece(player, new[] { hud.InventoryText }, HeldPiece.Way.Alpha, null));
            InventoryCover = Keep(new HeldPiece(player, new[] { hud.InventoryCover }, HeldPiece.Way.Scale, null));
        }

        /// <summary>A saved game loaded: each piece takes its game frames, looked up again on every machine, before the map's frames are made again, so a frame tied to a piece finds it.</summary>
        internal void Rebind(HudFrames fresh)
        {
            var made = new GameUi(player, fresh);
            hud = fresh;
            blockerOff = false;
            for (var i = 0; i < glowsOff.Length; i++)
            {
                glowsOff[i] = false;
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Rebind(made.pieces[i]);
            }
        }

        /// <summary>After the map's frames are made again, the player's machine sets the HUD again as the map left it, as the load may give it back as the game starts it; a piece tied to a frame follows the frame made again.</summary>
        internal void Restore()
        {
            if (!Runtime.World.IsLocal(player.Handle))
            {
                return;
            }

            if (!visible)
            {
                Apply();
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Restore();
            }

            Hold();
        }

        /// <summary>Whether the game's HUD shows on the player's screen; true unless set. Hidden, the console's art and the frames over it go, clicks reach the world down to the window's bottom, nothing left behind takes them (the command card's blocker is hidden), and the map's own frames still draw; the command buttons and life bars stay, as the game leaves them (seen 11-05 and 11-13). A piece set Visible again shows over it (see GamePiece.Visible); shown again, pieces the map hid stay hidden.</summary>
        [LocalSafe]
        public bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                if (!value)
                {
                    for (var i = 0; i < pieces.Count; i++)
                    {
                        pieces[i].overHud = false;
                    }
                }

                if (Runtime.World.IsLocal(player.Handle))
                {
                    Apply();
                }

                Held();
            }
        }

        /// <summary>The minimap, which can be moved: given a map's frame to sit by, it draws over the console's art.</summary>
        [LocalSafe]
        public MovablePiece Minimap { get; }

        /// <summary>The five buttons beside the minimap.</summary>
        [LocalSafe]
        public IReadOnlyList<GamePiece> MinimapButtons { get; }

        /// <summary>The portrait of the selected unit.</summary>
        [LocalSafe]
        public GamePiece Portrait { get; }

        /// <summary>The bar of hero icons at the top left, with its buttons.</summary>
        [LocalSafe]
        public GamePiece HeroBar { get; }

        /// <summary>The seven hero icons; hidden, or with the whole HUD hidden, each one's glow for unspent skill points is put off the screen too, as hiding the glow does not hold (seen 11-12).</summary>
        [LocalSafe]
        public IReadOnlyList<GamePiece> HeroButtons { get; }

        /// <summary>The top bar: its buttons, the gold, lumber and food, and the FPS line; hidden, it leaves its black boxes (seen 11-03).</summary>
        [LocalSafe]
        public GamePiece TopBar { get; }

        /// <summary>The twelve command buttons, from the top left, row by row; hidden by a scale of nothing, as the game shows them again at each selection otherwise, and their hotkeys still work (seen 11-03). A moved one leaves nothing behind taking clicks.</summary>
        [LocalSafe]
        public IReadOnlyList<MovablePiece> CommandButtons { get; }

        /// <summary>The six inventory buttons, from the top left, row by row. With the whole HUD hidden, set Visible they show alone: the inventory's text and cover stay hidden unless set Visible too. A moved one leaves nothing behind taking clicks.</summary>
        [LocalSafe]
        public IReadOnlyList<MovablePiece> ItemButtons { get; }

        /// <summary>The selection panel: a group's icons, with life and mana. Moved, it takes no clicks: tied at no size, a group's icons draw up and left of its point (three icons with their bars from about 0.093 to 0.002 left of it and 0.064 to 0.099 above it), and one unit's name and class line hang centred from it (seen 11-13). It shows one unit's details too, unless UnitDetails and UnitName are hidden. The inventory's bar sits in the panel, so moving the icons moves the inventory's text with them (seen 11-13), and the item buttons not moved too.</summary>
        [LocalSafe]
        public MovablePiece SelectionIcons { get; }

        /// <summary>One unit's damage, armour and stat boxes in the selection panel.</summary>
        [LocalSafe]
        public GamePiece UnitDetails { get; }

        /// <summary>One unit's name in the selection panel, with its class or level line and the hero's experience bar, which are tied to it.</summary>
        [LocalSafe]
        public GamePiece UnitName { get; }

        /// <summary>The inventory's "Inventory" text.</summary>
        [LocalSafe]
        public GamePiece InventoryText { get; }

        /// <summary>The console's stone art over the slots of a unit with no inventory.</summary>
        [LocalSafe]
        public GamePiece InventoryCover { get; }

        /// <summary>Whether a piece shows now on its player's screen: set Visible, and the whole HUD shown or the piece set Visible over it.</summary>
        internal bool Shows(GamePiece piece)
        {
            return piece.visible && (visible || piece.overHud);
        }

        /// <summary>Sets the HUD on this machine as the map left it, and starts setting again every second what the game puts back.</summary>
        internal void Held()
        {
            FramesSystem.HoldHud();
            Hold();
        }

        /// <summary>Sets again, on the player's machine, what the game shows or places again by itself.</summary>
        internal void Hold()
        {
            if (!Runtime.World.IsLocal(player.Handle))
            {
                return;
            }

            var world = FramesSystem.World;
            if (!visible)
            {
                // Hidden alone or moved alone it took clicks again later; both held (seen 11-13).
                world.ShowFrame(hud.CardBlocker, false);
                HudFrames.Off(world, hud.CardBlocker, true);
                blockerOff = true;
                for (var i = 0; i < ItemButtons.Count; i++)
                {
                    if (Shows(ItemButtons[i]))
                    {
                        // With the HUD hidden the item buttons show only once their first holder is shown (seen 11-12).
                        world.ShowFrame(hud.ItemHolders[0], true);
                        break;
                    }
                }
            }

            for (var i = 0; i < glowsOff.Length; i++)
            {
                if (!Shows(HeroButtons[i]))
                {
                    HudFrames.Off(world, hud.Glows[i], false);
                    glowsOff[i] = true;
                }
                else if (glowsOff[i])
                {
                    glowsOff[i] = false;
                    world.ClearFramePoints(hud.Glows[i]);
                    world.PlaceFrame(hud.Glows[i], (int)FramePoint.Center, hud.HeroButtons[i], (int)FramePoint.Center, 0.0f, 0.0f);
                }
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Hold(this);
            }
        }

        /// <summary>Hides or shows the whole HUD on this machine, as set.</summary>
        private void Apply()
        {
            var world = FramesSystem.World;
            world.HideHud(!visible);

            // Hidden, the console's backdrop still takes the clicks where the console was; shrunk, it lets them through (seen 11-05).
            world.SizeFrame(hud.ConsoleBackdrop, visible ? 0.8f : 0.0f, visible ? 0.13f : 0.0001f);
            if (!visible)
            {
                return;
            }

            if (blockerOff)
            {
                blockerOff = false;
                world.ShowFrame(hud.CardBlocker, true);
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i].visible)
                {
                    pieces[i].Show(false);
                }
            }
        }

        private T Keep<T>(T piece)
            where T : GamePiece
        {
            pieces.Add(piece);
            return piece;
        }

        private IReadOnlyList<GamePiece> Plain(framehandle[] frames)
        {
            var list = new List<GamePiece>();
            for (var i = 0; i < frames.Length; i++)
            {
                list.Add(Keep(new GamePiece(player, new[] { frames[i] })));
            }

            return list;
        }

        private IReadOnlyList<MovablePiece> Movable(framehandle[] frames, framehandle[] holders, bool hiddenByScale)
        {
            var list = new List<MovablePiece>();
            for (var i = 0; i < frames.Length; i++)
            {
                list.Add(Keep(new MovablePiece(player, frames[i], hiddenByScale, null, holders[i])));
            }

            return list;
        }
    }
}
