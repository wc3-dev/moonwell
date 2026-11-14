using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A row of a unit's buffs, of the UI kit's HUD (decision 340): each of the unit's Moonwell buffs that has an icon, in
    /// the order they came on, with its time left under it (12, or 2m from a minute on; nothing for one with no expiry).
    /// The icon is the buff's HudIcon, or with none set, a declared buff class's object data Icon, which the build writes
    /// as the game gives none while it runs (decision 351). Read 20 times a second. Eight to a line unless Columns says
    /// otherwise, each 0.02 square.
    /// </summary>
    public class BuffRow : Grid
    {
        /// <summary>An icon's side and the line of text under it.</summary>
        internal const float IconSize = 0.02f, TimeHeight = 0.012f;

        internal readonly List<BuffSlot> slots = new List<BuffSlot>();

        internal Unit? unit;

        internal bool ticking;

        /// <summary>Makes a row that shows no unit yet, to be added to the screen or to another frame.</summary>
        public BuffRow()
        {
            columns = 8;
            CellWidth = IconSize;
            CellHeight = IconSize + TimeHeight;
            Gap = 0.003f;
            KitTicks.Fit(this, 0);
        }

        /// <summary>The unit whose buffs show; null when none.</summary>
        [LocalSafe]
        public Unit? Unit => unit;

        /// <summary>Shows a unit's buffs from now on; null shows none.</summary>
        public void Show(Unit? shown)
        {
            if (HasEnded)
            {
                return;
            }

            unit = shown;
            Read();
            if (shown != null && !ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        private bool Tick()
        {
            if (HasEnded || unit == null)
            {
                ticking = false;
                return false;
            }

            Read();
            return true;
        }

        /// <summary>Puts the unit's buffs with an icon in the slots, making slots as needed and hiding those left over.</summary>
        internal void Read()
        {
            var shown = 0;
            if (unit != null && !unit.IsGone)
            {
                var buffs = unit.GetAll<Buff>();
                for (var i = 0; i < buffs.Count; i++)
                {
                    var buff = buffs[i];
                    var icon = buff.HudIcon.Length > 0 ? buff.HudIcon : ObjectTypes.BuffIconOf(buff);
                    if (icon.Length == 0)
                    {
                        continue;
                    }

                    if (shown == slots.Count)
                    {
                        slots.Add(Add(new BuffSlot()));
                    }

                    slots[shown].Show(buff, icon);
                    shown++;
                }
            }

            for (var i = shown; i < slots.Count; i++)
            {
                slots[i].Show(null, "");
            }

            KitTicks.Fit(this, shown);
        }
    }

    /// <summary>A buff's place in a BuffRow: its icon, and its time left under it.</summary>
    public class BuffSlot : Panel
    {
        internal readonly Image icon;

        internal readonly Label time;

        internal Buff? buff;

        internal BuffSlot()
        {
            Template = "";
            width = BuffRow.IconSize;
            height = BuffRow.IconSize + BuffRow.TimeHeight;
            sized = true;
            icon = KitTicks.Place(this, new Image(), 0.0f, 0.0f, BuffRow.IconSize, BuffRow.IconSize);
            time = KitTicks.Place(this, new Label { Alignment = FramePoint.Top, Color = Kit.Theme.Text }, -0.01f, BuffRow.IconSize, BuffRow.IconSize + 0.02f, BuffRow.TimeHeight);
        }

        /// <summary>The buff's icon.</summary>
        [LocalSafe]
        public Image Icon => icon;

        /// <summary>The buff's time left.</summary>
        [LocalSafe]
        public Label Time => time;

        /// <summary>The buff shown; null when the slot is hidden.</summary>
        [LocalSafe]
        public Buff? Buff => buff;

        /// <summary>Shows a buff with its icon, given anew only when it is another buff; null hides the slot.</summary>
        internal void Show(Buff? shown, string iconPath)
        {
            if (shown != buff)
            {
                buff = shown;
                Visible = shown != null;
                if (shown != null)
                {
                    icon.Texture = iconPath;
                }
            }

            var left = shown == null ? "" : TimeLeft(shown.Remaining);
            if (time.Text != left)
            {
                time.Text = left;
            }
        }

        /// <summary>A buff's seconds left as the row shows them: whole seconds, rounded up, minutes from a minute on; nothing with no expiry.</summary>
        internal static string TimeLeft(float seconds)
        {
            if (seconds <= 0.0f)
            {
                return "";
            }

            return seconds < 60.0f ? ((int)Math.Ceiling(seconds)).ToString() : (int)Math.Ceiling(seconds / 60.0f) + "m";
        }
    }
}
