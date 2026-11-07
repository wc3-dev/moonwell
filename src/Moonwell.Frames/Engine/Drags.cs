using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>
    /// Drag and drop (decision 210): a click picks up a draggable frame, and a click on a frame listened to for Dropped drops
    /// it there. Right-click, Esc or a left click on anything else puts it back. A frame gives no mouse down or up (seen
    /// 11-07), so a drag is two clicks. The picks, drops and put-backs come from synced events, the same on every machine;
    /// the icon that follows the cursor is made on every machine, shown and moved on the dragger's alone.
    /// </summary>
    internal sealed class Drags
    {
        /// <summary>How often the icon follows the cursor, in seconds.</summary>
        internal const float FollowEvery = 1.0f / 32.0f;

        /// <summary>The icon's side when the frame held has no size of the map's.</summary>
        internal const float IconSize = 0.04f;

        /// <summary>How long a left release waits for a click on a frame before it puts the frame back: the click comes in the same instant as the release (seen 11-07).</summary>
        internal const float ClickWait = 0.1f;

        /// <summary>The frame each player holds, by slot.</summary>
        private readonly Dictionary<int, Frame> held = new Dictionary<int, Frame>();

        /// <summary>Each player's icon, by slot, made at their first pick.</summary>
        private Dictionary<int, framehandle> icons = new Dictionary<int, framehandle>();

        /// <summary>The slots whose left release waits for a click.</summary>
        private readonly HashSet<int> released = new HashSet<int>();

        private bool listening;

        private Timer? follow;

        /// <summary>Listens for every player's mouse releases and Esc, once: at the first draggable frame or drop target.</summary>
        internal void Listen()
        {
            if (listening)
            {
                return;
            }

            listening = true;
            Moonwell.Events.Listen(GameEvent.Loaded);
            FramesSystem.World.ListenMouseUp(MouseUp);
            FramesSystem.World.ListenEscape(Escape);
        }

        /// <summary>The frame a player holds; null when none.</summary>
        internal Frame? HeldBy(Player who)
        {
            return held.TryGetValue(who.Slot, out var frame) ? frame : null;
        }

        /// <summary>A player clicked a frame: true when the drag took the click (a pick, a drop or a put-back on the frame held), so nothing else runs.</summary>
        internal bool Clicked(Frame frame, Player who)
        {
            released.Remove(who.Slot);
            if (!held.TryGetValue(who.Slot, out var holding))
            {
                if (!frame.draggable)
                {
                    return false;
                }

                Pick(frame, who);
                return true;
            }

            if (frame == holding)
            {
                PutBack(who);
                return true;
            }

            if (frame.dropped != null && frame.dropped.Count > 0)
            {
                PutBack(who);
                frame.Raise(frame.dropped, who, holding, "Dropped");
                return true;
            }

            if (frame.draggable)
            {
                Pick(frame, who);
                return true;
            }

            PutBack(who);
            return false;
        }

        /// <summary>A saved game loaded and took the icons: every drag is put back, and the icons are made anew at the next pick.</summary>
        internal void Loaded()
        {
            held.Clear();
            released.Clear();
            icons = new Dictionary<int, framehandle>();
            follow?.End();
            follow = null;
        }

        private void Pick(Frame frame, Player who)
        {
            var world = FramesSystem.World;
            var icon = IconOf(who);
            world.PaintFrame(icon, frame.DragTexture());
            held[who.Slot] = frame;
            var here = Runtime.World.IsLocal(who.Handle);
            if (here)
            {
                Place(icon, frame);
            }

            world.ShowFrame(icon, here);
            follow ??= Game.Instance.Every(FollowEvery, Follow);
        }

        private void PutBack(Player who)
        {
            released.Remove(who.Slot);
            if (!held.Remove(who.Slot))
            {
                return;
            }

            FramesSystem.World.ShowFrame(icons[who.Slot], false);
            if (held.Count == 0)
            {
                follow?.End();
                follow = null;
            }
        }

        /// <summary>A player's icon: a blank BACKDROP over the HUD, which lets clicks through (seen 11-03), made on every machine at that player's first pick, a synced event, so the handles stay in step.</summary>
        private framehandle IconOf(Player who)
        {
            if (icons.TryGetValue(who.Slot, out var icon))
            {
                return icon;
            }

            var world = FramesSystem.World;
            icon = world.MakeFrame("BACKDROP", "", FramesSystem.OverRoot)!;
            world.LevelFrame(icon, 100);
            icons[who.Slot] = icon;
            return icon;
        }

        /// <summary>Puts the icon under this machine's cursor: pixels from the window's top left to the game's units (seen 11-07).</summary>
        private static void Place(framehandle icon, Frame frame)
        {
            var world = FramesSystem.World;
            float width = world.ClientWidth();
            float height = world.ClientHeight();
            if (width <= 0.0f || height <= 0.0f)
            {
                return;
            }

            var x = 0.4f + (world.MouseScreenX() / width - 0.5f) * 0.6f * width / height;
            var y = 0.6f * (1.0f - world.MouseScreenY() / height);
            var halfWidth = (frame.width > 0.0f ? frame.width : IconSize) / 2.0f;
            var halfHeight = (frame.height > 0.0f ? frame.height : IconSize) / 2.0f;
            world.SpanFrame(icon, x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);
        }

        /// <summary>On every machine: puts back what a player holds once it has ended, and moves this machine's player's icon.</summary>
        private void Follow()
        {
            for (var slot = 0; slot < Player.PlayableSlots; slot++)
            {
                if (!held.TryGetValue(slot, out var frame))
                {
                    continue;
                }

                var who = Player.Get(slot);
                if (frame.HasEnded)
                {
                    PutBack(who);
                }
                else if (Runtime.World.IsLocal(who.Handle))
                {
                    Place(icons[slot], frame);
                }
            }
        }

        private void MouseUp()
        {
            var world = FramesSystem.World;
            var who = Player.Of(world.EventPlayer());
            if (who == null || !held.ContainsKey(who.Slot))
            {
                return;
            }

            if (!world.EventLeftButton())
            {
                PutBack(who);
                return;
            }

            released.Add(who.Slot);
            Game.Instance.After(ClickWait, () =>
            {
                if (released.Contains(who.Slot))
                {
                    PutBack(who);
                }
            });
        }

        private void Escape()
        {
            var who = Player.Of(FramesSystem.World.EventPlayer());
            if (who != null)
            {
                PutBack(who);
            }
        }
    }
}
