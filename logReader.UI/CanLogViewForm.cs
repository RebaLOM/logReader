using System.Linq;
using logReader.Processing;

namespace logReader.UI
{
    // Count through the shared readers and display only the requested virtual cells.
    public partial class CanLogViewForm : Form
    {
        private readonly string _sourcePath;
        private List<(string ID, int Count)> _packets = new();
        private List<(string ID, int Count)> _filtered = new();
        private CancellationTokenSource? _loadingCancellation;
        private ProgressBar _loadingProgress = null!;

        public CanLogViewForm(string sourcePath)
        {
            InitializeComponent();
            AppTheme.Apply(this);
            AppTheme.StyleGrid(dataGridPackets, "В этом логе нет распознанных посылок");
            dataGridPackets.Columns[0].DefaultCellStyle.Font = Typography.Mono;
            dataGridPackets.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridPackets.CellValueNeeded += (_, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
                var packet = _filtered[e.RowIndex];
                e.Value = e.ColumnIndex == 0 ? packet.ID : packet.Count.ToString("N0");
            };
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            _sourcePath = sourcePath;
            _loadingProgress = new ProgressBar
            {
                Dock = DockStyle.Bottom, Height = 4,
                Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = false
            };
            panelTop.Controls.Add(_loadingProgress);
            FormClosing += (_, _) => _loadingCancellation?.Cancel();
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
                var scan = await Task.Run(() => ReadPacketCounts(_sourcePath, cancellation.Token), cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                if (!scan.HasSupportedFiles)
                {
                    AppDialog.Show(this, "В выбранной папке нет файлов .csv, .trc, .asc или .txt.",
                        "Нет логов", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }
                _packets = scan.Counts.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => (kv.Key, kv.Value)).ToList();
                ApplyFilter();
                if (scan.Warnings.Count > 0)
                {
                    _loadingProgress.Visible = false;
                    AppDialog.Show(this, string.Join(Environment.NewLine, scan.Warnings),
                        "Часть логов не прочитана", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                // Closing the viewer cancels counting without showing an error.
            }
            catch (Exception ex)
            {
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                AppDialog.Show(this, "Ошибка чтения файла: " + ex.Message,
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

        private static (Dictionary<string, int> Counts, bool HasSupportedFiles, List<string> Warnings)
            ReadPacketCounts(string sourcePath, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            bool hasFiles = !Directory.Exists(sourcePath)
                || LogFolderScanner.EnumerateSupportedLogFiles(sourcePath).Any();
            var warnings = new List<string>();
            var counts = hasFiles
                ? UnknownDevicesScanner.CollectLogIds(sourcePath, warnings.Add, cancellation)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            cancellation.ThrowIfCancellationRequested();
            return (counts, hasFiles, warnings);
        }

        private void ApplyFilter()
        {
            string query = textBoxSearch.Text.Trim();
            _filtered = string.IsNullOrEmpty(query) ? _packets
                : _packets.Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            long total = _packets.Sum(p => (long)p.Count);
            long filteredTotal = _filtered.Sum(p => (long)p.Count);
            labelCount.Text = string.IsNullOrEmpty(query)
                ? $"Уникальных ID: {_packets.Count}   Всего посылок: {total:N0}"
                : $"Найдено ID: {_filtered.Count} из {_packets.Count}   Посылок: {filteredTotal:N0} из {total:N0}";

            dataGridPackets.RowCount = _filtered.Count;
            dataGridPackets.ClearSelection();
            dataGridPackets.Invalidate();
            bool empty = _filtered.Count == 0;
            dataGridPackets.Visible = !empty;
            emptyState.Visible = empty;
            emptyState.Title = query.Length == 0 ? "Посылки не найдены" : "Нет совпадений";
            emptyState.Description = query.Length == 0
                ? "В выбранных файлах нет распознанных CAN-посылок."
                : "Попробуйте другой CAN ID или очистите строку поиска.";
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e) => ApplyFilter();

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
            if (dataGridPackets != null)
                AppTheme.StyleGrid(dataGridPackets, "В этом логе нет распознанных посылок");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadingCancellation?.Cancel();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
