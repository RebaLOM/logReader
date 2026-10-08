using System.Globalization;
using System.Linq;
using logReader;
using static logReader.BitMath;

namespace logReader.UI
{
    internal sealed class DbcSignalEditForm : Form
    {
        private readonly ModernTextBox _txtName = new();
        private readonly ModernComboBox _cmbType = new();
        private readonly ModernNumericUpDown _numByteIndex = new();
        private readonly ModernNumericUpDown _numStartBitInByte = new();
        private readonly ModernNumericUpDown _numLength = new();
        private readonly ModernTextBox _txtMinHex = new();
        private readonly ModernTextBox _txtMaxHex = new();
        private readonly ModernTextBox _txtOffset = new();
        private readonly ModernTextBox _txtFactor = new();
        private readonly ModernTextBox _txtUnit = new();
        private readonly RadioButton _rbIntel = new();
        private readonly RadioButton _rbMotorola = new();
        private readonly ModernButton _btnOk = new();
        private readonly ModernButton _btnCancel = new();
        private readonly CanPayloadGridControl _payloadGrid = new();
        private readonly InlineNotice _validation = new() { Visible = false, Tone = StatusTone.Error };
        private readonly Label _selectionSummary = new() { AutoSize = true };

        private readonly int _messageDlc;
        private readonly IReadOnlyList<string> _existingSignalNames;
        private readonly IReadOnlyList<DbcSignal> _siblingSignals;
        private readonly string? _currentSignalName;
        private bool _syncingFromGrid;

        public DbcSignal Signal { get; private set; }

        public DbcSignalEditForm(
            DbcSignal? initial,
            int messageDlc,
            IEnumerable<string>? existingSignalNames = null,
            IEnumerable<DbcSignal>? siblingSignals = null,
            string? currentSignalName = null)
        {
            SuspendLayout();
            _messageDlc = Math.Clamp(messageDlc, 1, 8);
            _existingSignalNames = existingSignalNames != null
                ? existingSignalNames.ToList()
                : Array.Empty<string>();
            _siblingSignals = siblingSignals?.ToList() ?? new List<DbcSignal>();
            _currentSignalName = currentSignalName ?? initial?.Name;
            Signal = initial != null ? initial.Clone() : new DbcSignal();

            Text = initial == null ? "Новый сигнал DBC" : "Редактирование сигнала DBC";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            MinimumSize = new Size(860, 520);
            ClientSize = new Size(960, 780);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            LoadFromSignal(Signal);
            WireGridSync();

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void WireGridSync()
        {
            _numLength.ValueChanged += (_, _) => { RecalcHexBounds(); SyncGridFromFields(); };
            _cmbType.SelectedIndexChanged += (_, _) => RecalcHexBounds();
            _numByteIndex.ValueChanged += (_, _) => SyncGridFromFields();
            _numStartBitInByte.ValueChanged += (_, _) => SyncGridFromFields();
            _rbIntel.CheckedChanged += (_, _) => { if (_rbIntel.Checked) OnByteOrderChanged(); };
            _rbMotorola.CheckedChanged += (_, _) => { if (_rbMotorola.Checked) OnByteOrderChanged(); };
            _payloadGrid.SelectionChanged += (_, _) => SyncFieldsFromGrid();
        }

        private void OnByteOrderChanged()
        {
            if (_syncingFromGrid) return;
            _payloadGrid.IsLittleEndian = _rbIntel.Checked;
            UpdateSelectionSummary();
        }

        private void SyncGridFromFields()
        {
            if (_syncingFromGrid) return;
            _payloadGrid.IsLittleEndian = _rbIntel.Checked;
            _payloadGrid.SetSelectionFromFields(
                (int)_numByteIndex.Value,
                (int)_numStartBitInByte.Value,
                (int)_numLength.Value,
                _rbIntel.Checked,
                fireEvent: false);
            UpdateSelectionSummary();
        }

        private void SyncFieldsFromGrid()
        {
            _syncingFromGrid = true;
            _payloadGrid.ApplySelectionToFields(out int byteIndex, out int bitInByte, out int length);
            _numByteIndex.Value = Math.Clamp(byteIndex, (int)_numByteIndex.Minimum, (int)_numByteIndex.Maximum);
            _numStartBitInByte.Value = Math.Clamp(bitInByte, 0, 7);
            _numLength.Value = Math.Clamp(length, (int)_numLength.Minimum, (int)_numLength.Maximum);
            RecalcHexBounds();
            _syncingFromGrid = false;
            UpdateSelectionSummary();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Сигнал DBC", "Настройте расположение битов и преобразование значения.", IconKind.Signal), 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 0, 16), Margin = Padding.Empty };
            var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 0, 16, 0) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _txtName.PlaceholderText = "Например, EngineSpeed";
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Items.AddRange(new object[] { "int (знаковый)", "uint (беззнаковый)" });
            fields.Controls.Add(BuildCard("Идентификация", UiFactory.Field("Имя сигнала", _txtName, "Без пробелов; имя должно быть уникальным в сообщении."), UiFactory.Field("Тип исходного значения", _cmbType)));

            _numByteIndex.Minimum = 0;
            _numByteIndex.Maximum = _messageDlc - 1;
            _numStartBitInByte.Minimum = 0;
            _numStartBitInByte.Maximum = 7;
            _numLength.Minimum = 1;
            _numLength.Maximum = 64;
            _numLength.Value = 8;
            _txtMinHex.ReadOnly = true;
            _txtMaxHex.ReadOnly = true;
            _rbIntel.Text = "Intel — little-endian";
            _rbIntel.AutoSize = true;
            _rbMotorola.Text = "Motorola — big-endian";
            _rbMotorola.AutoSize = true;
            var order = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            order.Controls.AddRange(new Control[] { _rbIntel, _rbMotorola });
            fields.Controls.Add(BuildCard("Расположение в сообщении",
                BuildColumns(UiFactory.Field("Байт", _numByteIndex, $"0–{_messageDlc - 1}"), UiFactory.Field("Начальный бит", _numStartBitInByte, "0–7"), UiFactory.Field("Длина, бит", _numLength, "1–64")),
                UiFactory.Field("Порядок байтов", order),
                BuildColumns(UiFactory.Field("Минимум raw", _txtMinHex), UiFactory.Field("Максимум raw", _txtMaxHex))));
            _txtUnit.PlaceholderText = "Например, rpm";
            var physicalCard = BuildCard("Физическое значение",
                BuildColumns(UiFactory.Field("Множитель", _txtFactor), UiFactory.Field("Смещение", _txtOffset), UiFactory.Field("Единица", _txtUnit)),
                new Label { Text = "Значение = raw × Factor + Offset", Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, AutoSize = true });

            _payloadGrid.Mode = CanPayloadGridMode.Edit;
            _payloadGrid.ShowLegend = false;
            _payloadGrid.Dlc = _messageDlc;
            _payloadGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _payloadGrid.Margin = new Padding(0, 12, 0, 12);
            _selectionSummary.Font = Typography.Secondary;
            _selectionSummary.ForeColor = AppTheme.TextSecondary;
            var guide = new Label { AutoSize = true, MaximumSize = new Size(300, 0), Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, Text = "Выделите диапазон мышью или используйте поля слева." };
            var payloadCard = BuildCard($"Карта битов · DLC {_messageDlc}", _payloadGrid, _selectionSummary, guide);
            var preview = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = Padding.Empty };
            preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            preview.Controls.Add(payloadCard);
            preview.Controls.Add(physicalCard);
            content.Controls.Add(fields, 0, 0);
            content.Controls.Add(preview, 1, 0);
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, 1);
            _validation.Dock = DockStyle.Top;
            _validation.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(_validation, 0, 2);

            _btnOk.Text = "Сохранить сигнал";
            _btnOk.AutoSize = true;
            _btnOk.Variant = ButtonVariant.Primary;
            _btnOk.Icon = IconKind.Check;
            _btnOk.Click += (_, _) => OnOk();
            _btnCancel.Text = "Отмена";
            _btnCancel.AutoSize = true;
            _btnCancel.Variant = ButtonVariant.Secondary;
            _btnCancel.DialogResult = DialogResult.Cancel;
            root.Controls.Add(UiFactory.Footer(_btnOk, _btnCancel), 0, 3);
            Controls.Add(root);
        }

        private static ModernCard BuildCard(string title, params Control[] controls)
        {
            var card = new ModernCard { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stack.Controls.Add(new Label { Text = title, Font = Typography.CardTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
            foreach (var control in controls)
            {
                control.Dock = DockStyle.Top;
                control.Margin = new Padding(0, 0, 0, 8);
                stack.Controls.Add(control);
            }
            card.Controls.Add(stack);
            return card;
        }

        private static Control BuildColumns(params Control[] controls)
        {
            var columns = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = controls.Length, Margin = Padding.Empty };
            foreach (var control in controls)
            {
                columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / controls.Length));
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 0, columns.Controls.Count == controls.Length - 1 ? 0 : 8, 0);
                columns.Controls.Add(control);
            }
            return columns;
        }

        private void UpdateSelectionSummary()
        {
            bool fits = SignalFitsInDlc((int)_numByteIndex.Value * 8 + (int)_numStartBitInByte.Value, (int)_numLength.Value, _rbIntel.Checked, _messageDlc * 8);
            _selectionSummary.Text = $"Байт {_numByteIndex.Value} · бит {_numStartBitInByte.Value} · {_numLength.Value} бит · {(_rbIntel.Checked ? "Intel" : "Motorola")}" + (fits ? "" : "\nДиапазон выходит за DLC.");
            _selectionSummary.ForeColor = fits ? AppTheme.TextSecondary : AppTheme.Warning;
        }

        private void ShowValidation(string message, Control? field = null)
        {
            _validation.Text = message;
            _validation.Visible = true;
            if (field is ModernTextBox modern) modern.HasError = true;
            field?.Focus();
            if (field is TextBox textBox) textBox.SelectAll();
        }

        private void RefreshPayloadOverlays()
        {
            _payloadGrid.Overlays = CanPayloadGridFactory.FromDbcSignals(_siblingSignals, _currentSignalName);
        }

        private void RecalcHexBounds()
        {
            var tmp = new DbcSignal
            {
                Length = (int)_numLength.Value,
                IsSigned = _cmbType.SelectedIndex == 0
            };
            ComputeRawRange(tmp.Length, tmp.IsSigned, out long rawMin, out long rawMax);
            _txtMinHex.Text = FormatRawBound(rawMin, tmp.Length, tmp.IsSigned);
            _txtMaxHex.Text = FormatRawBound(rawMax, tmp.Length, tmp.IsSigned);
        }

        private void LoadFromSignal(DbcSignal s)
        {
            _txtName.Text = s.Name;
            _cmbType.SelectedIndex = s.IsSigned ? 0 : 1;

            int byteIndex = s.Length > 0 ? s.StartBit / 8 : 0;
            int bitInByte = s.StartBit % 8;

            if (byteIndex > (int)_numByteIndex.Maximum) byteIndex = (int)_numByteIndex.Maximum;
            _numByteIndex.Value = Math.Max(0, byteIndex);
            _numStartBitInByte.Value = Math.Max(0, Math.Min(7, bitInByte));
            _numLength.Value = Math.Max(1, Math.Min(64, s.Length == 0 ? 8 : s.Length));

            _txtOffset.Text = s.Offset.ToString(CultureInfo.InvariantCulture);
            _txtFactor.Text = s.Factor.ToString(CultureInfo.InvariantCulture);
            _txtUnit.Text = s.Unit ?? "";

            _rbIntel.Checked = s.IsLittleEndian;
            _rbMotorola.Checked = !s.IsLittleEndian;

            long rawMin, rawMax;
            ComputeRawRange(s.Length, s.IsSigned, out rawMin, out rawMax);
            _txtMinHex.Text = FormatRawBound(rawMin, s.Length, s.IsSigned);
            _txtMaxHex.Text = FormatRawBound(rawMax, s.Length, s.IsSigned);

            RefreshPayloadOverlays();
            SyncGridFromFields();
        }

        private void OnOk()
        {
            _validation.Visible = false;
            foreach (var field in new[] { _txtName, _txtFactor, _txtOffset }) field.HasError = false;
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidation("Введите имя сигнала.", _txtName);
                return;
            }

            if (name.Any(char.IsWhiteSpace))
            {
                ShowValidation("Имя сигнала не должно содержать пробелов.", _txtName);
                return;
            }
            if (!DbcLineParser.IsValidSymbolName(name))
            {
                ShowValidation("Недопустимое имя сигнала. " + DbcLineParser.SymbolNameRulesHint, _txtName);
                return;
            }
            if (_existingSignalNames.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowValidation("Сигнал с таким именем уже существует.", _txtName);
                return;
            }

            int byteIndex = (int)_numByteIndex.Value;
            int bitInByte = (int)_numStartBitInByte.Value;
            int length = (int)_numLength.Value;

            int globalStartBit = byteIndex * 8 + bitInByte;
            bool littleEndian = !_rbMotorola.Checked;
            int payloadBits = _messageDlc * 8;

            if (!SignalFitsInDlc(globalStartBit, length, littleEndian, payloadBits))
            {
                ShowValidation($"Сигнал выходит за пределы DLC ({_messageDlc} байт). Измените начало или длину.", _numLength);
                return;
            }

            if (!NumberParseHelper.TryParseOrDefault(_txtFactor.Text, 1.0, out double factor))
            {
                ShowValidation("Множитель: неверный формат числа.", _txtFactor);
                return;
            }
            if (double.IsNaN(factor) || double.IsInfinity(factor))
            {
                ShowValidation("Множитель должен быть конечным числом.", _txtFactor);
                return;
            }
            if (!NumberParseHelper.TryParseOrDefault(_txtOffset.Text, 0.0, out double offset))
            {
                ShowValidation("Смещение: неверный формат числа.", _txtOffset);
                return;
            }
            if (double.IsNaN(offset) || double.IsInfinity(offset))
            {
                ShowValidation("Смещение должно быть конечным числом.", _txtOffset);
                return;
            }

            bool isSigned = _cmbType.SelectedIndex == 0;

            Signal = new DbcSignal
            {
                Name = name,
                StartBit = globalStartBit,
                Length = length,
                IsLittleEndian = littleEndian,
                IsSigned = isSigned,
                Factor = factor == 0 ? 1.0 : factor,
                Offset = offset,
                Unit = _txtUnit.Text.Trim(),
                Receiver = Signal.Receiver ?? "Vector__XXX",
                MultiplexIndicator = Signal.MultiplexIndicator,
                ValueType = Signal.ValueType,
                OriginName = Signal.OriginName,
                TrailingLines = new List<string>(Signal.TrailingLines),
            };

            ComputeRawRange(Signal.Length, Signal.IsSigned, out long rawMin, out long rawMax);
            (Signal.Min, Signal.Max) = DbcPhysicalValue.PhysicalBoundsFromRaw(
                rawMin, rawMax, Signal.Factor, Signal.Offset);

            DialogResult = DialogResult.OK;
            Close();
        }

    }
}
