using System;

namespace Moonwell
{
    /// <summary>Something that holds what is made in it, and ends it, newest first, when it ends.</summary>
    public abstract class Scope : Scoped
    {
        // The instance fields are internal, not private, as a map's subclass may name a field of its own the same:
        // CSharp.lua gives a subclass's field a name of its own only when it sees the base's field of that name,
        // and it does not see a private one, so in Lua the two would be one field (PrivateFieldTests).

        /// <summary>Internal, as what code makes sits in the tree of scopes: a map's scope of its own is a component.</summary>
        internal Scope()
        {
        }

        /// <summary>The newest child, the first one to end.</summary>
        internal Scoped? newest;

        /// <summary>Whether the children have ended; a child adopted from then on ends at once.</summary>
        internal bool childrenEnded;

        /// <summary>How many children are linked to this scope.</summary>
        internal int count;

        /// <summary>How many children this scope holds that have not ended.</summary>
        [LocalSafe]
        public int ChildCount
        {
            get
            {
                return count;
            }
        }

        /// <summary>Runs an action once after the seconds, as a timer in this scope: it ends with the scope if the scope ends first.</summary>
        public Timer After(float seconds, Action action)
        {
            Dev.Check(seconds >= 0.0f, Names.NameOf(this) + ".After: seconds must be 0 or more");
            return Timer.Start(new Timer(Runtime.World.Now(), seconds, false, action), this);
        }

        /// <summary>
        /// Runs an action at every interval of the seconds, as a timer in this scope, until the scope ends. Seconds of 0 or less
        /// are reported and refused in every build, since such a timer would freeze the game: the timer given back has ended and
        /// never runs (decision 74).
        /// </summary>
        public Timer Every(float seconds, Action action)
        {
            // Not more than 0, so NaN is refused too.
            if (!(seconds > 0.0f))
            {
                return Timer.Refuse(Names.NameOf(this) + ".Every", seconds, action);
            }

            return Timer.Start(new Timer(Runtime.World.Now(), seconds, true, action), this);
        }

        /// <summary>
        /// Reads a file on a player's computer, on their machine, and hands its text to every machine once it has arrived, as a
        /// child of this scope: nothing runs if the scope ends first. Null when there is no file, when the player leaves first,
        /// or when they are not a person playing.
        /// </summary>
        public void ReadFile(Player player, string path, Action<string?> arrived)
        {
            Sharing.Request(this, player, () => PlayerFile.Read(path), arrived);
        }

        /// <summary>
        /// Runs <paramref name="read"/> on the player's machine only, where it may read what only that machine knows, and hands
        /// its answer to every machine once it has arrived, as a child of this scope. Null when the player leaves first or is
        /// not a person playing.
        /// </summary>
        public void Share(Player player, Func<string?> read, Action<string?> arrived)
        {
            Sharing.Request(this, player, read, arrived);
        }

        /// <summary>Shares a whole number one machine knows with every machine, as the text Share does; 0 when none arrives.</summary>
        public void Share(Player player, Func<int> read, Action<int> arrived)
        {
            Sharing.Request(this, player, () => read().ToString(), text => arrived(text != null && int.TryParse(text, out var value) ? value : 0));
        }

        /// <summary>Shares a float one machine knows with every machine, exactly, as the text Share does; 0 when none arrives.</summary>
        public void Share(Player player, Func<float> read, Action<float> arrived)
        {
            Sharing.Request(this, player, () =>
            {
                var bytes = new ByteWriter();
                bytes.WriteFloat(read());
                return bytes.ToCode();
            }, text =>
            {
                var bytes = text == null ? null : ByteReader.FromCode(text);
                arrived(bytes != null && bytes.Remaining == 4 ? bytes.ReadFloat() : 0.0f);
            });
        }

        /// <summary>Shares a bool one machine knows with every machine, as the text Share does; false when none arrives.</summary>
        public void Share(Player player, Func<bool> read, Action<bool> arrived)
        {
            Sharing.Request(this, player, () => read() ? "1" : "0", text => arrived(text == "1"));
        }

        /// <summary>
        /// Hears a chat command, a line that starts with a dash and the name, in any case: <c>Command("load", (player, rest) =>
        /// ...)</c> hears "-load X7KQ-M3PA" from any person, with the rest of the line trimmed, on every machine, as a child of
        /// this scope: it ends when the scope ends, or when the command given back ends.
        /// </summary>
        public ChatCommand Command(string name, Action<Player, string> heard)
        {
            return ChatCommand.Make(this, name, heard);
        }

        /// <summary>
        /// Reads a player's save from the default slot's file on their computer, and hands it to every machine once it has
        /// arrived, as a child of this scope: nothing runs if the scope ends first. Null when there is no file, when it fails
        /// its check (it is then written to the log), when the player leaves first, or when they are not a person playing.
        /// </summary>
        public void Load<T>(Player player, Action<T?> arrived)
            where T : SaveGame, new()
        {
            SaveGame.Load(this, player, SaveGame.DefaultSlot, arrived);
        }

        /// <summary>Reads a player's save from a slot's file on their computer, as Load from the default slot does.</summary>
        public void Load<T>(Player player, string slot, Action<T?> arrived)
            where T : SaveGame, new()
        {
            SaveGame.Load(this, player, slot, arrived);
        }

        /// <summary>Runs an action once after the seconds, as a timer of Moonwell's own work in this scope, which the never-ran report skips.</summary>
        internal Timer OwnAfter(float seconds, Action action)
        {
            var timer = new Timer(Runtime.World.Now(), seconds, false, action);
            timer.own = true;
            return Timer.Start(timer, this);
        }

        /// <summary>Runs an action at every interval of the seconds, more than 0, as a timer of Moonwell's own work in this scope, which the never-ran report skips.</summary>
        internal Timer OwnEvery(float seconds, Action action)
        {
            var timer = new Timer(Runtime.World.Now(), seconds, true, action);
            timer.own = true;
            return Timer.Start(timer, this);
        }

        /// <summary>Takes a child under this scope; a child adopted by an ended scope ends at once. A child that already has a parent, or has ended, is left as it is, and the error is reported, in every build: linked twice, it would break the scopes' lists (decision 74).</summary>
        internal void Adopt(Scoped child)
        {
            if (child.Parent != null || child.HasEnded)
            {
                Errors.Refuse(Names.NameOf(this) + ".Adopt", Names.NameOf(child) + " already has a parent or has ended");
                return;
            }

            child.Parent = this;
            if (HasEnded || childrenEnded)
            {
                child.End();
                return;
            }

            child.before = newest;
            if (newest != null)
            {
                newest.after = child;
            }

            newest = child;
            child.linked = true;
            count++;
        }

        /// <summary>Takes a child out of the list of children, so it does not end with this scope.</summary>
        internal void Unlink(Scoped child)
        {
            if (!child.linked)
            {
                return;
            }

            if (child.after != null)
            {
                child.after.before = child.before;
            }
            else
            {
                newest = child.before;
            }

            if (child.before != null)
            {
                child.before.after = child.after;
            }

            child.before = null;
            child.after = null;
            child.linked = false;
            count--;
        }
    }
}
