namespace Moonwell
{
    /// <summary>
    /// Map-wide reactions to each player's keys, on the game's components (every player's keys) and a player's (their own);
    /// write only the ones needed, as public methods. Every key is heard, with any modifiers: read them on the keyboard
    /// (<c>keyboard.Shift</c>). The events are synced: every machine gets them at the same game time, so the reactions may
    /// change the game. Online they arrive after the network delay. The game still does what it does with a key (a hotkey,
    /// the arrows moving the camera), and keys typed into the chat box fire nothing.
    /// </summary>
    public interface IKeyWatch
    {
        /// <summary>When a key goes down: once per press, as the game's repeats of a held key run nothing. The keyboard already says the key is down and holds the event's modifiers.</summary>
        /// @CSharpLua.Ignore
        void OnKeyPressed(Keyboard keyboard, Key key)
        {
        }

        /// <summary>When a key comes up. A key held when the game loses the keyboard (Alt+Tab, a click outside its window, the Start menu) comes up too, a quarter second and the network delay late, as Moonwell asks the player's machine. Windows' switch of keyboard language (Alt+Shift) makes the game forget the keys held, so they come up then, though still held.</summary>
        /// @CSharpLua.Ignore
        void OnKeyReleased(Keyboard keyboard, Key key)
        {
        }
    }
}
