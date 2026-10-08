using System.Linq;
using logReader.UI.Theme;

namespace logReader.UI
{
    public partial class HelpForm : Form
    {
        private readonly Dictionary<string, TreeNode> _nodesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _searchTextById = new(StringComparer.Ordinal);
        private bool _suppressSelect;

        public HelpForm()
        {
            InitializeComponent();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            CacheSearchText();
            ConfigureTreeChrome();
            ThemeForm.Wire(this);
            AppTheme.Changed += OnAppThemeChanged;
            FormClosed += (_, _) => AppTheme.Changed -= OnAppThemeChanged;
        }

        private void ConfigureTreeChrome()
        {
            // OwnerDrawAll: иначе WinForms рисует системный белый/синий selection под нашим жёлтым.
            treeViewTopics.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            treeViewTopics.ShowLines = false;
            treeViewTopics.ShowPlusMinus = true;
            treeViewTopics.HotTracking = false;
            treeViewTopics.FullRowSelect = true;
            treeViewTopics.ItemHeight = Math.Max(24, treeViewTopics.Font.Height + 8);
            // Без двойной буферизации при клике мелькает системный белый selection.
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                target: treeViewTopics,
                args: [true]);
            treeViewTopics.DrawNode += treeViewTopics_DrawNode;
        }

        private void ApplyHelpChrome()
        {
            ThemePalette p = AppTheme.Palette;
            BackColor = p.Canvas;
            panelSearch.BackColor = p.Elevated;
            panelSearch.Padding = new Padding(12, 8, 12, 8);
            textBoxSearch.BackColor = p.Surface;
            textBoxSearch.ForeColor = p.Text;
            textBoxSearch.BorderStyle = BorderStyle.FixedSingle;
            textBoxSearch.Font = Typography.Body();
            // Placeholder уже в Designer; AccessibleName — для AT/UIA.
            textBoxSearch.PlaceholderText = "Поиск по справке…";
            textBoxSearch.AccessibleName = "Поиск по справке";

            treeViewTopics.BackColor = p.Surface;
            treeViewTopics.ForeColor = p.Text;
            treeViewTopics.LineColor = p.Border;
            treeViewTopics.BorderStyle = BorderStyle.None;
            treeViewTopics.Font = Typography.Body();

            richTextBoxHelp.BackColor = p.Surface;
            richTextBoxHelp.ForeColor = p.Text;
            richTextBoxHelp.BorderStyle = BorderStyle.None;
            richTextBoxHelp.Font = Typography.Body();

            splitContainer.BackColor = p.Border;
            splitContainer.Panel1.BackColor = p.Surface;
            splitContainer.Panel2.BackColor = p.Canvas;
            // Дерево тем шире — меньше «…» в подписях разделов.
            splitContainer.Panel1MinSize = 220;
            if (splitContainer.Width > 0 && splitContainer.SplitterDistance < 300)
                splitContainer.SplitterDistance = Math.Min(320, Math.Max(260, splitContainer.Width / 3));
        }

        private void treeViewTopics_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.Height <= 0)
                return;

            ThemePalette p = AppTheme.Palette;
            bool selected = (e.State & TreeNodeStates.Selected) != 0
                || ReferenceEquals(treeViewTopics.SelectedNode, e.Node);

            // Полная ширина клиента — иначе FullRowSelect + OwnerDrawText даёт «дырки» и системный цвет.
            var row = new Rectangle(0, e.Bounds.Y, treeViewTopics.ClientSize.Width, e.Bounds.Height);
            // Смесь Surface+Primary — ближе к жёлтому nav main, без «грязного» olive-only Selection в Dark.
            Color back = selected ? Blend(p.Surface, p.Primary, 0.28f) : treeViewTopics.BackColor;
            Color fore = selected ? p.Text : treeViewTopics.ForeColor;

            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, row);

            if (selected)
            {
                using var accent = new SolidBrush(p.Primary);
                e.Graphics.FillRectangle(accent, new Rectangle(0, e.Bounds.Y + 4, 3, e.Bounds.Height - 8));
            }

            int indent = e.Node.Level * treeViewTopics.Indent;
            const int glyphSize = 10;
            int glyphX = e.Bounds.X + indent + 6;
            int glyphY = e.Bounds.Y + (e.Bounds.Height - glyphSize) / 2;

            if (e.Node.Nodes.Count > 0)
            {
                var glyph = new Rectangle(glyphX, glyphY, glyphSize, glyphSize);
                using var pen = new Pen(p.Muted);
                e.Graphics.DrawRectangle(pen, glyph);
                int midY = glyph.Y + glyphSize / 2;
                int midX = glyph.X + glyphSize / 2;
                e.Graphics.DrawLine(pen, glyph.X + 2, midY, glyph.Right - 2, midY);
                if (!e.Node.IsExpanded)
                    e.Graphics.DrawLine(pen, midX, glyph.Y + 2, midX, glyph.Bottom - 2);
            }

            int textX = glyphX + glyphSize + 8;
            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                treeViewTopics.Font,
                new Rectangle(textX, e.Bounds.Y, Math.Max(0, treeViewTopics.ClientSize.Width - textX - 8), e.Bounds.Height),
                fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void OnAppThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            BeginInvoke(() =>
            {
                if (IsDisposed)
                    return;
                ApplyHelpChrome();
                treeViewTopics.Invalidate();
                if (treeViewTopics.SelectedNode?.Tag is string topicId)
                    RenderSelectedTopic(topicId);
            });
        }

        private void HelpForm_Load(object? sender, EventArgs e)
        {
            ApplyHelpChrome();
            BuildTree();
            SelectTopic("quickstart");
        }

        private void CacheSearchText()
        {
            foreach (HelpTopic topic in HelpContent.AllTopics)
                _searchTextById[topic.Id] = HelpContent.GetSearchText(topic).ToLowerInvariant();
        }

        private void BuildTree(string? filter = null)
        {
            string needle = (filter ?? "").Trim().ToLowerInvariant();
            bool hasFilter = needle.Length > 0;

            _nodesById.Clear();
            treeViewTopics.BeginUpdate();
            treeViewTopics.Nodes.Clear();

            var roots = HelpContent.AllTopics.Where(t => t.ParentId == null).ToList();
            foreach (HelpTopic root in roots)
            {
                if (!TopicMatchesFilter(root, needle, hasFilter))
                    continue;

                var rootNode = CreateNode(root);
                treeViewTopics.Nodes.Add(rootNode);
                AddChildNodes(rootNode, root.Id, needle, hasFilter);
            }

            treeViewTopics.EndUpdate();
        }

        private void AddChildNodes(TreeNode parentNode, string parentId, string needle, bool hasFilter)
        {
            foreach (HelpTopic child in HelpContent.AllTopics.Where(t => t.ParentId == parentId))
            {
                if (!TopicMatchesFilter(child, needle, hasFilter))
                    continue;

                var childNode = CreateNode(child);
                parentNode.Nodes.Add(childNode);
                AddChildNodes(childNode, child.Id, needle, hasFilter);
            }

            if (parentNode.Nodes.Count > 0)
                parentNode.Expand();
        }

        private TreeNode CreateNode(HelpTopic topic)
        {
            var node = new TreeNode(topic.Title) { Tag = topic.Id };
            _nodesById[topic.Id] = node;
            return node;
        }

        private bool TopicMatchesFilter(HelpTopic topic, string needle, bool hasFilter)
        {
            if (!hasFilter)
                return true;

            if (MatchesNeedle(topic.Id, needle))
                return true;

            return HelpContent.AllTopics
                .Where(t => t.ParentId == topic.Id)
                .Any(child => TopicMatchesFilter(child, needle, true));
        }

        private bool MatchesNeedle(string topicId, string needle)
        {
            return _searchTextById.TryGetValue(topicId, out string? text)
                && text.Contains(needle, StringComparison.Ordinal);
        }

        private void SelectTopic(string topicId)
        {
            if (!_nodesById.TryGetValue(topicId, out TreeNode? node))
                return;

            _suppressSelect = true;
            treeViewTopics.SelectedNode = node;
            node.EnsureVisible();
            _suppressSelect = false;

            RenderSelectedTopic(topicId);
        }

        private void treeViewTopics_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_suppressSelect || e.Node?.Tag is not string topicId)
                return;

            RenderSelectedTopic(topicId);
        }

        private void RenderSelectedTopic(string topicId)
        {
            HelpTopic? topic = HelpContent.FindById(topicId);
            if (topic == null)
                return;

            HelpRenderer.Render(richTextBoxHelp, topic, textBoxSearch.Text.Trim());
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
        {
            string? selectedId = treeViewTopics.SelectedNode?.Tag as string;
            BuildTree(textBoxSearch.Text);

            if (selectedId != null && _nodesById.ContainsKey(selectedId))
                SelectTopic(selectedId);
            else if (treeViewTopics.Nodes.Count > 0)
            {
                TreeNode first = treeViewTopics.Nodes[0];
                while (first.Nodes.Count > 0)
                    first = first.Nodes[0];
                SelectTopic((string)first.Tag!);
            }
            else
                richTextBoxHelp.Clear();
        }

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
