namespace logReader.UI
{
    internal static class DialogLayout
    {
        public static Panel Header(Form owner, string category, string title, Label? description = null)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = description == null ? 92 : 112, Padding = new Padding(24, 12, 24, 10), Tag = "background" };
            var kicker = new Label { Text = category.ToUpperInvariant(), AutoSize = true, Location = new Point(24, 12), Tag = "muted" };
            var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
            var heading = new Label { Text = title, AutoSize = true, Location = new Point(22, 33), Font = titleFont };
            owner.Disposed += (_, _) => titleFont.Dispose();
            header.Controls.Add(kicker);
            header.Controls.Add(heading);
            if (description != null)
            {
                description.Dock = DockStyle.None;
                description.Location = new Point(24, 78);
                description.Size = new Size(owner.ClientSize.Width - 48, 24);
                description.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                description.AutoSize = false;
                description.AutoEllipsis = true;
                description.Padding = Padding.Empty;
                description.Tag = "muted";
                header.Controls.Add(description);
                var tooltip = new ToolTip();
                tooltip.SetToolTip(description, description.Text);
                description.TextChanged += (_, _) => tooltip.SetToolTip(description, description.Text);
                owner.Disposed += (_, _) => tooltip.Dispose();
            }
            return header;
        }

        public static FlowLayoutPanel Actions(params Control[] controls)
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24, 12, 24, 10),
                WrapContents = true, Tag = "background"
            };
            foreach (var control in controls)
            {
                if (control is Button button)
                {
                    button.AutoSize = true;
                    button.MinimumSize = new Size(90, 32);
                    button.Padding = new Padding(10, 3, 10, 3);
                    button.Margin = new Padding(0, 0, 8, 0);
                }
                bar.Controls.Add(control);
            }
            return bar;
        }

        public static Panel Footer(Button primary, string note)
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(24, 16, 24, 16), Tag = "background" };
            primary.AutoSize = true;
            primary.MinimumSize = new Size(180, 34);
            primary.Dock = DockStyle.Right;
            primary.Tag = "primary";
            footer.Controls.Add(primary);
            var hint = new Label { Text = note, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", AutoEllipsis = true };
            footer.Controls.Add(hint);
            hint.BringToFront();
            return footer;
        }

        public static Panel Section(string title, Control content, bool compact = false)
        {
            var card = new Panel { Dock = DockStyle.Fill, Padding = new Padding(compact ? 14 : 18), Tag = "surface", Margin = new Padding(0, 0, 12, 14) };
            var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
            card.Disposed += (_, _) => titleFont.Dispose();
            var heading = new Label { Text = title, Font = titleFont, Dock = DockStyle.Top, Height = compact ? 32 : 36 };
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            card.Controls.Add(heading);
            return card;
        }
    }

    // Retains native CheckedListBox keyboard, check-on-click and ItemCheck ordering;
    // only its drawing uses the LOGER palette.
    internal sealed class ThemedCheckedListBox : CheckedListBox
    {
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var p = ThemeManager.Current;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? p.AccentSoft : p.Surface);
            e.Graphics.FillRectangle(background, e.Bounds);
            int side = Math.Min(LogicalToDeviceUnits(14), e.Bounds.Height - 2);
            var square = new Rectangle(e.Bounds.X + LogicalToDeviceUnits(3), e.Bounds.Y + (e.Bounds.Height - side) / 2, side, side);
            bool isChecked = GetItemCheckState(e.Index) != CheckState.Unchecked;
            using var fill = new SolidBrush(isChecked ? p.Accent : p.SurfaceAlt);
            using var stroke = new Pen(isChecked ? p.Accent : p.Muted);
            e.Graphics.FillRectangle(fill, square);
            e.Graphics.DrawRectangle(stroke, square);
            if (isChecked)
            {
                using var tick = new Pen(p.AccentText, Math.Max(1, LogicalToDeviceUnits(2)));
                e.Graphics.DrawLines(tick, new[]
                {
                    new Point(square.Left + side / 5, square.Top + side / 2),
                    new Point(square.Left + side * 3 / 7, square.Top + side * 5 / 7),
                    new Point(square.Right - side / 7, square.Top + side * 2 / 7)
                });
            }
            int textOffset = LogicalToDeviceUnits(24);
            var text = new Rectangle(e.Bounds.X + textOffset, e.Bounds.Y, e.Bounds.Width - textOffset - 2, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text, Enabled ? p.Text : p.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0)
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, p.Accent, p.Surface);
        }
    }

    internal sealed class ThemedTabControl : TabControl
    {
        public ThemedTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Point(18, 8);
            SelectedIndexChanged += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeManager.Current.Background);
            for (int i = 0; i < TabPages.Count; i++)
            {
                var bounds = GetTabRect(i);
                bool active = i == SelectedIndex;
                using var brush = new SolidBrush(active ? ThemeManager.Current.Surface : ThemeManager.Current.Background);
                e.Graphics.FillRectangle(brush, bounds);
                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, bounds, ThemeManager.Current.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (active)
                {
                    using var pen = new Pen(ThemeManager.Current.Accent, 3);
                    e.Graphics.DrawLine(pen, bounds.Left + 6, bounds.Bottom - 2, bounds.Right - 6, bounds.Bottom - 2);
                    if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -4, -4), ThemeManager.Current.Accent, ThemeManager.Current.Surface);
                }
            }
        }
    }
}
