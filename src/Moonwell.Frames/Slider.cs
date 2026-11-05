using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// The game's slider (EscMenuSliderTemplate), one player's only (decision 156). A drag runs ValueChanged with the player
    /// and the value, about 85 times a second (seen), each synced online; a value the same as Value reads runs nothing, which
    /// drops the game's repeats and the event it sends back for a value the map set (seen). The slider keeps no keyboard.
    /// </summary>
    public class Slider : Frame
    {
        internal float min;

        internal float max = 100.0f;

        internal float step = 1.0f;

        internal float value;

        /// <summary>Values the map set that the game has not sent back yet; their events run nothing, whatever came between.</summary>
        internal List<float>? sent;

        internal List<Action<Player, float>>? valueChanged;

        /// <summary>Makes a slider from 0 to 100 in steps of 1, at 0, to be added to a frame for one player.</summary>
        public Slider()
        {
            Template = "EscMenuSliderTemplate";
            listened = 1 << FrameEvents.SliderChanged;
        }

        /// <summary>The lowest value, which Value is kept above; 0 unless set.</summary>
        [LocalSafe]
        public float Min
        {
            get { return min; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                min = value;
                KeepValue();
            }
        }

        /// <summary>The highest value, which Value is kept below; 100 unless set.</summary>
        [LocalSafe]
        public float Max
        {
            get { return max; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                max = value;
                KeepValue();
            }
        }

        /// <summary>The step the value moves by; 1 unless set.</summary>
        [LocalSafe]
        public float Step
        {
            get { return step; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                step = value;
                if (handle != null)
                {
                    FramesSystem.World.SetFrameStep(handle, value);
                }
            }
        }

        /// <summary>The value: what the last drag brought, or what the map last set, kept within Min and Max, the same on every machine; 0 unless set.</summary>
        [LocalSafe]
        public float Value
        {
            get { return value; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                var kept = value < min ? min : value > max ? max : value;
                // A set to the value it reads is not given to the game, where it would undo a drag on its way and come back as the player's (review G4).
                if (handle != null && kept != this.value)
                {
                    Expect(kept);
                    this.value = kept;
                    FramesSystem.World.SetFrameValue(handle, kept);
                }
                else
                {
                    this.value = kept;
                }
            }
        }

        /// <summary>Runs when the player drags the slider to a new value, with the player and the value. Not inside Local.For.</summary>
        public event Action<Player, float>? ValueChanged
        {
            add { Subscribe(ref valueChanged, value, FrameEvents.SliderChanged, FrameEvents.SliderChanged); }
            remove { Unsubscribe(valueChanged, value); }
        }

        /// <summary>Keeps the value within a new range and gives both to the game, so the value the game sends back for its own clamp is the one Value reads, and runs nothing.</summary>
        private void KeepValue()
        {
            var kept = value < min ? min : value > max ? max : value;
            if (handle != null)
            {
                Expect(kept);
            }

            value = kept;
            if (handle != null)
            {
                FramesSystem.World.SetFrameRange(handle, min, max);
                FramesSystem.World.SetFrameValue(handle, value);
            }
        }

        /// <summary>A SLIDER, for a slider whose template the game lacks.</summary>
        internal override string GameType => "SLIDER";

        internal override bool ForOnePlayerOnly() => true;

        /// <summary>An ended slider goes only to one whose range holds its value, as the game would send back its own clamp of that value as a player's drag (review E08).</summary>
        internal override bool Fits(Frame old)
        {
            var was = ((Slider)old).value;
            return base.Fits(old) && was >= min && was <= max;
        }

        /// <summary>Gives the game the range, step and value, then the points and looks.</summary>
        internal override void Place()
        {
            var world = FramesSystem.World;
            world.SetFrameRange(handle!, min, max);
            world.SetFrameStep(handle!, step);
            world.SetFrameValue(handle!, value);
            base.Place();
        }

        /// <summary>A new value is kept and runs ValueChanged; the one Value reads runs nothing.</summary>
        internal override void Receive(int kind, Player who, float eventValue, string text)
        {
            if (kind != FrameEvents.SliderChanged)
            {
                base.Receive(kind, who, eventValue, text);
                return;
            }

            if (eventValue == value)
            {
                sent?.Remove(eventValue);
                return;
            }

            if (sent != null && sent.Remove(eventValue))
            {
                value = eventValue;
                return;
            }

            value = eventValue;
            Raise(valueChanged, who, eventValue, "ValueChanged");
        }

        /// <summary>Keeps a value the map sets, when it changes the one Value reads, to know its event when the game sends it back. A set whose event never comes, as when the slider's player has left, is forgotten after eight more.</summary>
        private void Expect(float kept)
        {
            if (kept == value)
            {
                return;
            }

            sent ??= new List<float>();
            sent.Add(kept);
            if (sent.Count > 8)
            {
                sent.RemoveAt(0);
            }
        }
    }
}
