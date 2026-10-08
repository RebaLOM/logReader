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

        public static void Render(RichTextBox box, HelpTopic topic, string? highlightNeedle = null)
        {
            EnsureFonts(box);

            box.Clear();
            box.SelectionStart = 0;
            box.SelectionColor = ThemeManager.Current.Text;
            box.SelectionBackColor = box.BackColor;

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
                box.SelectionBackColor = ThemeManager.Current.Accent;
                box.SelectionColor = ThemeManager.Current.AccentText;
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
            _bodyFont = new Font(baseFont.FontFamily, baseFont.Size);
            _boldFont = new Font(baseFont, FontStyle.Bold);
            _heading1Font = new Font(baseFont.FontFamily, baseFont.Size + 4f, FontStyle.Bold);
            _heading2Font = new Font(baseFont.FontFamily, baseFont.Size + 2f, FontStyle.Bold);
            _heading3Font = new Font(baseFont.FontFamily, baseFont.Size + 1f, FontStyle.Bold);
            _monoFont = new Font(FontFamily.GenericMonospace, baseFont.Size - 0.5f);
            _fontsReady = true;
            Application.ApplicationExit += (_, _) =>
            {
                _bodyFont.Dispose(); _boldFont.Dispose(); _heading1Font.Dispose();
                _heading2Font.Dispose(); _heading3Font.Dispose(); _monoFont.Dispose();
                _fontsReady = false;
            };
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
            box.SelectionColor = ThemeManager.Current.Text;
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
                    ? ThemeManager.Current.Danger
                    : ThemeManager.Current.Success;
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
            box.SelectionColor = ThemeManager.Current.Text;
            box.SelectionBackColor = ThemeManager.Current.SurfaceAlt;
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
