using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A player's own view of the game's HUD, from player.GameUi(): each piece set here changes only that player's screen,
    /// and lasts until set again, as a camera's does. Reads give what was set, the same on every machine; setting is not
    /// allowed inside Local.For. The info panel, the chat, the game's messages and moving the portrait or the hero buttons
    /// are left out, as the game crashes or draws them wrong (seen 10-15 and 11-05).
    /// </summary>
    public sealed class GameUi
    {
        private readonly Player player;

        private framehandle backdrop;

        private readonly List<GamePiece> pieces = new List<GamePiece>();

        private bool visible = true;

        internal GameUi(Player player, HudFrames hud)
        {
            this.player = player;
            backdrop = hud.ConsoleBackdrop;
            Minimap = Keep(new MovablePiece(player, hud.Minimap, false, hud.ConsoleBackdrop));
            MinimapButtons = Plain(hud.MinimapButtons);
            Portrait = Keep(new GamePiece(player, new[] { hud.Portrait }));
            HeroBar = Keep(new GamePiece(player, new[] { hud.HeroBar }));
            HeroButtons = Plain(hud.HeroButtons);
            TopBar = Keep(new GamePiece(player, hud.TopBar));
            CommandButtons = Movable(hud.CommandButtons, true);
            ItemButtons = Movable(hud.ItemButtons, false);
        }

        /// <summary>A saved game loaded: each piece takes its game frames, looked up again on every machine, before the map's frames are made again, so a frame tied to a piece finds it.</summary>
        internal void Rebind(HudFrames hud)
        {
            var fresh = new GameUi(player, hud);
            backdrop = fresh.backdrop;
            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Rebind(fresh.pieces[i]);
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
                Visible = false;
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].Restore();
            }
        }

        /// <summary>Whether the game's HUD shows on the player's screen; true unless set. Hidden, the console's art and the frames over it go, and clicks reach the world down to the window's bottom; the command buttons and life bars stay, as the game leaves them, and the map's own frames still draw (seen 11-05). A piece set Visible again shows (see GamePiece.Visible); shown again, pieces the map hid stay hidden.</summary>
        [LocalSafe]
        public bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                if (!Runtime.World.IsLocal(player.Handle))
                {
                    return;
                }

                var world = FramesSystem.World;
                world.HideHud(!value);

                // Hidden, the console's backdrop still takes the clicks where the console was; shrunk, it lets them through (seen 11-05).
                world.SizeFrame(backdrop, value ? 0.8f : 0.0f, value ? 0.13f : 0.0001f);
                if (!value)
                {
                    return;
                }

                for (var i = 0; i < pieces.Count; i++)
                {
                    if (!pieces[i].visible)
                    {
                        pieces[i].Show(false);
                    }
                }
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

        /// <summary>The seven hero icons.</summary>
        [LocalSafe]
        public IReadOnlyList<GamePiece> HeroButtons { get; }

        /// <summary>The top bar: its buttons, the gold, lumber and food, and the FPS line; hidden, it leaves its black boxes (seen 11-03).</summary>
        [LocalSafe]
        public GamePiece TopBar { get; }

        /// <summary>The twelve command buttons, from the top left, row by row; hidden by a scale of nothing, as the game shows them again at each selection otherwise, and their hotkeys still work (seen 11-03).</summary>
        [LocalSafe]
        public IReadOnlyList<MovablePiece> CommandButtons { get; }

        /// <summary>The six inventory buttons, from the top left, row by row.</summary>
        [LocalSafe]
        public IReadOnlyList<MovablePiece> ItemButtons { get; }

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

        private IReadOnlyList<MovablePiece> Movable(framehandle[] frames, bool hiddenByScale)
        {
            var list = new List<MovablePiece>();
            for (var i = 0; i < frames.Length; i++)
            {
                list.Add(Keep(new MovablePiece(player, frames[i], hiddenByScale, null)));
            }

            return list;
        }
    }
}
