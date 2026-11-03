using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>The game as the frames package sees it, faked: it records each frame the package makes and what it sets.</summary>
    public sealed class FakeFramesWorld : IFramesWorld
    {
        /// <summary>The frames made, in the order made.</summary>
        public List<FakeFrame> Frames = new List<FakeFrame>();

        /// <summary>The fake behind each frame handle.</summary>
        private readonly Dictionary<framehandle, FakeFrame> frames = new Dictionary<framehandle, FakeFrame>();

        /// <summary>The templates the game does not have, as one from an .fdf whose TOC is not loaded: making a frame from one gives null.</summary>
        public List<string> MissingTemplates = new List<string>();

        /// <summary>This machine's window, in pixels; a case changes it to resize the window.</summary>
        public int WindowWidth = 800, WindowHeight = 600;

        /// <summary>The fake behind a frame handle the package was given.</summary>
        public FakeFrame FrameOf(framehandle frame)
        {
            return frames[frame];
        }

        /// <summary>Makes a frame under a game frame, by name, and records it.</summary>
        private framehandle Root(string under)
        {
            var made = new framehandle();
            var fake = new FakeFrame { Under = under };
            frames[made] = fake;
            Frames.Add(fake);
            return made;
        }

        /// <summary>Makes a frame from a template, or by type when the template is empty, under a parent.</summary>
        public framehandle? MakeFrame(string type, string template, framehandle parent)
        {
            if (MissingTemplates.Contains(template))
            {
                return null;
            }

            var made = new framehandle();
            var fake = new FakeFrame { Type = type, Template = template, Parent = frames[parent] };
            frames[made] = fake;
            Frames.Add(fake);
            return made;
        }

        /// <summary>Shows or hides a frame on this machine.</summary>
        public void ShowFrame(framehandle frame, bool shown)
        {
            Asked(frame).Shown = shown;
        }

        /// <summary>The fake behind a frame handle, counting the call.</summary>
        private FakeFrame Asked(framehandle frame)
        {
            var fake = frames[frame];
            fake.Calls++;
            return fake;
        }

        /// <summary>Ties a point of a frame to a point of another.</summary>
        public void PlaceFrame(framehandle frame, int point, framehandle other, int otherPoint, float x, float y)
        {
            Asked(frame).Points.Add(new FakePoint { Point = point, Other = frames[other], OtherPoint = otherPoint, X = x, Y = y });
        }

        /// <summary>Clears a frame's points, and what it covered.</summary>
        public void ClearFramePoints(framehandle frame)
        {
            var fake = Asked(frame);
            fake.Points.Clear();
            fake.Filled = null;
        }

        /// <summary>Sets a frame to cover another, all points; the points it had stay, as nothing says the game clears them.</summary>
        public void FillFrame(framehandle frame, framehandle other)
        {
            Asked(frame).Filled = frames[other];
        }

        /// <summary>Sizes a frame.</summary>
        public void SizeFrame(framehandle frame, float width, float height)
        {
            var fake = Asked(frame);
            fake.Width = width;
            fake.Height = height;
        }

        /// <summary>Scales a frame.</summary>
        public void ScaleFrame(framehandle frame, float scale)
        {
            Asked(frame).Scale = scale;
        }

        /// <summary>Lets a frame take the mouse, or not.</summary>
        public void EnableFrame(framehandle frame, bool enabled)
        {
            Asked(frame).Enabled = enabled;
        }

        /// <summary>Sets a frame's opacity.</summary>
        public void FadeFrame(framehandle frame, int alpha)
        {
            Asked(frame).Alpha = alpha;
        }

        /// <summary>Writes a frame's text.</summary>
        public void WriteFrameText(framehandle frame, string text)
        {
            Asked(frame).Text = text;
        }

        /// <summary>Aligns a frame's text.</summary>
        public void AlignFrameText(framehandle frame, int vertical, int horizontal)
        {
            var fake = Asked(frame);
            fake.Vertical = vertical;
            fake.Horizontal = horizontal;
        }

        /// <summary>Paints a frame with a texture.</summary>
        public void PaintFrame(framehandle frame, string texture)
        {
            Asked(frame).Texture = texture;
        }

        /// <summary>Moves a frame under another parent.</summary>
        public void ParentFrame(framehandle frame, framehandle parent)
        {
            Asked(frame).Parent = frames[parent];
        }

        /// <summary>Sets where a frame's corners sit on the screen.</summary>
        public void SpanFrame(framehandle frame, float left, float bottom, float right, float top)
        {
            var fake = Asked(frame);
            fake.Left = left;
            fake.Bottom = bottom;
            fake.Right = right;
            fake.Top = top;
        }

        /// <summary>This machine's window's width, in pixels.</summary>
        public int ClientWidth()
        {
            return WindowWidth;
        }

        /// <summary>This machine's window's height, in pixels.</summary>
        public int ClientHeight()
        {
            return WindowHeight;
        }

        /// <summary>Sets a frame's drawing level.</summary>
        public void LevelFrame(framehandle frame, int level)
        {
            Asked(frame).Level = level;
        }

        /// <summary>Makes the whole-width root under the console's backdrop.</summary>
        public framehandle ScreenRoot()
        {
            return Root("ConsoleUIBackdrop");
        }

        /// <summary>Makes the over-the-HUD root under the game UI.</summary>
        public framehandle OverRoot()
        {
            return Root("GameUI");
        }
    }
}
