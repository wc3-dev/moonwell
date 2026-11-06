using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check teams and the map's slots, victory and defeat, and the reaction to alliance changes (decisions 197 and 198).</summary>
    public static class TeamCases
    {
        private static readonly Team West = new Team("West") { SharedVision = true };
        private static readonly Team East = new Team("East");
        private static readonly Team Unlisted = new Team("Unlisted");

        /// <summary>Two teams: slots 0 and 1 on West, 2 and 3 on East, listed with East's first slot between West's.</summary>
        private sealed class TwoTeams : Map
        {
            public TwoTeams()
            {
                Slots.Add(new Slot(0, West, Controller.User), new Slot(2, East, Controller.Computer, Race.Orc), new Slot(1, West, Controller.User));
                Slots.Add(new Slot(3, East, Controller.Computer) { Name = "The Horde" });
            }
        }

        /// <summary>Logs each alliance change it hears, with who heard it.</summary>
        private sealed class AllianceLog : GameComponent
        {
            public override void OnAllianceChanged(Player givingPlayer, Player receivingPlayer, Alliance before)
            {
                TestLog.Lines.Add("game: " + givingPlayer.Slot + ">" + receivingPlayer.Slot + " before " + (int)before + " now " + (int)givingPlayer.AllianceWith(receivingPlayer));
            }
        }

        /// <summary>Logs the alliance changes its player hears.</summary>
        private sealed class PlayerAllianceLog : PlayerComponent
        {
            public override void OnAllianceChanged(Player givingPlayer, Player receivingPlayer, Alliance before)
            {
                TestLog.Lines.Add("player " + Player.Slot + ": " + givingPlayer.Slot + ">" + receivingPlayer.Slot);
            }
        }

        // The game's setup of TwoTeams: four players in the game, on their teams, allied within them as config makes them.
        private static void StartTwoTeams(FakeWorld world)
        {
            for (var slot = 0; slot < 4; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, slot < 2 ? Controller.User : Controller.Computer);
                world.MoveToTeam(world.PlayerAt(slot), slot < 2 ? 0 : 1);
            }

            Runtime.Install(new TwoTeams(), () => { });
            Player.One.MakeAllies(Player.Two, sharedVision: true);
            Player.Three.MakeAllies(Player.Four);
        }

        /// <summary>The map's teams are numbered in the order its slots first name them, and a player's Team is the declared one.</summary>
        public static void Numbered(FakeWorld world)
        {
            StartTwoTeams(world);

            Expect.Equal(0, West.Number, "West's number");
            Expect.Equal(1, East.Number, "East's number");
            Expect.Equal(-1, Unlisted.Number, "a team no slot names");
            Expect.Same(West, Player.Two.Team, "player two's team");
            Expect.Same(East, Player.Four.Team, "player four's team");
        }

        /// <summary>A team's Players are those in the game on it, in slot order.</summary>
        public static void Players(FakeWorld world)
        {
            StartTwoTeams(world);
            world.SetSlot(2, SlotState.Left, Controller.Computer);

            var east = East.Players;
            Expect.Equal(1, east.Count, "East's players in the game");
            Expect.Same(Player.Four, east[0], "East's player");
            Expect.Equal(2, West.Players.Count, "West's players");
            Expect.Equal(0, Unlisted.Players.Count, "a team no slot names has none");
        }

        /// <summary>Without the map's own teams, a team number is a team named after it, the same object each time.</summary>
        public static void Undeclared(FakeWorld world)
        {
            world.MoveToTeam(world.PlayerAt(2), 3);

            Expect.Equal("Team 4", Player.Three.Team.Name, "the name");
            Expect.Equal(3, Player.Three.Team.Number, "the number");
            Expect.Same(Player.Three.Team, Player.Three.Team, "the same team each time");
            Expect.Same(Player.One.Team, Player.Two.Team, "one team for one number");
        }

        /// <summary>Moving a player to a team makes it an ally of the new team's players, as the team sets, and an enemy of the old team's, both ways.</summary>
        public static void MoveSetsAlliances(FakeWorld world)
        {
            StartTwoTeams(world);

            Player.Two.Team = East;

            Expect.Equal(1, world.TeamOf(world.PlayerAt(1)), "the game's team");
            Expect.True(Player.Two.AllianceWith(Player.Three) == Alliance.Allied, "two gives three East's alliance");
            Expect.True(Player.Four.AllianceWith(Player.Two) == Alliance.Allied, "four gives two East's alliance");
            Expect.True(Player.Two.AllianceWith(Player.One) == Alliance.None, "two gives its old team nothing");
            Expect.True(Player.One.AllianceWith(Player.Two) == Alliance.None, "its old team gives it nothing");

            Player.Two.Team = West;

            Expect.True(Player.One.AllianceWith(Player.Two) == (Alliance.Allied | Alliance.SharedVision), "back on West, with its shared vision");
            Expect.True(Player.Three.AllianceWith(Player.Two) == Alliance.None, "East gives it nothing again");
        }

        /// <summary>A team no slot names is refused: the player stays where it was.</summary>
        public static void MoveToUnlistedRefused(FakeWorld world)
        {
            StartTwoTeams(world);

            Player.Two.Team = Unlisted;

            Expect.Same(West, Player.Two.Team, "the team kept");
            Expect.Equal(1, world.Logged.Count, "the refusal reported");
            Expect.True(world.Logged[0].Contains("Unlisted is not one of the map's teams"), "the refusal says why: " + world.Logged[0]);
        }

        /// <summary>Win and Lose take one player out each; a loser's slot turns Left, and the others play on.</summary>
        public static void WinAndLose(FakeWorld world)
        {
            StartTwoTeams(world);

            Player.One.Win();
            Player.Three.Lose("Your base has fallen.");
            Player.Four.Win(dialog: false);

            Expect.Equal(3, world.Results.Count, "results");
            Expect.Equal("win 0", world.Results[0], "the winner");
            Expect.Equal("lose 2: Your base has fallen.", world.Results[1], "the loser, with the message");
            Expect.Equal("win 3 (no dialog)", world.Results[2], "a winner without the dialog");
            Expect.True(Player.Three.SlotState == SlotState.Left, "the loser's slot");
            Expect.True(Player.Two.SlotState == SlotState.Playing, "another plays on");
        }

        /// <summary>A second Win or Lose, or one for a player not in the game, is refused.</summary>
        public static void SecondResultRefused(FakeWorld world)
        {
            StartTwoTeams(world);

            Player.One.Win();
            Player.One.Lose();
            Player.Six.Win();

            Expect.Equal(1, world.Results.Count, "results");
            Expect.Equal(2, world.Logged.Count, "refusals reported");
            Expect.True(world.Logged[0].Contains("won or lost already"), "the second result: " + world.Logged[0]);
            Expect.True(world.Logged[1].Contains("not in the game"), "an empty slot: " + world.Logged[1]);
        }

        /// <summary>Game.Over with a team: the others lose first, then the team wins; a player with a result already is left out.</summary>
        public static void OverTeam(FakeWorld world)
        {
            StartTwoTeams(world);
            Player.Four.Lose();
            world.Results.Clear();

            Game.Over(West);

            Expect.Equal(3, world.Results.Count, "results");
            Expect.Equal("lose 2: ", world.Results[0], "East's player in the game loses first");
            Expect.Equal("win 0", world.Results[1], "West's first");
            Expect.Equal("win 1", world.Results[2], "West's second");
            Expect.Equal(0, world.Logged.Count, "nothing refused");
        }

        /// <summary>Game.Over with one player: everyone else in the game loses, and the winner's dialog and scores are as given.</summary>
        public static void OverPlayer(FakeWorld world)
        {
            StartTwoTeams(world);

            Game.Over(Player.Two, scores: false);

            Expect.Equal(4, world.Results.Count, "results");
            Expect.Equal("lose 0: ", world.Results[0], "the first loser");
            Expect.Equal("lose 3: ", world.Results[2], "the last loser");
            Expect.Equal("win 1 (no scores)", world.Results[3], "the winner, last");
        }

        /// <summary>Ending the game or a player as a scope is still refused, and says what ends them.</summary>
        public static void EndRefusalNamesOver(FakeWorld world)
        {
            Game.Instance.End();

            Expect.Equal(1, world.Logged.Count, "the refusal reported");
            Expect.True(world.Logged[0].Contains("Game.Over") && world.Logged[0].Contains("Win or Lose"), "it names Game.Over, Win and Lose: " + world.Logged[0]);
        }

        /// <summary>A set runs the reaction once for its pair, after the change, with what was given before; one that changes nothing runs none.</summary>
        public static void AllianceOncePerPair(FakeWorld world)
        {
            TestLog.Lines.Clear();
            Game.Instance.Add(new AllianceLog());

            Player.One.SetAlliance(Player.Two, Alliance.Allied | Alliance.SharedVision);
            Player.One.SetAlliance(Player.Two, Alliance.Allied | Alliance.SharedVision);
            Player.One.MakeEnemies(Player.Two);

            Expect.Equal(2, TestLog.Lines.Count, "reactions");
            Expect.Equal("game: 0>1 before 0 now " + (int)(Alliance.Allied | Alliance.SharedVision), TestLog.Lines[0], "the set");
            Expect.Equal("game: 0>1 before " + (int)(Alliance.Allied | Alliance.SharedVision) + " now 0", TestLog.Lines[1], "the enemies: two gave one nothing, so only one way changed");
        }

        /// <summary>The game's components hear a change first, then the giving player's, then the receiving player's.</summary>
        public static void AllianceOrder(FakeWorld world)
        {
            TestLog.Lines.Clear();
            Player.Two.Add(new PlayerAllianceLog());
            Player.One.Add(new PlayerAllianceLog());
            Player.Three.Add(new PlayerAllianceLog());
            Game.Instance.Add(new AllianceLog());

            Player.One.SetAlliance(Player.Two, Alliance.Passive);

            Expect.Equal(3, TestLog.Lines.Count, "reactions");
            Expect.Equal("game: 0>1 before 0 now 1", TestLog.Lines[0], "the game's first");
            Expect.Equal("player 0: 0>1", TestLog.Lines[1], "then the giver's");
            Expect.Equal("player 1: 0>1", TestLog.Lines[2], "then the receiver's");
        }

        /// <summary>A change the game makes, or a native, is found from the game's event, which names only the giver.</summary>
        public static void AllianceFromTheGame(FakeWorld world)
        {
            TestLog.Lines.Clear();
            Game.Instance.Add(new AllianceLog());

            world.SetAllianceOf(world.PlayerAt(2), world.PlayerAt(3), (int)Alliance.SharedVision);

            Expect.Equal(1, TestLog.Lines.Count, "reactions");
            Expect.Equal("game: 2>3 before 0 now " + (int)Alliance.SharedVision, TestLog.Lines[0], "the pair found");
        }

        /// <summary>Alliances are listened for only once a component reacts to them, and what was set before is not heard.</summary>
        public static void AllianceListenedWhenReacted(FakeWorld world)
        {
            TestLog.Lines.Clear();
            Player.One.SetAlliance(Player.Two, Alliance.Passive);
            Expect.True(!world.Listening.Contains(GameEvent.Alliance), "not listened for before a reaction needs it");

            Game.Instance.Add(new AllianceLog());

            Expect.True(world.Listening.Contains(GameEvent.Alliance), "listened for");
            Expect.Equal(0, TestLog.Lines.Count, "the earlier set runs nothing");
        }

        /// <summary>A team move runs the reaction for each pair it changes.</summary>
        public static void AllianceTeamMove(FakeWorld world)
        {
            StartTwoTeams(world);
            TestLog.Lines.Clear();
            Game.Instance.Add(new AllianceLog());

            Player.Two.Team = East;

            Expect.Equal(6, TestLog.Lines.Count, "two ways with each of three players");
        }
    }
}
