using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>The fake world's quest log, cinematic screen and Esc key.</summary>
    public sealed partial class FakeWorld
    {
        /// <summary>The quests the runtime made, in the order made.</summary>
        public List<FakeQuest> Quests = new List<FakeQuest>();

        private readonly Dictionary<quest, FakeQuest> quests = new Dictionary<quest, FakeQuest>();

        private readonly Dictionary<questitem, FakeRequirement> requirements = new Dictionary<questitem, FakeRequirement>();

        /// <summary>The quest messages shown on this machine: the kind, then the text.</summary>
        public List<string> QuestMessages = new List<string>();

        /// <summary>What the runtime did to this machine's screen for cinematics and transmissions, in order.</summary>
        public List<string> ScreenCalls = new List<string>();

        /// <summary>The slots whose Esc is listened for.</summary>
        public List<int> ListenedEsc = new List<int>();

        /// <summary>The fake behind a quest handle.</summary>
        public FakeQuest QuestOf(quest quest)
        {
            return quests[quest];
        }

        /// <summary>Makes a quest, not shown.</summary>
        public quest MakeQuest()
        {
            var made = new quest();
            var fake = new FakeQuest();
            quests[made] = fake;
            Quests.Add(fake);
            return made;
        }

        /// <summary>Sets a quest's text.</summary>
        public void WriteQuest(quest quest, string title, string description, string icon)
        {
            var fake = LiveQuest(quest);
            fake.Title = title;
            fake.Description = description;
            fake.Icon = icon;
        }

        /// <summary>Sets a quest's state.</summary>
        public void SetQuestState(quest quest, bool main, bool discovered, bool completed, bool failed)
        {
            var fake = LiveQuest(quest);
            fake.Main = main;
            fake.Discovered = discovered;
            fake.Completed = completed;
            fake.Failed = failed;
        }

        /// <summary>Shows or hides a quest on this machine.</summary>
        public void ShowQuest(quest quest, bool shown)
        {
            LiveQuest(quest).Shown = shown;
        }

        /// <summary>Ends a quest; a second end fails the case.</summary>
        public void EndQuest(quest quest)
        {
            LiveQuest(quest).Ended = true;
        }

        /// <summary>Adds a requirement to a quest.</summary>
        public questitem MakeRequirement(quest quest)
        {
            var made = new questitem();
            var fake = new FakeRequirement();
            requirements[made] = fake;
            LiveQuest(quest).Requirements.Add(fake);
            return made;
        }

        /// <summary>Sets a requirement.</summary>
        public void SetRequirement(questitem requirement, string text, bool done)
        {
            var fake = requirements[requirement];
            fake.Text = text;
            fake.Done = done;
        }

        /// <summary>Records a quest message on this machine.</summary>
        void IWorld.QuestNews(QuestNews kind, string text)
        {
            QuestMessages.Add(kind + ": " + text);
        }

        /// <summary>Records that a person's Esc is listened for.</summary>
        public void ListenEsc(player player)
        {
            ListenedEsc.Add(SlotOf(player));
        }

        /// <summary>A person presses Esc; it reaches Moonwell when their Esc is listened for.</summary>
        public void Esc(Player from)
        {
            if (ListenedEsc.Contains(from.Slot))
            {
                Inbound.EscArrived(from.Handle);
            }
        }

        /// <summary>Records the letterbox.</summary>
        public void Letterbox(bool on, float seconds)
        {
            ScreenCalls.Add("letterbox " + (on ? "on" : "off"));
        }

        /// <summary>Records the controls.</summary>
        public void UserControl(bool on)
        {
            ScreenCalls.Add("controls " + (on ? "on" : "off"));
        }

        /// <summary>Records the sounds lowered or put back.</summary>
        public void CinematicSounds(bool on)
        {
            ScreenCalls.Add("sounds " + (on ? "lowered" : "back"));
        }

        /// <summary>Records the messages cleared.</summary>
        public void ClearMessages()
        {
            ScreenCalls.Add("clear");
        }

        /// <summary>Records a speaker's portrait and line.</summary>
        public void ShowScene(int unitTypeId, int playerColor, string name, string text, float seconds, float talkSeconds)
        {
            ScreenCalls.Add("scene " + name + ": " + text + " " + seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "/" + talkSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Records the portrait ended.</summary>
        public void EndScene()
        {
            ScreenCalls.Add("end scene");
        }

        /// <summary>Records a speaker marked.</summary>
        public void MarkSpeaker(unit unit, float x, float y)
        {
            ScreenCalls.Add("mark");
        }

        /// <summary>Records a screen fade.</summary>
        public void ScreenFilter(int red, int green, int blue, int fromAlpha, int toAlpha, float seconds)
        {
            ScreenCalls.Add("filter " + fromAlpha + " to " + toAlpha);
        }

        /// <summary>Records the screen's cover taken away.</summary>
        public void EndScreenFilter()
        {
            ScreenCalls.Add("filter off");
        }

        private FakeQuest LiveQuest(quest quest)
        {
            var fake = quests[quest];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a quest it had ended");
            }

            return fake;
        }
    }

    /// <summary>A quest the runtime asked the fake world for.</summary>
    public sealed class FakeQuest
    {
        /// <summary>Its text.</summary>
        public string Title = "", Description = "", Icon = "";

        /// <summary>Its state.</summary>
        public bool Main, Discovered, Completed, Failed, Shown, Ended;

        /// <summary>Its requirements, in the order added.</summary>
        public List<FakeRequirement> Requirements = new List<FakeRequirement>();
    }

    /// <summary>A requirement of a fake quest.</summary>
    public sealed class FakeRequirement
    {
        /// <summary>Its line.</summary>
        public string Text = "";

        /// <summary>Whether it is done.</summary>
        public bool Done;
    }
}
