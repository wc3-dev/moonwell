using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A game component that logs every key reaction as "label pressed W" and "label released W", into a log a case shares.</summary>
    public sealed class LogKeyGame : GameComponent, IKeyWatch
    {
        /// <summary>The log the case shares.</summary>
        private readonly List<string> log;

        /// <summary>The name in each line.</summary>
        private readonly string label;

        /// <summary>The name of a key the cases use, spelt out so that .NET and Lua write the same line; any other is its number.</summary>
        public static string Name(Key key)
        {
            switch (key)
            {
                case Key.W: return "W";
                case Key.A: return "A";
                case Key.Q: return "Q";
                case Key.LeftShift: return "LeftShift";
                case Key.LeftCtrl: return "LeftCtrl";
                default: return ((int)key).ToString();
            }
        }

        /// <summary>Makes a component that logs under a label.</summary>
        public LogKeyGame(List<string> log, string label)
        {
            this.log = log;
            this.label = label;
        }

        /// <summary>Logs the press.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            log.Add(label + " pressed " + Name(key));
        }

        /// <summary>Logs the release.</summary>
        public void OnKeyReleased(Keyboard keyboard, Key key)
        {
            log.Add(label + " released " + Name(key));
        }
    }

    /// <summary>A player component that logs every key reaction as "label pressed W" and "label released W".</summary>
    public sealed class LogKeyPlayer : PlayerComponent, IKeyWatch
    {
        /// <summary>The log the case shares.</summary>
        private readonly List<string> log;

        /// <summary>The name in each line.</summary>
        private readonly string label;

        /// <summary>Makes a component that logs under a label.</summary>
        public LogKeyPlayer(List<string> log, string label)
        {
            this.log = log;
            this.label = label;
        }

        /// <summary>Logs the press, with the keyboard's player.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            log.Add(label + " pressed " + LogKeyGame.Name(key) + " of " + (keyboard.Player.Slot + 1));
        }

        /// <summary>Logs the release.</summary>
        public void OnKeyReleased(Keyboard keyboard, Key key)
        {
            log.Add(label + " released " + LogKeyGame.Name(key) + " of " + (keyboard.Player.Slot + 1));
        }
    }

    /// <summary>A game component that writes only OnKeyPressed.</summary>
    public sealed class PressOnlyKeys : GameComponent, IKeyWatch
    {
        /// <summary>The log the case shares.</summary>
        private readonly List<string> log;

        /// <summary>Makes a component that logs into a case's log.</summary>
        public PressOnlyKeys(List<string> log)
        {
            this.log = log;
        }

        /// <summary>Logs the press.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            log.Add("press only pressed " + LogKeyGame.Name(key));
        }
    }

    /// <summary>A game component whose OnKeyPressed throws.</summary>
    public sealed class ThrowingKeys : GameComponent, IKeyWatch
    {
        /// <summary>Throws.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            throw new InvalidOperationException("boom");
        }
    }

    /// <summary>A game component whose press reaction logs "adder pressed", adds a watcher to the game the first time, and ends another.</summary>
    public sealed class AddingKeys : GameComponent, IKeyWatch
    {
        private readonly List<string> log;

        private readonly GameComponent toAdd;

        private readonly GameComponent toEnd;

        private bool done;

        /// <summary>Makes it with the log, the watcher it adds and the one it ends.</summary>
        public AddingKeys(List<string> log, GameComponent added, GameComponent ended)
        {
            this.log = log;
            toAdd = added;
            toEnd = ended;
        }

        /// <summary>Logs, and the first time adds one watcher and ends the other.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            log.Add("adder pressed");
            if (!done)
            {
                done = true;
                Game.Instance.Add(toAdd);
                toEnd.End();
            }
        }
    }

    /// <summary>A game component that, at its first press, adds a player component to the player who pressed.</summary>
    public sealed class AddingPlayerKeys : GameComponent, IKeyWatch
    {
        private readonly PlayerComponent toAdd;

        private bool done;

        /// <summary>Makes it with the player component it adds.</summary>
        public AddingPlayerKeys(PlayerComponent added)
        {
            toAdd = added;
        }

        /// <summary>The first time, adds the player component.</summary>
        public void OnKeyPressed(Keyboard keyboard, Key key)
        {
            if (!done)
            {
                done = true;
                keyboard.Player.Add(toAdd);
            }
        }
    }
}
