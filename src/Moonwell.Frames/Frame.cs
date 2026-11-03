using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A frame of the map's own UI. It is made in the game when it is added, to the screen with Screen.Add or to another
    /// frame with Add, and ends with the scope it was added in; a frame added to another ends with it. A frame for one
    /// player is made on every machine and shown only on that player's. Every read gives what was set, the same on every
    /// machine; sizes and places are in the game's units, the screen's 4:3 middle 0.8 wide and 0.6 high.
    /// </summary>
    public abstract class Frame : Scope
    {
        // The fields are internal, not private, as a map's panel class may name a field of its own the same (see Scope).

        /// <summary>The game's frame; null until the frame is made.</summary>
        internal framehandle? handle;

        /// <summary>The game's frame it was made under; null until the frame is made.</summary>
        internal framehandle? parentHandle;

        /// <summary>The player the frame is shown to; null for everyone.</summary>
        internal Player? player;

        /// <summary>The frames added to this one before it is made, in the order added, made with it and then let go; a frame added once it is made is made at once and not kept here.</summary>
        internal readonly List<Frame> children = new List<Frame>();

        internal FramePoint anchor = FramePoint.Center;

        internal Vector2 offset;

        /// <summary>The points tied to other frames, which replace the anchor; null when the anchor places it.</summary>
        internal List<Tie>? ties;

        /// <summary>The frame it covers, all points; null when none.</summary>
        internal Frame? filled;

        internal float width;

        internal float height;

        internal bool sized;

        internal float scale = 1.0f;

        internal bool visible = true;

        internal bool enabled = true;

        internal bool keepsKeyboard;

        internal int alpha = 255;

        internal int level;

        /// <summary>The kinds of the game's frame events the frame is listened to for, a bit each by the game's number.</summary>
        internal int listened;

        internal List<Action<Player, int>>? wheeled;

        /// <summary>Whether the frame is on the layer over the HUD, as one added with Screen.AddOver, or added to a frame there.</summary>
        internal bool over;

        /// <summary>The frame's tooltip; null unless set.</summary>
        internal Tooltip? tooltip;

        /// <summary>The template the game's frame was made from, which the pool matches; empty until made.</summary>
        internal string madeTemplate = "";

        /// <summary>Internal, as a map's own frame inherits Panel.</summary>
        internal Frame()
        {
        }

        /// <summary>The game's frame, for anything Moonwell does not do; null until the frame is added to the screen, or to a frame that is. Once the frame has ended, the game's frame may be given to the next frame of its kind.</summary>
        [LocalSafe]
        public framehandle? Handle => handle;

        /// <summary>The player the frame is shown to; null when it is shown to everyone. A frame added to another is shown to the other's player.</summary>
        [LocalSafe]
        public Player? Player => player;

        /// <summary>The game's template the frame is made from; empty to make it by its type alone. Read when the frame is made.</summary>
        [LocalSafe]
        public string Template { get; set; } = "";

        /// <summary>The point of the frame that sits on the same point of its parent, moved by Offset; Center unless set. Setting it replaces the frame's points.</summary>
        [LocalSafe]
        public FramePoint Anchor
        {
            get { return anchor; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                anchor = value;
                UseAnchor();
            }
        }

        /// <summary>How far the frame's Anchor sits from its parent's same point, in the game's units; none unless set. Setting it replaces the frame's points.</summary>
        [LocalSafe]
        public Vector2 Offset
        {
            get { return offset; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                offset = value;
                UseAnchor();
            }
        }

        /// <summary>The frame's width in the game's units; 0 unless set, which leaves the size its template gives.</summary>
        [LocalSafe]
        public float Width
        {
            get { return width; }
            set { Size(value, height); }
        }

        /// <summary>The frame's height in the game's units; 0 unless set, which leaves the size its template gives.</summary>
        [LocalSafe]
        public float Height
        {
            get { return height; }
            set { Size(width, value); }
        }

        /// <summary>The frame's scale, with its children's; 1 unless set.</summary>
        [LocalSafe]
        public float Scale
        {
            get { return scale; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                scale = value;
                if (handle != null)
                {
                    FramesSystem.World.ScaleFrame(handle, value);
                }
            }
        }

        /// <summary>Whether the frame is shown, with its children; true unless set. A frame for another player stays hidden on this machine whatever it reads.</summary>
        [LocalSafe]
        public bool Visible
        {
            get { return visible; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                visible = value;
                if (handle != null)
                {
                    FramesSystem.World.ShowFrame(handle, visible && ShownHere());
                }
            }
        }

        /// <summary>Whether the frame takes the mouse, so a click on it stops there; true unless set, and false for a label.</summary>
        [LocalSafe]
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                enabled = value;
                if (handle != null)
                {
                    TakeMouse(value);
                }
            }
        }

        /// <summary>Whether a button, an icon button or a check box keeps the keyboard after a click. Unless set, it gives the keyboard back to the game on the clicking player's machine, so the hotkeys go on, and its tooltip and the cursor blink once (seen); set, nothing blinks, and the game's hotkeys stop until the player clicks the world (Sven, 11-03). False unless set.</summary>
        [LocalSafe]
        public bool KeepsKeyboard
        {
            get { return keepsKeyboard; }
            set { keepsKeyboard = value; }
        }

        /// <summary>The frame's opacity, 0 to 255, kept within them; 255 unless set.</summary>
        [LocalSafe]
        public int Alpha
        {
            get { return alpha; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                alpha = value < 0 ? 0 : value > 255 ? 255 : value;
                if (handle != null)
                {
                    FramesSystem.World.FadeFrame(handle, alpha);
                }
            }
        }

        /// <summary>Which of its siblings the frame is drawn above: a higher level draws above a lower; 0 unless set.</summary>
        [LocalSafe]
        public int Level
        {
            get { return level; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                level = value;
                if (handle != null)
                {
                    FramesSystem.World.LevelFrame(handle, value);
                }
            }
        }

        /// <summary>Runs when a player turns the mouse wheel over the frame, with the player and the notches, up positive. A frame listened to takes the mouse, so a click on it stops there (decision 158). There are no events for the mouse coming onto a frame or going off it: listening for them makes the cursor and a tooltip flicker (seen 11-03). Not inside Local.For.</summary>
        public event Action<Player, int>? Wheel
        {
            add { Subscribe(ref wheeled, value, FrameEvents.Wheel, FrameEvents.Wheel); }
            remove { Unsubscribe(wheeled, value); }
        }

        /// <summary>A tooltip the game shows while a player's mouse is on the frame: made on the frame's layer for the frame's player (the screen's whole width, beneath the HUD, or over the HUD for a frame added with Screen.AddOver), above the frame unless the map places it, and ended with the frame; setting another ends the one before. The frame takes the mouse for it, but no event is sent, as listening to the mouse's enter and leave makes a tooltip and the cursor flicker (seen 11-03). Null unless set.</summary>
        [LocalSafe]
        public Tooltip? Tooltip
        {
            get { return tooltip; }
            set
            {
                if (HasEnded || value == tooltip)
                {
                    return;
                }

                if (value != null && (value.Parent != null || value.HasEnded || value == this))
                {
                    Errors.Refuse(Names.NameOf(this) + ".Tooltip", Names.NameOf(value) + " is another frame's, has ended, or is this frame: a tooltip is one frame's, made once");
                    return;
                }

                var wanted = WantsMouse();
                var before = tooltip;
                tooltip = null;
                before?.End();
                if (value == null)
                {
                    return;
                }

                value.player = player;
                Adopt(value);
                if (value.HasEnded)
                {
                    return;
                }

                tooltip = value;
                if (!wanted)
                {
                    PrepareMouse();
                }

                if (handle != null)
                {
                    MakeTip();
                }
            }
        }

        /// <summary>Whether the frame takes the mouse for the map's sake: it is listened to, or has a tooltip.</summary>
        internal bool WantsMouse()
        {
            return listened != 0 || tooltip != null;
        }

        /// <summary>Makes the tooltip in the slot of the frame's mouse on the frame's layer (Sven, 11-03: the screen's whole width beneath the HUD, or over the HUD), above the frame unless the map placed it; a tooltip taken away, ended or left by an ended frame leaves the slot showing nothing.</summary>
        private void MakeTip()
        {
            var mouse = MouseHandle;
            if (mouse == null)
            {
                return;
            }

            var tip = tooltip!;
            if (tip.ties == null && tip.filled == null && tip.anchor == FramePoint.Center && tip.offset.X == 0.0f && tip.offset.Y == 0.0f)
            {
                tip.PlaceBy(FramePoint.Bottom, this, FramePoint.Top, 0.0f, 0.01f);
            }

            tip.over = over;
            tip.MakeTree(FramesSystem.SlotFor(mouse, over), player);
        }

        /// <summary>The game's frame that takes the frame's mouse and gets its events: its own, or a catcher over it; null until there is one.</summary>
        internal virtual framehandle? MouseHandle => handle;

        /// <summary>The game's frame type the frame is made by when it has no template: a FRAME, which draws nothing, unless the kind says otherwise. Not abstract: CSharp.lua reads an abstract property by its bare name, in its own class, as a field (C45 in docs/upstream.md).</summary>
        internal virtual string GameType => "FRAME";

        /// <summary>Ties a point of the frame to a point of another, moved by an offset, in place of its Anchor; another Attach adds a point, so a frame tied by two points stretches between them. Does nothing once it has ended.</summary>
        public void Attach(FramePoint point, Frame other, FramePoint otherPoint, float x = 0.0f, float y = 0.0f)
        {
            if (HasEnded)
            {
                return;
            }

            var first = ties == null;
            if (first)
            {
                ties = new List<Tie>();
                filled = null;
            }

            var tie = new Tie(point, other, otherPoint, x, y);
            ties!.Add(tie);
            if (handle == null)
            {
                return;
            }

            if (first)
            {
                FramesSystem.World.ClearFramePoints(handle);
            }

            Tie(tie);
        }

        /// <summary>Sets the frame to cover another, all points, in place of its Anchor and its ties. Does nothing once it has ended.</summary>
        public void Fill(Frame other)
        {
            if (HasEnded)
            {
                return;
            }

            filled = other;
            ties = null;
            if (handle != null && other.handle != null)
            {
                FramesSystem.World.ClearFramePoints(handle);
                FramesSystem.World.FillFrame(handle, other.handle);
            }
        }

        /// <summary>Adds a frame to this one and returns it: it is made under this frame, shown to this frame's player, and ends with it. Added to a frame that has ended, it ends at once and nothing is made.</summary>
        public T Add<T>(T child)
            where T : Frame
        {
            Adopt(child);
            if (child.Parent != this || child.HasEnded)
            {
                return child;
            }

            child.player = player;
            child.over = over;
            Adopted(child);
            if (handle != null)
            {
                child.MakeTree(handle, player);
            }
            else
            {
                children.Add(child);
            }

            return child;
        }

        /// <summary>Adds a handler, and listens for its kinds of event; nothing once the frame has ended.</summary>
        internal void Subscribe<T>(ref List<T>? handlers, T? handler, int kind, int other)
            where T : class
        {
            if (HasEnded || handler == null)
            {
                return;
            }

            handlers ??= new List<T>();
            handlers.Add(handler);
            Listen(kind);
            Listen(other);
        }

        /// <summary>Takes a handler away.</summary>
        internal void Unsubscribe<T>(List<T>? handlers, T? handler)
            where T : class
        {
            if (handlers != null && handler != null)
            {
                handlers.Remove(handler);
            }
        }

        /// <summary>Listens for a kind of event: the first makes the frame take the mouse; the game frame is registered once it is made.</summary>
        internal void Listen(int kind)
        {
            var first = listened == 0;
            listened |= 1 << kind;
            if (first)
            {
                PrepareMouse();
            }

            if (handle != null)
            {
                FramesSystem.Events.Own(this);
            }
        }

        /// <summary>Makes the frame take the mouse once it is listened to; a kind that does not take it on its own does more.</summary>
        internal virtual void PrepareMouse()
        {
        }

        /// <summary>Runs an event of the frame's for a player: the kinds every frame has here, the rest in the kinds'.</summary>
        internal virtual void Receive(int kind, Player who, float value, string text)
        {
            if (kind == FrameEvents.Wheel)
            {
                Raise(wheeled, who, (int)(value / 120.0f), "Wheel");
            }
        }

        /// <summary>Gives the keyboard back to the game after a click, on the clicking player's machine only (decision 161): a clicked frame keeps it, and the hotkeys stop (seen). Not for a frame the map has turned off, which giving back would turn on again, nor for one that keeps the keyboard (KeepsKeyboard).</summary>
        internal void GiveKeyboardBack(Player who)
        {
            var mouse = MouseHandle;
            if (enabled && !keepsKeyboard && mouse != null && Runtime.World.IsLocal(who.Handle))
            {
                FramesSystem.World.ReleaseKeyboard(mouse);
            }
        }

        /// <summary>Runs each handler, in the order added; one that throws is reported with the frame and the event, and the rest still run.</summary>
        internal void Raise(List<Action<Player>>? handlers, Player who, string name)
        {
            if (handlers == null || HasEnded)
            {
                return;
            }

            var now = handlers.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i](who);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(this) + "." + name, error);
                }
            }
        }

        /// <summary>Runs each handler with a value, as Raise does.</summary>
        internal void Raise<T>(List<Action<Player, T>>? handlers, Player who, T value, string name)
        {
            if (handlers == null || HasEnded)
            {
                return;
            }

            var now = handlers.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i](who, value);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(this) + "." + name, error);
                }
            }
        }

        /// <summary>Runs when a frame is added to this one, before it is made: a container places it here.</summary>
        internal virtual void Adopted(Frame child)
        {
        }

        /// <summary>Runs when a frame added to this one ends on its own, not with this one: the frame's own tooltip is taken away, and a container closes up.</summary>
        internal virtual void ChildEnded(Frame child)
        {
            if (child == tooltip)
            {
                tooltip = null;
            }
        }

        /// <summary>Ties the frame by one point only, replacing its anchor, ties and what it covers: how a container places the frames added to it.</summary>
        internal void PlaceBy(FramePoint point, Frame other, FramePoint otherPoint, float x, float y)
        {
            ties = null;
            filled = null;
            Attach(point, other, otherPoint, x, y);
        }

        /// <summary>Makes the frame and the frames added to it, then places them all, so a point tied to a frame added later finds it made.</summary>
        internal void MakeTree(framehandle parent, Player? shownTo)
        {
            Make(parent, shownTo);
            Place();
        }

        /// <summary>Makes the frame in the game under a parent, for a player or everyone, taking an ended one of its kind made under the same parent when the pool has one, and then the frames added to it.</summary>
        internal void Make(framehandle parent, Player? shownTo)
        {
            player = shownTo;
            parentHandle = parent;
            var old = FramesSystem.Pool.Take(this, parent);
            if (old == null)
            {
                var made = FramesSystem.World.MakeFrame(GameType, Template, parent);
                madeTemplate = Template;
                if (made == null)
                {
                    Errors.Refuse(Names.NameOf(this) + ".Template", "the game has no frame template named " + Template + " (one from an .fdf needs its TOC loaded): made by its type instead");
                    made = FramesSystem.World.MakeFrame(GameType, "", parent)!;
                    madeTemplate = "";
                }

                handle = made;
                MakeParts();
            }
            else
            {
                handle = old.handle!;
                madeTemplate = old.madeTemplate;
                TakeOver(old);
            }

            if (listened != 0)
            {
                FramesSystem.Events.Own(this);
            }

            if (tooltip != null && !tooltip.HasEnded)
            {
                MakeTip();
            }

            if (player == null && ForOnePlayerOnly())
            {
                Dev.Report(Names.NameOf(this), "a check box, slider or text box is one player's only (decision 156): add it to a frame made for one player, shownTo, so the screens never disagree");
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].HasEnded)
                {
                    children[i].over = over;
                    children[i].Make(handle, player);
                }
            }
        }

        /// <summary>Whether the kind is one player's only, as a control with a value is (decision 156).</summary>
        internal virtual bool ForOnePlayerOnly()
        {
            return false;
        }

        /// <summary>Makes the game's frames a kind keeps under its own, before the frames added to it, so those draw above them.</summary>
        internal virtual void MakeParts()
        {
        }

        /// <summary>Takes an ended frame's game frame, undoing what the ended one set that this one's Place does not set again; Place then gives this one's values.</summary>
        internal virtual void TakeOver(Frame old)
        {
            var world = FramesSystem.World;
            if (old.sized && !sized)
            {
                world.SizeFrame(handle!, 0.0f, 0.0f);
            }

            if (old.scale != 1.0f)
            {
                world.ScaleFrame(handle!, 1.0f);
            }

            if (old.alpha != 255)
            {
                world.FadeFrame(handle!, 255);
            }

            if (old.level != 0)
            {
                world.LevelFrame(handle!, 0);
            }

            if (!old.enabled && enabled)
            {
                TakeMouse(true);
            }

            if (visible && ShownHere())
            {
                world.ShowFrame(handle!, true);
            }
        }

        /// <summary>Whether an ended frame of its kind and template can be given to this one: one sized on a template only to one that sets its own size, as the template's size cannot be given back.</summary>
        internal virtual bool Fits(Frame old)
        {
            return sized || !(old.sized && old.madeTemplate.Length > 0);
        }

        /// <summary>Gives the game whether the frame takes the mouse.</summary>
        internal virtual void TakeMouse(bool on)
        {
            FramesSystem.World.EnableFrame(handle!, on);
        }

        /// <summary>Gives the game the frame's points and looks, then its children's.</summary>
        internal virtual void Place()
        {
            var world = FramesSystem.World;
            PlacePoints();
            if (sized)
            {
                world.SizeFrame(handle!, width, height);
            }

            if (scale != 1.0f)
            {
                world.ScaleFrame(handle!, scale);
            }

            if (!(visible && ShownHere()))
            {
                world.ShowFrame(handle!, false);
            }

            if (!enabled)
            {
                TakeMouse(false);
            }

            if (alpha != 255)
            {
                world.FadeFrame(handle!, alpha);
            }

            if (level != 0)
            {
                world.LevelFrame(handle!, level);
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].HasEnded)
                {
                    children[i].Place();
                }
            }

            children.Clear();
        }

        /// <summary>Gives the game the frame's points: what it covers, its ties, or its Anchor.</summary>
        private void PlacePoints()
        {
            var world = FramesSystem.World;
            world.ClearFramePoints(handle!);
            if (filled != null)
            {
                if (filled.handle != null)
                {
                    world.FillFrame(handle!, filled.handle);
                }

                return;
            }

            if (ties != null)
            {
                for (var i = 0; i < ties.Count; i++)
                {
                    Tie(ties[i]);
                }

                return;
            }

            world.PlaceFrame(handle!, (int)anchor, parentHandle!, (int)anchor, offset.X, offset.Y);
        }

        /// <summary>Gives the game one tie, once the other frame is made.</summary>
        private void Tie(Tie tie)
        {
            if (tie.Other.handle != null)
            {
                FramesSystem.World.PlaceFrame(handle!, (int)tie.Point, tie.Other.handle, (int)tie.OtherPoint, tie.X, tie.Y);
            }
        }

        /// <summary>Drops the ties and what it covers, and places the frame by its Anchor and Offset.</summary>
        private void UseAnchor()
        {
            ties = null;
            filled = null;
            if (handle != null)
            {
                PlacePoints();
            }
        }

        /// <summary>Keeps a size and gives it to the game. Does nothing once it has ended.</summary>
        private void Size(float newWidth, float newHeight)
        {
            if (HasEnded)
            {
                return;
            }

            width = newWidth;
            height = newHeight;
            sized = true;
            if (handle != null)
            {
                FramesSystem.World.SizeFrame(handle, width, height);
            }

            Resized();
        }

        /// <summary>Runs when the map gives the frame a new size: a progress bar refills here.</summary>
        internal virtual void Resized()
        {
        }

        /// <summary>Whether the frame is to be shown on this machine: it is everyone's, or this machine's player's.</summary>
        internal bool ShownHere()
        {
            return player == null || Runtime.World.IsLocal(player.Handle);
        }

        /// <summary>Hides the game's frame and keeps it for the next of its kind: a frame is never destroyed, as destroying one can desync (a claim in the game facts).</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                FramesSystem.World.ShowFrame(handle, false);
                FramesSystem.Events.Disown(this);
                FramesSystem.Pool.Keep(this);
            }

            if (Parent is Frame holder && !holder.ending && !holder.HasEnded)
            {
                holder.ChildEnded(this);
            }
        }
    }

    /// <summary>A point of a frame tied to a point of another, moved by an offset.</summary>
    internal sealed class Tie
    {
        internal Tie(FramePoint point, Frame other, FramePoint otherPoint, float x, float y)
        {
            Point = point;
            Other = other;
            OtherPoint = otherPoint;
            X = x;
            Y = y;
        }

        internal FramePoint Point { get; }

        internal Frame Other { get; }

        internal FramePoint OtherPoint { get; }

        internal float X { get; }

        internal float Y { get; }
    }
}
