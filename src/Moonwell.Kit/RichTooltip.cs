namespace Moonwell
{
    /// <summary>
    /// A tooltip with an icon, a title and a cost over its text (decision 212), in the kit's Theme: the icon at the top left,
    /// the title beside it, the text below, and the cost at the bottom right, each left out while empty. 0.24 by 0.12 unless
    /// sized. Give it to a frame's Tooltip.
    /// </summary>
    public class RichTooltip : Tooltip
    {
        /// <summary>The icon's side, and the title's line.</summary>
        internal const float IconSize = 0.032f, TitleLine = 0.016f;

        internal readonly Image icon;

        internal readonly Label title;

        internal readonly Label cost;

        internal Theme theme;

        /// <summary>Makes a rich tooltip in Kit.Theme.</summary>
        public RichTooltip()
        {
            width = 0.24f;
            height = 0.12f;
            theme = Kit.Theme;
            icon = Add(new Image { Width = IconSize, Height = IconSize, Anchor = FramePoint.TopLeft, Offset = new Vector2(Inset, -Inset), Visible = false });
            title = Add(new Label { Anchor = FramePoint.TopLeft });
            cost = Add(new Label { Anchor = FramePoint.BottomRight, Offset = new Vector2(-Inset, Inset), Alignment = FramePoint.BottomRight });
            Dress();
            Lay();
        }

        /// <summary>The tooltip's look, Kit.Theme as it was made unless set: its template, the title's and cost's colour, the text's, and their sizes and font; set it before the tooltip is given to a frame, as the sizes and font are read when its labels are made.</summary>
        [LocalSafe]
        public Theme Theme
        {
            get { return theme; }
            set
            {
                theme = value;
                Dress();
            }
        }

        /// <summary>The icon's texture; empty unless set, which leaves the icon out.</summary>
        [LocalSafe]
        public string Icon
        {
            get { return icon.Texture; }
            set
            {
                icon.Texture = value;
                icon.Visible = value != "";
                Lay();
            }
        }

        /// <summary>The title, in the theme's title colour; empty unless set.</summary>
        [LocalSafe]
        public string Title
        {
            get { return title.Text; }
            set
            {
                title.Text = value;
                Lay();
            }
        }

        /// <summary>The cost, at the bottom right in the theme's title colour, as the map writes it ("150 gold"); empty unless set.</summary>
        [LocalSafe]
        public string Cost
        {
            get { return cost.Text; }
            set { cost.Text = value; }
        }

        private void Dress()
        {
            Template = theme.Window;
            title.Color = theme.Title;
            cost.Color = theme.Title;
            words.Color = theme.Text;
            theme.Style(title, theme.TitleSize);
            theme.Style(cost, theme.TextSize);
            theme.Style(words, theme.TextSize);
        }

        /// <summary>Places the title beside the icon, or at the corner without one, and the text below whichever is lower.</summary>
        private void Lay()
        {
            var hasIcon = icon.Texture != "";
            var left = Inset + (hasIcon ? IconSize + 0.006f : 0.0f);
            title.Offset = new Vector2(left, -Inset);
            var top = hasIcon ? IconSize + 0.006f : title.Text != "" ? TitleLine + 0.004f : 0.0f;
            words.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, Inset, -Inset - top);
        }
    }
}
