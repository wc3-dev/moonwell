using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The game's dialog: a message and buttons in the middle of the screen, shown to players, in a scope, which takes it off
    /// every screen when it ends, as End does. A click runs its button's action with the clicking player, as a synced event, so
    /// it may change the game, and closes the dialog for that player. A player sees one dialog at a time: one shown while
    /// another is up waits, and shows when that one is answered or ends (decision 206). In single player a dialog pauses the
    /// game while it shows (seen 11-07). Every read gives what was set, the same on every machine.
    /// </summary>
    public sealed class Dialog : Scoped
    {
        /// <summary>The most buttons that fit on the screen: 16 ran off its bottom, 14 showed whole (seen 11-07).</summary>
        public const int MaxButtons = 14;

        private readonly List<Action<Player>> actions = new List<Action<Player>>();

        private readonly string message;

        private dialog? handle;

        private Dialog(string message)
        {
            this.message = message ?? "";
        }

        /// <summary>The message over the buttons.</summary>
        [LocalSafe]
        public string Message => message;

        /// <summary>The buttons added.</summary>
        [LocalSafe]
        public int ButtonCount => actions.Count;

        /// <summary>The game's dialog, for anything Moonwell does not do; null for one that ended before it was made.</summary>
        [LocalSafe]
        public dialog? Handle => handle;

        /// <summary>Makes a dialog with a message, in a scope, which ends it when it ends, and returns it; ShowTo shows it. In a scope that has ended it ends at once.</summary>
        public static Dialog Create(string message, Scope scope)
        {
            var made = new Dialog(message);
            scope.Adopt(made);
            if (!made.HasEnded)
            {
                made.handle = Runtime.World.MakeDialog(made.message);
            }

            return made;
        }

        /// <summary>
        /// Adds a button whose click runs an action with the clicking player. A hotkey is a capital letter or a digit ('E'); none
        /// unless given. A button added while the dialog shows appears the next time it shows. More than MaxButtons run off the
        /// screen, and a development build reports it.
        /// </summary>
        public void AddButton(string text, Action<Player> clicked, char hotkey = '\0')
        {
            if (handle == null || HasEnded)
            {
                return;
            }

            var index = actions.Count;
            actions.Add(clicked);
            Dev.Check(actions.Count <= MaxButtons, "Dialog.AddButton: a dialog of more than " + MaxButtons + " buttons runs off the screen");
            Runtime.World.AddDialogButton(handle, text ?? "", hotkey, from => Dialogs.Clicked(this, index, from));
        }

        /// <summary>Shows the dialog to a player, at once, or once the dialog they see is answered or ends; nothing when it shows to them or waits already.</summary>
        public void ShowTo(Player player)
        {
            if (handle != null && !HasEnded)
            {
                Dialogs.Show(this, player);
            }
        }

        /// <summary>Shows the dialog to each of the players, as ShowTo(player) does.</summary>
        public void ShowTo(IEnumerable<Player> players)
        {
            foreach (var player in players)
            {
                ShowTo(player);
            }
        }

        /// <summary>Whether the dialog is on a player's screen now: shown and not yet answered, not waiting behind another. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return Dialogs.Current(player) == this;
        }

        /// <summary>Runs a button's action.</summary>
        internal void Run(int index, Player from)
        {
            try
            {
                actions[index](from);
            }
            catch (Exception error)
            {
                Errors.Report("Dialog button " + index, error);
            }
        }

        /// <summary>Takes the dialog off every screen, showing what waited behind it, and destroys it.</summary>
        internal override void Finish()
        {
            Dialogs.Gone(this);
            if (handle != null)
            {
                Runtime.World.EndDialog(handle);
                handle = null;
            }
        }
    }

    /// <summary>The dialog each player sees and those that wait behind it, the same on every machine.</summary>
    internal static class Dialogs
    {
        private const int Slots = Player.MaxSlots;

        private static readonly Dialog?[] current = new Dialog?[Slots];

        private static readonly List<Dialog>[] waiting = Lists();

        internal static void Reset()
        {
            for (var slot = 0; slot < Slots; slot++)
            {
                current[slot] = null;
                waiting[slot].Clear();
            }
        }

        internal static Dialog? Current(Player player)
        {
            return current[player.Slot];
        }

        internal static void Show(Dialog dialog, Player player)
        {
            var slot = player.Slot;
            if (current[slot] == dialog || waiting[slot].Contains(dialog))
            {
                return;
            }

            if (current[slot] == null)
            {
                Display(dialog, player);
            }
            else
            {
                waiting[slot].Add(dialog);
            }
        }

        /// <summary>A button was clicked: the game closed the dialog for the player, so the next one waiting shows, then the button's action runs.</summary>
        internal static void Clicked(Dialog dialog, int index, player from)
        {
            var player = Player.Of(from);
            if (player == null || current[player.Slot] != dialog)
            {
                return;
            }

            current[player.Slot] = null;
            Next(player);
            dialog.Run(index, player);
        }

        internal static void Gone(Dialog dialog)
        {
            for (var slot = 0; slot < Slots; slot++)
            {
                waiting[slot].Remove(dialog);
                if (current[slot] == dialog)
                {
                    var player = Player.Get(slot);
                    current[slot] = null;
                    if (dialog.Handle != null)
                    {
                        Runtime.World.DisplayDialog(player.Handle, dialog.Handle, false);
                    }

                    Next(player);
                }
            }
        }

        // A dialog displayed from inside a click's event shows once the clicked one closes (seen 11-07).
        private static void Next(Player player)
        {
            var queue = waiting[player.Slot];
            if (queue.Count > 0)
            {
                var next = queue[0];
                queue.RemoveAt(0);
                Display(next, player);
            }
        }

        private static void Display(Dialog dialog, Player player)
        {
            current[player.Slot] = dialog;
            Runtime.World.DisplayDialog(player.Handle, dialog.Handle!, true);
        }

        private static List<Dialog>[] Lists()
        {
            var lists = new List<Dialog>[Slots];
            for (var slot = 0; slot < Slots; slot++)
            {
                lists[slot] = new List<Dialog>();
            }

            return lists;
        }
    }
}
