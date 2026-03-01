using System.Text;

namespace logReader.UI
{
    public partial class CanLogViewForm : Form
    {
        private readonly string _csvPath;
        private Panel _innerPanel = null!;
        private const int ROW_MARGIN = 2;

        private int RowH => Font.Height + 10;
        private int HeaderH => Font.Height + 14;

        // Список уникальных ID с количеством посылок
        private List<(string ID, int Count)> _packets = new();

        public CanLogViewForm(string csvPath)
        {
            InitializeComponent();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            _csvPath = csvPath;
            Shown += (_, _) => LoadAndBuild();
        }

        // ─── Чтение файла и сбор уникальных ID ──────────────────────────
        private void LoadAndBuild()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool isTrc = Path.GetExtension(_csvPath).Equals(".trc", StringComparison.OrdinalIgnoreCase);

            // Regex для .trc: номер) время  Rx/Tx  ID  длина  байты
            var trcRegex = new System.Text.RegularExpressions.Regex(
                @"^\s*\d+\)\s+[\d.]+\s+\w+\s+([0-9A-Fa-f]{4,})\s+\d",
                System.Text.RegularExpressions.RegexOptions.Compiled);

            try
            {
                var encoding = LogFileEncoding.Detect(_csvPath);
                foreach (var line in File.ReadLines(_csvPath, encoding))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string id;
                    if (isTrc)
                    {
                        if (line.TrimStart().StartsWith(';')) continue;
                        var m = trcRegex.Match(line);
                        if (!m.Success) continue;
                        id = m.Groups[1].Value.Trim().ToUpperInvariant();
                    }
                    else
                    {
                        var parts = line.Split(';');
                        if (parts.Length < 3) continue;
                        id = parts[2].Trim();
                    }

                    if (string.IsNullOrWhiteSpace(id)) continue;
                    counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения файла: " + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            // Сортируем по ID
            _packets = counts.OrderBy(kv => kv.Key)
                             .Select(kv => (kv.Key, kv.Value))
                             .ToList();

            labelCount.Text = $"Всего уникальных ID: {_packets.Count}";
            BuildList(_packets);
        }

        // ─── Построение списка ────────────────────────────────────────────
        private void BuildList(List<(string ID, int Count)> items)
        {
            scrollPanel.Controls.Clear();

            if (items.Count == 0)
            {
                scrollPanel.Controls.Add(new Label
                {
                    Text = "Ничего не найдено",
                    Left = 12,
                    Top = 12,
                    AutoSize = true,
                    ForeColor = Color.Gray
                });
                return;
            }

            int totalH = 4 + HeaderH + ROW_MARGIN;
            foreach (var _ in items)
                totalH += RowH + ROW_MARGIN;

            int panelW = scrollPanel.ClientSize.Width
                         - SystemInformation.VerticalScrollBarWidth - 2;
            if (panelW < 100) panelW = scrollPanel.ClientSize.Width;

            _innerPanel = new Panel
            {
                Top = 0,
                Left = 0,
                Width = panelW,
                Height = totalH
            };

            // Заголовок таблицы
            int colCntW = 140;
            var headerPanel = new Panel
            {
                Left = 4,
                Top = 4,
                Height = HeaderH,
                Width = panelW - 8,
                BackColor = Color.FromArgb(60, 80, 120)
            };

            int colIdW = headerPanel.Width - colCntW - 12;

            headerPanel.Controls.Add(new Label
            {
                Text = "ID посылки",
                Left = 8,
                Top = 0,
                Width = colIdW,
                Height = HeaderH,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            });
            headerPanel.Controls.Add(new Label
            {
                Text = "Кол-во посылок",
                Left = colIdW + 4,
                Top = 0,
                Width = colCntW,
                Height = HeaderH,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            });

            _innerPanel.Controls.Add(headerPanel);

            int yOffset = 4 + HeaderH + ROW_MARGIN;

            for (int i = 0; i < items.Count; i++)
            {
                var (id, count) = items[i];
                Color bg = i % 2 == 0 ? Color.White : Color.FromArgb(245, 246, 250);

                int rowW = panelW - 8;
                var row = new Panel
                {
                    Left = 4,
                    Top = yOffset,
                    Height = RowH,
                    Width = rowW,
                    BackColor = bg
                };

                row.Controls.Add(new Label
                {
                    Text = id,
                    Left = 8,
                    Top = 0,
                    Width = rowW - colCntW - 12,
                    Height = RowH,
                    TextAlign = ContentAlignment.MiddleLeft
                });
                row.Controls.Add(new Label
                {
                    Text = count.ToString("N0"),
                    Left = rowW - colCntW - 4,
                    Top = 0,
                    Width = colCntW,
                    Height = RowH,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Color.DimGray
                });

                _innerPanel.Controls.Add(row);
                yOffset += RowH + ROW_MARGIN;
            }

            scrollPanel.Controls.Add(_innerPanel);
        }

        // ─── Поиск ────────────────────────────────────────────────────────
        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
        {
            string query = textBoxSearch.Text.Trim();

            var filtered = string.IsNullOrEmpty(query)
                ? _packets
                : _packets.Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            labelCount.Text = string.IsNullOrEmpty(query)
                ? $"Всего уникальных ID: {_packets.Count}"
                : $"Найдено: {filtered.Count} из {_packets.Count}";

            BuildList(filtered);
        }

        // ─── Resize ───────────────────────────────────────────────────────
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_packets.Count == 0 || scrollPanel == null) return;

            // Пересобираем с учётом текущего фильтра
            string query = textBoxSearch.Text.Trim();
            var current = string.IsNullOrEmpty(query)
                ? _packets
                : _packets.Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            BuildList(current);
        }
    }
}
