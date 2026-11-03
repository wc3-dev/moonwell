namespace Moonwell.Hosting
{
    /// <summary>The game as the frames package sees it: the frames it makes and what it sets on them. Types are the game's.</summary>
    internal interface IFramesWorld
    {
        /// <summary>Makes the whole-width root: a frame under the console's backdrop, which reaches the window's edges and draws beneath the HUD.</summary>
        framehandle ScreenRoot();

        /// <summary>Makes the over-the-HUD root: a frame under the game UI covering the 4:3 middle, drawn over the HUD.</summary>
        framehandle OverRoot();

        /// <summary>Makes a frame from a template, or by type when the template is empty, under a parent; null when the game has no such template (game facts).</summary>
        framehandle? MakeFrame(string type, string template, framehandle parent);

        /// <summary>Shows or hides a frame on this machine.</summary>
        void ShowFrame(framehandle frame, bool shown);

        /// <summary>Ties a point of a frame to a point of another, moved by an offset; points are numbered 0 top left to 8 bottom right.</summary>
        void PlaceFrame(framehandle frame, int point, framehandle other, int otherPoint, float x, float y);

        /// <summary>Clears a frame's points.</summary>
        void ClearFramePoints(framehandle frame);

        /// <summary>Sets a frame to cover another, all points.</summary>
        void FillFrame(framehandle frame, framehandle other);

        /// <summary>Sizes a frame.</summary>
        void SizeFrame(framehandle frame, float width, float height);

        /// <summary>Scales a frame, with its children.</summary>
        void ScaleFrame(framehandle frame, float scale);

        /// <summary>Lets a frame take the mouse, or not.</summary>
        void EnableFrame(framehandle frame, bool enabled);

        /// <summary>Sets a frame's opacity, 0 to 255.</summary>
        void FadeFrame(framehandle frame, int alpha);

        /// <summary>Sets a frame's drawing level among its siblings.</summary>
        void LevelFrame(framehandle frame, int level);

        /// <summary>Writes a frame's text.</summary>
        void WriteFrameText(framehandle frame, string text);

        /// <summary>Aligns a frame's text, as the game numbers it: vertical 0 top, 1 middle, 2 bottom; horizontal 3 left, 4 centre, 5 right.</summary>
        void AlignFrameText(framehandle frame, int vertical, int horizontal);

        /// <summary>Paints a frame with a texture.</summary>
        void PaintFrame(framehandle frame, string texture);

        /// <summary>Moves a frame under another parent.</summary>
        void ParentFrame(framehandle frame, framehandle parent);

        /// <summary>Sets where a frame's corners sit on the screen: its bottom left at left, bottom and its top right at right, top.</summary>
        void SpanFrame(framehandle frame, float left, float bottom, float right, float top);

        /// <summary>This machine's window's width, in pixels; differs between machines.</summary>
        int ClientWidth();

        /// <summary>This machine's window's height, in pixels; differs between machines.</summary>
        int ClientHeight();
    }
}
