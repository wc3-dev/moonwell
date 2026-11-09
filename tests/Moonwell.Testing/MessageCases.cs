using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases for the game's message kinds beside Print: hints, warnings, errors and clearing, to a player or a team (decision 218).</summary>
    public static class MessageCases
    {
        /// <summary>A hint, a warning and a clear show on the player's own machine only.</summary>
        public static void OnTheirMachine(FakeWorld world)
        {
            world.LocalSlot = 0;
            Player.One.Hint("Press F");
            Player.Two.Hint("not here");
            Player.One.Warning("The boss wakes");
            Player.Two.ClearMessages();
            Player.One.ClearMessages();

            Expect.Equal("Hint: Press F,Warning: The boss wakes", string.Join(",", world.QuestMessages), "the messages on this machine");
            Expect.Equal("clear", string.Join(",", world.ScreenCalls), "one clear, this machine's");
        }

        /// <summary>An error shows its line to the player and plays the error sound, made on every machine and heard by that player alone.</summary>
        public static void Error(FakeWorld world)
        {
            world.LocalSlot = 0;
            Player.Two.Error("Not enough gold.");

            Expect.Equal(1, world.PrintedSlots[0], "shown to player two");
            Expect.Equal("error: Not enough gold.", world.PrintedTexts[0], "the error line");
            Expect.Equal("Sound\\Interface\\Error.wav", world.Sounds[0].Path, "the error sound");
            Expect.Equal(0, world.Sounds[0].Volume, "silent on another player's machine");
            Expect.Equal(true, world.Sounds[0].Started, "but started");
        }

        /// <summary>A team's messages go to each of its players in the game.</summary>
        public static void ToATeam(FakeWorld world)
        {
            world.LocalSlot = 1;
            for (var slot = 0; slot < 3; slot++)
            {
                world.SetSlot(slot, SlotState.Playing, Controller.User);
                world.MoveToTeam(world.PlayerAt(slot), slot < 2 ? 0 : 1);
            }

            var team = Player.One.Team;
            team.Print("Wave 3", 5.0f);
            team.Warning("Hold the gate");
            team.Error("Too far");
            team.ClearMessages();

            Expect.Equal("0,1,0,1", string.Join(",", world.PrintedSlots), "printed to slots 0 and 1, then their errors");
            Expect.Equal("Warning: Hold the gate", string.Join(",", world.QuestMessages), "the warning on this machine, once");
            Expect.Equal(2, world.Sounds.Count, "an error sound for each player");
            Expect.Equal("clear", string.Join(",", world.ScreenCalls), "this machine's screen cleared once");
        }
    }
}
