using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A button with text, the game's dialog button (ScriptDialogButton). A click runs Clicked with the player; a second click
    /// by the same player within 0.3 s also runs DoubleClicked, as the game sends no double click (seen). After every click
    /// the keyboard goes back to the game on that player's machine, so the hotkeys go on (decision 161).
    /// </summary>
    public class Button : Frame
    {
        /// <summary>The longest time between two clicks of one player that make a double click, in seconds of the game.</summary>
        internal const float DoubleClickTime = 0.3f;

        internal string text = "";

        internal List<Action<Player>>? clicked;

        internal List<Action<Player>>? doubleClicked;

        /// <summary>When each player last clicked, by slot, until a double click uses it.</summary>
        internal Dictionary<int, float>? lastClicks;

        /// <summary>Makes a button, to be added to the screen or to another frame. It is registered for clicks from the start, to give the keyboard back.</summary>
        public Button()
        {
            Template = "ScriptDialogButton";
            listened = 1 << FrameEvents.Click;
        }

        /// <summary>The button's text, the same on every machine; empty unless set.</summary>
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

                text = value;
                if (handle != null)
                {
                    FramesSystem.World.WriteFrameText(handle, value);
                }
            }
        }

        /// <summary>Runs when a player clicks the button, with the player. Not inside Local.For.</summary>
        public event Action<Player>? Clicked
        {
            add { Subscribe(ref clicked, value, FrameEvents.Click, FrameEvents.Click); }
            remove { Unsubscribe(clicked, value); }
        }

        /// <summary>Runs when a player clicks the button a second time within 0.3 s, with the player, after that click's Clicked. Not inside Local.For.</summary>
        public event Action<Player>? DoubleClicked
        {
            add { Subscribe(ref doubleClicked, value, FrameEvents.Click, FrameEvents.Click); }
            remove { Unsubscribe(doubleClicked, value); }
        }

        /// <summary>A GLUETEXTBUTTON, for a button whose template the game lacks.</summary>
        internal override string GameType => "GLUETEXTBUTTON";

        /// <summary>A click gives the keyboard back, runs Clicked, and DoubleClicked when it is the player's second within the time.</summary>
        internal override void Receive(int kind, Player who, float value, string eventText)
        {
            if (kind != FrameEvents.Click)
            {
                base.Receive(kind, who, value, eventText);
                return;
            }

            GiveKeyboardBack(who);
            Raise(clicked, who, "Clicked");
            var now = Game.Time;
            lastClicks ??= new Dictionary<int, float>();
            if (lastClicks.TryGetValue(who.Slot, out var last) && now - last <= DoubleClickTime)
            {
                lastClicks.Remove(who.Slot);
                Raise(doubleClicked, who, "DoubleClicked");
            }
            else
            {
                lastClicks[who.Slot] = now;
            }
        }

        /// <summary>Clears an ended button's text when this one has none; Place writes one it has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (text.Length == 0)
            {
                FramesSystem.World.WriteFrameText(handle!, "");
            }
        }

        /// <summary>Gives the game the text, then the points and looks.</summary>
        internal override void Place()
        {
            if (text.Length > 0)
            {
                FramesSystem.World.WriteFrameText(handle!, text);
            }

            base.Place();
        }
    }
}
