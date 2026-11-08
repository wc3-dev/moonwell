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

        /// <summary>The frames tied to this one or covering it, placed again when it is made; ended ones are dropped as they are met.</summary>
        internal List<Frame>? tiedHere;

        /// <summary>Whether a frame inside this one was kept out of the pool for a live frame tied to it, so this one is too: given out, its game frame would move the one inside (review G6).</summary>
        internal bool holdsTie;

        internal float width;

        internal float height;

        internal bool sized;

        /// <summary>The size the game gave the frame from its template, read as it was made: this machine's own reading, given to the game for a side the map leaves at 0 and never read back; 0 by 0 for a frame made by type.</summary>
        internal float madeWidth, madeHeight;

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

        /// <summary>The frames found in this one's template by name, each one object; null until one is found. A frame with found ones is not given out again.</summary>
        internal Dictionary<string, Frame>? found;

        /// <summary>Whether the frame was found in its window's template, not made: it is never given out on its own.</summary>
        internal bool foundInTemplate;

        /// <summary>The name it was found by in its window's template, to be found again after a load; empty for a frame made.</summary>
        internal string foundName = "";

        /// <summary>Whether the frame was added to the screen, so it is made again when a saved game loads.</summary>
        internal bool rooted;

        /// <summary>Internal, as a map's own frame inherits Panel.</summary>
        internal Frame()
        {
        }

        /// <summary>The game's frame, for anything Moonwell does not do; null until the frame is added to the screen, or to a frame that is. Once the frame has ended, the game's frame may be given to the next frame of its kind. A saved game's load makes a new one, with what Moonwell set: redo what the map set itself in GameComponent.OnGameLoaded.</summary>
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

        internal bool draggable;

        internal string dragIcon = "";

        internal List<Action<Player, Frame>>? dropped;

        /// <summary>Whether a player can pick the frame up with a click, to drop it on a frame listened to for Dropped with another click (decision 210); false unless set. While it is held, its DragIcon follows that player's cursor, on their screen only. Right-click, Esc, a click on it again or a left click on anything else puts it back. A click that picks up or drops runs nothing else, as Clicked does not. A frame with no icon to draw, as an icon button with none, is not picked up: its click is a click.</summary>
        [LocalSafe]
        public bool Draggable
        {
            get { return draggable; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                draggable = value;
                if (value)
                {
                    Listen(FrameEvents.Click);
                    FramesSystem.Drags.Listen();
                }
            }
        }

        /// <summary>The texture that follows the cursor while the frame is held, the frame's size or 0.04 square; empty unless set, which draws an icon button's Icon or an image's Texture.</summary>
        [LocalSafe]
        public string DragIcon
        {
            get { return dragIcon; }
            set { dragIcon = value; }
        }

        /// <summary>Runs when a player drops a frame they hold on this one, with the player and the frame dropped; the frame dropped is put back first, and a click that drops runs nothing else. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, Frame>? Dropped
        {
            add
            {
                Subscribe(ref dropped, value, FrameEvents.Click, FrameEvents.Click);
                FramesSystem.Drags.Listen();
            }

            remove { Unsubscribe(dropped, value); }
        }

        /// <summary>The texture that follows the cursor while the frame is held.</summary>
        internal virtual string DragTexture()
        {
            return dragIcon;
        }

        /// <summary>A tooltip the game shows while a player's mouse is on the frame: made on the frame's layer for the frame's player (the screen's whole width, beneath the HUD, or over the HUD for a frame added with Screen.AddOver), above the frame unless the map places it (or below it, kept in at the screen's sides, where there is no room as it shows), and ended with the frame; setting another ends the one before. The frame takes the mouse for it, but no event is sent, as listening to the mouse's enter and leave makes a tooltip and the cursor flicker (seen 11-03). Null unless set.</summary>
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
                    if (wanted)
                    {
                        LetMouseGo();
                    }

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

        /// <summary>
        /// Swaps two frames' tooltips without ending them, for a kit slot's SwapWith (Sven, 11-07): each goes to the other
        /// frame, a made one moved into that frame's slot, and what was tied to the frame it left is tied to the one it joins.
        /// A made tooltip cannot go to a frame not made: that is refused and nothing changes.
        /// </summary>
        internal static void SwapTooltips(Frame a, Frame b)
        {
            var tipA = a.tooltip;
            var tipB = b.tooltip;
            if (a == b || tipA == tipB || a.HasEnded || b.HasEnded)
            {
                return;
            }

            if ((tipA?.handle != null && b.MouseHandle == null) || (tipB?.handle != null && a.MouseHandle == null))
            {
                Errors.Refuse(Names.NameOf(a) + ".SwapWith", "a tooltip on the screen cannot go to a frame that is not: add both to the screen first");
                return;
            }

            a.GiveUpTooltip();
            b.GiveUpTooltip();
            b.TakeTooltip(tipA, a);
            a.TakeTooltip(tipB, b);
        }

        // Out of the tree, but not ended: its game frame stays where it is until the frame it goes to takes it.
        private void GiveUpTooltip()
        {
            var tip = tooltip;
            if (tip == null)
            {
                return;
            }

            tooltip = null;
            Unlink(tip);
            tip.Parent = null;
            if (!WantsMouse())
            {
                LetMouseGo();
            }
        }

        private void TakeTooltip(Tooltip? tip, Frame from)
        {
            if (tip == null)
            {
                return;
            }

            tip.Retie(from, this);
            if (tip.handle == null)
            {
                Tooltip = tip;
                return;
            }

            var wanted = WantsMouse();
            tip.player = player;
            Adopt(tip);
            tooltip = tip;
            if (!wanted)
            {
                PrepareMouse();
            }

            var slot = FramesSystem.SlotFor(MouseHandle!, over);
            FramesSystem.World.ParentFrame(tip.handle, slot);
            tip.parentHandle = slot;
            tip.over = over;
            tip.PlacePoints();
        }

        // The ties and cover to one frame, made to another.
        private void Retie(Frame from, Frame to)
        {
            if (filled == from)
            {
                filled = to;
                to.TiedBy(this);
            }

            if (ties == null)
            {
                return;
            }

            for (var i = 0; i < ties.Count; i++)
            {
                var tie = ties[i];
                if (tie.Other == from)
                {
                    ties[i] = new Tie(tie.Point, to, null, tie.OtherPoint, tie.X, tie.Y);
                    to.TiedBy(this);
                }
            }
        }

        /// <summary>Whether the frame takes the mouse for the map's sake: it is listened to, or has a tooltip.</summary>
        internal bool WantsMouse()
        {
            return listened != 0 || tooltip != null;
        }

        /// <summary>Gives the game again whether the frame takes the mouse once nothing the map set wants it, so a catcher over an empty panel or an image lets clicks through again.</summary>
        private void LetMouseGo()
        {
            if (handle != null && !WantsMouse())
            {
                TakeMouse(enabled);
            }
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
                tip.PlaceBy(FramePoint.Bottom, this, FramePoint.Top, 0.0f, Tips.Gap);
                FramesSystem.Tips.Watch(tip);
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
            if (!Ended(other, "Attach") && AddTie(new Tie(point, other, null, otherPoint, x, y)))
            {
                other.TiedBy(this);
            }
        }

        /// <summary>Ties a point of the frame to a point of a piece of the game's HUD, from player.GameUi(), as Attach to a frame does: on each machine, to that machine's own piece, wherever its player has put it.</summary>
        public void Attach(FramePoint point, GamePiece piece, FramePoint piecePoint, float x = 0.0f, float y = 0.0f)
        {
            AddTie(new Tie(point, null, piece, piecePoint, x, y));
        }

        /// <summary>Adds a tie in place of the Anchor, or to the ties there are, and gives it to the game; false once the frame has ended.</summary>
        private bool AddTie(Tie tie)
        {
            if (HasEnded)
            {
                return false;
            }

            var first = ties == null;
            if (first)
            {
                ties = new List<Tie>();
                filled = null;
            }

            ties!.Add(tie);
            if (handle == null)
            {
                return true;
            }

            if (first)
            {
                FramesSystem.World.ClearFramePoints(handle);
            }

            Tie(tie);
            return true;
        }

        /// <summary>Whether a frame to tie to or cover has ended, reported when it has: its game frame may be given to the next frame of its kind, which the tie would follow (review G6).</summary>
        private bool Ended(Frame other, string what)
        {
            if (HasEnded || !other.HasEnded)
            {
                return false;
            }

            Errors.Refuse(Names.NameOf(this) + "." + what, Names.NameOf(other) + " has ended: its game frame may be given to the next frame of its kind");
            return true;
        }

        /// <summary>Sets the frame to cover another, all points, in place of its Anchor and its ties. Does nothing once it has ended; refused for a frame that has ended, or that covers this one.</summary>
        public void Fill(Frame other)
        {
            if (HasEnded || Ended(other, "Fill"))
            {
                return;
            }

            for (Frame? covered = other; covered != null; covered = covered.filled)
            {
                if (covered == this)
                {
                    Errors.Refuse(Names.NameOf(this) + ".Fill", Names.NameOf(other) + " is this frame, or covers it");
                    return;
                }
            }

            filled = other;
            ties = null;
            other.TiedBy(this);
            if (handle != null)
            {
                PlacePoints();
            }

            Resized();
        }

        /// <summary>Keeps a frame that ties itself to this one or covers it.</summary>
        private void TiedBy(Frame frame)
        {
            tiedHere ??= new List<Frame>();
            if (!tiedHere.Contains(frame))
            {
                tiedHere.Add(frame);
            }
        }

        /// <summary>Whether the frame is tied to another or covers it.</summary>
        private bool TiedTo(Frame other)
        {
            if (filled == other)
            {
                return true;
            }

            if (ties != null)
            {
                for (var i = 0; i < ties.Count; i++)
                {
                    if (ties[i].Other == other)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether a frame not ending, nor in an ending tree, is still tied to this one or to a frame inside it (review G6); one not made yet too, as it ties to this one's game frame when it is made.</summary>
        private bool HeldByLive()
        {
            if (holdsTie)
            {
                return true;
            }

            if (tiedHere == null)
            {
                return false;
            }

            for (var i = 0; i < tiedHere.Count; i++)
            {
                var frame = tiedHere[i];
                if (!frame.TiedTo(this))
                {
                    continue;
                }

                var ending = false;
                for (Scoped? scope = frame; scope != null; scope = scope.Parent)
                {
                    if (scope.ending || scope.HasEnded)
                    {
                        ending = true;
                        break;
                    }
                }

                if (!ending)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Adds a frame to this one and returns it: it is made under this frame, shown to this frame's player, and ends with it. Added to a frame that has ended, it ends at once and nothing is made.</summary>
        public T Add<T>(T child)
            where T : Frame
        {
            var added = child.Parent != null;
            Adopt(child);
            if (added || child.Parent != this || child.HasEnded)
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

        /// <summary>
        /// A frame inside this one's template, found by its name in the .fdf at any depth, as a T: a window from the map's own
        /// .fdf comes whole, and its parts are reached this way, window.Child&lt;Label&gt;("ShopTitle"). It is the same object at
        /// each call, is shown to this frame's player, and ends with this frame; the frames T's constructor adds are made in
        /// it. Null, and reported, when this frame is not added yet or has ended, has no frame of that name, the one of that
        /// name has ended or was found as another kind, or T is drawn from parts of Moonwell's own (CheckBox, IconButton,
        /// ProgressBar). This frame is then not given out again when it ends, as the game cannot give back what was set on
        /// its parts.
        /// </summary>
        public T? Child<T>(string name)
            where T : Frame, new()
        {
            Frame? known = null;
            if (found != null && found.TryGetValue(name, out known) && !known.HasEnded && known is T kept)
            {
                return kept;
            }

            var what = Names.NameOf(this) + ".Child(\"" + name + "\")";
            if (handle == null || HasEnded)
            {
                Errors.Refuse(what, "the frame is not added yet, or has ended");
                return null;
            }

            if (known != null)
            {
                Errors.Refuse(what, known.HasEnded ? "the frame of that name has ended" : "it was found as " + Names.NameOf(known) + " before");
                return null;
            }

            var child = new T();
            if (child.DrawnFromParts())
            {
                Errors.Refuse(what, Names.NameOf(child) + " is drawn from parts of Moonwell's own; find the template's frame as another kind");
                return null;
            }

            var match = Find(handle, name);
            if (match == null)
            {
                Errors.Refuse(what, "no frame named " + name + " in its template");
                return null;
            }

            child.handle = match;
            child.parentHandle = handle;
            child.player = player;
            child.over = over;
            child.foundInTemplate = true;
            child.foundName = name;
            Adopt(child);
            found ??= new Dictionary<string, Frame>();
            found[name] = child;
            if (child.listened != 0)
            {
                FramesSystem.Events.Own(child);
            }

            if (!child.enabled)
            {
                child.TakeMouse(false);
            }

            child.MakeChildren();
            child.PlaceChildren();
            return child;
        }

        /// <summary>The first frame of a name under a game frame, depth first, as the template made them.</summary>
        private static framehandle? Find(framehandle frame, string name)
        {
            var world = FramesSystem.World;
            var count = world.ChildCount(frame);
            for (var i = 0; i < count; i++)
            {
                var child = world.ChildAt(frame, i);
                if (world.NameOf(child) == name)
                {
                    return child;
                }

                var deeper = Find(child, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }

        /// <summary>Whether the kind draws itself from parts it makes, so it cannot be found in a template.</summary>
        internal virtual bool DrawnFromParts()
        {
            return false;
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
                LetMouseGo();
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
                if (madeTemplate.Length > 0)
                {
                    // A template's size reads at once; a size the game works out from points reads only later (seen 11-05).
                    madeWidth = FramesSystem.World.FrameWidth(made);
                    madeHeight = FramesSystem.World.FrameHeight(made);
                }

                MakeParts();
            }
            else
            {
                handle = old.handle!;
                madeTemplate = old.madeTemplate;
                madeWidth = old.madeWidth;
                madeHeight = old.madeHeight;
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

            MakeChildren();
            if (found != null)
            {
                FindAgain();
            }
        }

        /// <summary>
        /// Forgets the game's frames a saved game's load took (game facts, 11-06), this one's and those inside it, and lines up
        /// the frames made under it to be made again with it, in the order added. Its tooltip is made again with it, and the
        /// frames found in its template are found again (FindAgain); a frame added to the screen in its scope is made again as
        /// the screen's.
        /// </summary>
        internal void Forget()
        {
            var was = handle;
            handle = null;
            ForgetParts();
            children.Clear();
            for (var item = Oldest(); item != null; item = item.after)
            {
                if (!(item is Frame frame) || frame.HasEnded || frame.handle == null)
                {
                    continue;
                }

                if (frame == tooltip || frame.foundInTemplate)
                {
                    frame.Forget();
                }
                else if (frame.parentHandle == was)
                {
                    frame.Forget();
                    children.Add(frame);
                }
            }
        }

        /// <summary>The oldest of the frame's children in its scope, the first added; null when it has none.</summary>
        private Scoped? Oldest()
        {
            var oldest = newest;
            while (oldest?.before != null)
            {
                oldest = oldest.before;
            }

            return oldest;
        }

        /// <summary>Forgets the game's frames a kind keeps that its MakeParts does not always make again.</summary>
        internal virtual void ForgetParts()
        {
        }

        /// <summary>Finds again, in this frame made anew after a load, the frames found in its template before, in the order found, and gives each what the map set on it, as Child did.</summary>
        private void FindAgain()
        {
            for (var item = Oldest(); item != null; item = item.after)
            {
                if (!(item is Frame frame) || !frame.foundInTemplate || frame.HasEnded)
                {
                    continue;
                }

                var match = Find(handle!, frame.foundName);
                if (match == null)
                {
                    continue;
                }

                frame.handle = match;
                frame.parentHandle = handle;
                frame.player = player;
                frame.over = over;
                if (frame.listened != 0)
                {
                    FramesSystem.Events.Own(frame);
                }

                if (frame.WantsMouse())
                {
                    frame.PrepareMouse();
                }

                if (frame.tooltip != null && !frame.tooltip.HasEnded)
                {
                    frame.MakeTip();
                }

                frame.MakeChildren();
                if (frame.found != null)
                {
                    frame.FindAgain();
                }

                frame.Place();
            }
        }

        /// <summary>Whether the map placed the frame: a frame found in its template keeps the template's place until it does.</summary>
        private bool PlacedByMap()
        {
            return ties != null || filled != null || anchor != FramePoint.Center || offset.X != 0.0f || offset.Y != 0.0f;
        }

        /// <summary>Makes the frames added to this one before it was made, under its game frame.</summary>
        private void MakeChildren()
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].HasEnded)
                {
                    children[i].over = over;
                    children[i].Make(handle!, player);
                }
            }
        }

        /// <summary>Places the frames added to this one before it was made, then lets them go.</summary>
        private void PlaceChildren()
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].HasEnded)
                {
                    children[i].Place();
                }
            }

            children.Clear();
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

            // shortcut: a found frame given Anchor Center with no offset keeps its template's place after a load; keep a flag if a map needs that.
            if (!foundInTemplate || PlacedByMap())
            {
                PlacePoints();
            }

            if (sized)
            {
                GiveSize();
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

            PlaceChildren();
            if (tiedHere != null)
            {
                for (var i = tiedHere.Count - 1; i >= 0; i--)
                {
                    var frame = tiedHere[i];
                    if (frame.HasEnded)
                    {
                        tiedHere.RemoveAt(i);
                    }
                    else if (frame.handle != null && frame.TiedTo(this))
                    {
                        frame.PlacePoints();
                    }
                }
            }
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
            var other = tie.Other != null ? tie.Other.handle : tie.Piece!.Handle;
            if (other != null)
            {
                FramesSystem.World.PlaceFrame(handle!, (int)tie.Point, other, (int)tie.OtherPoint, tie.X, tie.Y);
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
                GiveSize();
            }

            Resized();
        }

        /// <summary>Gives the game the frame's size, a side left at 0 as its template made it.</summary>
        private void GiveSize()
        {
            FramesSystem.World.SizeFrame(handle!, width != 0.0f ? width : madeWidth, height != 0.0f ? height : madeHeight);
        }

        /// <summary>Runs when the map gives the frame a new size, or the frame it covers one: a progress bar refills here, and the frames covering this one that set no size of their own follow.</summary>
        internal virtual void Resized()
        {
            if (tiedHere == null)
            {
                return;
            }

            for (var i = 0; i < tiedHere.Count; i++)
            {
                var frame = tiedHere[i];
                if (frame.filled == this && !frame.sized && !frame.HasEnded)
                {
                    frame.Resized();
                }
            }
        }

        /// <summary>Whether the frame is to be shown on this machine: it is everyone's, or this machine's player's.</summary>
        internal bool ShownHere()
        {
            return (player == null || Runtime.World.IsLocal(player.Handle)) && !HiddenByCinematic();
        }

        /// <summary>Shows or hides the game's frame again as it should be on this machine: after a cinematic's letterbox comes or goes.</summary>
        internal void Reshow()
        {
            if (handle != null && !HasEnded)
            {
                FramesSystem.World.ShowFrame(handle, visible && ShownHere());
            }
        }

        // A frame over the HUD hides while this machine's screen has a cinematic's letterbox, as the HUD does, unless it was made on that cinematic (decision 208).
        private bool HiddenByCinematic()
        {
            if (!rooted || !over)
            {
                return false;
            }

            var cinematic = Cinematic.LetterboxHere();
            for (var scope = Parent; cinematic != null && scope != null; scope = scope.Parent)
            {
                if (scope == cinematic)
                {
                    return false;
                }
            }

            return cinematic != null;
        }

        /// <summary>Hides the game's frame and keeps it for the next of its kind: a frame is never destroyed, as destroying one can desync (a claim in the game facts). One a live frame is still tied to, once its parent has closed up, is not given out again, so the tied frame stays where it was rather than follow the next frame of the kind (review G6).</summary>
        internal override void Finish()
        {
            if (rooted)
            {
                FramesSystem.Unrooted(this);
            }

            if (handle != null)
            {
                FramesSystem.World.ShowFrame(handle, false);
                FramesSystem.Events.Disown(this);
            }

            if (Parent is Frame holder && !holder.ending && !holder.HasEnded)
            {
                holder.ChildEnded(this);
            }

            if (handle == null)
            {
                return;
            }

            // shortcut: a frame kept out of the pool stays out, with the frames it is inside, even when the frame tied to it later moves or ends; recheck then if hidden frames pile up.
            if (HeldByLive())
            {
                for (var outer = Parent as Frame; outer != null; outer = outer.Parent as Frame)
                {
                    outer.holdsTie = true;
                }
            }
            else if (!foundInTemplate && found == null)
            {
                FramesSystem.Pool.Keep(this);
            }
        }
    }

    /// <summary>A point of a frame tied to a point of another frame or of a piece of the game's HUD, moved by an offset; also a piece of the HUD tied to a frame.</summary>
    internal sealed class Tie
    {
        internal Tie(FramePoint point, Frame? other, GamePiece? piece, FramePoint otherPoint, float x, float y)
        {
            Point = point;
            Other = other;
            Piece = piece;
            OtherPoint = otherPoint;
            X = x;
            Y = y;
        }

        internal FramePoint Point { get; }

        internal Frame? Other { get; }

        /// <summary>The piece of the HUD tied to, whose game frame is looked up again after a load; null for a frame.</summary>
        internal GamePiece? Piece { get; }

        internal FramePoint OtherPoint { get; }

        internal float X { get; }

        internal float Y { get; }
    }
}
