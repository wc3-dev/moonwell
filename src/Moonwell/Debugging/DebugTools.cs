#if DEBUG
using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The debug mode's commands past -debug, -step, -speed and -stats (decisions 261 to 265): what shows in the panel
    /// (-leaks, -profile, -scopes, -timers, -help), -find, the test cheats, and the map's own debug commands. They answer only
    /// while the mode is on; a map's own command of the same name wins, and the built-in one steps aside.
    /// </summary>
    internal static class DebugTools
    {
        /// <summary>The built-in commands this class answers, without the dash.</summary>
        private static readonly string[] BuiltIn = { "leaks", "profile", "scopes", "timers", "help", "find", "gold", "lumber", "time", "spawn", "level", "kill", "heal", "god", "nocd", "reveal" };

        /// <summary>The most units one -spawn makes.</summary>
        private const int MostSpawned = 100;

        /// <summary>The most scopes -leaks and -scopes walk, so a huge tree does not stall the game.</summary>
        private const int MostWalked = 20000;

        /// <summary>The most child scopes -scopes lists under one scope.</summary>
        private const int MostChildren = 8;

        /// <summary>The timers -timers lists.</summary>
        private const int MostTimers = 20;

        private static List<string> mapNames = new List<string>();
        private static List<string> mapHelps = new List<string>();
        private static List<Unit> gods = new List<Unit>();
        private static List<Player> noCooldowns = new List<Player>();
        private static List<Player> revealed = new List<Player>();
        private static List<Vision> visions = new List<Vision>();
        private static NoCooldowns? noCooldownWatch;

        /// <summary>Forgets every command, cheat and section; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            mapNames = new List<string>();
            mapHelps = new List<string>();
            gods = new List<Unit>();
            noCooldowns = new List<Player>();
            revealed = new List<Player>();
            visions = new List<Vision>();
            noCooldownWatch = null;
        }

        /// <summary>Whether a word is one of these built-in commands.</summary>
        internal static bool IsBuiltIn(string word)
        {
            return Array.IndexOf(BuiltIn, word) >= 0;
        }

        /// <summary>DebugMode.Command: a map's chat command for development builds, heard all game, which -help lists with its text.</summary>
        internal static void MapCommand(string name, string help, Action<Player, string> heard)
        {
            Game.Instance.Command(name, heard);
            mapNames.Add(name.ToLower());
            mapHelps.Add(help);
        }

        /// <summary>A chat line from a slot while the mode is on, trimmed, that no older debug command took: a built-in command of this class runs, unless a command of the map's has its name.</summary>
        internal static void Heard(int slot, string text)
        {
            if (text.Length < 2 || text[0] != '-')
            {
                return;
            }

            var space = text.IndexOf(' ');
            var word = (space < 0 ? text.Substring(1) : text.Substring(1, space - 1)).ToLower();
            var rest = space < 0 ? string.Empty : text.Substring(space + 1).Trim();
            if (!IsBuiltIn(word) || ChatCommand.Has(word))
            {
                return;
            }

            var player = Player.Get(slot);
            switch (word)
            {
                case "leaks":
                case "scopes":
                case "timers":
                case "help":
                    DebugPanel.ToggleSection(word);
                    break;
                case "profile":
                    Profiler.Toggle();
                    DebugPanel.ShowSection(Profiler.On ? "profile" : string.Empty);
                    break;
                case "find":
                    Find(rest);
                    break;
                case "gold":
                case "lumber":
                case "time":
                case "level":
                    WithNumber(word, rest, player);
                    break;
                case "spawn":
                    Spawn(player, rest);
                    break;
                case "nocd":
                    ToggleNoCooldowns(player);
                    break;
                case "reveal":
                    ToggleReveal(player);
                    break;
                default:
                    OnSelectedUnit(word);
                    break;
            }
        }

        /// <summary>The lines of a section of the panel.</summary>
        internal static void AddSection(string section, DebugLines lines)
        {
            if (section == "profile")
            {
                Profiler.AddLines(lines);
            }
            else if (section == "leaks")
            {
                AddLeaks(lines);
            }
            else if (section == "scopes")
            {
                var selected = DebugSession.Selected;
                Scope root = selected != null && !selected.HasEnded ? selected : Game.Instance;
                lines.Add("SCOPES  of " + DebugPanel.NameOf(root));
                AddScope(root, 0, lines);
            }
            else if (section == "timers")
            {
                AddTimers(lines);
            }
            else if (section == "help")
            {
                AddHelp(lines);
            }
        }

        /// <summary>-leaks: the scopes holding the most, the game's handles alive, and the ended objects still held.</summary>
        private static void AddLeaks(DebugLines lines)
        {
            lines.Add("LEAKS");
            var scopes = new List<Scope>();
            Walk(scopes);
            var top = new List<Scope>();
            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope.count == 0)
                {
                    continue;
                }

                var at = 0;
                while (at < top.Count && top[at].count >= scope.count)
                {
                    at++;
                }

                if (at < 5)
                {
                    top.Insert(at, scope);
                    if (top.Count > 5)
                    {
                        top.RemoveAt(5);
                    }
                }
            }

            for (var i = 0; i < top.Count; i++)
            {
                lines.Add(DebugPanel.NameOf(top[i]) + " holds " + DebugPanel.Count(top[i].count) + ": " + Leaks.KindsOf(top[i]));
            }

            var handles = Leaks.HandlesLine();
            lines.Add("handles alive: " + (handles.Length > 0 ? handles : "none counted"));
            lines.Add("ended but held over a minute: " + DebugPanel.Count(Leaks.Held) + (Leaks.Held > 0 ? " (" + HeldObjects.Summary() + ")" : string.Empty));
        }

        /// <summary>Every scope from the roots (the game, the players, and the units, items and destructables no scope holds), at most MostWalked.</summary>
        private static void Walk(List<Scope> found)
        {
            var roots = new List<Scope>();
            roots.Add(Game.Instance);
            var players = Player.Playing;
            for (var i = 0; i < players.Count; i++)
            {
                roots.Add(players[i]);
            }

            for (var i = 0; i < KnownUnits.Live.Count; i++)
            {
                AddRoot(KnownUnits.Live[i], roots);
            }

            for (var i = 0; i < KnownItems.Live.Count; i++)
            {
                AddRoot(KnownItems.Live[i], roots);
            }

            for (var i = 0; i < KnownDestructables.Live.Count; i++)
            {
                AddRoot(KnownDestructables.Live[i], roots);
            }

            var next = 0;
            found.AddRange(roots);
            while (next < found.Count && found.Count < MostWalked)
            {
                for (var child = found[next].newest; child != null && found.Count < MostWalked; child = child.before)
                {
                    if (child is Scope scope)
                    {
                        found.Add(scope);
                    }
                }

                next++;
            }
        }

        private static void AddRoot(Actor actor, List<Scope> roots)
        {
            if (actor.Parent == null && !actor.HasEnded)
            {
                roots.Add(actor);
            }
        }

        /// <summary>-scopes: a scope's line, "Rage 3: 12 (Timer 12)", and its child scopes holding the most, three levels deep.</summary>
        private static void AddScope(Scope scope, int depth, DebugLines lines)
        {
            var indent = depth == 0 ? string.Empty : (depth == 1 ? "  " : "    ");
            lines.Add(indent + DebugPanel.NameOf(scope) + ": " + DebugPanel.Count(scope.count) + (scope.count > 0 ? " (" + Leaks.KindsOf(scope) + ")" : string.Empty));
            if (depth == 2)
            {
                return;
            }

            var children = new List<Scope>();
            for (var child = scope.newest; child != null; child = child.before)
            {
                if (child is Scope inner)
                {
                    var at = 0;
                    while (at < children.Count && children[at].count >= inner.count)
                    {
                        at++;
                    }

                    children.Insert(at, inner);
                }
            }

            for (var i = 0; i < children.Count && i < MostChildren; i++)
            {
                AddScope(children[i], depth + 1, lines);
            }

            if (children.Count > MostChildren)
            {
                lines.Add(indent + "  and " + (children.Count - MostChildren) + " more scopes");
            }
        }

        /// <summary>-timers: the next timers due, "in 1.2 s · every 0.5 s · Spells/Rage.cs:12 · Rage 3".</summary>
        private static void AddTimers(DebugLines lines)
        {
            var timers = Runtime.Scheduler.Next(MostTimers);
            lines.Add("TIMERS  " + DebugPanel.Count(Runtime.Scheduler.Count) + " waiting, the next " + timers.Count);
            for (var i = 0; i < timers.Count; i++)
            {
                var timer = timers[i];
                var line = "in " + DebugPanel.Tenths(timer.Remaining) + " s · " + (timer.IsPeriodic ? "every " + DebugPanel.Tenths(timer.Interval) + " s" : "once");
                var made = Profiler.LineName(timer.madeLine);
                if (made.Length > 0)
                {
                    line = line + " · " + made;
                }

                if (timer.Parent != null)
                {
                    line = line + " · " + DebugPanel.NameOf(timer.Parent);
                }

                lines.Add(line);
            }
        }

        /// <summary>-help: the built-in commands, the map's debug commands with their text, and the map's own commands.</summary>
        private static void AddHelp(DebugLines lines)
        {
            lines.Add("HELP  while -debug is on; a command again hides what it shows");
            lines.Add("-debug  -step [n]  -speed [x]  -stats");
            lines.Add("-leaks  -profile  -scopes  -timers  -help");
            lines.Add("-find <class>: count its units, select the next");
            lines.Add("-gold <n>  -lumber <n>  -time <hour>");
            lines.Add("-spawn <class or id> [n]  -level <n>");
            lines.Add("-kill  -heal  -god: on the selected unit");
            lines.Add("-nocd  -reveal: for you, again to undo");
            for (var i = 0; i < mapNames.Count; i++)
            {
                lines.Add("-" + mapNames[i] + ": " + mapHelps[i]);
            }

            var own = ChatCommand.LiveNames();
            var text = string.Empty;
            for (var i = 0; i < own.Count; i++)
            {
                if (!mapNames.Contains(own[i]))
                {
                    text = text + (text.Length > 0 ? "  " : string.Empty) + "-" + own[i];
                }
            }

            if (text.Length > 0)
            {
                lines.Add("The map's: " + text);
            }
        }

        /// <summary>-find Footman: how many units of a class there are, and selects the next after the selected one.</summary>
        private static void Find(string name)
        {
            if (name.Length == 0)
            {
                Game.Print("-find takes a class: -find Footman.");
                return;
            }

            var wanted = name.ToLower();
            var found = new List<Unit>();
            for (var i = 0; i < KnownUnits.Live.Count; i++)
            {
                var unit = KnownUnits.Live[i];
                if (!unit.HasEnded && Names.NameOf(unit).ToLower() == wanted)
                {
                    found.Add(unit);
                }
            }

            if (found.Count == 0)
            {
                Game.Print("No units of the class " + name + ".");
                return;
            }

            var selected = DebugSession.Selected as Unit;
            var at = selected != null ? found.IndexOf(selected) : -1;
            var next = found[(at + 1) % found.Count];
            DebugSession.Select(next);
            Game.Print(Names.NameOf(next) + ": " + DebugPanel.Count(found.Count) + " units; selected " + DebugPanel.NameOf(next) + ".");
        }

        /// <summary>-gold, -lumber, -time and -level, which take a whole number.</summary>
        private static void WithNumber(string word, string rest, Player player)
        {
            if (!DebugSession.Number(rest, out var value))
            {
                Game.Print("-" + word + " takes a number: -" + word + " 10.");
                return;
            }

            var amount = (int)value;
            if (word == "gold")
            {
                player.Gold += amount;
            }
            else if (word == "lumber")
            {
                player.Lumber += amount;
            }
            else if (word == "time")
            {
                Game.TimeOfDay = value;
            }
            else if (DebugSession.Selected is Hero hero && !hero.HasEnded)
            {
                hero.SetLevel(amount);
            }
            else
            {
                Game.Print("Select a hero first: Ctrl + click.");
            }
        }

        /// <summary>-spawn Footman 5: units of a map's class or an object id ("hfoo") for the player, at the selected unit or the player's start.</summary>
        private static void Spawn(Player player, string rest)
        {
            var space = rest.IndexOf(' ');
            var name = space < 0 ? rest : rest.Substring(0, space);
            var count = 1;
            if (space >= 0)
            {
                var readCount = DebugSession.Number(rest.Substring(space + 1).Trim(), out var value);
                if (!readCount || value < 1.0f)
                {
                    Game.Print("-spawn takes a class or id and a count: -spawn Footman 5.");
                    return;
                }

                count = value > MostSpawned ? MostSpawned : (int)value;
            }

            var id = ObjectTypes.UnitIdOfName(name);
            if (id == 0 && name.Length == 4)
            {
                id = ObjectTypes.FourCC(name);
            }

            if (id == 0)
            {
                Game.Print("-spawn takes a unit class of the map or a four-letter id: -spawn Footman, -spawn hfoo.");
                return;
            }

            var x = player.StartX;
            var y = player.StartY;
            var selected = DebugSession.Selected;
            if (selected != null && !selected.HasEnded && DebugSession.PlaceOf(selected, out var sx, out var sy))
            {
                x = sx;
                y = sy;
            }

            for (var i = 0; i < count; i++)
            {
                Unit.Create(id, player, x, y, Game.Instance);
            }
        }

        /// <summary>-kill, -heal and -god, on the selected unit.</summary>
        private static void OnSelectedUnit(string word)
        {
            var unit = DebugSession.Selected as Unit;
            if (unit == null || unit.HasEnded)
            {
                Game.Print("Select a unit first: Ctrl + click.");
                return;
            }

            if (word == "kill")
            {
                unit.Kill();
            }
            else if (word == "heal")
            {
                unit.Life = unit.MaxLife;
                unit.Mana = unit.MaxMana;
            }
            else
            {
                var god = !gods.Contains(unit);
                if (god)
                {
                    gods.Add(unit);
                }
                else
                {
                    gods.Remove(unit);
                }

                DebugSession.SetInvulnerable(unit, god);
                Game.Print(DebugPanel.NameOf(unit) + (god ? " can't be hurt." : " can be hurt again."));
            }
        }

        /// <summary>-nocd: a player's units' cooldowns end after each cast, or no longer.</summary>
        private static void ToggleNoCooldowns(Player player)
        {
            if (noCooldowns.Remove(player))
            {
                Game.Print("Cooldowns are back for " + player.Name + ".");
                return;
            }

            noCooldowns.Add(player);
            if (noCooldownWatch == null)
            {
                noCooldownWatch = Game.Instance.Add(new NoCooldowns());
            }

            Game.Print("No cooldowns for " + player.Name + ".");
        }

        /// <summary>Whether a player's units have no cooldowns.</summary>
        internal static bool HasNoCooldowns(Player player)
        {
            return noCooldowns.Contains(player);
        }

        /// <summary>-reveal: the whole map seen by a player, or no longer.</summary>
        private static void ToggleReveal(Player player)
        {
            var at = revealed.IndexOf(player);
            if (at >= 0)
            {
                visions[at].End();
                revealed.RemoveAt(at);
                visions.RemoveAt(at);
                Game.Print("The map is hidden again for " + player.Name + ".");
                return;
            }

            revealed.Add(player);
            visions.Add(player.Reveal(Game.Instance));
            Game.Print("The map is revealed for " + player.Name + ".");
        }
    }

    /// <summary>-nocd's watch: after each cast by a unit of a player with no cooldowns, the ability's cooldown ends; the game starts it after the effect event, so a zero-second timer ends it (game facts).</summary>
    internal sealed class NoCooldowns : GameComponent
    {
        public override void OnUnitCast(Cast cast)
        {
            if (DebugTools.HasNoCooldowns(cast.Caster.Owner))
            {
                var ability = cast.Ability;
                Game.Instance.After(0.0f, ability.ResetCooldown);
            }
        }
    }
}
#endif
