using System;

namespace Moonwell.Hosting
{
    /// <summary>The game as the keyboard sees it: the synced key events, the keys this machine holds now, and the messages that release a key let go while the game took no keyboard input. Keys are the game's codes, as Key numbers them.</summary>
    internal interface IKeyWorld
    {
        /// <summary>Starts delivering one player's key events: every key, with every modifier, pressed and released, runs the action. Inside it, <c>EventPlayer</c>, <c>EventKey</c>, <c>EventMeta</c> and <c>EventKeyDown</c> are the event's.</summary>
        void ListenKeys(player p, Action key);

        /// <summary>The player of the key event or the release message being delivered.</summary>
        player EventPlayer();

        /// <summary>The code of the key event being delivered; 0 for a key the world does not know.</summary>
        int EventKey();

        /// <summary>The modifiers held at the key event being delivered: 1 Shift, 2 Ctrl, 4 Alt, 8 Windows, added.</summary>
        int EventMeta();

        /// <summary>Whether the key event being delivered is a press, not a release.</summary>
        bool EventKeyDown();

        /// <summary>Whether this machine holds the key now.</summary>
        [MachineLocal]
        bool IsKeyHeld(int key);

        /// <summary>Runs the action when the player leaves the game; inside it, <c>EventPlayer</c> is the one who left.</summary>
        void ListenLeave(player p, Action left);

        /// <summary>Starts delivering one player's release messages: the action runs on every machine when one arrives. Inside it, <c>EventPlayer</c>, <c>EventReleasedKey</c> and <c>EventReleasedDowns</c> are the message's.</summary>
        void ListenReleases(player p, Action arrived);

        /// <summary>On that player's machine only: when it does not hold the key now, sends a release message with the key and the count of its downs (presses and repeats).</summary>
        void SendIfLetGo(player p, int key, int downs);

        /// <summary>The key of the release message being delivered.</summary>
        int EventReleasedKey();

        /// <summary>The count of downs the release message being delivered was sent with.</summary>
        int EventReleasedDowns();
    }
}
