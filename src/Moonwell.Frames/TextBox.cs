using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// The game's text box (EscMenuEditBoxTemplate), one player's only (decision 156). Typing runs TextChanged with the player
    /// and the whole text, and Enter runs Submitted with it (seen); a text the same as Text reads runs nothing, which drops
    /// the event the game sends back for a text the map set (seen). A text box keeps the keyboard while typed in.
    /// </summary>
    public class TextBox : Frame
    {
        internal string text = "";

        /// <summary>Texts the map set that the game has not sent back yet; their events run nothing, whatever came between.</summary>
        internal List<string>? sent;

        internal int maxLength;

        internal List<Action<Player, string>>? textChanged;

        internal List<Action<Player, string>>? submitted;

        /// <summary>Makes an empty text box, 0.2 by 0.03, to be added to a frame for one player.</summary>
        public TextBox()
        {
            Template = "EscMenuEditBoxTemplate";
            listened = (1 << FrameEvents.TextChanged) | (1 << FrameEvents.TextEntered);
            width = 0.2f;
            height = 0.03f;
            sized = true;
        }

        /// <summary>The text: what the player last typed, or what the map last set, the same on every machine; empty unless set.</summary>
        [LocalSafe]
        public string Text
        {
            get { return text; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                // A set to the text it reads is not given to the game, where it would undo typing on its way and come back as the player's (review G4).
                if (handle == null || value == text)
                {
                    text = value;
                    return;
                }

                sent ??= new List<string>();
                sent.Add(value);
                if (sent.Count > 8)
                {
                    sent.RemoveAt(0);
                }

                text = value;
                FramesSystem.World.WriteFrameText(handle, value);
            }
        }

        /// <summary>The most characters the player can type; 0 unless set, which leaves the game's. Once the game has a limit of the map's, 0 is refused and reported, as the game's own cannot be given back.</summary>
        [LocalSafe]
        public int MaxLength
        {
            get { return maxLength; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                if (value <= 0 && maxLength > 0 && handle != null)
                {
                    Errors.Refuse(Names.NameOf(this) + ".MaxLength", "the game's own limit cannot be given back once the box has one of the map's: give a limit of your own (review E09)");
                    return;
                }

                maxLength = value;
                if (handle != null && value > 0)
                {
                    FramesSystem.World.LimitFrameText(handle, value);
                }
            }
        }

        /// <summary>Runs when the player changes the text, with the player and the whole text. Not inside Local.For.</summary>
        public event Action<Player, string>? TextChanged
        {
            add { Subscribe(ref textChanged, value, FrameEvents.TextChanged, FrameEvents.TextEntered); }
            remove { Unsubscribe(textChanged, value); }
        }

        /// <summary>Runs when the player presses Enter in it, with the player and the whole text. Not inside Local.For.</summary>
        public event Action<Player, string>? Submitted
        {
            add { Subscribe(ref submitted, value, FrameEvents.TextChanged, FrameEvents.TextEntered); }
            remove { Unsubscribe(submitted, value); }
        }

        /// <summary>An EDITBOX, for a text box whose template the game lacks.</summary>
        internal override string GameType => "EDITBOX";

        internal override bool ForOnePlayerOnly() => true;

        /// <summary>An ended text box given a limit goes only to one given its own, as the game's own limit cannot be given back.</summary>
        internal override bool Fits(Frame old)
        {
            return base.Fits(old) && (maxLength > 0 || ((TextBox)old).maxLength == 0);
        }

        /// <summary>Clears an ended text box's text when this one has none; Place writes one it has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (text.Length == 0)
            {
                FramesSystem.World.WriteFrameText(handle!, "");
            }
        }

        /// <summary>Gives the game the limit and the text, then the points and looks.</summary>
        internal override void Place()
        {
            if (maxLength > 0)
            {
                FramesSystem.World.LimitFrameText(handle!, maxLength);
            }

            if (text.Length > 0)
            {
                FramesSystem.World.WriteFrameText(handle!, text);
            }

            base.Place();
        }

        /// <summary>A new text is kept and runs TextChanged; Enter runs Submitted.</summary>
        internal override void Receive(int kind, Player who, float value, string eventText)
        {
            if (kind == FrameEvents.TextChanged)
            {
                if (eventText == text)
                {
                    sent?.Remove(eventText);
                    return;
                }

                if (sent != null && sent.Remove(eventText))
                {
                    text = eventText;
                    return;
                }

                text = eventText;
                Raise(textChanged, who, eventText, "TextChanged");
            }
            else if (kind == FrameEvents.TextEntered)
            {
                text = eventText;
                Raise(submitted, who, eventText, "Submitted");
            }
            else
            {
                base.Receive(kind, who, value, eventText);
            }
        }
    }
}
