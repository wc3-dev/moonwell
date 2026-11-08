using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The kinds of quest message, each with the game's own sound; a hint and a warning are the game's lines of those kinds, which flash no quest button.</summary>
    internal enum QuestNews
    {
        Discovered,
        Updated,
        Completed,
        Failed,
        Hint,
        Warning,
    }

    /// <summary>
    /// A quest in the game's quest log (F9), main or optional, in a scope, which takes it out of the log when it ends, as End
    /// does. Discovering, completing and failing it, and a requirement done, show the game's quest message, play its sound
    /// and flash the quest button, unless quiet. Given shownTo, it is made on every machine and shown in that player's log
    /// only, with its messages (decision 207). Every read gives what was set, the same on every machine.
    /// </summary>
    public sealed class Quest : Scoped
    {
        /// <summary>The icon a quest has unless given one, as World Editor's.</summary>
        public const string DefaultIcon = @"ReplaceableTextures\CommandButtons\BTNAmbush.blp";

        private readonly Player? shownTo;

        private readonly bool optional;

        private readonly List<QuestRequirement> requirements = new List<QuestRequirement>();

        private quest? handle;

        private string title;

        private string description;

        private string icon;

        private bool discovered;

        private bool completed;

        private bool failed;

        private Quest(string title, string description, string icon, Player? shownTo, bool optional, bool discovered)
        {
            this.title = title ?? "";
            this.description = description ?? "";
            this.icon = icon ?? DefaultIcon;
            this.shownTo = shownTo;
            this.optional = optional;
            this.discovered = discovered;
        }

        /// <summary>The title, in the log's list and its messages.</summary>
        [LocalSafe]
        public string Title
        {
            get
            {
                return title;
            }

            set
            {
                if (!HasEnded)
                {
                    title = value ?? "";
                    Write();
                }
            }
        }

        /// <summary>The text under its requirements in the log.</summary>
        [LocalSafe]
        public string Description
        {
            get
            {
                return description;
            }

            set
            {
                if (!HasEnded)
                {
                    description = value ?? "";
                    Write();
                }
            }
        }

        /// <summary>The icon's path; DefaultIcon unless given.</summary>
        [LocalSafe]
        public string Icon
        {
            get
            {
                return icon;
            }

            set
            {
                if (!HasEnded)
                {
                    icon = value ?? DefaultIcon;
                    Write();
                }
            }
        }

        /// <summary>Whether the quest leaves out its messages; a call's own quiet leaves out that one too.</summary>
        [LocalSafe]
        public bool Quiet { get; set; }

        /// <summary>Whether it is an optional quest, in the log's right column; a main quest is in the left.</summary>
        [LocalSafe]
        public bool IsOptional => optional;

        /// <summary>Whether it is discovered; an undiscovered quest shows in the log as "Undiscovered Quest" with a "?" icon (seen 11-07).</summary>
        [LocalSafe]
        public bool IsDiscovered => discovered;

        /// <summary>Whether it is completed.</summary>
        [LocalSafe]
        public bool IsCompleted => completed;

        /// <summary>Whether it has failed.</summary>
        [LocalSafe]
        public bool IsFailed => failed;

        /// <summary>Whether a player sees the quest: the one it was given to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>
        /// Puts a quest in the log, in a scope, which takes it out when it ends, and returns it. A discovered quest shows the
        /// new-quest message in the instant after, so the requirements added on the next lines are in it; quiet leaves it out,
        /// and every later message too. Given shownTo, only that player sees it. In a scope that has ended it ends at once.
        /// </summary>
        public static Quest Create(string title, string description, Scope scope, Player? shownTo = null, bool optional = false, bool discovered = true, string? icon = null, bool quiet = false)
        {
            var made = new Quest(title, description, icon ?? DefaultIcon, shownTo, optional, discovered);
            made.Quiet = quiet;
            scope.Adopt(made);
            if (made.HasEnded)
            {
                return made;
            }

            made.handle = Runtime.World.MakeQuest();
            made.Write();
            made.State();
            Runtime.World.ShowQuest(made.handle, made.ShownHere());
            if (discovered)
            {
                made.AnnounceSoon();
            }

            return made;
        }

        /// <summary>Adds a requirement, a line under the title in the log, and returns it; on a quest that has ended it does nothing to the log.</summary>
        public QuestRequirement AddRequirement(string text)
        {
            var added = new QuestRequirement(this, text ?? "");
            if (handle != null && !HasEnded)
            {
                added.handle = Runtime.World.MakeRequirement(handle);
                added.Write();
                requirements.Add(added);
            }

            return added;
        }

        /// <summary>Discovers the quest, with the new-quest message unless quiet; nothing when it is discovered already.</summary>
        public void Discover(bool quiet = false)
        {
            if (HasEnded || discovered)
            {
                return;
            }

            discovered = true;
            State();
            News(QuestNews.Discovered, quiet);
        }

        /// <summary>Completes the quest, discovering it, with the quest-completed message unless quiet; nothing when it is completed already.</summary>
        public void Complete(bool quiet = false)
        {
            if (HasEnded || completed)
            {
                return;
            }

            completed = true;
            failed = false;
            discovered = true;
            State();
            News(QuestNews.Completed, quiet);
        }

        /// <summary>Fails the quest, discovering it, with the quest-failed message unless quiet; nothing when it has failed already.</summary>
        public void Fail(bool quiet = false)
        {
            if (HasEnded || failed)
            {
                return;
            }

            failed = true;
            completed = false;
            discovered = true;
            State();
            News(QuestNews.Failed, quiet);
        }

        /// <summary>A requirement turned done: the quest-updated message with its line, when the quest is discovered and still open.</summary>
        internal void RequirementDone(QuestRequirement requirement)
        {
            if (discovered && !completed && !failed)
            {
                Show(QuestNews.Updated, Header("QUEST UPDATED") + "\n" + title + "\n  - " + requirement.Text + " (Completed)", false);
            }
        }

        internal bool Live => handle != null && !HasEnded;

        /// <summary>Takes the quest out of the log, its requirements with it.</summary>
        internal override void Finish()
        {
            if (handle != null)
            {
                Runtime.World.EndQuest(handle);
                handle = null;
            }
        }

        private bool ShownHere()
        {
            return shownTo == null || Runtime.World.IsLocal(shownTo.Handle);
        }

        private void Write()
        {
            if (handle != null)
            {
                Runtime.World.WriteQuest(handle, title, description, icon);
            }
        }

        private void State()
        {
            if (handle != null)
            {
                Runtime.World.SetQuestState(handle, !optional, discovered, completed, failed);
            }
        }

        // The new-quest message waits for the instant after Create, so it lists the requirements added on the lines after.
        private void AnnounceSoon()
        {
            if (Quiet)
            {
                return;
            }

            Game.Instance.OwnAfter(0.0f, () =>
            {
                if (!HasEnded && discovered && !completed && !failed)
                {
                    News(QuestNews.Discovered, false);
                }
            });
        }

        private void News(QuestNews kind, bool quiet)
        {
            var kindName = optional ? "OPTIONAL QUEST" : "MAIN QUEST";
            switch (kind)
            {
                case QuestNews.Discovered:
                    var text = Header(kindName) + "\n" + title;
                    for (var i = 0; i < requirements.Count; i++)
                    {
                        text += "\n  - " + requirements[i].Text;
                    }

                    Show(kind, text, quiet);
                    break;
                case QuestNews.Completed:
                    Show(kind, Header(kindName + " COMPLETED") + "\n" + title, quiet);
                    break;
                default:
                    Show(kind, Header(kindName + " FAILED") + "\n" + title, quiet);
                    break;
            }
        }

        private void Show(QuestNews kind, string text, bool quiet)
        {
            if (!quiet && !Quiet && ShownHere())
            {
                Runtime.World.QuestNews(kind, text);
            }
        }

        private static string Header(string words)
        {
            return words.Colored(Color.TooltipGold);
        }
    }

    /// <summary>A requirement of a quest: a line under its title in the log, open or done. It lasts as long as its quest.</summary>
    public sealed class QuestRequirement
    {
        private readonly Quest quest;

        private string text;

        private bool done;

        internal questitem? handle;

        internal QuestRequirement(Quest quest, string text)
        {
            this.quest = quest;
            this.text = text;
        }

        /// <summary>The quest it is a requirement of.</summary>
        [LocalSafe]
        public Quest Quest => quest;

        /// <summary>The line in the log; setting it shows no message, so it can count ("Wolves killed: 3/5").</summary>
        [LocalSafe]
        public string Text
        {
            get
            {
                return text;
            }

            set
            {
                if (quest.Live)
                {
                    text = value ?? "";
                    Write();
                }
            }
        }

        /// <summary>Whether it is done, shown in the log as "( Completed )". Turning it done shows the quest-updated message, unless the quest is quiet, undiscovered, completed or failed.</summary>
        [LocalSafe]
        public bool Done
        {
            get
            {
                return done;
            }

            set
            {
                if (!quest.Live || value == done)
                {
                    return;
                }

                done = value;
                Write();
                if (done)
                {
                    quest.RequirementDone(this);
                }
            }
        }

        internal void Write()
        {
            if (handle != null)
            {
                Runtime.World.SetRequirement(handle, text, done);
            }
        }
    }
}
