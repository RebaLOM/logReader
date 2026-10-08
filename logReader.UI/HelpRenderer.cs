namespace logReader.UI
{
    internal static class HelpRenderer
    {
        public static void Render(RichTextBox box, HelpTopic topic, string? highlightNeedle = null)
        {
            // RichTextBox copies font information into its native document. Keep font
            // ownership local so each render uses the current theme without a stale cache.
            using var fonts = new RenderFonts();
            box.SuspendLayout();
            try
            {
                box.Clear();
                box.SelectionStart = 0;
                box.SelectionRightIndent = UiScale.Px(box, 12);
                AppendHeading(box, topic.Title, HelpHeadingLevel.H1, fonts);
                AppendParagraph(box, "", fonts);
                foreach (HelpBlock block in topic.Blocks)
                    RenderBlock(box, block, fonts);
                ApplyHighlight(box, highlightNeedle);
            }
            finally { box.ResumeLayout(); }
        }

        private static void ApplyHighlight(RichTextBox box, string? highlightNeedle)
        {
            string needle = (highlightNeedle ?? "").Trim();
            if (needle.Length == 0)
            {
                box.Select(0, 0);
                box.ScrollToCaret();
                return;
            }
            int searchFrom = 0;
            int firstMatch = -1;
            while (searchFrom < box.TextLength)
            {
                int index = box.Text.IndexOf(needle, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;
                if (firstMatch < 0) firstMatch = index;
                box.Select(index, needle.Length);
                box.SelectionBackColor = AppTheme.PrimarySoft;
                searchFrom = index + needle.Length;
            }
            box.Select(firstMatch >= 0 ? firstMatch : 0, firstMatch >= 0 ? needle.Length : 0);
            box.ScrollToCaret();
        }

        private static void RenderBlock(RichTextBox box, HelpBlock block, RenderFonts fonts)
        {
            switch (block)
            {
                case HelpHeading heading: AppendHeading(box, heading.Text, heading.Level, fonts); break;
                case HelpParagraph paragraph: AppendParagraph(box, paragraph.Text, fonts); break;
                case HelpBullet bullet: AppendBullet(box, bullet.Text, fonts); break;
                case HelpLabeledItem item: AppendLabeled(box, item, fonts); break;
                case HelpExample example: AppendExample(box, example.Text, fonts); break;
            }
        }

        private static void ResetBody(RichTextBox box, RenderFonts fonts)
        {
            box.SelectionFont = fonts.Body;
            box.SelectionColor = AppTheme.TextPrimary;
            box.SelectionBackColor = AppTheme.Surface;
        }

        private static void AppendHeading(RichTextBox box, string text, HelpHeadingLevel level, RenderFonts fonts)
        {
            ResetBody(box, fonts);
            box.SelectionFont = level switch
            {
                HelpHeadingLevel.H1 => fonts.Title,
                HelpHeadingLevel.H2 => fonts.Section,
                _ => fonts.Subheading
            };
            box.AppendText(text + Environment.NewLine);
            ResetBody(box, fonts);
        }

        private static void AppendParagraph(RichTextBox box, string text, RenderFonts fonts)
        {
            ResetBody(box, fonts);
            box.AppendText(text.Length == 0 ? Environment.NewLine
                : text + Environment.NewLine + Environment.NewLine);
        }

        private static void AppendBullet(RichTextBox box, string text, RenderFonts fonts)
        {
            ResetBody(box, fonts);
            box.AppendText("  •  " + text + Environment.NewLine);
        }

        private static void AppendLabeled(RichTextBox box, HelpLabeledItem item, RenderFonts fonts)
        {
            ResetBody(box, fonts);
            string prefix = item.Kind switch
            {
                HelpCalloutKind.Tip => "Совет: ",
                HelpCalloutKind.Important => "Важно: ",
                _ => ""
            };
            if (prefix.Length > 0)
            {
                box.SelectionFont = fonts.Bold;
                box.SelectionColor = item.Kind == HelpCalloutKind.Important ? AppTheme.Warning : AppTheme.Info;
                box.AppendText(prefix);
            }
            box.SelectionFont = fonts.Bold;
            box.SelectionColor = AppTheme.TextPrimary;
            box.AppendText(item.Label);
            ResetBody(box, fonts);
            box.AppendText(" — " + item.Text + Environment.NewLine + Environment.NewLine);
        }

        private static void AppendExample(RichTextBox box, string text, RenderFonts fonts)
        {
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                box.SelectionFont = fonts.Mono;
                box.SelectionColor = AppTheme.TextSecondary;
                box.SelectionBackColor = AppTheme.SurfaceSecondary;
                box.AppendText("  " + line + Environment.NewLine);
            }
            ResetBody(box, fonts);
            box.AppendText(Environment.NewLine);
        }

        private sealed class RenderFonts : IDisposable
        {
            public readonly Font Body = new(Typography.Body, FontStyle.Regular);
            public readonly Font Title = new(Typography.PageTitle, FontStyle.Bold);
            public readonly Font Section = new(Typography.SectionTitle, FontStyle.Bold);
            public readonly Font Subheading = new(Typography.CardTitle, FontStyle.Bold);
            public readonly Font Mono = new(Typography.Mono, FontStyle.Regular);
            public readonly Font Bold = new(Typography.Body, FontStyle.Bold);

            public void Dispose()
            {
                Body.Dispose(); Title.Dispose(); Section.Dispose();
                Subheading.Dispose(); Mono.Dispose(); Bold.Dispose();
            }
        }
    }
}
