using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A ready settings window of the UI kit (decision 212): a line for each setting, its label at the left and its control
    /// at the right, a check box (Check), a slider (Slider) or a button that steps through choices (Choice). It is one
    /// player's, as check boxes and sliders are: add it with shownTo. Each setting's Changed runs when the player changes it,
    /// synced; the values are kept in that player's file (FileName) and come back in their next game, running Changed once
    /// the file has arrived, a moment after the window is added. Reading the file needs the Moonwell.Objects package.
    /// </summary>
    public class SettingsWindow : Window
    {
        /// <summary>A line's height, and the controls' sizes.</summary>
        internal const float LineHeight = 0.032f, SliderWidth = 0.12f, ChoiceWidth = 0.1f;

        /// <summary>The kinds of setting, as the file writes them.</summary>
        internal const int KindCheck = 1, KindSlider = 2, KindChoice = 3;

        internal readonly Panel body;

        internal readonly List<SettingLine> lines = new List<SettingLine>();

        internal string fileName = "kit-settings.txt";

        /// <summary>Whether the player's file was asked for, at the window's first making.</summary>
        internal bool asked;

        /// <summary>Whether the values are being set from the file, which writes nothing back.</summary>
        internal bool loading;

        /// <summary>Makes an empty settings window titled Settings, to be given settings and added for one player.</summary>
        public SettingsWindow()
        {
            Title = "Settings";
            body = Add(new Panel { Template = "" });
        }

        /// <summary>The file the values are kept in, in the player's map folder; kit-settings.txt unless set. Set it before the window is added.</summary>
        [LocalSafe]
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        /// <summary>Adds a line with a check box, on or off unless the player's file says otherwise.</summary>
        public Setting<bool> Check(string label, bool on = false)
        {
            var setting = new Setting<bool>(this, label, on);
            var box = Line(label, new CheckBox { Checked = on });
            box.Toggled += (who, value) => setting.Change(who, value);
            setting.show = value => box.Checked = value;
            lines.Add(new SettingLine(label, KindCheck, bytes => bytes.WriteBool(setting.value), bytes => setting.Load(bytes.ReadBool())));
            return setting;
        }

        /// <summary>Adds a line with a slider from min to max, at value unless the player's file says otherwise; min unless given.</summary>
        public Setting<float> Slider(string label, float min, float max, float? value = null, float step = 1.0f)
        {
            var start = value ?? min;
            var setting = new Setting<float>(this, label, start);
            var slider = Line(label, new Slider { Min = min, Max = max, Step = step, Value = start, Width = SliderWidth });
            slider.ValueChanged += (who, now) => setting.Change(who, now);
            setting.show = now => slider.Value = now;
            lines.Add(new SettingLine(label, KindSlider, bytes => bytes.WriteFloat(setting.value), bytes =>
            {
                var read = bytes.ReadFloat();
                if (read >= min && read <= max)
                {
                    setting.Load(read);
                }
            }));
            return setting;
        }

        /// <summary>Adds a line with a button that steps to the next choice at each click, its Value the choice's number from 0; the first unless the player's file says otherwise.</summary>
        public Setting<int> Choice(string label, params string[] choices)
        {
            var setting = new Setting<int>(this, label, 0);
            var button = Line(label, new Button { Text = choices.Length > 0 ? choices[0] : "", Width = ChoiceWidth, Height = LineHeight - 0.004f });
            button.Clicked += who =>
            {
                if (choices.Length > 0)
                {
                    setting.Change(who, (setting.value + 1) % choices.Length);
                }
            };
            setting.show = index => button.Text = index >= 0 && index < choices.Length ? choices[index] : "";
            lines.Add(new SettingLine(label, KindChoice, bytes => bytes.WriteInt(setting.value), bytes =>
            {
                var read = bytes.ReadInt();
                if (read >= 0 && read < choices.Length)
                {
                    setting.Load(read);
                }
            }));
            return setting;
        }

        /// <summary>At the window's first making for its player, asks for their file.</summary>
        internal override void Place()
        {
            base.Place();
            if (asked || player == null)
            {
                return;
            }

            asked = true;
            ReadFile(player, fileName, Loaded);
        }

        /// <summary>Writes every value to the player's file, on their machine; nothing while the file's values are being set.</summary>
        internal void Save()
        {
            if (loading || player == null)
            {
                return;
            }

            var bytes = new ByteWriter();
            bytes.WriteInt(lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                bytes.WriteString(lines[i].Label);
                bytes.WriteInt(lines[i].Kind);
                lines[i].Write(bytes);
            }

            PlayerFile.Write(player, fileName, bytes.ToCode());
        }

        /// <summary>Sets the values the player's file holds, for the settings of the same label and kind, running their Changed; a file the game or the player spoiled is let be.</summary>
        private void Loaded(string? text)
        {
            var bytes = text == null ? null : ByteReader.FromCode(text);
            if (bytes == null)
            {
                return;
            }

            loading = true;
            try
            {
                var count = bytes.ReadInt();
                for (var i = 0; i < count; i++)
                {
                    var label = bytes.ReadString();
                    var kind = bytes.ReadInt();
                    var line = Find(label, kind);
                    if (line == null)
                    {
                        // An old setting: its value is read past.
                        SkipValue(bytes, kind);
                        continue;
                    }

                    line.Read(bytes);
                }
            }
            catch (FormatException)
            {
            }
            finally
            {
                loading = false;
            }
        }

        private SettingLine? Find(string label, int kind)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Label == label && lines[i].Kind == kind)
                {
                    return lines[i];
                }
            }

            return null;
        }

        private static void SkipValue(ByteReader bytes, int kind)
        {
            if (kind == KindCheck)
            {
                bytes.ReadBool();
            }
            else if (kind == KindSlider)
            {
                bytes.ReadFloat();
            }
            else
            {
                bytes.ReadInt();
            }
        }

        /// <summary>Adds a line under the ones before it: the label at the left and the control at the right.</summary>
        private T Line<T>(string label, T control)
            where T : Frame
        {
            var line = body.Add(new Panel { Template = "", Height = LineHeight });
            var top = -lines.Count * LineHeight;
            line.Attach(FramePoint.TopLeft, body, FramePoint.TopLeft, 0.0f, top);
            line.Attach(FramePoint.TopRight, body, FramePoint.TopRight, 0.0f, top);
            line.Add(new Label { Text = label, Anchor = FramePoint.Left, Alignment = FramePoint.Left, Color = theme.Text });
            control.Anchor = FramePoint.Right;
            return line.Add(control);
        }
    }

    /// <summary>A setting of a SettingsWindow: its value and when the player changes it.</summary>
    public sealed class Setting<T>
    {
        internal readonly SettingsWindow window;

        internal T value;

        internal Action<T>? show;

        internal List<Action<Player, T>>? changed;

        internal Setting(SettingsWindow window, string label, T value)
        {
            this.window = window;
            Label = label;
            this.value = value;
        }

        /// <summary>The line's label, which the player's file keeps the value under.</summary>
        [LocalSafe]
        public string Label { get; }

        /// <summary>The value: the player's last choice, the file's once it has arrived, or the one it was made with.</summary>
        [LocalSafe]
        public T Value
        {
            get { return value; }
        }

        /// <summary>Runs when the player changes the setting, and once when their file brings a value, with the player and the value. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, T>? Changed
        {
            add
            {
                if (value != null)
                {
                    (changed ??= new List<Action<Player, T>>()).Add(value);
                }
            }

            remove
            {
                if (value != null && changed != null)
                {
                    changed.Remove(value);
                }
            }
        }

        /// <summary>The player changed it on the window: keep it, run Changed and write the file.</summary>
        internal void Change(Player who, T now)
        {
            value = now;
            show?.Invoke(now);
            Raise(who);
            window.Save();
        }

        /// <summary>The player's file brought a value: show it and run Changed.</summary>
        internal void Load(T now)
        {
            value = now;
            show?.Invoke(now);
            if (window.player != null)
            {
                Raise(window.player);
            }
        }

        private void Raise(Player who)
        {
            if (changed == null || window.HasEnded)
            {
                return;
            }

            var now = changed.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i](who, value);
                }
                catch (Exception error)
                {
                    Errors.Report("Setting " + Label + ".Changed", error);
                }
            }
        }
    }

    /// <summary>How a settings window writes and reads one line's value in the player's file.</summary>
    internal sealed class SettingLine
    {
        internal readonly string Label;

        internal readonly int Kind;

        internal readonly Action<ByteWriter> Write;

        internal readonly Action<ByteReader> Read;

        internal SettingLine(string label, int kind, Action<ByteWriter> write, Action<ByteReader> read)
        {
            Label = label;
            Kind = kind;
            Write = write;
            Read = read;
        }
    }
}
