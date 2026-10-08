namespace logReader.UI.Controls;

public static class UiFactory
{
    public static Control Header(string title, string subtitle, IconKind icon = IconKind.None)
    {
        var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = icon == IconKind.None ? 1 : 2,
            RowCount = 2, Margin = new Padding(0, 0, 0, 20), Padding = new Padding(0, 0, 0, 20), BackColor = Color.Transparent };
        int textColumn = icon == IconKind.None ? 0 : 1;
        if (icon != IconKind.None)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            var mark = new IconView { Icon = icon, ForeColor = AppTheme.Primary, Size = new Size(32, 32), Margin = new Padding(0, 6, 16, 0) };
            layout.Controls.Add(mark, 0, 0);
            layout.SetRowSpan(mark, 2);
        }
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new Label { Text = title, Font = Typography.PageTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true,
            Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        var description = new Label { Text = subtitle, Font = Typography.Secondary, ForeColor = AppTheme.TextSecondary, AutoSize = true,
            Dock = DockStyle.Fill, Margin = new Padding(0) };
        layout.Controls.Add(heading, textColumn, 0);
        layout.Controls.Add(description, textColumn, 1);
        ConstrainWrapping(layout, textColumn, heading, description);
        return layout;
    }

    public static Control Field(string label, Control input, string? hint = null)
    {
        bool hasLabel = !string.IsNullOrWhiteSpace(label);
        int inputRow = hasLabel ? 1 : 0;
        var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1,
            RowCount = inputRow + (hint == null ? 1 : 2), Margin = new Padding(0, 0, 0, 16), BackColor = Color.Transparent };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (hasLabel)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, Font = Typography.Secondary,
                ForeColor = AppTheme.TextPrimary, Margin = new Padding(0, 0, 0, 6) }, 0, 0);
        }
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (hasLabel && string.IsNullOrEmpty(input.AccessibleName)) input.AccessibleName = label.TrimEnd(':');
        layout.Controls.Add(Input(input), 0, inputRow);
        if (hint != null)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var helper = new Label { Text = hint, AutoSize = true, Dock = DockStyle.Fill, Font = Typography.Caption,
                ForeColor = AppTheme.TextMuted, Margin = new Padding(0, 6, 0, 0) };
            layout.Controls.Add(helper, 0, inputRow + 1);
            ConstrainWrapping(layout, 0, helper);
        }
        return layout;
    }

    /// <summary>Native input with the shared field frame, for compound browse/search rows.</summary>
    public static Control Input(Control input)
    {
        if (input.Parent is InputFrame existing) return existing;
        input.Margin = new Padding(0);
        input.Dock = DockStyle.Fill;
        if (input is TextBox { Multiline: false } text) text.BorderStyle = BorderStyle.None;
        else if (input is NumericUpDown numeric) numeric.BorderStyle = BorderStyle.None;
        else if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        else return input;
        return new InputFrame(input) { Dock = DockStyle.Fill, Margin = new Padding(0) };
    }

    internal static bool IsHostedInput(Control input) => input.Parent is InputFrame;

    private static void ConstrainWrapping(TableLayoutPanel layout, int column, params Label[] labels)
    {
        layout.Layout += (_, _) =>
        {
            int[] widths = layout.GetColumnWidths();
            int width = column < widths.Length ? widths[column] : layout.ClientSize.Width - layout.Padding.Horizontal;
            if (width <= 0) return;
            foreach (var label in labels)
            {
                var maximum = new Size(Math.Max(1, width - label.Margin.Horizontal), 0);
                if (label.MaximumSize != maximum) label.MaximumSize = maximum;
            }
        };
    }

    public static FlowLayoutPanel Footer(params Control[] actions)
    {
        var layout = new ActionFooter { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(16), Margin = new Padding(0),
            BackColor = AppTheme.Surface };
        foreach (var action in actions)
        {
            action.Margin = new Padding(8, 0, 0, 0);
            layout.Controls.Add(action);
        }
        return layout;
    }

    public static Label SectionLabel(string text) => new()
    {
        Text = text, Font = Typography.SectionTitle, ForeColor = AppTheme.TextPrimary,
        AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12)
    };

    private sealed class ActionFooter : FlowLayoutPanel
    {
        private bool constraining;

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (!constraining && Parent is { ClientSize.Width: > 0 } parent)
            {
                int available = Math.Max(1, parent.ClientSize.Width - parent.Padding.Horizontal - Margin.Horizontal);
                if (parent is TableLayoutPanel table)
                {
                    var cell = table.GetCellPosition(this);
                    int[] widths = table.GetColumnWidths();
                    if (cell.Column >= 0 && cell.Column < widths.Length)
                    {
                        int span = table.GetColumnSpan(this);
                        int cellWidth = widths.Skip(cell.Column).Take(span).Sum() - Margin.Horizontal;
                        if (cellWidth > 0) available = Math.Min(available, cellWidth);
                    }
                }
                var maximum = new Size(available, 0);
                if (MaximumSize != maximum)
                {
                    constraining = true;
                    try { MaximumSize = maximum; }
                    finally { constraining = false; }
                }
            }
            base.OnLayout(e);
        }
    }

    private sealed class InputFrame : Panel
    {
        private readonly Control input;
        private bool hover;
        private bool layingOut;
        private int nativePreferredHeight;
        private int preferredDpi;
        private Font? preferredFont;
        public InputFrame(Control nativeInput)
        {
            input = nativeInput;
            BackColor = AppTheme.Surface;
            Height = 40;
            MinimumSize = new Size(0, 40);
            Padding = new Padding(12, 4, 12, 4);
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            input.Dock = DockStyle.None;
            input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(input);
            AttachNativeState(input);
            input.BackColorChanged += (_, _) => Invalidate();
            input.EnabledChanged += (_, _) => Invalidate();
            input.FontChanged += (_, _) => PerformLayout();
            if (input is ComboBox combo)
            {
                combo.DropDown += (_, _) => Invalidate();
                combo.DropDownClosed += (_, _) => Invalidate();
            }
            Click += (_, _) => input.Focus();
        }

        private void AttachNativeState(Control control)
        {
            control.Enter += (_, _) => Invalidate();
            control.Leave += (_, _) => Invalidate();
            control.GotFocus += (_, _) => Invalidate();
            control.LostFocus += (_, _) => Invalidate();
            control.MouseEnter += (_, _) => { hover = true; Invalidate(); };
            control.MouseLeave += (_, _) => RefreshHover();
            control.ControlAdded += (_, args) => { if (args.Control != null) AttachNativeState(args.Control); };
            foreach (Control child in control.Controls) AttachNativeState(child);
        }

        private void RefreshHover()
        {
            hover = IsHandleCreated && RectangleToScreen(ClientRectangle).Contains(Cursor.Position);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { RefreshHover(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (input == null || layingOut) return;
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            int nativeHeight = NativeHeight(width);
            if (nativeHeight <= 0) nativeHeight = input.Height;
            // Native edit/combobox/spinner height follows its font. The frame's
            // baseline size/padding are scaled by WinForms, never scaled twice.
            int availableHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
            nativeHeight = Math.Min(nativeHeight, availableHeight);
            var bounds = new Rectangle(Padding.Left, (ClientSize.Height - nativeHeight) / 2, width, nativeHeight);
            if (input.Bounds == bounds) return;
            layingOut = true;
            try { input.Bounds = bounds; }
            finally { layingOut = false; }
        }

        private int NativeHeight(int width)
        {
            if (input == null) return 0;
            if (nativePreferredHeight <= 0 || preferredDpi != input.DeviceDpi || !ReferenceEquals(preferredFont, input.Font))
            {
                nativePreferredHeight = input.GetPreferredSize(new Size(Math.Max(1, width), 0)).Height;
                preferredDpi = input.DeviceDpi;
                preferredFont = input.Font;
            }
            return nativePreferredHeight;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int height = NativeHeight(proposedSize.Width - Padding.Horizontal);
            int width = proposedSize.Width > 0 && proposedSize.Width < 16384 ? proposedSize.Width : (input?.PreferredSize.Width ?? 0) + Padding.Horizontal;
            return new Size(Math.Max(MinimumSize.Width, width), Math.Max(MinimumSize.Height, height + Padding.Vertical));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            bool error = input is IInputValidation { HasError: true };
            bool enabled = Enabled && input.Enabled;
            Color border = !enabled ? AppTheme.Border : error ? AppTheme.Error : ContainsFocus ? AppTheme.Primary : hover ? AppTheme.BorderHover : AppTheme.Border;
            bool readOnly = input is TextBoxBase { ReadOnly: true } or NumericUpDown { ReadOnly: true };
            Color fill = AppTheme.InputSurface(input, readOnly);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float stroke = Math.Max(1f, DeviceDpi / 96f);
            float inset = stroke / 2f + .5f;
            using var path = PaintGeometry.Rounded(new RectangleF(inset, inset, Math.Max(1, Width - 2 * inset), Math.Max(1, Height - 2 * inset)), UiScale.Px(this, 6));
            using var brush = new SolidBrush(fill);
            using var pen = new Pen(border, stroke);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
        protected override void OnPaintBackground(PaintEventArgs e) =>
            e.Graphics.Clear(PaintGeometry.SurfaceFor(this));
    }
}
