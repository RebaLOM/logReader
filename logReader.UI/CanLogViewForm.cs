using System.Linq;
using logReader.UI.Controls;
using logReader.UI.Theme;

namespace logReader.UI
{
    // Статистика ID лога: чтение в фоне с отменой, виртуальный список — без тысяч дочерних контролов.
    public partial class CanLogViewForm : Form
    {
        private readonly string _sourcePath;
        private readonly ListView _list = new();
        private readonly EmptyState _empty = new();
        private readonly CancellationTokenSource _loading = new();
        private List<(string ID, int Count)> _packets = new();
        private List<(string ID, int Count)> _filtered = new();

        public CanLogViewForm(string sourcePath)
        {
            InitializeComponent();
            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;
            _sourcePath = sourcePath;
            panelTop.Tag = ThemeTags.Elevated;
            labelCount.Tag = ThemeTags.Muted;
            ThemeForm.Wire(this);

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.VirtualMode = true;
            _list.Columns.Add("ID посылки");
            _list.Columns.Add("Кол-во посылок", -2, HorizontalAlignment.Right);
            _list.RetrieveVirtualItem += (_, e) =>
            {
                var (id, count) = _filtered[e.ItemIndex];
                e.Item = new ListViewItem(new[] { id, count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) });
            };
            _list.Resize += (_, _) => FitColumns();
            _empty.Dock = DockStyle.Fill;
            _empty.TitleText = "Ничего не найдено";
            _empty.BodyText = "Измените строку поиска или выберите другой лог.";
            _empty.Visible = false;
            scrollPanel.AutoScroll = false;
            scrollPanel.Controls.Add(_list);
            scrollPanel.Controls.Add(_empty);

            labelCount.Text = "Чтение лога…";
            textBoxSearch.Enabled = false;
            Shown += async (_, _) => await LoadAsync();
            FormClosing += (_, _) => _loading.Cancel();
        }

        private async Task LoadAsync()
        {
            UseWaitCursor = true;
            try
            {
                var token = _loading.Token;
                var counts = await Task.Run(() => logReader.Processing.UnknownDevicesScanner.CollectLogIds(_sourcePath, null, token), token);
                if (IsDisposed) return;

                if (counts.Count == 0 && Directory.Exists(_sourcePath)
                    && !logReader.Processing.LogFolderScanner.EnumerateSupportedLogFiles(_sourcePath).Any())
                {
                    MessageBox.Show(this, "В выбранной папке нет файлов .csv, .trc, .asc или .txt.",
                        "Нет логов", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }

                _packets = counts.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                 .Select(kv => (kv.Key, kv.Value))
                                 .ToList();
                textBoxSearch.Enabled = true;
                ApplyFilter();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (IsDisposed) return;
                MessageBox.Show(this, "Ошибка чтения файла: " + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            finally
            {
                if (!IsDisposed) UseWaitCursor = false;
            }
        }

        private void ApplyFilter()
        {
            string query = textBoxSearch.Text.Trim();
            _filtered = string.IsNullOrEmpty(query)
                ? _packets
                : _packets.Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            int totalPackets = _packets.Sum(p => p.Count);
            int filteredPackets = _filtered.Sum(p => p.Count);
            labelCount.Text = string.IsNullOrEmpty(query)
                ? $"Уникальных ID: {_packets.Count}   Всего посылок: {totalPackets:N0}"
                : $"Найдено ID: {_filtered.Count} из {_packets.Count}   Посылок: {filteredPackets:N0} из {totalPackets:N0}";

            _list.VirtualListSize = _filtered.Count;
            _list.Visible = _filtered.Count > 0;
            _empty.Visible = _filtered.Count == 0 && _packets.Count > 0;
            _list.Invalidate();
            FitColumns();
        }

        private void FitColumns()
        {
            if (_list.Columns.Count < 2) return;
            int countWidth = TextRenderer.MeasureText("Кол-во посылок__", _list.Font).Width;
            _list.Columns[1].Width = countWidth;
            _list.Columns[0].Width = Math.Max(80, _list.ClientSize.Width - countWidth);
        }

        private void textBoxSearch_TextChanged(object? sender, EventArgs e) => ApplyFilter();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loading.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
