namespace Moonwell
{
    /// <summary>A texture, stretched over the frame. An image lets clicks through to what is under it (seen 11-03).</summary>
    public class Image : Frame
    {
        /// <summary>A texture that draws nothing (seen 10-14), for an image given an ended one's frame and no texture of its own.</summary>
        internal const string Blank = "UI\\Widgets\\EscMenu\\Human\\blank-background.blp";

        internal string texture = "";

        /// <summary>The texture this machine draws in place of Texture, such as a unit's icon, which can differ by graphics mode; null for Texture. Setting Texture clears it.</summary>
        internal string? local;

        /// <summary>An empty text over the image that takes its mouse once it is listened to, as an image lets the mouse through; null until then.</summary>
        internal framehandle? catcher;

        /// <summary>Makes an image, to be added to the screen or to another frame.</summary>
        public Image()
        {
        }

        /// <summary>The texture's path, as ReplaceableTextures\CommandButtons\BTNFootman.blp; empty unless set, which draws nothing.</summary>
        [LocalSafe]
        public string Texture
        {
            get { return texture; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                texture = value;
                local = null;
                if (handle != null)
                {
                    FramesSystem.World.PaintFrame(handle, value);
                }
            }
        }

        /// <summary>The texture follows the cursor while the image is held, unless DragIcon is set.</summary>
        internal override string DragTexture()
        {
            return dragIcon != "" ? dragIcon : texture;
        }

        /// <summary>A BACKDROP, which draws a texture (seen 10-14).</summary>
        internal override string GameType => "BACKDROP";

        /// <summary>The catcher, once the image is listened to.</summary>
        internal override framehandle? MouseHandle => catcher;

        /// <summary>Makes the catcher for an image listened to before it was made.</summary>
        internal override void MakeParts()
        {
            if (WantsMouse())
            {
                MakeCatcher();
            }
        }

        /// <summary>Makes the catcher, or turns an ended image's back on, as Enabled says.</summary>
        internal override void PrepareMouse()
        {
            if (catcher == null && handle != null)
            {
                MakeCatcher();
            }

            TakeMouse(enabled);
        }

        /// <summary>Turns the catcher on or off, once there is one; an image not listened to never takes the mouse.</summary>
        internal override void TakeMouse(bool on)
        {
            if (catcher != null)
            {
                FramesSystem.World.EnableFrame(catcher, on && WantsMouse());
            }
        }

        /// <summary>The catcher went with the game's frames at a load; one found in a template, or not listened to, gets one only when it wants the mouse.</summary>
        internal override void ForgetParts()
        {
            catcher = null;
        }

        private void MakeCatcher()
        {
            catcher = FramesSystem.World.MakeFrame("TEXT", "", handle!)!;
            FramesSystem.World.FillFrame(catcher, handle!);
        }

        /// <summary>Paints over an ended image's texture when this one has none; Place paints one it has. An ended image's catcher comes with its frame, on only when this one is listened to.</summary>
        internal override void TakeOver(Frame old)
        {
            catcher = ((Image)old).catcher;
            base.TakeOver(old);
            if (catcher != null)
            {
                FramesSystem.World.EnableFrame(catcher, enabled && WantsMouse());
            }
            else if (WantsMouse())
            {
                MakeCatcher();
            }

            if (texture.Length == 0 && ((Image)old).texture.Length > 0)
            {
                FramesSystem.World.PaintFrame(handle!, Blank);
            }
        }

        /// <summary>Draws a texture of this machine's own, called on every machine with its own: Texture keeps reading the shared one given, and setting it again ends this.</summary>
        internal void ShowLocal(string shared, string own)
        {
            if (HasEnded)
            {
                return;
            }

            texture = shared;
            local = own;
            if (handle != null)
            {
                FramesSystem.World.PaintFrame(handle, own.Length > 0 ? own : Blank);
            }
        }

        /// <summary>Gives the game the texture, or the blank one for an image with none, as a BACKDROP never painted draws the game's green missing texture (seen 11-14); then the points and looks.</summary>
        internal override void Place()
        {
            var shown = local ?? texture;
            if (shown.Length > 0)
            {
                FramesSystem.World.PaintFrame(handle!, shown);
            }
            else if (!foundInTemplate)
            {
                FramesSystem.World.PaintFrame(handle!, Blank);
            }

            base.Place();
        }
    }
}
