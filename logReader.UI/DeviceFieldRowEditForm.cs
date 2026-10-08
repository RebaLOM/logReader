using System.Globalization;
using System.Linq;
using logReader;
using static logReader.BitMath;

namespace logReader.UI
{
    internal sealed class DeviceFieldRowEditForm : Form
    {
        private readonly RadioButton _rbKindNum = new();
        private readonly RadioButton _rbKindBin = new();

        private readonly ModernTextBox _txtName = new();
        private readonly ModernComboBox _cmbRawType = new();
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

        private readonly ModernTextBox _txtBinName = new();
        private readonly ModernNumericUpDown _numBinByte = new();
        private readonly ModernNumericUpDown _numBinBitStart = new();
        private readonly ModernNumericUpDown _numBinLength = new();

        private readonly Panel _panelNum = new();
        private readonly Panel _panelBin = new();
        private readonly ModernButton _btnOk = new();
        private readonly ModernButton _btnCancel = new();
        private readonly CanPayloadGridControl _payloadGrid = new();
        private readonly InlineNotice _validation = new() { Visible = false, Tone = StatusTone.Error };
        private readonly Label _selectionSummary = new() { AutoSize = true };
        private ModernCard _numericPhysicalCard = null!;

        private readonly int _fieldIndex;
        private readonly int _dlc;
        private readonly IReadOnlyList<DeviceFieldRow> _siblingRows;
        private readonly string? _currentHeader;
        private bool _syncingFromGrid;

        public DeviceFieldRow Row { get; private set; }

        public DeviceFieldRowEditForm(
            DeviceFieldRow? initial,
            int dlc,
            int fieldIndex,
            IEnumerable<DeviceFieldRow>? siblingRows = null,
            string? currentHeader = null)
        {
            SuspendLayout();
            _dlc = Math.Clamp(dlc, 1, 8);
            _fieldIndex = fieldIndex;
            _siblingRows = siblingRows?.ToList() ?? new List<DeviceFieldRow>();
            _currentHeader = currentHeader ?? initial?.Header;

            Text = initial == null ? "Новый параметр XLSX" : "Редактирование параметра XLSX";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            MinimumSize = new Size(860, 520);
            ClientSize = new Size(960, 800);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            Row = initial ?? CreateDefaultNumRow();

            BuildLayout();
            ResumeLayout(true);
            AppTheme.Apply(this);
            LoadFromRow(Row);
            WireGridSync();

            _rbKindNum.CheckedChanged += (_, _) => { SwitchPanels(); RefreshGridMode(); };
            _rbKindBin.CheckedChanged += (_, _) => { SwitchPanels(); RefreshGridMode(); };
            _cmbRawType.SelectedIndexChanged += (_, _) => RecalcHexBounds();

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private static DeviceFieldRow CreateDefaultNumRow() => new(
            FieldIndex: 0,
            Header: "",
            Type: "NUM",
            StartBit: 0,
            Length: 8,
            IsLittleEndian: true,
            SignedRaw: true,
            Scale: 1,
            Offset: 0,
            Unit: null,
            MinPhys: null,
            MaxPhys: null,
            BitStart: null);

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(UiFactory.Header("Параметр XLSX", "Определите тип параметра, расположение битов и единицу измерения.", IconKind.Signal), 0, 0);

            var topKind = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            _rbKindNum.Text = "Числовой · NUM";
            _rbKindNum.AutoSize = true;
            _rbKindNum.Margin = new Padding(0, 4, 24, 4);
            _rbKindBin.Text = "Битовый · BIN";
            _rbKindBin.AutoSize = true;
            _rbKindBin.Margin = new Padding(0, 4, 0, 4);
            topKind.Controls.Add(_rbKindNum);
            topKind.Controls.Add(_rbKindBin);

            _panelNum.Dock = DockStyle.Top;
            _panelNum.AutoSize = true;
            _panelBin.Dock = DockStyle.Top;
            _panelBin.AutoSize = true;
            _panelBin.Visible = false;
            BuildPanelNum();
            BuildPanelBin();

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 0, 16), Margin = Padding.Empty };
            var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            var editor = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 0, 16, 0) };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.Controls.Add(topKind);
            editor.Controls.Add(_panelNum);
            editor.Controls.Add(_panelBin);

            _payloadGrid.Mode = CanPayloadGridMode.Edit;
            _payloadGrid.ShowLegend = false;
            _payloadGrid.Dlc = _dlc;
            _payloadGrid.Margin = new Padding(0, 12, 0, 12);
            _selectionSummary.Font = Typography.Secondary;
            _selectionSummary.ForeColor = AppTheme.TextSecondary;
            var guide = new Label { AutoSize = true, MaximumSize = new Size(300, 0), Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, Text = "Выделите диапазон мышью или используйте поля слева." };
            var payloadCard = BuildCard($"Карта битов · DLC {_dlc}", _payloadGrid, _selectionSummary, guide);
            var preview = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = Padding.Empty };
            preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            preview.Controls.Add(payloadCard);
            preview.Controls.Add(_numericPhysicalCard);
            content.Controls.Add(editor, 0, 0);
            content.Controls.Add(preview, 1, 0);
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, 1);
            _validation.Dock = DockStyle.Top;
            _validation.Margin = new Padding(0, 12, 0, 0);
            root.Controls.Add(_validation, 0, 2);

            _btnOk.Text = "Сохранить параметр";
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

        private void WireGridSync()
        {
            _numLength.ValueChanged += (_, _) => { RecalcHexBounds(); SyncGridFromFields(); };
            _numByteIndex.ValueChanged += (_, _) => SyncGridFromFields();
            _numStartBitInByte.ValueChanged += (_, _) => SyncGridFromFields();
            _rbIntel.CheckedChanged += (_, _) => { if (_rbIntel.Checked) OnByteOrderChanged(); };
            _rbMotorola.CheckedChanged += (_, _) => { if (_rbMotorola.Checked) OnByteOrderChanged(); };
            _numBinByte.ValueChanged += (_, _) => SyncGridFromFields();
            _numBinBitStart.ValueChanged += (_, _) => SyncGridFromFields();
            _numBinLength.ValueChanged += (_, _) => SyncGridFromFields();
            _payloadGrid.SelectionChanged += (_, _) => SyncFieldsFromGrid();
        }

        private void OnByteOrderChanged()
        {
            if (_syncingFromGrid || _rbKindBin.Checked) return;
            _payloadGrid.IsLittleEndian = _rbIntel.Checked;
            UpdateSelectionSummary();
        }

        private void RefreshGridMode()
        {
            bool bin = _rbKindBin.Checked;
            _payloadGrid.BinByteMode = bin;
            if (bin)
            {
                _payloadGrid.BinByteIndex = (int)_numBinByte.Value;
            }
            RefreshPayloadOverlays();
            SyncGridFromFields();
        }

        private void RefreshPayloadOverlays()
        {
            _payloadGrid.Overlays = CanPayloadGridFactory.FromDeviceRows(_siblingRows, _currentHeader);
        }

        private void SyncGridFromFields()
        {
            if (_syncingFromGrid) return;
            if (_rbKindBin.Checked)
            {
                _payloadGrid.BinByteMode = true;
                _payloadGrid.BinByteIndex = (int)_numBinByte.Value;
                _payloadGrid.IsLittleEndian = true;
                int global = BitMath.CellToGlobalBit((int)_numBinByte.Value, (int)_numBinBitStart.Value);
                _payloadGrid.SetSelection(global, (int)_numBinLength.Value, fireEvent: false);
                UpdateSelectionSummary();
                return;
            }

            _payloadGrid.BinByteMode = false;
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
            if (_rbKindBin.Checked)
            {
                _numBinByte.Value = Math.Clamp(byteIndex, 0, _dlc - 1);
                _numBinBitStart.Value = Math.Clamp(bitInByte, 0, 7);
                _numBinLength.Value = Math.Clamp(length, 1, 8);
            }
            else
            {
                _numByteIndex.Value = Math.Clamp(byteIndex, (int)_numByteIndex.Minimum, (int)_numByteIndex.Maximum);
                _numStartBitInByte.Value = Math.Clamp(bitInByte, 0, 7);
                _numLength.Value = Math.Clamp(length, (int)_numLength.Minimum, (int)_numLength.Maximum);
                RecalcHexBounds();
            }
            _syncingFromGrid = false;
            UpdateSelectionSummary();
        }

        private void BuildPanelNum()
        {
            var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _txtName.PlaceholderText = "Например, EngineSpeed";
            _cmbRawType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRawType.Items.AddRange(new object[] { "int (знаковый)", "uint (беззнаковый)" });
            stack.Controls.Add(BuildCard("Идентификация", UiFactory.Field("Имя параметра", _txtName, "Без пробелов. Это имя появится в результирующей таблице."), UiFactory.Field("Тип исходного значения", _cmbRawType)));
            _numByteIndex.Minimum = 0;
            _numByteIndex.Maximum = _dlc - 1;
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
            stack.Controls.Add(BuildCard("Расположение в сообщении",
                BuildColumns(UiFactory.Field("Байт", _numByteIndex, $"0–{_dlc - 1}"), UiFactory.Field("Начальный бит", _numStartBitInByte, "0–7"), UiFactory.Field("Длина, бит", _numLength, "1–64")),
                UiFactory.Field("Порядок байтов", order),
                BuildColumns(UiFactory.Field("Минимум raw", _txtMinHex), UiFactory.Field("Максимум raw", _txtMaxHex))));
            _txtUnit.PlaceholderText = "Например, rpm";
            _numericPhysicalCard = BuildCard("Физическое значение",
                BuildColumns(UiFactory.Field("Множитель", _txtFactor), UiFactory.Field("Смещение", _txtOffset), UiFactory.Field("Единица", _txtUnit)),
                new Label { Text = "Значение = raw × Factor + Offset", Font = Typography.Caption, ForeColor = AppTheme.TextSecondary, AutoSize = true });
            _panelNum.Controls.Add(stack);
        }

        private void BuildPanelBin()
        {
            _numBinByte.Minimum = 0;
            _numBinByte.Maximum = 7;
            _numBinBitStart.Minimum = 0;
            _numBinBitStart.Maximum = 7;
            _numBinLength.Minimum = 1;
            _numBinLength.Maximum = 8;
            _numBinLength.Value = 1;
            _txtBinName.PlaceholderText = "Например, StatusFlags";
            var hint = new InlineNotice { Text = "BIN объединяет последовательные биты одного байта. Результат — целое число без множителя и смещения.", Tone = StatusTone.Info };
            _panelBin.Controls.Add(BuildCard("Битовый параметр",
                UiFactory.Field("Имя параметра", _txtBinName),
                BuildColumns(UiFactory.Field("Байт", _numBinByte, $"DLC: 0–{_dlc - 1}"), UiFactory.Field("Начальный бит", _numBinBitStart, "0–7"), UiFactory.Field("Длина, бит", _numBinLength, "1–8")),
                hint));
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
            bool fits = _rbKindBin.Checked
                ? _numBinByte.Value < _dlc && _numBinBitStart.Value + _numBinLength.Value <= 8
                : SignalFitsInDlc((int)_numByteIndex.Value * 8 + (int)_numStartBitInByte.Value, (int)_numLength.Value, _rbIntel.Checked, _dlc * 8);
            _selectionSummary.Text = (_rbKindBin.Checked
                ? $"BIN · байт {_numBinByte.Value} · бит {_numBinBitStart.Value} · {_numBinLength.Value} бит"
                : $"Байт {_numByteIndex.Value} · бит {_numStartBitInByte.Value} · {_numLength.Value} бит · {(_rbIntel.Checked ? "Intel" : "Motorola")}") + (fits ? "" : "\nДиапазон выходит за пределы.");
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

        private void SwitchPanels()
        {
            bool bin = _rbKindBin.Checked;
            _panelNum.Visible = !bin;
            _panelBin.Visible = bin;
            _numericPhysicalCard.Visible = !bin;
        }

        private void LoadFromRow(DeviceFieldRow row)
        {
            bool isBin = string.Equals(row.Type, "BIN", StringComparison.OrdinalIgnoreCase);
            _rbKindNum.Checked = !isBin;
            _rbKindBin.Checked = isBin;
            SwitchPanels();

            if (isBin)
            {
                _txtBinName.Text = row.Header ?? "";
                _numBinByte.Value = Math.Clamp(row.StartBit, 0, 7);
                _numBinBitStart.Value = Math.Clamp(row.BitStart ?? 0, 0, 7);
                _numBinLength.Value = Math.Clamp(row.Length <= 0 ? 1 : row.Length, 1, 8);
                RefreshGridMode();
                return;
            }

            _txtName.Text = row.Header ?? "";
            _cmbRawType.SelectedIndex = row.SignedRaw ? 0 : 1;

            int byteIndex = row.Length > 0 ? row.StartBit / 8 : 0;
            int bitInByte = row.StartBit % 8;
            if (byteIndex > (int)_numByteIndex.Maximum) byteIndex = (int)_numByteIndex.Maximum;
            _numByteIndex.Value = Math.Max(0, byteIndex);
            _numStartBitInByte.Value = Math.Max(0, Math.Min(7, bitInByte));
            _numLength.Value = Math.Max(1, Math.Min(64, row.Length == 0 ? 8 : row.Length));

            _txtOffset.Text = row.Offset.ToString(CultureInfo.InvariantCulture);
            _txtFactor.Text = row.Scale.ToString(CultureInfo.InvariantCulture);
            _txtUnit.Text = row.Unit ?? "";

            _rbIntel.Checked = row.IsLittleEndian;
            _rbMotorola.Checked = !row.IsLittleEndian;

            RecalcHexBounds();
            RefreshGridMode();
        }

        private void RecalcHexBounds()
        {
            int length = (int)_numLength.Value;
            bool signed = _cmbRawType.SelectedIndex == 0;
            ComputeRawRange(length, signed, out long rawMin, out long rawMax);
            _txtMinHex.Text = FormatRawBound(rawMin, length, signed);
            _txtMaxHex.Text = FormatRawBound(rawMax, length, signed);
        }

        private void OnOk()
        {
            _validation.Visible = false;
            foreach (var field in new[] { _txtName, _txtBinName, _txtFactor, _txtOffset }) field.HasError = false;
            if (_rbKindBin.Checked)
            {
                string header = _txtBinName.Text.Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    ShowValidation("Введите имя параметра.", _txtBinName);
                    return;
                }

                int low = (int)_numBinByte.Value;
                int bitStart = (int)_numBinBitStart.Value;
                int len = (int)_numBinLength.Value;
                if (low >= _dlc)
                {
                    ShowValidation($"Байт {low} находится за пределами DLC ({_dlc} байт). Укажите байт от 0 до {_dlc - 1}.", _numBinByte);
                    return;
                }
                if (bitStart + len > 8)
                {
                    ShowValidation("Начальный бит + длина не должны превышать 8: BIN располагается внутри одного байта.", _numBinLength);
                    return;
                }

                Row = new DeviceFieldRow(
                    FieldIndex: _fieldIndex,
                    Header: header,
                    Type: "BIN",
                    StartBit: low,
                    Length: len,
                    IsLittleEndian: true,
                    SignedRaw: false,
                    Scale: 1,
                    Offset: 0,
                    Unit: null,
                    MinPhys: null,
                    MaxPhys: null,
                    BitStart: bitStart);

                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidation("Введите имя параметра.", _txtName);
                return;
            }
            if (name.Any(char.IsWhiteSpace))
            {
                ShowValidation("Имя не должно содержать пробелов.", _txtName);
                return;
            }

            int byteIndex = (int)_numByteIndex.Value;
            int bitInByte = (int)_numStartBitInByte.Value;
            int length = (int)_numLength.Value;
            int globalStartBit = byteIndex * 8 + bitInByte;
            bool littleEndian = !_rbMotorola.Checked;

            // Motorola: start+length не описывает раскладку по байтам.
            if (!SignalFitsInDlc(globalStartBit, length, littleEndian, _dlc * 8))
            {
                ShowValidation($"Параметр выходит за пределы DLC ({_dlc} байт). Измените начало или длину.", _numLength);
                return;
            }

            if (!NumberParseHelper.TryParseOrDefault(_txtFactor.Text, 1.0, out double factor))
            {
                ShowValidation("Множитель: неверный формат числа.", _txtFactor);
                return;
            }
            if (factor == 0) factor = 1;

            if (!NumberParseHelper.TryParseOrDefault(_txtOffset.Text, 0.0, out double offset))
            {
                ShowValidation("Смещение: неверный формат числа.", _txtOffset);
                return;
            }

            bool isSigned = _cmbRawType.SelectedIndex == 0;

            ComputeRawRange(length, isSigned, out long rawMin, out long rawMax);
            (double minP, double maxP) = DbcPhysicalValue.PhysicalBoundsFromRaw(rawMin, rawMax, factor, offset);

            Row = new DeviceFieldRow(
                FieldIndex: _fieldIndex,
                Header: name,
                Type: "NUM",
                StartBit: globalStartBit,
                Length: length,
                IsLittleEndian: littleEndian,
                SignedRaw: isSigned,
                Scale: factor,
                Offset: offset,
                Unit: string.IsNullOrWhiteSpace(_txtUnit.Text) ? null : _txtUnit.Text.Trim(),
                MinPhys: minP,
                MaxPhys: maxP,
                BitStart: null);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
