using System.Globalization;

namespace logReader.UI;

internal sealed class DtcDiagnosticsForm : Form
{
    private readonly string _sourcePath;
    private readonly bool _sourceIsFolder;
    private readonly Func<string, double, ProcessingContext, DtcAnalysisResult> _analyze;
    private readonly DtcDisplayFormat _displayFormat;
    private readonly NumericUpDown _timeout;
    private readonly Button _analyzeButton;
    private readonly Button _cancelButton;
    private readonly TextBox _search;
    private readonly ComboBox _fileFilter;
    private readonly Label _status;
    private readonly Label _counts;
    private readonly ProgressBar _progress;
    private readonly DataGridView _summaryGrid;
    private readonly DataGridView _episodeGrid;
    private readonly DataGridView _snapshotGrid;
    private readonly TabControl _detailsTabs;
    private readonly TextBox _rawData;
    private readonly TextBox _warnings;
    private DtcAnalysisResult? _result;
    private DtcSummary[] _summaries = [];
    private DtcEpisode[] _episodes = [];
    private DtcObservation[] _observations = [];
    private CancellationTokenSource? _operation;
    private bool _closeWhenIdle;
    private bool _updating;

    internal double TimeoutMs => (double)_timeout.Value * 1000;

    internal DtcDiagnosticsForm(string sourcePath, double timeoutMs = 2000,
        Func<string, double, ProcessingContext, DtcAnalysisResult>? analyze = null,
        DtcDisplayFormat displayFormat = DtcDisplayFormat.Compact)
    {
        _displayFormat = Enum.IsDefined(displayFormat) ? displayFormat : DtcDisplayFormat.Compact;
        _sourcePath = sourcePath;
        _sourceIsFolder = Directory.Exists(sourcePath);
        _analyze = analyze ?? ((path, timeout, context) => DtcAnalysisService.Analyze(path, timeout, context));
        Text = "Диагностика DTC — LOGER";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 620);
        ClientSize = new Size(1120, 780);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var source = new TextBox
        {
            ReadOnly = true, Text = sourcePath, Dock = DockStyle.Fill,
            AccessibleName = "Источник диагностики DTC", Name = "dtcSource"
        };
        layout.Controls.Add(source, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        actions.Controls.Add(new Label { Text = "Допустимый разрыв данных, с:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        _timeout = new NumericUpDown
        {
            Name = "dtcTimeoutSeconds", AccessibleName = "Допустимый разрыв данных DTC, секунд",
            Minimum = .001M, Maximum = 3600, DecimalPlaces = 3, Increment = .1M, Width = 100,
            Value = (decimal)Math.Clamp(double.IsFinite(timeoutMs) && timeoutMs > 0 ? timeoutMs / 1000 : 2, .001, 3600)
        };
        _analyzeButton = new Button { Name = "dtcAnalyze", Text = "Анализировать", AutoSize = true };
        _analyzeButton.Click += async (_, _) => await AnalyzeAsync();
        _cancelButton = new Button { Name = "dtcCancel", Text = "Отмена", AutoSize = true, Enabled = false };
        _cancelButton.Click += (_, _) => CancelAnalysis();
        _progress = new ProgressBar { Width = 160, Height = 23, Maximum = 1000, Visible = false };
        _status = new Label { AutoSize = true, Text = "Готово к анализу", Margin = new Padding(8, 8, 3, 3) };
        actions.Controls.AddRange([_timeout, _analyzeButton, _cancelButton, _progress, _status]);
        layout.Controls.Add(actions, 0, 1);
        _counts = new Label
        {
            Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 6, 3, 6),
            Text = "DTC1–DTC6 — позиции в списке. Пусто — свободно; Н/Д — неизвестно."
        };
        layout.Controls.Add(_counts, 0, 2);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        filters.Controls.Add(new Label { Text = "Поиск SPN / FMI:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        _search = new TextBox { Name = "dtcSearch", AccessibleName = "Поиск по SPN и FMI", Width = 200 };
        filters.Controls.Add(_search);
        filters.Controls.Add(new Label { Text = "Файл:", AutoSize = true, Margin = new Padding(12, 6, 3, 3) });
        _fileFilter = new ComboBox
        {
            Name = "dtcFileFilter", AccessibleName = "Фильтр по исходному файлу", Width = 450,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _fileFilter.Items.Add(new FileChoice(null, "Все файлы"));
        _fileFilter.SelectedIndex = 0;
        filters.Controls.Add(_fileFilter);
        _search.TextChanged += (_, _) => ApplyFilter();
        _fileFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        layout.Controls.Add(filters, 0, 3);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Size = new Size(1080, 560);
        split.Panel1MinSize = 110;
        split.Panel2MinSize = 170;
        split.SplitterDistance = 220;
        _summaryGrid = CreateGrid("dtcSummary", ["Файл", "Код DTC", "Первое подтверждение", "Последнее подтверждение", "Интервалы", "Последний снимок"]);
        _summaryGrid.CellValueNeeded += SummaryCellValueNeeded;
        _summaryGrid.CurrentCellChanged += (_, _) => { if (!_updating) UpdateDetails(); };
        split.Panel1.Controls.Add(_summaryGrid);

        var tabs = _detailsTabs = new TabControl { Dock = DockStyle.Fill };
        var episodesTab = new TabPage("История выбранного кода");
        _episodeGrid = CreateGrid("dtcEpisodes", ["Файл", "Код DTC", "Первое подтверждение", "Последнее подтверждение", "Первое отсутствие", "Окончание интервала"]);
        _episodeGrid.CellValueNeeded += EpisodeCellValueNeeded;
        episodesTab.Controls.Add(_episodeGrid);
        var statesTab = new TabPage("Снимки и исходные данные");
        var statesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        statesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        _snapshotGrid = CreateGrid("dtcSnapshots", ["Файл", "Время", "Количество ошибок", "Полнота списка", "DTC1", "DTC2", "DTC3", "DTC4", "DTC5", "DTC6"]);
        _snapshotGrid.CellValueNeeded += SnapshotCellValueNeeded;
        _snapshotGrid.CurrentCellChanged += (_, _) => { if (!_updating) UpdateRawData(); };
        _rawData = new TextBox
        {
            Name = "dtcRawData", AccessibleName = "Исходные данные выбранного снимка",
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
            WordWrap = false
        };
        statesLayout.Controls.Add(_snapshotGrid, 0, 0);
        statesLayout.Controls.Add(_rawData, 0, 1);
        statesTab.Controls.Add(statesLayout);
        var warningsTab = new TabPage("Предупреждения");
        _warnings = new TextBox
        {
            Name = "dtcWarnings", AccessibleName = "Предупреждения диагностики DTC",
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical
        };
        warningsTab.Controls.Add(_warnings);
        tabs.TabPages.AddRange([episodesTab, statesTab, warningsTab]);
        split.Panel2.Controls.Add(tabs);
        layout.Controls.Add(split, 0, 4);
        Controls.Add(layout);
        Shown += async (_, _) => await AnalyzeAsync();
        FormClosing += OnFormClosing;
        UiScaling.Apply(this);
    }

    private DataGridView CreateGrid(string name, string[] headers)
    {
        var grid = new DataGridView
        {
            Name = name, AccessibleName = name switch
            {
                "dtcSummary" => "Сводка кодов DTC", "dtcEpisodes" => "Интервалы наблюдения DTC", _ => "Снимки DTC"
            },
            Dock = DockStyle.Fill, ReadOnly = true, VirtualMode = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        };
        foreach (string header in headers)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header, Name = header, Width = header == "Файл" ? 200
                    : header == "Код DTC" || header.StartsWith("DTC", StringComparison.Ordinal)
                        ? _displayFormat == DtcDisplayFormat.Compact ? 105 : 200 : 150,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        return grid;
    }

    private async Task AnalyzeAsync()
    {
        if (_operation != null || _closeWhenIdle || IsDisposed || Disposing) return;
        using var cts = new CancellationTokenSource();
        _operation = cts;
        _analyzeButton.Enabled = false;
        _timeout.Enabled = false;
        _cancelButton.Enabled = true;
        _progress.Visible = true;
        _progress.Value = 0;
        _status.Text = "Анализ…";
        _result = null;
        _summaries = [];
        _episodes = [];
        _observations = [];
        _summaryGrid.RowCount = 0;
        _episodeGrid.RowCount = 0;
        _snapshotGrid.RowCount = 0;
        _rawData.Clear();
        _warnings.Clear();
        _counts.Text = "Чтение диагностических снимков…";
        double timeout = TimeoutMs;
        var progress = new Progress<ProcessingProgress>(p =>
        {
            if (!ReferenceEquals(_operation, cts) || IsDisposed || Disposing) return;
            _progress.Value = (int)Math.Round(Math.Clamp(p.Fraction, 0, 1) * _progress.Maximum);
        });
        var context = new ProcessingContext(progress: progress, cancellationToken: cts.Token);
        try
        {
            var result = await Task.Run(() => _analyze(_sourcePath, timeout, context), cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            if (IsDisposed || Disposing || _closeWhenIdle) return;
            _result = result;
            _updating = true;
            try
            {
                _fileFilter.Items.Clear();
                _fileFilter.Items.Add(new FileChoice(null, "Все файлы"));
                foreach (string path in result.Observations.Select(o => o.SourceFile).Distinct(StringComparer.OrdinalIgnoreCase))
                    _fileFilter.Items.Add(new FileChoice(path, FileLabel(path)));
                _fileFilter.SelectedIndex = 0;
                _warnings.Text = result.Warnings.Count == 0 ? "Предупреждений нет." : string.Join(Environment.NewLine, result.Warnings);
            }
            finally { _updating = false; }
            ApplyFilter();
            if (result.Observations.Count == 0)
            {
                _status.Text = "Диагностические снимки не найдены";
            }
            else if (result.Summaries.Count == 0)
            {
                ShowSnapshotsForEmptySummary();
            }
            else
            {
                _status.Text = "Анализ завершён";
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing) _status.Text = "Анализ отменён";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                _status.Text = "Ошибка анализа";
                _warnings.Text = ex.Message;
            }
        }
        finally
        {
            _operation = null;
            if (!IsDisposed && !Disposing)
            {
                _analyzeButton.Enabled = !_closeWhenIdle;
                _timeout.Enabled = !_closeWhenIdle;
                _cancelButton.Enabled = false;
                _progress.Visible = false;
                if (_closeWhenIdle && IsHandleCreated) BeginInvoke(Close);
            }
        }
    }

    private void CancelAnalysis()
    {
        _operation?.Cancel();
        _cancelButton.Enabled = false;
        _status.Text = "Отмена анализа…";
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_operation == null) return;
        e.Cancel = true;
        _closeWhenIdle = true;
        CancelAnalysis();
    }

    private void ApplyFilter()
    {
        if (_updating || _result == null) return;
        string? file = (_fileFilter.SelectedItem as FileChoice)?.Path;
        string query = _search.Text.Trim();
        string[] terms = query.Split([' ', '/', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _updating = true;
        try
        {
            _summaries = _result.Summaries.Where(s => (file == null || SameFile(s.SourceFile, file))
                && terms.All(term => s.Code.ToDisplayString(DtcDisplayFormat.Compact).Contains(term, StringComparison.OrdinalIgnoreCase)
                    || s.Code.ToDisplayString(DtcDisplayFormat.Detailed).Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
            _summaryGrid.RowCount = _summaries.Length;
            _summaryGrid.Invalidate();
        }
        finally { _updating = false; }
        _counts.Text = $"Кодов в файлах: {_result.Summaries.Count}; показано: {_summaries.Length}. "
            + $"Интервалов: {_result.Episodes.Count}; снимков: {_result.Observations.Count}. "
            + "Пусто — свободно; Н/Д — неизвестно.";
        UpdateDetails();
        if (query.Length == 0)
        {
            if (_summaries.Length == 0 && _observations.Length > 0)
                ShowSnapshotsForEmptySummary();
            else
                _status.Text = _observations.Length == 0 ? "Диагностические снимки не найдены" : "Анализ завершён";
        }
    }

    private void ShowSnapshotsForEmptySummary()
    {
        _detailsTabs.SelectedIndex = 1;
        if (_snapshotGrid.RowCount > 0)
        {
            int completeIndex = Array.FindIndex(_observations, o => o.Snapshot.IsComplete);
            _snapshotGrid.CurrentCell = _snapshotGrid.Rows[Math.Max(0, completeIndex)].Cells[0];
        }
        UpdateRawData();
        _status.Text = _observations.Any(o => o.Snapshot.IsComplete && o.Snapshot.ReportedCount == 0)
            ? "Диагностические посылки прочитаны; подтверждённых ошибок нет"
            : "Посылки прочитаны; коды не удалось восстановить";
    }

    private void UpdateDetails()
    {
        if (_result == null) return;
        int index = _summaryGrid.CurrentCell?.RowIndex ?? -1;
        DtcSummary? selected = index >= 0 && index < _summaries.Length ? _summaries[index] : null;
        string? file = selected?.SourceFile ?? (_fileFilter.SelectedItem as FileChoice)?.Path;
        _updating = true;
        try
        {
            _episodes = selected == null ? [] : _result.Episodes
                .Where(e => SameFile(e.SourceFile, selected.SourceFile) && e.Code == selected.Code).ToArray();
            // Include absent and unknown snapshots: they explain the interval boundaries.
            _observations = _result.Observations.Where(o => file == null || SameFile(o.SourceFile, file)).ToArray();
            _episodeGrid.RowCount = _episodes.Length;
            _snapshotGrid.RowCount = _observations.Length;
            _episodeGrid.Invalidate();
            _snapshotGrid.Invalidate();
        }
        finally { _updating = false; }
        UpdateRawData();
    }

    private void UpdateRawData()
    {
        int index = _snapshotGrid.CurrentCell?.RowIndex ?? -1;
        _rawData.Text = index >= 0 && index < _observations.Length
            ? $"{_observations[index].SourceFile}  |  {_observations[index].TimeText}{Environment.NewLine}{_observations[index].Snapshot.RawData}"
            : "Выберите снимок, чтобы увидеть исходные данные.";
    }

    private void SummaryCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _summaries.Length) return;
        var row = _summaries[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => FileLabel(row.SourceFile), 1 => row.Code.ToDisplayString(_displayFormat),
            2 => row.FirstSeen, 3 => row.LastSeen, 4 => row.IntervalCount, 5 => row.State, _ => null
        };
    }

    private void EpisodeCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _episodes.Length) return;
        var row = _episodes[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => FileLabel(row.SourceFile), 1 => row.Code.ToDisplayString(_displayFormat),
            2 => row.FirstSeen, 3 => row.LastSeen, 4 => row.FirstAbsent ?? "", 5 => row.EndReason, _ => null
        };
    }

    private void SnapshotCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _observations.Length) return;
        var row = _observations[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => FileLabel(row.SourceFile), 1 => row.TimeText,
            2 => row.Snapshot.ReportedCount?.ToString(CultureInfo.InvariantCulture) ?? "Н/Д",
            3 => CompletenessText(row.Snapshot),
            >= 4 and < 10 => row.Snapshot.Slots[e.ColumnIndex - 4].ToDisplayString(_displayFormat),
            _ => null
        };
    }

    private static string CompletenessText(DtcSnapshot snapshot)
    {
        if (snapshot.IsComplete) return "Полный";
        if (snapshot.ReportedCount is > 6)
            return $"Неполный: {snapshot.Slots.Count(slot => slot.Code != null)} из {snapshot.ReportedCount}";
        return "Неполный";
    }

    private string FileLabel(string path) => _sourceIsFolder ? Path.GetRelativePath(_sourcePath, path) : Path.GetFileName(path);
    private static bool SameFile(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private sealed record FileChoice(string? Path, string Label)
    {
        public override string ToString() => Label;
    }
}
