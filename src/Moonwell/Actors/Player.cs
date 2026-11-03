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
        }

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

        /// <summary>The player's team.</summary>
        [LocalSafe]
        public int Team
        {
            get { return Runtime.World.TeamOf(Handle); }
            set { Runtime.World.MoveToTeam(Handle, value); }
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

        /// <summary>Sets what the player gives another player, e.g. Alliance.Allied | Alliance.SharedVision; the other's alliance with this one stays.</summary>
        public void SetAlliance(Player other, Alliance alliance)
        {
            Runtime.World.SetAllianceOf(Handle, other.Handle, (int)alliance);
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

        /// <summary>Adds a component to the player, and returns it.</summary>
        public T Add<T>(T component) where T : PlayerComponent => AddComponent(component);

        // What the two players give each other, unless they are one player.
        private void SetBothWays(Player other, Alliance alliance)
        {
            if (other != this)
            {
                SetAlliance(other, alliance);
                other.SetAlliance(this, alliance);
            }
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
