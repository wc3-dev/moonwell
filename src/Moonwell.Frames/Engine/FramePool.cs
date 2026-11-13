using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>The ended frames, kept hidden for the next frame of their class and template, as a frame is never destroyed.</summary>
    internal sealed class FramePool
    {
        /// <summary>The kept frames of each class, by the class's own object (Names.ClassOf), in the order ended.</summary>
        private readonly Dictionary<object, List<Frame>> kept = new Dictionary<object, List<Frame>>();

        /// <summary>Keeps an ended frame for the next of its class and template.</summary>
        internal void Keep(Frame frame)
        {
            var kind = Names.ClassOf(frame);
            if (!kept.TryGetValue(kind, out var frames))
            {
                frames = new List<Frame>();
                kept[kind] = frames;
            }

            frames.Add(frame);
        }

        /// <summary>Takes the last kept frame of a frame's class made from the template it is to be made from, under the parent it is to be made under, and that fits it (Frame.Fits); null when there is none. A frame is never moved to another parent, as it would still hide with its old one (seen 11-03).</summary>
        internal Frame? Take(Frame frame, framehandle parent)
        {
            if (!kept.TryGetValue(Names.ClassOf(frame), out var frames))
            {
                return null;
            }

            for (var i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].parentHandle == parent && frames[i].madeTemplate == frame.MakeTemplate && frame.Fits(frames[i]))
                {
                    var old = frames[i];
                    frames.RemoveAt(i);
                    return old;
                }
            }

            return null;
        }
    }
}
