using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>The game as the frames package sees it, faked: it records each frame the package makes and what it sets.</summary>
    public sealed class FakeFramesWorld : IFramesWorld
    {
        /// <summary>The action the package gave for each kind of frame event, by the game's FRAMEEVENT_ number.</summary>
        private readonly Dictionary<int, System.Action> fired = new Dictionary<int, System.Action>();

        private framehandle? eventFrame;

        private player? eventPlayer;

        private float eventValue;

        private string eventText = "";

        /// <summary>The TOC files loaded, in the order loaded.</summary>
        public List<string> LoadedTocs = new List<string>();

        /// <summary>The frames made, in the order made.</summary>
        public List<FakeFrame> Frames = new List<FakeFrame>();

        /// <summary>The fake behind each frame handle.</summary>
        private readonly Dictionary<framehandle, FakeFrame> frames = new Dictionary<framehandle, FakeFrame>();

        /// <summary>The frames a template makes under the one made from it, by name, as an .fdf gives them: a name that is a template itself makes its own.</summary>
        public Dictionary<string, string[]> TemplateParts = new Dictionary<string, string[]>();

        /// <summary>The handle of each fake, for a child asked for by index.</summary>
        private readonly Dictionary<FakeFrame, framehandle> handles = new Dictionary<FakeFrame, framehandle>();

        /// <summary>The size each template makes a frame, width then height; a template not listed makes one 0 by 0.</summary>
        public Dictionary<string, Vector2> TemplateSizes = new Dictionary<string, Vector2>();

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

        /// <summary>Registers a frame for a kind of event, recording it; every frame of a kind gets the same action.</summary>
        public void ListenFrame(framehandle frame, int kind, System.Action action)
        {
            Asked(frame).Listened.Add(kind);
            fired[kind] = action;
        }

        /// <summary>Makes a frame the tooltip of another; throws for one already given to a different frame, which crashes the game (seen 11-03).</summary>
        public void TipFrame(framehandle frame, framehandle tooltip)
        {
            var owner = Asked(frame);
            var tip = frames[tooltip];
            if (tip.TipOf != null && tip.TipOf != owner)
            {
                throw new System.InvalidOperationException("the game crashes: a frame given as one frame's tooltip was given to a second");
            }

            tip.TipOf = owner;
            owner.Tip = tip;
        }

        /// <summary>Adds a line to a frame's text, as the game's text area does.</summary>
        public void AddFrameText(framehandle frame, string line)
        {
            var fake = Asked(frame);
            fake.Text = fake.Text.Length == 0 ? line : fake.Text + "\n" + line;
        }

        /// <summary>Colours a frame's text.</summary>
        public void ColourFrameText(framehandle frame, int r, int g, int b, int a)
        {
            Asked(frame).TextColor = new Color(r, g, b, a);
        }

        /// <summary>Sets a frame's value.</summary>
        public void SetFrameValue(framehandle frame, float value)
        {
            Asked(frame).Value = value;
        }

        /// <summary>Sets a frame's range.</summary>
        public void SetFrameRange(framehandle frame, float min, float max)
        {
            var fake = Asked(frame);
            fake.Min = min;
            fake.Max = max;
        }

        /// <summary>Sets a frame's step.</summary>
        public void SetFrameStep(framehandle frame, float step)
        {
            Asked(frame).Step = step;
        }

        /// <summary>Limits a frame's text to a number of characters.</summary>
        public void LimitFrameText(framehandle frame, int characters)
        {
            Asked(frame).TextLimit = characters;
        }

        /// <summary>Gives the keyboard back from a frame on this machine, recording it.</summary>
        public void ReleaseKeyboard(framehandle frame)
        {
            Asked(frame).KeyboardReleases++;
        }

        /// <summary>The frame of the event being delivered.</summary>
        public framehandle EventFrame()
        {
            return eventFrame!;
        }

        /// <summary>The player of the event being delivered.</summary>
        public player EventPlayer()
        {
            return eventPlayer!;
        }

        /// <summary>The value of the event being delivered.</summary>
        public float EventValue()
        {
            return eventValue;
        }

        /// <summary>The text of the event being delivered.</summary>
        public string EventText()
        {
            return eventText;
        }

        /// <summary>Sends a frame event, as the game does, from the player in a slot: it reaches the package only when the frame was registered for the kind.</summary>
        public void Fire(FakeFrame frame, int kind, int slot, float value = 0.0f, string text = "")
        {
            if (!frame.Listened.Contains(kind))
            {
                return;
            }

            foreach (var pair in frames)
            {
                if (pair.Value == frame)
                {
                    eventFrame = pair.Key;
                }
            }

            eventPlayer = Player.Get(slot).Handle;
            eventValue = value;
            eventText = text;
            fired[kind]();
        }

        /// <summary>Loads a TOC file, recording its path.</summary>
        public bool LoadToc(string path)
        {
            LoadedTocs.Add(path);
            return true;
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
            if (TemplateSizes.TryGetValue(template, out var size))
            {
                fake.Width = size.X;
                fake.Height = size.Y;
            }

            frames[made] = fake;
            handles[fake] = made;
            Frames.Add(fake);
            frames[parent].Children.Add(fake);
            MakeParts(fake, template);
            return made;
        }

        /// <summary>Makes the frames a template makes under one made from it.</summary>
        private void MakeParts(FakeFrame under, string template)
        {
            if (!TemplateParts.TryGetValue(template, out var names))
            {
                return;
            }

            foreach (var name in names)
            {
                var part = new FakeFrame { Name = name, Parent = under };
                var handle = new framehandle();
                frames[handle] = part;
                handles[part] = handle;
                under.Children.Add(part);
                MakeParts(part, name);
            }
        }

        /// <summary>How many children a frame has.</summary>
        public int ChildCount(framehandle frame)
        {
            return frames[frame].Children.Count;
        }

        /// <summary>A frame's child by index.</summary>
        public framehandle ChildAt(framehandle frame, int index)
        {
            return handles[frames[frame].Children[index]];
        }

        /// <summary>A frame's name.</summary>
        public string NameOf(framehandle frame)
        {
            return frames[frame].Name;
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

        /// <summary>A frame's width, as last sized or as its template made it.</summary>
        public float FrameWidth(framehandle frame)
        {
            return frames[frame].Width;
        }

        /// <summary>A frame's height, as last sized or as its template made it.</summary>
        public float FrameHeight(framehandle frame)
        {
            return frames[frame].Height;
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

        /// <summary>The game's HUD frames looked up, by name or as "origin kind index", made at the first lookup.</summary>
        public Dictionary<string, FakeFrame> GameFrames = new Dictionary<string, FakeFrame>();

        /// <summary>How many lookups of the game's HUD were made.</summary>
        public int Lookups;

        /// <summary>Whether the game's HUD is hidden on this machine.</summary>
        public bool HudHidden;

        /// <summary>A frame of the game's HUD by name, the same fake at each lookup.</summary>
        public framehandle? GameFrame(string name)
        {
            Lookups++;
            if (!named.TryGetValue(name, out var found))
            {
                found = new framehandle();
                named[name] = found;
                frames[found] = new FakeFrame { Under = name };
                GameFrames[name] = frames[found];
            }

            return found;
        }

        private readonly Dictionary<string, framehandle> named = new Dictionary<string, framehandle>();

        /// <summary>A frame of the game's HUD by origin, kept as "origin kind index".</summary>
        public framehandle? OriginFrame(int kind, int index)
        {
            return GameFrame("origin " + kind + " " + index);
        }

        /// <summary>Hides the game's HUD, or shows it.</summary>
        public void HideHud(bool hidden)
        {
            HudHidden = hidden;
        }

        /// <summary>Gives a frame another parent.</summary>
        public void ParentFrame(framehandle frame, framehandle parent)
        {
            Asked(frame).Parent = frames[parent];
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
