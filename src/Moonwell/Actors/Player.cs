using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>A player slot of the map: an actor that holds the components of one player, with one object per slot.</summary>
    public sealed class Player : Actor
    {
        /// <summary>The number of player slots, the game's bj_MAX_PLAYER_SLOTS.</summary>
        public const int MaxSlots = 28;

        /// <summary>The number of slots a person or a computer can play in, the game's bj_MAX_PLAYERS.</summary>
        public const int PlayableSlots = 24;

        /// <summary>No limit: what MaxAllowed reads for a unit class without one, and what SetMaxAllowed takes to lift one.</summary>
        public const int NoLimit = int.MaxValue;

        /// <summary>The player object of each slot, made when the slot is first asked for; null until then. The array is made on first use, so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static Player?[]? bySlot;

        /// <summary>Makes the object of a slot, with the game's player for it.</summary>
        private Player(int slot, player handle)
        {
            Slot = slot;
            Handle = handle;
            StartName = Runtime.World.NameOf(handle);
        }

        /// <summary>The units the game's events say the player has selected, in the order selected, and each one's owner then; a unit the game dropped with no event stays until a read or an event outside Local.For finds it gone.</summary>
        private readonly List<Unit> selection = new List<Unit>();

        /// <summary>The owner of each unit in selection when it was selected, in the same order.</summary>
        private readonly List<Player> selectedOwners = new List<Player>();

        /// <summary>The player's name when the player object was made, at the start, which a save is tied to, as the map may rename the player.</summary>
        internal string StartName { get; }

        /// <summary>The slot of the player.</summary>
        [LocalSafe]
        public int Slot { get; }

        /// <summary>The game's player in this slot.</summary>
        [LocalSafe]
        public player Handle { get; }

        /// <summary>The player object of a slot; made on first ask.</summary>
        [LocalSafe]
        public static Player Get(int slot)
        {
            if (bySlot == null)
            {
                bySlot = new Player?[MaxSlots];
            }

            var existing = bySlot[slot];
            if (existing != null)
            {
                return existing;
            }

            var made = new Player(slot, Runtime.World.PlayerAt(slot));
            bySlot[slot] = made;
            return made;
        }

        /// <summary>The x of the player's start location.</summary>
        [LocalSafe]
        public float StartX => Runtime.World.StartXOf(Handle);

        /// <summary>The y of the player's start location.</summary>
        [LocalSafe]
        public float StartY => Runtime.World.StartYOf(Handle);

        /// <summary>The player in slot 0, the lobby's Player 1. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player One => Get(0);

        /// <summary>The player in slot 1, the lobby's Player 2. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Two => Get(1);

        /// <summary>The player in slot 2, the lobby's Player 3. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Three => Get(2);

        /// <summary>The player in slot 3, the lobby's Player 4. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Four => Get(3);

        /// <summary>The player in slot 4, the lobby's Player 5. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Five => Get(4);

        /// <summary>The player in slot 5, the lobby's Player 6. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Six => Get(5);

        /// <summary>The player in slot 6, the lobby's Player 7. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Seven => Get(6);

        /// <summary>The player in slot 7, the lobby's Player 8. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Eight => Get(7);

        /// <summary>The player in slot 8, the lobby's Player 9. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Nine => Get(8);

        /// <summary>The player in slot 9, the lobby's Player 10. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Ten => Get(9);

        /// <summary>The player in slot 10, the lobby's Player 11. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Eleven => Get(10);

        /// <summary>The player in slot 11, the lobby's Player 12. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Twelve => Get(11);

        /// <summary>The player in slot 12, the lobby's Player 13. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Thirteen => Get(12);

        /// <summary>The player in slot 13, the lobby's Player 14. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Fourteen => Get(13);

        /// <summary>The player in slot 14, the lobby's Player 15. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Fifteen => Get(14);

        /// <summary>The player in slot 15, the lobby's Player 16. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Sixteen => Get(15);

        /// <summary>The player in slot 16, the lobby's Player 17. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Seventeen => Get(16);

        /// <summary>The player in slot 17, the lobby's Player 18. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Eighteen => Get(17);

        /// <summary>The player in slot 18, the lobby's Player 19. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Nineteen => Get(18);

        /// <summary>The player in slot 19, the lobby's Player 20. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player Twenty => Get(19);

        /// <summary>The player in slot 20, the lobby's Player 21. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player TwentyOne => Get(20);

        /// <summary>The player in slot 21, the lobby's Player 22. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player TwentyTwo => Get(21);

        /// <summary>The player in slot 22, the lobby's Player 23. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player TwentyThree => Get(22);

        /// <summary>The player in slot 23, the lobby's Player 24. A slot is not a colour: a map can recolour players.</summary>
        [LocalSafe]
        public static Player TwentyFour => Get(23);

        /// <summary>The player in the game with this colour, the first in slot order; null when nobody in the game has it.</summary>
        [LocalSafe]
        public static Player? ByColor(PlayerColor color)
        {
            var playing = Playing;
            for (var i = 0; i < playing.Count; i++)
            {
                if (playing[i].Color == color)
                {
                    return playing[i];
                }
            }

            return null;
        }

        /// <summary>The player in the game coloured red, the lobby's red; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Red => ByColor(PlayerColor.Red);

        /// <summary>The player in the game coloured blue, the lobby's blue; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Blue => ByColor(PlayerColor.Blue);

        /// <summary>The player in the game coloured cyan, the lobby's teal; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Cyan => ByColor(PlayerColor.Cyan);

        /// <summary>The player in the game coloured purple, the lobby's purple; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Purple => ByColor(PlayerColor.Purple);

        /// <summary>The player in the game coloured yellow, the lobby's yellow; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Yellow => ByColor(PlayerColor.Yellow);

        /// <summary>The player in the game coloured orange, the lobby's orange; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Orange => ByColor(PlayerColor.Orange);

        /// <summary>The player in the game coloured green, the lobby's green; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Green => ByColor(PlayerColor.Green);

        /// <summary>The player in the game coloured pink, the lobby's pink; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Pink => ByColor(PlayerColor.Pink);

        /// <summary>The player in the game coloured light gray, the lobby's gray; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? LightGray => ByColor(PlayerColor.LightGray);

        /// <summary>The player in the game coloured light blue, the lobby's light blue; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? LightBlue => ByColor(PlayerColor.LightBlue);

        /// <summary>The player in the game coloured aqua, the lobby's dark green; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Aqua => ByColor(PlayerColor.Aqua);

        /// <summary>The player in the game coloured brown, the lobby's brown; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Brown => ByColor(PlayerColor.Brown);

        /// <summary>The player in the game coloured maroon, the lobby's maroon; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Maroon => ByColor(PlayerColor.Maroon);

        /// <summary>The player in the game coloured navy, the lobby's navy; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Navy => ByColor(PlayerColor.Navy);

        /// <summary>The player in the game coloured turquoise, the lobby's turquoise; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Turquoise => ByColor(PlayerColor.Turquoise);

        /// <summary>The player in the game coloured violet, the lobby's violet; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Violet => ByColor(PlayerColor.Violet);

        /// <summary>The player in the game coloured wheat, the lobby's wheat; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Wheat => ByColor(PlayerColor.Wheat);

        /// <summary>The player in the game coloured peach, the lobby's peach; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Peach => ByColor(PlayerColor.Peach);

        /// <summary>The player in the game coloured mint, the lobby's mint; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Mint => ByColor(PlayerColor.Mint);

        /// <summary>The player in the game coloured lavender, the lobby's lavender; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Lavender => ByColor(PlayerColor.Lavender);

        /// <summary>The player in the game coloured coal, the lobby's coal; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Coal => ByColor(PlayerColor.Coal);

        /// <summary>The player in the game coloured snow, the lobby's snow; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Snow => ByColor(PlayerColor.Snow);

        /// <summary>The player in the game coloured emerald, the lobby's emerald; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Emerald => ByColor(PlayerColor.Emerald);

        /// <summary>The player in the game coloured peanut, the lobby's peanut; null when nobody in the game is.</summary>
        [LocalSafe]
        public static Player? Peanut => ByColor(PlayerColor.Peanut);

        /// <summary>The neutral hostile player, which owns creeps.</summary>
        [LocalSafe]
        public static Player NeutralAggressive => Get(Runtime.World.NeutralAggressiveSlot());

        /// <summary>The neutral victim player.</summary>
        [LocalSafe]
        public static Player NeutralVictim => Get(Runtime.World.NeutralVictimSlot());

        /// <summary>The neutral extra player.</summary>
        [LocalSafe]
        public static Player NeutralExtra => Get(Runtime.World.NeutralExtraSlot());

        /// <summary>The neutral passive player, which owns critters and shops.</summary>
        [LocalSafe]
        public static Player NeutralPassive => Get(Runtime.World.NeutralPassiveSlot());

        /// <summary>The player object of a game player; null for null.</summary>
        [LocalSafe]
        public static Player? Of(player? handle)
        {
            if (handle == null)
            {
                return null;
            }

            return Get(Runtime.World.SlotOf(handle));
        }

        /// <summary>The player object of a slot when one has been made; null otherwise.</summary>
        internal static Player? Existing(int slot)
        {
            if (slot < 0 || slot >= MaxSlots || bySlot == null)
            {
                return null;
            }

            return bySlot[slot];
        }

        /// <summary>The gold the player has.</summary>
        [LocalSafe]
        public int Gold
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.Gold); }
            set { Runtime.World.SetState(Handle, PlayerState.Gold, value); }
        }

        /// <summary>The lumber the player has.</summary>
        [LocalSafe]
        public int Lumber
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.Lumber); }
            set { Runtime.World.SetState(Handle, PlayerState.Lumber, value); }
        }

        /// <summary>The food the player's units use.</summary>
        [LocalSafe]
        public int FoodUsed
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.FoodUsed); }
            set { Runtime.World.SetState(Handle, PlayerState.FoodUsed, value); }
        }

        /// <summary>The food the player's buildings give.</summary>
        [LocalSafe]
        public int FoodCap
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.FoodCap); }
            set { Runtime.World.SetState(Handle, PlayerState.FoodCap, value); }
        }

        /// <summary>The most food the player can have, whatever its buildings give.</summary>
        [LocalSafe]
        public int FoodCapCeiling
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.FoodCapCeiling); }
            set { Runtime.World.SetState(Handle, PlayerState.FoodCapCeiling, value); }
        }

        /// <summary>The gold the player has gathered in the game.</summary>
        [LocalSafe]
        public int GoldGathered
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.GoldGathered); }
            set { Runtime.World.SetState(Handle, PlayerState.GoldGathered, value); }
        }

        /// <summary>The lumber the player has gathered in the game.</summary>
        [LocalSafe]
        public int LumberGathered
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.LumberGathered); }
            set { Runtime.World.SetState(Handle, PlayerState.LumberGathered, value); }
        }

        /// <summary>The percent of gathered gold the player loses to upkeep.</summary>
        [LocalSafe]
        public int GoldUpkeepRate
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.GoldUpkeepRate); }
            set { Runtime.World.SetState(Handle, PlayerState.GoldUpkeepRate, value); }
        }

        /// <summary>The percent of gathered lumber the player loses to upkeep.</summary>
        [LocalSafe]
        public int LumberUpkeepRate
        {
            get { return Runtime.World.StateOf(Handle, PlayerState.LumberUpkeepRate); }
            set { Runtime.World.SetState(Handle, PlayerState.LumberUpkeepRate, value); }
        }

        /// <summary>Shows a line to the player, for the seconds, or for the game's own time without them; it changes nothing in the game.</summary>
        [LocalSafe]
        public void Print(string text, float seconds = -1.0f)
        {
            Runtime.World.PrintTo(Handle, text, seconds);
        }

        /// <summary>Shows the player a line where the game shows its errors ("Not enough gold."), in their colour and with the error sound; it changes nothing in the game. Not inside Local.For, as the sound is made on every machine.</summary>
        public void Error(string text)
        {
            Runtime.World.ErrorTo(Handle, text);
            Sound.Play("Sound\\Interface\\Error.wav", this);
        }

        /// <summary>Shows the player a hint as the game does, with its sound, for 12 seconds; it changes nothing in the game.</summary>
        [LocalSafe]
        public void Hint(string text)
        {
            if (Runtime.World.IsLocal(Handle))
            {
                Runtime.World.QuestNews(QuestNews.Hint, text);
            }
        }

        /// <summary>Shows the player a warning as the game does, with its sound, for 12 seconds; it changes nothing in the game.</summary>
        [LocalSafe]
        public void Warning(string text)
        {
            if (Runtime.World.IsLocal(Handle))
            {
                Runtime.World.QuestNews(QuestNews.Warning, text);
            }
        }

        /// <summary>Clears the lines shown on the player's screen; it changes nothing in the game.</summary>
        [LocalSafe]
        public void ClearMessages()
        {
            if (Runtime.World.IsLocal(Handle))
            {
                Runtime.World.ClearMessages();
            }
        }

        /// <summary>Pings the minimap for the player alone, in white unless a colour is given, for the seconds; it changes nothing in the game. PingStyle.Attack is the game's red warning ping, whatever the colour.</summary>
        [LocalSafe]
        public void Ping(float x, float y, float seconds = 3.0f, PingStyle style = PingStyle.Simple)
        {
            Ping(x, y, Moonwell.Color.White, seconds, style);
        }

        /// <summary>Pings the minimap for the player alone in a colour, as Ping does.</summary>
        [LocalSafe]
        public void Ping(float x, float y, Color color, float seconds = 3.0f, PingStyle style = PingStyle.Simple)
        {
            if (Runtime.World.IsLocal(Handle))
            {
                Game.Ping(x, y, color, seconds, style);
            }
        }

        /// <summary>Lets the player see a circle of the map until the scope ends, or the vision's End, and returns the vision; allies who share the player's vision see it too. The ground stays explored after.</summary>
        public Vision Reveal(float x, float y, float radius, Scope scope)
        {
            return Vision.Circle(this, Vision.Visible, x, y, radius, false, scope);
        }

        /// <summary>Lets the player see a rect of the map, by its left, bottom, right and top edges, as Reveal of a circle does.</summary>
        public Vision Reveal(float left, float bottom, float right, float top, Scope scope)
        {
            return Vision.Rect(this, Vision.Visible, left, bottom, right, top, false, scope);
        }

        /// <summary>Lets the player see the whole map, as Reveal of a circle does.</summary>
        public Vision Reveal(Scope scope)
        {
            var world = Runtime.World.WorldBounds();
            return Reveal(world[0], world[1], world[2], world[3], scope);
        }

        /// <summary>Covers a circle of the map in fog for the player, or in the black mask when black is true, until the scope ends, or the vision's End, and returns the vision; with overUnits, also over what the player's units see there. The newest of the player's visions on a place wins over older ones, and one with overUnits over those without.</summary>
        public Vision Hide(float x, float y, float radius, Scope scope, bool black = false, bool overUnits = false)
        {
            return Vision.Circle(this, black ? Vision.Masked : Vision.Fogged, x, y, radius, overUnits, scope);
        }

        /// <summary>Covers a rect of the map, by its left, bottom, right and top edges, as Hide of a circle does.</summary>
        public Vision Hide(float left, float bottom, float right, float top, Scope scope, bool black = false, bool overUnits = false)
        {
            return Vision.Rect(this, black ? Vision.Masked : Vision.Fogged, left, bottom, right, top, overUnits, scope);
        }

        /// <summary>Covers the whole map, as Hide of a circle does.</summary>
        public Vision Hide(Scope scope, bool black = false, bool overUnits = false)
        {
            var world = Runtime.World.WorldBounds();
            return Hide(world[0], world[1], world[2], world[3], scope, black, overUnits);
        }

        /// <summary>Whether the slot is in the game: empty, playing, or left.</summary>
        [LocalSafe]
        public SlotState SlotState => (SlotState)Runtime.World.SlotStateOf(Handle);

        /// <summary>Who controls the slot: a person, a computer, or another kind.</summary>
        [LocalSafe]
        public Controller Controller => (Controller)Runtime.World.ControllerOf(Handle);

        /// <summary>The players in the game, people and computers, in slot order: a new list each time it is read.</summary>
        [LocalSafe]
        public static List<Player> Playing => InGame(false);

        /// <summary>The players in the game that a person controls, in slot order: a new list each time it is read.</summary>
        [LocalSafe]
        public static List<Player> Users => InGame(true);

        /// <summary>The level the player has of an upgrade class.</summary>
        [LocalSafe]
        public int LevelOf<T>() where T : Upgrade
        {
            return Runtime.World.UpgradeLevel(Handle, ObjectTypes.IdOf<T>());
        }

        /// <summary>Sets the level the player has of an upgrade class.</summary>
        public void SetLevel<T>(int level) where T : Upgrade
        {
            Runtime.World.Research(Handle, ObjectTypes.IdOf<T>(), level);
        }

        /// <summary>How many units of a unit class the player may have, or the top level of an upgrade class it may research; NoLimit for a unit class without a limit.</summary>
        [LocalSafe]
        public int MaxAllowed<T>() where T : class
        {
            return Runtime.World.TechMaxOf(Handle, ObjectTypes.IdOf<T>());
        }

        /// <summary>Sets how many units of a unit class the player may have, or the top level of an upgrade class; 0 forbids it, and NoLimit lifts a unit limit.</summary>
        public void SetMaxAllowed<T>(int max) where T : class
        {
            Runtime.World.SetTechMax(Handle, ObjectTypes.IdOf<T>(), max);
        }

        /// <summary>Makes an ability class available to the player's units or not.</summary>
        public void SetAvailable<T>(bool available) where T : Ability
        {
            Runtime.World.MakeAvailable(Handle, ObjectTypes.IdOf<T>(), available);
        }

        /// <summary>The player's name.</summary>
        [LocalSafe]
        public string Name
        {
            get { return Runtime.World.NameOf(Handle); }
            set { Runtime.World.Rename(Handle, value); }
        }

        /// <summary>The player's name in the player's colour, for a line such as "Thrall has fallen".</summary>
        [LocalSafe]
        public string ColoredName => Name.Colored(Moonwell.Color.Of(Color));

        /// <summary>The player's colour; setting it also recolours the units the player has.</summary>
        [LocalSafe]
        public PlayerColor Color
        {
            get { return (PlayerColor)Runtime.World.ColorOf(Handle); }
            set { Runtime.World.Recolor(Handle, (int)value); }
        }

        /// <summary>
        /// The player's team. Setting it moves the player: it becomes an ally of the new team's players, as the team sets
        /// (shared vision, shared control), and an enemy of the old team's, both ways. A team no slot of the map names is
        /// refused (decision 198).
        /// </summary>
        [LocalSafe]
        public Team Team
        {
            get
            {
                return Moonwell.Team.Of(Runtime.World.TeamOf(Handle));
            }

            set
            {
                if (value.Number < 0)
                {
                    Errors.Refuse(Names.NameOf(this) + ".Team", value.Name + " is not one of the map's teams: no slot of the map names it");
                    return;
                }

                var old = Team;
                Runtime.World.MoveToTeam(Handle, value.Number);
                var playing = Playing;
                for (var i = 0; i < playing.Count; i++)
                {
                    var other = playing[i];
                    var team = other.Team;
                    if (team == value)
                    {
                        SetBothWays(other, value.Gives);
                    }
                    else if (team == old)
                    {
                        SetBothWays(other, Alliance.None);
                    }
                }
            }
        }

        /// <summary>Whether the player has won or lost, by Win, Lose or Game.Over.</summary>
        internal bool HasResult { get; private set; }

        /// <summary>
        /// Takes the player out of the game as a winner, as World Editor's Victory does. A person sees the victory box, which
        /// pauses a single-player game, and its Continue ends their game, with the score screen unless scores is false; without
        /// the dialog it ends at once. The others play on. A player who has won or lost already, or is not in the game, is
        /// refused (decision 197).
        /// </summary>
        public void Win(bool dialog = true, bool scores = true)
        {
            if (TakesResult("Win"))
            {
                Runtime.World.Win(Handle, dialog, scores);
            }
        }

        /// <summary>
        /// Takes the player out of the game as a loser, as World Editor's Defeat does: its slot turns Left, its units stay, and
        /// OnLeave does not run. A person sees the defeat box with the message, or the game's own words without one; in single
        /// player it pauses the game and offers Restart. The others play on. A player who has won or lost already, or is not in
        /// the game, is refused (decision 197).
        /// </summary>
        public void Lose(string message = "")
        {
            if (TakesResult("Lose"))
            {
                Runtime.World.Lose(Handle, message);
            }
        }

        /// <summary>The race the player plays.</summary>
        [LocalSafe]
        public Race Race => (Race)Runtime.World.RaceOf(Handle);

        /// <summary>What the player gives another player; the other's alliance with this one can differ.</summary>
        [LocalSafe]
        public Alliance AllianceWith(Player other)
        {
            return (Alliance)Runtime.World.AllianceOf(Handle, other.Handle);
        }

        /// <summary>
        /// The units the player has selected, in the order selected, as the game's selection events told every machine: the
        /// same on every machine, so it is read anywhere, Local.For included (decision 203). It follows the events, so a click
        /// or a Select shows a moment later (0.1 to 0.2 s in single player, the network's delay online). It leaves out a unit
        /// the game drops with no event: one that died, is gone, is hidden, has Locust, or changed hands to a player who does not
        /// share control with this one (seen 11-06). A unit hidden and shown again before a read stays in it, though the game
        /// dropped it. A new list at each read.
        /// </summary>
        [LocalSafe]
        public List<Unit> Selection
        {
            get
            {
                var units = new List<Unit>();
                for (var i = 0; i < selection.Count; i++)
                {
                    if (StillSelected(i))
                    {
                        units.Add(selection[i]);
                    }
                    else if (Local.Depth == 0)
                    {
                        selection.RemoveAt(i);
                        selectedOwners.RemoveAt(i);
                        i--;
                    }
                }

                return units;
            }
        }

        /// <summary>Selects only this unit for the player, as a click does: the player's machine clears its selection and selects the unit, and the game's events then reach every machine, with OnDeselected and OnSelected, a moment later. Does nothing for a unit that is gone, dead, hidden or has Locust (seen 11-06). A change to the game: not for Local.For.</summary>
        public void Select(Unit unit)
        {
            if (unit.InGame())
            {
                Runtime.World.ClearSelectionFor(Handle);
                Runtime.World.SelectFor(Handle, unit.Handle, true);
            }
        }

        /// <summary>Adds a unit to the player's selection, as a Shift-click does; one already selected is selected again, with its OnSelected (seen 11-06). As Select otherwise.</summary>
        public void AddToSelection(Unit unit)
        {
            if (unit.InGame())
            {
                Runtime.World.SelectFor(Handle, unit.Handle, true);
            }
        }

        /// <summary>Takes a unit out of the player's selection, with its OnDeselected a moment later; one not selected runs nothing. As Select otherwise.</summary>
        public void Deselect(Unit unit)
        {
            if (unit.InGame())
            {
                Runtime.World.SelectFor(Handle, unit.Handle, false);
            }
        }

        /// <summary>Clears the player's selection, with OnDeselected for each unit a moment later. A change to the game: not for Local.For.</summary>
        public void ClearSelection()
        {
            Runtime.World.ClearSelectionFor(Handle);
        }

        /// <summary>A selection event reached this machine: the unit joins the selection at the end, or keeps its place with its owner now, or leaves it; then the units the game dropped are let go.</summary>
        internal void Selected(Unit unit, bool selected)
        {
            var at = selection.IndexOf(unit);
            if (selected && at < 0)
            {
                selection.Add(unit);
                selectedOwners.Add(unit.Owner);
            }
            else if (selected)
            {
                selectedOwners[at] = unit.Owner;
            }
            else if (at >= 0)
            {
                selection.RemoveAt(at);
                selectedOwners.RemoveAt(at);
            }

            for (var i = selection.Count - 1; i >= 0; i--)
            {
                if (!StillSelected(i))
                {
                    selection.RemoveAt(i);
                    selectedOwners.RemoveAt(i);
                }
            }
        }

        /// <summary>Whether the unit at an index of the selection is still selected: the game drops one that dies, is removed, hidden, given Locust or given to a player who shares no control with this one, and fires no event (seen 11-06).</summary>
        private bool StillSelected(int index)
        {
            var unit = selection[index];
            if (!unit.IsAlive || Runtime.World.IsHidden(unit.Handle) || unit.HasAbility(LocustId))
            {
                return false;
            }

            var owner = unit.Owner;
            return owner == selectedOwners[index] || owner == this || (owner.AllianceWith(this) & Alliance.SharedControl) != 0;
        }

        /// <summary>Locust, Aloc, which makes a unit unselectable.</summary>
        private const int LocustId = 0x416C6F63;

        /// <summary>Sets what the player gives another player, e.g. Alliance.Allied | Alliance.SharedVision; the other's alliance with this one stays.</summary>
        public void SetAlliance(Player other, Alliance alliance)
        {
            Inbound.HoldAlliances();
            try
            {
                Runtime.World.SetAllianceOf(Handle, other.Handle, (int)alliance);
            }
            finally
            {
                Inbound.ReleaseAlliances(this, other);
            }
        }

        /// <summary>Makes the two players allies, both ways, in place of what they gave each other, and lets each see what the other sees with sharedVision; does nothing for the player itself.</summary>
        public void MakeAllies(Player other, bool sharedVision = false)
        {
            SetBothWays(other, sharedVision ? Alliance.Allied | Alliance.SharedVision : Alliance.Allied);
        }

        /// <summary>Makes the two players enemies, both ways, with nothing left between them, shared vision and control included; does nothing for the player itself.</summary>
        public void MakeEnemies(Player other)
        {
            SetBothWays(other, Alliance.None);
        }

        /// <summary>Whether the other player sees what this player sees.</summary>
        [LocalSafe]
        public bool ShowsVisionTo(Player other)
        {
            return AllianceWith(other).HasFlag(Alliance.SharedVision);
        }

        /// <summary>Lets the other player see what this player sees; it goes one way and leaves the alliance as it is, and does nothing for the player itself.</summary>
        public void ShowVisionTo(Player other)
        {
            if (other != this)
            {
                SetAlliance(other, AllianceWith(other) | Alliance.SharedVision);
            }
        }

        /// <summary>Stops the other player seeing what this player sees; it goes one way and leaves the alliance as it is, and does nothing for the player itself.</summary>
        public void HideVisionFrom(Player other)
        {
            if (other != this)
            {
                SetAlliance(other, AllianceWith(other) & ~Alliance.SharedVision);
            }
        }

        /// <summary>Whether the game counts the other player as this player's ally.</summary>
        [LocalSafe]
        public bool IsAlly(Player other)
        {
            return Runtime.World.IsAllyOf(Handle, other.Handle);
        }

        /// <summary>Whether the game counts the other player as this player's enemy.</summary>
        [LocalSafe]
        public bool IsEnemy(Player other)
        {
            return Runtime.World.IsEnemyOf(Handle, other.Handle);
        }

        /// <summary>Adds a flat amount to a stat while a scope holds it (decision 221), and returns the modifier, whose End takes it off sooner: Modify(Stats.GoldBonus, 10, heldBy: this). A holder that has ended adds nothing, reported in development builds.</summary>
        public Modifier Modify(Stat stat, float amount, Scope heldBy) => Stats.Add(stat, amount, 0.0f, heldBy, "Player.Modify");

        /// <summary>Adds a percent to a stat while a scope holds it (decision 221), and returns the modifier: Modify(Stats.GoldBonus, percent: 20, heldBy: this). Percents add up, then multiply the base and the flat amounts.</summary>
        public Modifier Modify(Stat stat, Scope heldBy, float percent) => Stats.Add(stat, 0.0f, percent, heldBy, "Player.Modify");

        /// <summary>A stat's value now: (base + flat) × (1 + percents / 100). A player takes the map's own stats only: a game stat is refused, in every build.</summary>
        [LocalSafe]
        public float Get(Stat stat) => Stats.Get(stat);

        internal StatSheet? stats;

        internal StatSheet Stats => stats ??= new StatSheet(this);

        /// <summary>Adds a component to the player, and returns it; held by a scope, it also ends when that scope ends (decision 164).</summary>
        public T Add<T>(T component, Scope? heldBy = null) where T : PlayerComponent => AddComponent(component, heldBy);

        /// <summary>
        /// Casts a spell of a registered class at a unit, item or destructable for this player, with no unit of its own: a hidden
        /// dummy of this player's casts it, standing at the target, as a unit's CastOn&lt;T&gt; does. The game gives this player the
        /// bounty and experience; Moonwell names no unit as the spell's buff's or hit's source, nor as the killer (decision 172).
        /// </summary>
        public bool CastOn<T>(Widget target, Scope scope, int level = 1)
            where T : Ability => CastOn(ObjectTypes.IdOf<T>(), target, scope, level);

        /// <summary>Casts a spell by its ability id at a unit, item or destructable for this player, as CastOn&lt;T&gt; does.</summary>
        public bool CastOn(int abilityId, Widget target, Scope scope, int level = 1)
        {
            return Dummies.Cast(null, this, abilityId, target, 0.0f, 0.0f, scope, level);
        }

        /// <summary>Casts a spell of a registered class at a place for this player, through a hidden dummy of this player's standing at the place, as CastOn&lt;T&gt; does.</summary>
        public bool CastAt<T>(float x, float y, Scope scope, int level = 1)
            where T : Ability => CastAt(ObjectTypes.IdOf<T>(), x, y, scope, level);

        /// <summary>Casts a spell by its ability id at a place for this player, as CastAt&lt;T&gt; does.</summary>
        public bool CastAt(int abilityId, float x, float y, Scope scope, int level = 1)
        {
            return Dummies.Cast(null, this, abilityId, null, x, y, scope, level);
        }

        // What the two players give each other, unless they are one player.
        private void SetBothWays(Player other, Alliance alliance)
        {
            if (other != this)
            {
                SetAlliance(other, alliance);
                other.SetAlliance(this, alliance);
            }
        }

        // Whether the player can take a result: in the game, and none taken yet; it is taken if so.
        private bool TakesResult(string action)
        {
            if (HasResult)
            {
                Errors.Refuse(Names.NameOf(this) + "." + action, "the player has won or lost already");
                return false;
            }

            if (SlotState != SlotState.Playing)
            {
                Errors.Refuse(Names.NameOf(this) + "." + action, "the player is not in the game");
                return false;
            }

            HasResult = true;
            return true;
        }

        // The playing players of the slots a person or computer can have, people only when asked.
        private static List<Player> InGame(bool usersOnly)
        {
            var found = new List<Player>();
            for (var slot = 0; slot < PlayableSlots; slot++)
            {
                var player = Get(slot);
                if (player.SlotState == SlotState.Playing && (!usersOnly || player.Controller == Controller.User))
                {
                    found.Add(player);
                }
            }

            return found;
        }

        /// <summary>Forgets every player object; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            bySlot = null;
        }
    }
}
