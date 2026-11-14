using System;

namespace Moonwell
{
    /// <summary>
    /// A line or block of text, in the game's font unless Font names another, at the game's height unless Size is set; Scale
    /// makes its text bigger and leaves its place and box alone. A label lets clicks through to what is under it unless
    /// Enabled is set. The game takes a TEXT's font and height only from its template, by path, and BlzFrameSetFont
    /// changes nothing (seen 11-13), so Size, Font and Shadow pick one of the text templates the build writes. An Effect
    /// moves its colours by itself.
    /// </summary>
    public class Label : Frame
    {
        internal string text = "";

        internal FramePoint alignment = FramePoint.TopLeft;

        internal Color color = Color.White;

        internal TextEffect? effect;

        /// <summary>When the Effect was set, which it moves from.</summary>
        internal float effectSince;

        /// <summary>Whether it is in MovingTexts' redraws.</summary>
        internal bool redrawing;

        /// <summary>The text ShowLocal shows on this machine, until Text is set; null for the shared text.</summary>
        internal string? local;

        internal float size;

        internal string font = "";

        internal bool shadow;

        /// <summary>The text templates' heights, in the game's units: 0.005 to 0.04, in steps of 0.0005 (TextStep).</summary>
        internal const int FirstStep = 10;

        internal const int LastStep = 80;

        internal const float TextStep = 0.0005f;

        /// <summary>The step for a label given a Font or Shadow and no Size: 0.0095, nearest the game's own 0.0094 (seen 11-13).</summary>
        internal const int GameStep = 19;

        /// <summary>Makes a label, to be added to the screen or to another frame.</summary>
        public Label()
        {
            enabled = false;
        }

        /// <summary>The label's text, the same on every machine; empty unless set.</summary>
        [LocalSafe]
        public string Text
        {
            get { return text; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                text = value;
                local = null;
                Write();
            }
        }

        /// <summary>The text's colour, the same on every machine; white unless set. It is a colour code around the text, as a label turned off (so clicks pass) draws the game's text colour grey (seen 11-07), and codes in the text (B9's Text) draw over it. Its alpha is not drawn: text draws solid (seen), so fade a label with Alpha.</summary>
        [LocalSafe]
        public Color Color
        {
            get { return color; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                color = value;
                Write();
            }
        }

        /// <summary>A colour effect Moonwell moves by itself, a rainbow turning or a sweep passing, from when it is set, on the shared text and on what ShowLocal and ShowNameOf show; null, the default, for none. Text reads the words as set, and Color is not drawn while it runs.</summary>
        [LocalSafe]
        public TextEffect? Effect
        {
            get { return effect; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                effect = value;
                effectSince = Game.Time;
                Write();
                if (effect != null && !redrawing)
                {
                    redrawing = true;
                    MovingTexts.Join(Redraw);
                }
            }
        }

        /// <summary>Where the text sits in the label: Left for the middle of the left edge, Center for the centre; TopLeft unless set.</summary>
        [LocalSafe]
        public FramePoint Alignment
        {
            get { return alignment; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                alignment = value;
                if (handle != null)
                {
                    Align();
                }
            }
        }

        /// <summary>The text's height in the units Screen.DesignHeight sets (12 is the game's own at 768), snapped to the nearest 0.0005 of the game's units from 0.005 to 0.04; 0, the game's, unless set. Read when the label is made, as the game takes it only from a template.</summary>
        [LocalSafe]
        public float Size
        {
            get { return size; }
            set
            {
                if (Unmade("Size"))
                {
                    size = value;
                    var step = (int)(value * 0.6f / Screen.DesignHeight / TextStep + 0.5f);
                    if (value != 0.0f && (step < FirstStep || step > LastStep))
                    {
                        Errors.Refuse(Names.NameOf(this) + ".Size", value + " is outside the text sizes, 0.005 to 0.04 of the game's units: drawn at the nearest");
                    }
                }
            }
        }

        /// <summary>The path in the map of a .ttf font in the map's assets ("war3mapImported\Fonts\Title.ttf"), for which the build writes the text templates; empty for the game's font unless set. Read when the label is made.</summary>
        [LocalSafe]
        public string Font
        {
            get { return font; }
            set
            {
                if (Unmade("Font"))
                {
                    font = value;
                }
            }
        }

        /// <summary>Whether the text has a black shadow, down and right; false unless set. Read when the label is made.</summary>
        [LocalSafe]
        public bool Shadow
        {
            get { return shadow; }
            set
            {
                if (Unmade("Shadow"))
                {
                    shadow = value;
                }
            }
        }

        /// <summary>The build's text template for the label's Size, Font and Shadow; the label's Template when it has one or none of them is set.</summary>
        internal override string MakeTemplate
        {
            get
            {
                if (Template.Length > 0 || (size == 0.0f && font.Length == 0 && !shadow))
                {
                    return Template;
                }

                var step = size == 0.0f ? GameStep : (int)(size * 0.6f / Screen.DesignHeight / TextStep + 0.5f);
                return TextTemplate(font, Math.Min(Math.Max(step, FirstStep), LastStep), shadow);
            }
        }

        /// <summary>A text template's name, as the build writes it: MoonwellText, the font's path with each letter lowered and all else an underscore (Game for the game's font), the step, and S for a shadow.</summary>
        internal static string TextTemplate(string fontPath, int step, bool shadowed)
        {
            return "MoonwellText_" + (fontPath.Length == 0 ? "Game" : FontKey(fontPath)) + "_" + step + (shadowed ? "S" : "");
        }

        private static string FontKey(string fontPath)
        {
            var lower = fontPath.ToLowerInvariant();
            var key = "";
            for (var i = 0; i < lower.Length; i++)
            {
                var c = lower.Substring(i, 1);
                key += "abcdefghijklmnopqrstuvwxyz0123456789".IndexOf(c) >= 0 ? c : "_";
            }

            return key;
        }

        /// <summary>Whether a template's value may still be set: false, and reported, once the label is made; false once it has ended.</summary>
        private bool Unmade(string what)
        {
            if (HasEnded)
            {
                return false;
            }

            if (handle != null)
            {
                Errors.Refuse(Names.NameOf(this) + "." + what, "read when the label is made: set it before adding the label");
                return false;
            }

            return true;
        }

        /// <summary>A TEXT, which draws its text in the game's font (seen 10-14).</summary>
        internal override string GameType => "TEXT";

        /// <summary>Moves the text's Color to a new one over the seconds, eased (linear unless given); it stops the label's last ColorTo.</summary>
        public Tween ColorTo(Color to, float seconds, Func<float, float>? ease = null)
        {
            var from = color;
            return Tweened(TweenColour, seconds, ease, t => Color = new Color(Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t), Mix(from.A, to.A, t)));
        }

        private static int Mix(int from, int to, float t)
        {
            return (int)(Maths.Lerp(from, to, t) + 0.5f);
        }

        /// <summary>Shows a text on this machine only, inside Local.For, as for a player's own language or a number only they see. Text keeps the shared text, and setting it shows that on every machine again. Does nothing until the label is made or once it has ended.</summary>
        [MachineLocal]
        public void ShowLocal(string localText)
        {
            if (handle != null && !HasEnded)
            {
                local = localText;
                Write();
            }
        }

        /// <summary>Shows a unit's name, called anywhere outside Local.For: each machine shows its own (a hero's proper name, else the unit's name), as the game gives them in each player's language, and Text reads the type's name, the same everywhere. It shows the name at the call; call it again after a rename. Does nothing once the label has ended.</summary>
        public void ShowNameOf(Unit unit)
        {
            if (HasEnded)
            {
                return;
            }

            text = unit.Default.Name;
            local = unit is Hero hero ? hero.ProperName : unit.Name;
            Write();
        }

        /// <summary>A label's Scale sizes its text only: the game scales a TEXT's offset and box with it (seen 11-12), so they are given divided by it.</summary>
        internal override float PlaceScale => scale;

        /// <summary>A label listened to takes the mouse: it is turned on for it, and Enabled reads so.</summary>
        internal override void PrepareMouse()
        {
            if (!enabled)
            {
                enabled = true;
                if (handle != null)
                {
                    TakeMouse(true);
                }
            }
        }

        /// <summary>Clears an ended label's text, shared or local, with its colour, when this one has none; Place writes what this one has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (text.Length == 0)
            {
                FramesSystem.World.WriteFrameText(handle!, "");
            }
        }

        /// <summary>Gives the game the label's text and alignment, then its points and looks.</summary>
        internal override void Place()
        {
            if (text.Length > 0)
            {
                Write();
            }

            if (!foundInTemplate || alignment != FramePoint.TopLeft)
            {
                Align();
            }

            base.Place();
        }

        /// <summary>Gives the game this machine's text in the label's colour: a code around it, again after each reset in it, as its own codes end with one.</summary>
        private void Write()
        {
            if (handle == null)
            {
                return;
            }

            var shown = local ?? text;
            if (effect != null)
            {
                shown = effect.Paint(shown, Game.Time - effectSince);
            }
            else if (!color.Equals(Color.White) && shown.Length > 0)
            {
                var code = "|cff" + color.ToHtml(false);
                shown = code + shown.Replace("|r", "|r" + code).Replace("|R", "|R" + code) + "|r";
            }

            FramesSystem.World.WriteFrameText(handle, shown);
        }

        /// <summary>A redraw of the Effect; answers false, and leaves the redraws, once the label has ended or has none.</summary>
        private bool Redraw()
        {
            if (HasEnded || effect == null)
            {
                redrawing = false;
                return false;
            }

            Write();
            return true;
        }

        /// <summary>Gives the game the alignment: the point's row is the vertical, its column the horizontal.</summary>
        private void Align()
        {
            var point = (int)alignment;
            FramesSystem.World.AlignFrameText(handle!, point / 3, 3 + point % 3);
        }
    }
}
