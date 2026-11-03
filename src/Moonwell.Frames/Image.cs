namespace Moonwell
{
    /// <summary>A texture, stretched over the frame. An image lets clicks through to what is under it (seen 11-03).</summary>
    public class Image : Frame
    {
        /// <summary>A texture that draws nothing (seen 10-14), for an image given an ended one's frame and no texture of its own.</summary>
        internal const string Blank = "UI\\Widgets\\EscMenu\\Human\\blank-background.blp";

        internal string texture = "";

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
                if (handle != null)
                {
                    FramesSystem.World.PaintFrame(handle, value);
                }
            }
        }

        /// <summary>A BACKDROP, which draws a texture (seen 10-14).</summary>
        internal override string GameType => "BACKDROP";

        /// <summary>Paints over an ended image's texture when this one has none; Place paints one it has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (texture.Length == 0 && ((Image)old).texture.Length > 0)
            {
                FramesSystem.World.PaintFrame(handle!, Blank);
            }
        }

        /// <summary>Gives the game the texture, then the points and looks.</summary>
        internal override void Place()
        {
            if (texture.Length > 0)
            {
                FramesSystem.World.PaintFrame(handle!, texture);
            }

            base.Place();
        }
    }
}
