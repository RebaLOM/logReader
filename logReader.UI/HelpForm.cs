using System.Linq;

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
        }

        private void HelpForm_Load(object? sender, EventArgs e)
        {
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
    }
}
