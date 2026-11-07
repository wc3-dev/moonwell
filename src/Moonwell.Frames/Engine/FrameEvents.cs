using System;
using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game's frame events, sent to the frames that hold the game's frames now. Each kind has one trigger (the world's), and
    /// each game frame is registered for a kind once: a frame given out again by the pool keeps its registrations, and its
    /// events go to its new owner; an ended frame owns nothing, so its events reach no one.
    /// </summary>
    internal sealed class FrameEvents
    {
        // The game's FRAMEEVENT_ numbers (common.j).
        internal const int Click = 1, Wheel = 6, TextChanged = 9, SliderChanged = 13, TextEntered = 16;

        private const int Kinds = 16;

        /// <summary>The frame each game frame that takes the mouse belongs to now.</summary>
        private readonly Dictionary<framehandle, Frame> owners = new Dictionary<framehandle, Frame>();

        /// <summary>The kinds each game frame is registered for, a bit each by the game's number.</summary>
        private readonly Dictionary<framehandle, int> registered = new Dictionary<framehandle, int>();

        /// <summary>The action given for each kind, made once, so every registration of a kind runs the same.</summary>
        private readonly Action?[] fired = new Action?[Kinds + 1];

        /// <summary>Makes a frame the owner of the game frame that takes its mouse, and registers that game frame for each kind the frame is listened to for and it is not registered for yet. Nothing until that game frame is made.</summary>
        internal void Own(Frame frame)
        {
            var mouse = frame.MouseHandle;
            if (mouse == null)
            {
                return;
            }

            owners[mouse] = frame;
            registered.TryGetValue(mouse, out var done);
            for (var kind = 1; kind <= Kinds; kind++)
            {
                var bit = 1 << kind;
                if ((frame.listened & bit) != 0 && (done & bit) == 0)
                {
                    FramesSystem.World.ListenFrame(mouse, kind, ActionOf(kind));
                    done |= bit;
                }
            }

            registered[mouse] = done;
        }

        /// <summary>Forgets every game frame, as a saved game's load took them: the frames made again register anew, on the same triggers.</summary>
        internal void Forget()
        {
            owners.Clear();
            registered.Clear();
        }

        /// <summary>Takes away an ended frame's ownership, so the events of its game frame reach no one until another frame owns it.</summary>
        internal void Disown(Frame frame)
        {
            var mouse = frame.MouseHandle;
            if (mouse != null && owners.TryGetValue(mouse, out var owner) && owner == frame)
            {
                owners.Remove(mouse);
            }
        }

        private Action ActionOf(int kind)
        {
            var action = fired[kind];
            if (action == null)
            {
                action = () => Fire(kind);
                fired[kind] = action;
            }

            return action;
        }

        /// <summary>Sends the event being delivered to the frame that owns its game frame, unless the frame is for another player.</summary>
        private void Fire(int kind)
        {
            var world = FramesSystem.World;
            if (!owners.TryGetValue(world.EventFrame(), out var frame) || frame.HasEnded)
            {
                return;
            }

            var who = Player.Of(world.EventPlayer());
            if (who == null || (frame.player != null && frame.player != who))
            {
                return;
            }

            if (kind == Click && FramesSystem.Drags.Clicked(frame, who))
            {
                frame.GiveKeyboardBack(who);
                return;
            }

            frame.Receive(kind, who, world.EventValue(), world.EventText());
        }
    }
}
