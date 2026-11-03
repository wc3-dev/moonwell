namespace Moonwell
{
    /// <summary>
    /// A player's keyboard: the keys and the modifiers its synced key events say are down. Get one with
    /// <c>player.Keyboard()</c>. They are the same on every machine, so a map reads them anywhere, a mouse reaction too
    /// (Shift and a click); a key held now is this machine's, and is read inside <c>Local.For</c>, whichever player's keyboard
    /// it is read from.
    /// </summary>
    public sealed class Keyboard
    {
        /// <summary>The keys the events say are down, by code.</summary>
        private readonly bool[] down = new bool[256];

        /// <summary>The game time of each key's last press or repeat, by code.</summary>
        private readonly float[] lastPress = new float[256];

        /// <summary>How many down events each key has had, presses and repeats, by code: a release message carries the count it was sent at, so a down after the send voids it.</summary>
        private readonly int[] downs = new int[256];

        /// <summary>Makes the keyboard of a slot.</summary>
        internal Keyboard(int slot)
        {
            Slot = slot;
        }

        /// <summary>The slot of the keyboard's player.</summary>
        internal int Slot { get; }

        /// <summary>The player of this keyboard.</summary>
        [LocalSafe]
        public Player Player => Player.Get(Slot);

        /// <summary>Whether Shift was held at the player's last key event, unless a Shift key let go unseen came up since. Online, events arrive after the network delay.</summary>
        [LocalSafe]
        public bool Shift { get; private set; }

        /// <summary>Whether Ctrl was held at the player's last key event, unless a Ctrl key let go unseen came up since.</summary>
        [LocalSafe]
        public bool Control { get; private set; }

        /// <summary>Whether Alt was held at the player's last key event, unless an Alt key let go unseen came up since.</summary>
        [LocalSafe]
        public bool Alt { get; private set; }

        /// <summary>Whether the events say a key is down: from its press to its release, so online it is the network delay behind the key. False for a number no key has.</summary>
        [LocalSafe]
        public bool IsDown(Key key)
        {
            var code = (int)key;
            return code > 0 && code < down.Length && down[code];
        }

        /// <summary>Whether this machine holds a key now, whichever player's keyboard it is read from, with no network delay (IsDown has it). This machine's alone, so it is read inside Local.For.</summary>
        [MachineLocal]
        public bool IsHeldNow(Key key)
        {
            return InputSystem.KeyWorld.IsKeyHeld((int)key);
        }

        /// <summary>Takes the modifiers a key event gave.</summary>
        internal void TakeModifiers(int meta)
        {
            Shift = (meta & 1) != 0;
            Control = (meta & 2) != 0;
            Alt = (meta & 4) != 0;
        }

        /// <summary>Whether the events say the key of a code (1 to 254) is down.</summary>
        internal bool IsDownCode(int code)
        {
            return down[code];
        }

        /// <summary>The game time of the key's last press or repeat.</summary>
        internal float LastPressOf(int code)
        {
            return lastPress[code];
        }

        /// <summary>How many down events the key has had, presses and repeats.</summary>
        internal int DownsOf(int code)
        {
            return downs[code];
        }

        /// <summary>How many keys the events say are down.</summary>
        internal int DownCount { get; private set; }

        /// <summary>Marks a key down by a new press at a game time.</summary>
        internal void TakePress(int code, float now)
        {
            if (!down[code])
            {
                DownCount++;
            }

            down[code] = true;
            lastPress[code] = now;
            downs[code]++;
        }

        /// <summary>Notes a repeat of a held key at a game time.</summary>
        internal void TakeRepeat(int code, float now)
        {
            lastPress[code] = now;
            downs[code]++;
        }

        /// <summary>Marks a key up.</summary>
        internal void TakeRelease(int code)
        {
            if (down[code])
            {
                DownCount--;
            }

            down[code] = false;
        }

        /// <summary>Marks every key up and takes every modifier off, for a player who left.</summary>
        internal void LetGoAll()
        {
            for (var code = 0; code < down.Length; code++)
            {
                down[code] = false;
            }

            DownCount = 0;
            Shift = false;
            Control = false;
            Alt = false;
        }

        /// <summary>Takes off each modifier whose keys (left and right) are all up: a release message gives no modifiers, as a key event does.</summary>
        internal void LetGoModifiers()
        {
            Shift = Shift && (down[(int)Key.LeftShift] || down[(int)Key.RightShift]);
            Control = Control && (down[(int)Key.LeftCtrl] || down[(int)Key.RightCtrl]);
            Alt = Alt && (down[(int)Key.LeftAlt] || down[(int)Key.RightAlt]);
        }
    }
}
