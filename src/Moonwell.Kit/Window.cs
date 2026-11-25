using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A window of the UI kit (decision 212): a panel in the kit's Theme with a title, a close button and a title bar that
    /// moves it. A frame added to it with no place or size of its own covers its body, below the title. Click the title to
    /// pick the window up and click again to put it down: it follows that player's cursor on their screen alone, and its
    /// new place reaches every machine when it is put down. 0.3 by 0.3 unless sized.
    /// </summary>
    public class Window : Panel
    {
        /// <summary>How far the body sits in from the border, and the title bar's height.</summary>
        internal const float Inset = 0.025f, BarHeight = 0.024f, BarTop = 0.014f;

        internal readonly Label title;

        /// <summary>The title bar: an icon button with no icon over the title, which takes the clicks that move the window.</summary>
        internal readonly IconButton bar;

        /// <summary>The game's dialog button that closes the window, with closeMark over it, while the theme names no Close texture.</summary>
        internal readonly Button close;

        /// <summary>The X over the game's close button, a label at the game's text height, which fits inside the button where the button's own text is cut by its edge (J84).</summary>
        internal readonly Label closeMark;

        /// <summary>The icon button that closes the window when the theme names a Close texture; null until one does.</summary>
        internal IconButton? closeIcon;

        internal Theme theme;

        internal bool closable = true;

        internal bool movable = true;

        internal List<Action<Player>>? closed;

        /// <summary>The player moving the window; null when no one is.</summary>
        internal Player? mover;

        internal Timer? following;

        /// <summary>Makes a window in Kit.Theme, to be added to the screen or to another frame.</summary>
        public Window()
        {
            width = 0.3f;
            height = 0.3f;
            sized = true;
            theme = Kit.Theme;
            title = Add(new Label { Anchor = FramePoint.Top, Offset = new Vector2(0.0f, -BarTop - BarHeight / 2.0f), Alignment = FramePoint.Center });
            bar = Add(new IconButton { Height = BarHeight });
            bar.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, Inset + BarHeight, -BarTop);
            bar.Attach(FramePoint.TopRight, this, FramePoint.TopRight, -Inset - BarHeight, -BarTop);
            bar.Clicked += Grab;
            close = Add(new Button { Anchor = FramePoint.TopRight });
            close.Clicked += Close;
            closeMark = close.Add(new Label { Text = "X", Alignment = FramePoint.Center });
            closeMark.Fill(close);
            Dress();
        }

        /// <summary>The title, centred at the top; empty unless set.</summary>
        [LocalSafe]
        public string Title
        {
            get { return title.Text; }
            set { title.Text = value; }
        }

        /// <summary>The window's look, Kit.Theme as the window was made unless set, passed down to the frames the window makes (its title, close button, and a ready window's tooltips and texts); set it before the window is added, as the text sizes and font are read when the labels are made.</summary>
        [LocalSafe]
        public Theme Theme
        {
            get { return theme; }
            set
            {
                theme = value;
                TakeTheme();
            }
        }

        /// <summary>The close button the theme gives: an icon button of its Close texture, or the game's dialog button with an X; a map may place it again.</summary>
        [LocalSafe]
        public Frame CloseButton => theme.Close.Length > 0 && closeIcon != null ? closeIcon : close;

        /// <summary>Whether the close button shows; true unless set.</summary>
        [LocalSafe]
        public bool Closable
        {
            get { return closable; }
            set
            {
                closable = value;
                ShowClose();
            }
        }

        /// <summary>Whether a click on the title picks the window up to move it; true unless set. Only a window added to the screen moves.</summary>
        [LocalSafe]
        public bool Movable
        {
            get { return movable; }
            set { movable = value; }
        }

        /// <summary>Runs when a player closes the window with its close button, after it is hidden: Visible = true shows it again. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player>? Closed
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (closed ??= new List<Action<Player>>()).Add(value);
                }
            }

            remove { Unsubscribe(closed, value); }
        }

        /// <summary>Runs when Theme is set: the window's own frames take it, and a ready window passes it on to what it made.</summary>
        internal virtual void TakeTheme()
        {
            Dress();
        }

        /// <summary>Gives the window's own frames the theme: its template, the title's colour, size and font, and the close button.</summary>
        private void Dress()
        {
            Template = theme.Window;
            title.Color = theme.Title;
            theme.Style(title, theme.TitleSize);
            closeMark.Color = theme.Title;
            theme.Style(closeMark, 0.0f);
            if (theme.Close.Length > 0 && closeIcon == null)
            {
                closeIcon = Add(new IconButton { Anchor = FramePoint.TopRight });
                closeIcon.Clicked += Close;
            }

            PlaceClose();
        }

        /// <summary>Puts the close buttons at the theme's size and inset, the icon one with its texture, and shows the one the theme names.</summary>
        private void PlaceClose()
        {
            var size = theme.CloseSize;
            var inset = new Vector2(-theme.CloseInset, -theme.CloseInset);
            close.Width = size;
            close.Height = size;
            close.Offset = inset;
            if (closeIcon != null)
            {
                closeIcon.Icon = theme.Close;
                closeIcon.Width = size;
                closeIcon.Height = size;
                closeIcon.Offset = inset;
            }

            ShowClose();
        }

        /// <summary>Shows the close button the theme names while the window is Closable, and hides the other.</summary>
        private void ShowClose()
        {
            var icon = theme.Close.Length > 0;
            close.Visible = closable && !icon;
            if (closeIcon != null)
            {
                closeIcon.Visible = closable && icon;
            }
        }

        /// <summary>A frame added with no place or size of its own covers the body, inside the border and below the title.</summary>
        internal override void Adopted(Frame child)
        {
            base.Adopted(child);
            if (child.ties != null || child.filled != null || child.sized || child.anchor != FramePoint.Center || child.offset.X != 0.0f || child.offset.Y != 0.0f)
            {
                return;
            }

            child.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, Inset, -BarTop - BarHeight - 0.006f);
            child.Attach(FramePoint.BottomRight, this, FramePoint.BottomRight, -Inset, Inset);
        }

        private void Close(Player who)
        {
            Visible = false;
            Raise(closed, who, "Closed");
        }

        // A first click picks the window up for that player; their next click on the title, which is under their cursor, puts it down.
        private void Grab(Player who)
        {
            if (!movable || !rooted)
            {
                return;
            }

            if (mover == null)
            {
                mover = who;
                following = Every(Drags.FollowEvery, Follow);
                return;
            }

            if (mover != who)
            {
                return;
            }

            mover = null;
            following?.End();
            following = null;
            Share(who, () =>
            {
                if (!FramesSystem.CursorAt(out var cursor))
                {
                    return null;
                }

                var bytes = new ByteWriter();
                bytes.WriteFloat(cursor.X);
                bytes.WriteFloat(cursor.Y);
                return bytes.ToCode();
            }, text =>
            {
                var bytes = text == null ? null : ByteReader.FromCode(text);
                if (bytes == null || bytes.Remaining != 8)
                {
                    // None came, as the player left: back where it was, on every machine.
                    Offset = offset;
                    return;
                }

                var x = bytes.ReadFloat();
                var y = bytes.ReadFloat();
                Anchor = FramePoint.Center;
                Offset = new Vector2(x - 0.4f, y - TitleDrop() - 0.3f);
            });
        }

        /// <summary>How far below the cursor the window's centre is while it is held by the middle of its title.</summary>
        private float TitleDrop()
        {
            return height / 2.0f - BarTop - BarHeight / 2.0f;
        }

        /// <summary>On every machine: on the mover's, the window's game frame follows the cursor, held by its title; nothing shared changes until it is put down.</summary>
        private void Follow()
        {
            var who = mover;
            if (who == null || handle == null)
            {
                return;
            }

            Local.For(who, () =>
            {
                if (!FramesSystem.CursorAt(out var cursor))
                {
                    return;
                }

                var world = FramesSystem.World;
                var x = cursor.X;
                var y = cursor.Y - TitleDrop();
                world.ClearFramePoints(handle!);
                world.SpanFrame(handle!, x - width / 2.0f, y - height / 2.0f, x + width / 2.0f, y + height / 2.0f);
            });
        }
    }
}
