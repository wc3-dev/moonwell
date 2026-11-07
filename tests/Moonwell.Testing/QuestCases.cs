namespace Moonwell.Testing
{
    /// <summary>Cases that check quests in the log, their requirements, their messages, who sees them and their end.</summary>
    public static class QuestCases
    {
        /// <summary>A game component that holds quests, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>A quest is put in the log, a main quest, discovered and shown; its new-quest message waits an instant, so it lists the requirements added after.</summary>
        public static void Create(FakeWorld world)
        {
            var sword = Quest.Create("Find the sword", "The blade lies in the crypt.", Game.Instance);
            sword.AddRequirement("Enter the crypt");
            var made = world.Quests[0];
            Expect.Equal("Find the sword", made.Title, "the title");
            Expect.Equal("The blade lies in the crypt.", made.Description, "the description");
            Expect.Equal(Quest.DefaultIcon, made.Icon, "the default icon");
            Expect.Equal(true, made.Main && made.Discovered && made.Shown, "a main quest, discovered and shown");
            Expect.Equal(false, made.Completed || made.Failed, "open");
            Expect.Equal("Enter the crypt", made.Requirements[0].Text, "the requirement");
            Expect.Equal(0, world.QuestMessages.Count, "no message yet");
            world.Advance(0.01f);
            Expect.Equal(1, world.QuestMessages.Count, "the new-quest message");
            var message = world.QuestMessages[0];
            Expect.True(message.StartsWith("Discovered: ") && message.Contains("MAIN QUEST") && message.Contains("Find the sword") && message.Contains("  - Enter the crypt"), "the message lists the requirement: " + message);
            sword.Title = "Find the blade";
            Expect.Equal("Find the blade", made.Title, "retitled");
        }

        /// <summary>Complete and Fail set the state and show their message; quiet leaves one out, and a quiet quest every one.</summary>
        public static void CompleteAndFail(FakeWorld world)
        {
            var sword = Quest.Create("Find the sword", "", Game.Instance, quiet: true);
            world.Advance(0.01f);
            Expect.Equal(0, world.QuestMessages.Count, "a quiet quest shows no new-quest message");
            sword.Quiet = false;
            sword.Complete();
            Expect.Equal(true, world.Quests[0].Completed, "completed");
            Expect.True(world.QuestMessages[0].StartsWith("Completed: ") && world.QuestMessages[0].Contains("MAIN QUEST COMPLETED"), "the message: " + world.QuestMessages[0]);
            sword.Complete();
            Expect.Equal(1, world.QuestMessages.Count, "completing again shows nothing");
            sword.Fail(quiet: true);
            Expect.Equal(true, world.Quests[0].Failed && !world.Quests[0].Completed, "failed, no longer completed");
            Expect.Equal(1, world.QuestMessages.Count, "quiet: no message");
            var optional = Quest.Create("Free the slaves", "", Game.Instance, optional: true);
            optional.Fail();
            Expect.Equal(false, world.Quests[1].Main, "optional");
            Expect.True(world.QuestMessages[1].StartsWith("Failed: ") && world.QuestMessages[1].Contains("OPTIONAL QUEST FAILED"), "the message: " + world.QuestMessages[1]);
            world.Advance(0.01f);
            Expect.Equal(2, world.QuestMessages.Count, "a quest failed in its first instant shows no new-quest message");
        }

        /// <summary>A requirement's text changes with no message; turning it done shows the quest-updated message, but not on a quest that is closed.</summary>
        public static void Requirements(FakeWorld world)
        {
            var sword = Quest.Create("Find the sword", "", Game.Instance);
            var wolves = sword.AddRequirement("Wolves killed: 0/5");
            world.Advance(0.01f);
            world.QuestMessages.Clear();
            wolves.Text = "Wolves killed: 3/5";
            Expect.Equal("Wolves killed: 3/5", world.Quests[0].Requirements[0].Text, "counted");
            Expect.Equal(0, world.QuestMessages.Count, "no message for a new text");
            wolves.Done = true;
            Expect.Equal(true, world.Quests[0].Requirements[0].Done, "done");
            Expect.True(world.QuestMessages[0].StartsWith("Updated: ") && world.QuestMessages[0].Contains("Wolves killed: 3/5 (Completed)"), "the message: " + world.QuestMessages[0]);
            wolves.Done = true;
            Expect.Equal(1, world.QuestMessages.Count, "done again shows nothing");
            sword.Complete(quiet: true);
            var crypt = sword.AddRequirement("Enter the crypt");
            crypt.Done = true;
            Expect.Equal(1, world.QuestMessages.Count, "a requirement of a completed quest shows no message");
            Expect.Same(sword, crypt.Quest, "its quest");
        }

        /// <summary>An undiscovered quest shows no message until Discover, which shows the new-quest message at once.</summary>
        public static void Undiscovered(FakeWorld world)
        {
            var secret = Quest.Create("The hidden shrine", "", Game.Instance, discovered: false, optional: true);
            world.Advance(0.01f);
            Expect.Equal(false, world.Quests[0].Discovered, "undiscovered");
            Expect.Equal(0, world.QuestMessages.Count, "no message");
            secret.Discover();
            Expect.Equal(true, world.Quests[0].Discovered, "discovered");
            Expect.True(world.QuestMessages[0].Contains("OPTIONAL QUEST"), "the message: " + world.QuestMessages[0]);
            secret.Discover();
            Expect.Equal(1, world.QuestMessages.Count, "discovering again shows nothing");
        }

        /// <summary>A quest for another player is made here too, but not shown, and its messages are not shown here.</summary>
        public static void ShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var theirs = Quest.Create("Theirs", "", Game.Instance, shownTo: Player.Two);
            var mine = Quest.Create("Mine", "", Game.Instance, shownTo: Player.One);
            world.Advance(0.01f);
            theirs.Complete();
            Expect.Equal(2, world.Quests.Count, "both made here");
            Expect.Equal(false, world.Quests[0].Shown, "not shown for another player");
            Expect.Equal(true, world.Quests[1].Shown, "shown for this machine's player");
            Expect.Equal(1, world.QuestMessages.Count, "only this player's quest's message");
            Expect.True(world.QuestMessages[0].Contains("Mine"), "the message: " + world.QuestMessages[0]);
            Expect.Equal(false, theirs.IsShownTo(Player.One), "not shown to another");
            Expect.Equal(true, mine.IsShownTo(Player.One), "shown to its player");
        }

        /// <summary>A quest leaves the log when its scope ends; after that it changes nothing, and one made in an ended scope is never made.</summary>
        public static void EndsWithScope(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var sword = Quest.Create("Find the sword", "", holder);
            var wolves = sword.AddRequirement("Wolves");
            holder.End();
            Expect.Equal(true, sword.HasEnded && world.Quests[0].Ended, "taken out of the log");
            world.Advance(0.01f);
            sword.Title = "Late";
            sword.Complete();
            wolves.Done = true;
            sword.AddRequirement("Late");
            Expect.Equal(0, world.QuestMessages.Count, "no message from an ended quest");
            Expect.Equal(1, world.Quests[0].Requirements.Count, "nothing added to an ended quest");
            var late = Quest.Create("Late", "", holder);
            Expect.Equal(true, late.HasEnded, "ended at once");
            Expect.Equal(1, world.Quests.Count, "nothing made in an ended scope");
        }
    }
}
