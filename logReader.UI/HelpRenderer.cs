namespace logReader.UI
{
    internal static class HelpRenderer
    {
        private static Font _bodyFont = null!;
        private static Font _heading1Font = null!;
        private static Font _heading2Font = null!;
        private static Font _heading3Font = null!;
        private static Font _monoFont = null!;
        private static Font _boldFont = null!;
        private static bool _fontsReady;

        private static readonly Color HighlightBackColor = Color.FromArgb(255, 255, 160);

        public static void Render(RichTextBox box, HelpTopic topic, string? highlightNeedle = null)
        {
            EnsureFonts(box);

            box.Clear();
            box.SelectionStart = 0;

            AppendHeading(box, topic.Title, HelpHeadingLevel.H1);
            AppendParagraph(box, "");

            foreach (HelpBlock block in topic.Blocks)
                RenderBlock(box, block);

            ApplyHighlight(box, highlightNeedle);
        }

        private static void ApplyHighlight(RichTextBox box, string? highlightNeedle)
        {
            string needle = (highlightNeedle ?? "").Trim();
            if (needle.Length == 0)
            {
                box.SelectionStart = 0;
                box.ScrollToCaret();
                return;
            }

            string text = box.Text;
            int searchFrom = 0;
            int firstMatch = -1;

            while (searchFrom < text.Length)
            {
                int index = text.IndexOf(needle, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    break;

                if (firstMatch < 0)
                    firstMatch = index;

                box.Select(index, needle.Length);
                box.SelectionBackColor = HighlightBackColor;
                searchFrom = index + needle.Length;
            }

            if (firstMatch >= 0)
            {
                box.Select(firstMatch, needle.Length);
                box.ScrollToCaret();
            }
            else
            {
                box.SelectionStart = 0;
                box.ScrollToCaret();
            }
        }

        private static void EnsureFonts(Control host)
        {
            if (_fontsReady) return;

            var baseFont = host.Font;
            _bodyFont = baseFont;
            _boldFont = new Font(baseFont, FontStyle.Bold);
            _heading1Font = new Font(baseFont.FontFamily, baseFont.Size + 4f, FontStyle.Bold);
            _heading2Font = new Font(baseFont.FontFamily, baseFont.Size + 2f, FontStyle.Bold);
            _heading3Font = new Font(baseFont.FontFamily, baseFont.Size + 1f, FontStyle.Bold);
            _monoFont = new Font(FontFamily.GenericMonospace, baseFont.Size - 0.5f);
            _fontsReady = true;
        }

        private static void RenderBlock(RichTextBox box, HelpBlock block)
        {
            switch (block)
            {
                case HelpHeading h:
                    AppendHeading(box, h.Text, h.Level);
                    break;
                case HelpParagraph p:
                    AppendParagraph(box, p.Text);
                    break;
                case HelpBullet b:
                    AppendBullet(box, b.Text);
                    break;
                case HelpLabeledItem item:
                    AppendLabeled(box, item);
                    break;
                case HelpExample ex:
                    AppendExample(box, ex.Text);
                    break;
            }
        }

        private static void AppendHeading(RichTextBox box, string text, HelpHeadingLevel level)
        {
            box.SelectionFont = level switch
            {
                HelpHeadingLevel.H1 => _heading1Font,
                HelpHeadingLevel.H2 => _heading2Font,
                _ => _heading3Font,
            };
            box.SelectionColor = Color.FromArgb(32, 32, 32);
            box.AppendText(text + Environment.NewLine);
            box.SelectionFont = _bodyFont;
            box.SelectionColor = box.ForeColor;
        }

        private static void AppendParagraph(RichTextBox box, string text)
        {
            if (text.Length == 0)
            {
                box.AppendText(Environment.NewLine);
                return;
            }

            box.SelectionFont = _bodyFont;
            box.AppendText(text + Environment.NewLine + Environment.NewLine);
        }

        private static void AppendBullet(RichTextBox box, string text)
        {
            box.SelectionFont = _bodyFont;
            box.AppendText("  • " + text + Environment.NewLine);
        }

        private static void AppendLabeled(RichTextBox box, HelpLabeledItem item)
        {
            string prefix = item.Kind switch
            {
                HelpCalloutKind.Tip => "Совет: ",
                HelpCalloutKind.Important => "Важно: ",
                _ => "",
            };

            if (prefix.Length > 0)
            {
                box.SelectionFont = _boldFont;
                box.SelectionColor = item.Kind == HelpCalloutKind.Important
                    ? Color.FromArgb(153, 51, 0)
                    : Color.FromArgb(0, 102, 51);
                box.AppendText(prefix);
            }

            box.SelectionFont = _boldFont;
            box.SelectionColor = box.ForeColor;
            box.AppendText(item.Label);
            box.SelectionFont = _bodyFont;
            box.AppendText(" — " + item.Text + Environment.NewLine + Environment.NewLine);
        }

        private static void AppendExample(RichTextBox box, string text)
        {
            box.SelectionFont = _monoFont;
            box.SelectionColor = Color.FromArgb(48, 48, 96);
            box.SelectionBackColor = Color.FromArgb(245, 245, 250);
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                box.AppendText("  " + line + Environment.NewLine);
                box.SelectionBackColor = box.BackColor;
            }
            box.SelectionFont = _bodyFont;
            box.SelectionColor = box.ForeColor;
            box.AppendText(Environment.NewLine);
        }
    }
}
