using System.Linq;
using System.Text;

namespace logReader.UI
{
    public partial class CanLogViewForm : Form
    {
        private readonly string _sourcePath;
        private List<(string ID, int Count)> _packets = new();
        private CancellationTokenSource? _loadingCancellation;
        private ProgressBar _loadingProgress = null!;

        public CanLogViewForm(string sourcePath)
        {
            InitializeComponent();
            AppTheme.Apply(this);
            AppTheme.StyleGrid(dataGridPackets, "В этом логе нет распознанных посылок");
            dataGridPackets.Columns[0].DefaultCellStyle.Font = Typography.Mono;
            dataGridPackets.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            _sourcePath = sourcePath;
            _loadingProgress = new ProgressBar { Dock = DockStyle.Bottom, Height = UiScale.Px(this, 4),
                Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = false };
            panelTop.Controls.Add(_loadingProgress);
            FormClosed += (_, _) => _loadingCancellation?.Cancel();
            Disposed += (_, _) => _loadingCancellation?.Cancel();
            Shown += async (_, _) => await LoadAndBuildAsync();
        }

        private async Task LoadAndBuildAsync()
        {
            using var cancellation = new CancellationTokenSource();
            _loadingCancellation = cancellation;
            textBoxSearch.Enabled = false;
            _loadingProgress.Visible = true;
            dataGridPackets.Visible = false;
            emptyState.Visible = true;
            emptyState.Title = "Подсчитываем посылки";
            emptyState.Description = "Читаем выбранные логи. Это может занять время для больших файлов.";
            labelCount.Text = "Чтение логов…";
            try
            {
                var packets = await Task.Run(() => ReadPacketCounts(_sourcePath, cancellation.Token), cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                if (packets == null)
                {
                    AppDialog.Show("В выбранной папке нет файлов .csv, .trc, .asc или .txt.",
                        "Нет логов", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }
                _packets = packets;
                labelCount.Text = $"Уникальных ID: {_packets.Count}   Всего посылок: {_packets.Sum(p => p.Count):N0}";
                BuildList(_packets);
            }
            catch (OperationCanceledException)
            {
                // Closing the viewer cancels counting and does not show an error.
            }
            catch (Exception ex)
            {
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                AppDialog.Show("Ошибка чтения файла: " + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            finally
            {
                _loadingCancellation = null;
                if (!IsDisposed)
                {
                    textBoxSearch.Enabled = true;
                    _loadingProgress.Visible = false;
                }
            }
        }

        private static List<(string ID, int Count)>? ReadPacketCounts(string sourcePath, CancellationToken cancellation)
        {
            var paths = ResolveInputPaths(sourcePath);
            if (paths.Count == 0) return null;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Span<int> bytes = stackalloc int[8];
            foreach (string path in paths)
            {
                cancellation.ThrowIfCancellationRequested();
                ProcessSingleLogFile(path, counts, bytes, cancellation);
            }
            return counts.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
        }

        private static List<string> ResolveInputPaths(string sourcePath)
        {
            if (Directory.Exists(sourcePath))
            {
                return EnumerateLogFilesInFolder(sourcePath)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (File.Exists(sourcePath))
                return new List<string> { sourcePath };

            return new List<string>();
        }

        private static IEnumerable<string> EnumerateLogFilesInFolder(string folder)
        {
            string[] patterns = { "*.csv", "*.trc", "*.asc", "*.txt" };
            foreach (var pattern in patterns)
            {
                foreach (var path in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                    yield return path;
            }
        }

        private static void ProcessSingleLogFile(
            string inputPath,
            Dictionary<string, int> counts,
            Span<int> bytes,
            CancellationToken cancellation)
        {
            string ext = Path.GetExtension(inputPath);
            bool isTrc = ext.Equals(".trc", StringComparison.OrdinalIgnoreCase);
            bool isAsc = ext.Equals(".asc", StringComparison.OrdinalIgnoreCase);
            var encoding = LogFileEncoding.Detect(inputPath);
            bool isCanfox = !isTrc && !isAsc && CanfoxLogParser.LooksLikeCanfoxLog(inputPath, encoding);

            if (ext.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                && MatrixCsvLogParser.LooksLikeMatrixCsv(inputPath, encoding))
            {
                ProcessMatrixCsvFile(inputPath, encoding, counts, cancellation);
                return;
            }

            foreach (var line in File.ReadLines(inputPath, encoding))
            {
                cancellation.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string id;
                if (isTrc)
                {
                    if (!TrcLogParser.TryParseTrcFrameLine(
                            line,
                            out _,
                            out _,
                            out id,
                            out _,
                            bytes,
                            out _))
                    {
                        continue;
                    }
                }
                else if (isAsc)
                {
                    if (!AscLogParser.TryParseFrameId(line, out id))
                        continue;
                }
                else if (isCanfox)
                {
                    if (!CanfoxLogParser.TryParseCanfoxFrameLine(line, out _, out id, bytes, out _))
                        continue;
                }
                else
                {
                    // Как в CanLogProcessor: только priority==1 (заголовок и дубли — иначе).
                    var parts = line.Split(';');
                    if (parts.Length < 4)
                        continue;
                    if (!int.TryParse(parts[3], out int pri) || pri != 1)
                        continue;
                    id = parts[2].Trim();
                }

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
        }

        private static void ProcessMatrixCsvFile(
            string inputPath,
            Encoding encoding,
            Dictionary<string, int> counts,
            CancellationToken cancellation)
        {
            bool headerRead = false;
            List<MatrixCsvColumn> columns = new();
            Span<int> msgBytes = stackalloc int[8];

            foreach (string line in File.ReadLines(inputPath, encoding))
            {
                cancellation.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!headerRead)
                {
                    if (!MatrixCsvLogParser.TryReadHeader(line, out columns, out _))
                        return;

                    foreach (MatrixCsvColumn col in columns)
                    {
                        if (!counts.ContainsKey(col.Id))
                            counts[col.Id] = 0;
                    }

                    headerRead = true;
                    continue;
                }

                string[] parts = line.Split(';');
                if (parts.Length < 2 || !MatrixCsvLogParser.TryParseTimeCell(parts[0], out _))
                    continue;

                foreach (MatrixCsvColumn col in columns)
                {
                    if (col.ColumnIndex >= parts.Length)
                        continue;

                    string cell = parts[col.ColumnIndex];
                    if (MatrixCsvLogParser.IsCellEmpty(cell))
                        continue;
                    if (!MatrixCsvLogParser.TryParsePayloadHex(cell, msgBytes))
                        continue;

                    counts[col.Id] = counts.TryGetValue(col.Id, out int n) ? n + 1 : 1;
                }
            }
        }

        private void BuildList(List<(string ID, int Count)> items)
        {
            dataGridPackets.SuspendLayout();
            dataGridPackets.Rows.Clear();
            foreach (var (id, count) in items)
                dataGridPackets.Rows.Add(id, count.ToString("N0"));
            dataGridPackets.ClearSelection();
            dataGridPackets.ResumeLayout();

            bool empty = items.Count == 0;
            dataGridPackets.Visible = !empty;
            emptyState.Visible = empty;
            emptyState.Title = string.IsNullOrWhiteSpace(textBoxSearch.Text)
                ? "Посылки не найдены" : "Нет совпадений";
            emptyState.Description = string.IsNullOrWhiteSpace(textBoxSearch.Text)
                ? "В выбранных файлах нет распознанных CAN-посылок."
                : "Попробуйте другой CAN ID или очистите строку поиска.";
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e)
        {
            string query = textBoxSearch.Text.Trim();

            var filtered = string.IsNullOrEmpty(query)
                ? _packets
                : _packets.Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            int totalPackets = _packets.Sum(p => p.Count);
            int filteredPackets = filtered.Sum(p => p.Count);
            labelCount.Text = string.IsNullOrEmpty(query)
                ? $"Уникальных ID: {_packets.Count}   Всего посылок: {totalPackets:N0}"
                : $"Найдено ID: {filtered.Count} из {_packets.Count}   Посылок: {filteredPackets:N0} из {totalPackets:N0}";

            BuildList(filtered);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                textBoxSearch.Focus();
                textBoxSearch.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (dataGridPackets == null) return;
            int rowHeight = UiScale.Px(this, 36);
            dataGridPackets.RowTemplate.Height = rowHeight;
            foreach (DataGridViewRow row in dataGridPackets.Rows) row.Height = rowHeight;
            AppTheme.StyleGrid(dataGridPackets, "В этом логе нет распознанных посылок");
        }
    }
}
