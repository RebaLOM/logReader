using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    /// <summary>Параметр XLSX — окно повторяет DBC "Signal Details" + опциональный режим BIN.</summary>
    internal sealed class DeviceFieldRowEditForm : Form
    {
        private readonly RadioButton _rbKindNum = new();
        private readonly RadioButton _rbKindBin = new();

        private readonly TextBox _txtName = new();
        private readonly ComboBox _cmbRawType = new();
        private readonly NumericUpDown _numByteIndex = new();
        private readonly NumericUpDown _numStartBitInByte = new();
        private readonly NumericUpDown _numLength = new();
        private readonly TextBox _txtMinHex = new();
        private readonly TextBox _txtMaxHex = new();
        private readonly TextBox _txtOffset = new();
        private readonly TextBox _txtFactor = new();
        private readonly TextBox _txtUnit = new();
        private readonly RadioButton _rbIntel = new();
        private readonly RadioButton _rbMotorola = new();

        private readonly TextBox _txtBinName = new();
        private readonly NumericUpDown _numBinByte = new();
        private readonly NumericUpDown _numBinBitStart = new();
        private readonly NumericUpDown _numBinLength = new();

        private readonly Panel _panelNum = new();
        private readonly Panel _panelBin = new();
        private readonly Button _btnOk = new();
        private readonly Button _btnCancel = new();

        private readonly int _fieldIndex;
        private readonly int _dlc;

        public DeviceFieldRow Row { get; private set; }

        public DeviceFieldRowEditForm(DeviceFieldRow? initial, int dlc, int fieldIndex)
        {
            _dlc = Math.Clamp(dlc, 1, 8);
            _fieldIndex = fieldIndex;

            Text = initial == null ? "Signal Details" : "Signal Details (изменение)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(540, 370);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            Row = initial ?? CreateDefaultNumRow();

            BuildLayout();
            LoadFromRow(Row);

            _rbKindNum.CheckedChanged += (_, _) => SwitchPanels();
            _rbKindBin.CheckedChanged += (_, _) => SwitchPanels();
            _numLength.ValueChanged += (_, _) => RecalcHexBounds();
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
            var topKind = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(12, 8, 12, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _rbKindNum.Text = "NUM";
            _rbKindNum.AutoSize = true;
            _rbKindNum.Margin = new Padding(0, 4, 20, 0);
            _rbKindBin.Text = "BIN";
            _rbKindBin.AutoSize = true;
            _rbKindBin.Margin = new Padding(0, 4, 0, 0);
            topKind.Controls.Add(_rbKindNum);
            topKind.Controls.Add(_rbKindBin);

            _panelNum.Dock = DockStyle.Fill;
            _panelBin.Dock = DockStyle.Fill;
            _panelBin.Visible = false;

            BuildPanelNum();
            BuildPanelBin();

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(_panelBin);
            host.Controls.Add(_panelNum);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                Height = 50
            };
            _btnOk.Text = "OK";
            _btnOk.Width = 90;
            _btnOk.Click += (_, _) => OnOk();
            _btnCancel.Text = "Отмена";
            _btnCancel.Width = 90;
            _btnCancel.DialogResult = DialogResult.Cancel;
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);

            Controls.Add(host);
            Controls.Add(topKind);
            Controls.Add(bottom);
        }

        private void BuildPanelNum()
        {
            void Lbl(string t, int x, int y) => _panelNum.Controls.Add(new Label { Text = t, Location = new Point(x, y + 3), AutoSize = true });
            void Add(Control c, int x, int y) { c.Location = new Point(x, y); _panelNum.Controls.Add(c); }

            int y = 10;

            Lbl("Name:", 12, y);
            _txtName.Size = new Size(440, 23);
            Add(_txtName, 70, y);
            y += 32;

            Lbl("Type:", 12, y);
            _cmbRawType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRawType.Items.AddRange(new object[] { "int (знаковый)", "uint (беззнаковый)" });
            _cmbRawType.Size = new Size(170, 23);
            Add(_cmbRawType, 70, y);

            Lbl("Byte Index:", 260, y);
            _numByteIndex.Minimum = 0;
            _numByteIndex.Maximum = _dlc - 1;
            _numByteIndex.Size = new Size(55, 23);
            Add(_numByteIndex, 340, y);

            Lbl("Start Bit:", 405, y);
            _numStartBitInByte.Minimum = 0;
            _numStartBitInByte.Maximum = 7;
            _numStartBitInByte.Size = new Size(50, 23);
            Add(_numStartBitInByte, 470, y);
            y += 32;

            Lbl("Length:", 12, y);
            _numLength.Minimum = 1;
            _numLength.Maximum = 64;
            _numLength.Value = 8;
            _numLength.Size = new Size(55, 23);
            Add(_numLength, 70, y);
            Lbl("Bits", 130, y);

            Lbl("Min Val: 0x", 175, y);
            _txtMinHex.Size = new Size(75, 23);
            Add(_txtMinHex, 254, y);
            Lbl("Max Val: 0x", 345, y);
            _txtMaxHex.Size = new Size(75, 23);
            Add(_txtMaxHex, 425, y);
            y += 32;

            Lbl("Offset:", 12, y);
            _txtOffset.Size = new Size(85, 23);
            Add(_txtOffset, 70, y);

            Lbl("Factor:", 175, y);
            _txtFactor.Size = new Size(85, 23);
            Add(_txtFactor, 230, y);

            Lbl("Unit:", 345, y);
            _txtUnit.Size = new Size(130, 23);
            Add(_txtUnit, 390, y);
            y += 38;

            Lbl("Byte Order:", 12, y);
            y += 22;
            _rbIntel.Text = "Intel — little-endian";
            _rbIntel.AutoSize = true;
            _rbIntel.Location = new Point(12, y);
            _panelNum.Controls.Add(_rbIntel);
            y += 24;
            _rbMotorola.Text = "Motorola — big-endian";
            _rbMotorola.AutoSize = true;
            _rbMotorola.Location = new Point(12, y);
            _panelNum.Controls.Add(_rbMotorola);
        }

        private void BuildPanelBin()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(12)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            int r = 0;
            void AddRow(string label, Control c)
            {
                t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 0) }, 0, r);
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(3, 4, 3, 4);
                t.Controls.Add(c, 1, r);
                r++;
            }

            _numBinByte.Minimum = 0;
            _numBinByte.Maximum = 7;
            _numBinBitStart.Minimum = 0;
            _numBinBitStart.Maximum = 7;
            _numBinLength.Minimum = 1;
            _numBinLength.Maximum = 8;
            _numBinLength.Value = 1;

            AddRow("Name:", _txtBinName);
            AddRow("Байт (0–7):", _numBinByte);
            AddRow("BitStart (0–7):", _numBinBitStart);
            AddRow("Length (1–8):", _numBinLength);

            var hint = new Label
            {
                Text = "Режим BIN: параметр = подряд идущие биты внутри ОДНОГО байта. " +
                       "Выводится как целое число (без scale/offset).",
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(12, 4, 12, 8),
                ForeColor = Color.DimGray
            };

            _panelBin.Controls.Add(hint);
            _panelBin.Controls.Add(t);
        }

        private void SwitchPanels()
        {
            bool bin = _rbKindBin.Checked;
            _panelNum.Visible = !bin;
            _panelBin.Visible = bin;
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
        }

        private void RecalcHexBounds()
        {
            int length = (int)_numLength.Value;
            bool signed = _cmbRawType.SelectedIndex == 0;
            ComputeRawRange(length, signed, out long rawMin, out long rawMax);
            _txtMinHex.Text = FormatHex(rawMin, length);
            _txtMaxHex.Text = FormatHex(rawMax, length);
        }

        private static void ComputeRawRange(int length, bool signed, out long rawMin, out long rawMax)
        {
            if (signed)
            {
                if (length >= 64) { rawMin = long.MinValue; rawMax = long.MaxValue; }
                else { rawMin = -(1L << (length - 1)); rawMax = (1L << (length - 1)) - 1; }
            }
            else
            {
                rawMin = 0;
                rawMax = length >= 64 ? long.MaxValue : (1L << length) - 1;
            }
        }

        private static string FormatHex(long raw, int length)
        {
            int nibbles = Math.Max(1, (length + 3) / 4);
            ulong masked;
            if (length >= 64) masked = unchecked((ulong)raw);
            else
            {
                ulong mask = (1UL << length) - 1;
                masked = unchecked((ulong)raw) & mask;
            }
            return masked.ToString("X" + nibbles, CultureInfo.InvariantCulture);
        }

        private void OnOk()
        {
            if (_rbKindBin.Checked)
            {
                string header = _txtBinName.Text.Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    MessageBox.Show(this, "Введите Name.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtBinName.Focus();
                    return;
                }

                int low = (int)_numBinByte.Value;
                int bitStart = (int)_numBinBitStart.Value;
                int len = (int)_numBinLength.Value;
                if (bitStart + len > 8)
                {
                    MessageBox.Show(this, "BitStart + Length не должны превышать 8.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "Введите Name.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }
            if (name.Any(char.IsWhiteSpace))
            {
                MessageBox.Show(this, "Имя не должно содержать пробелов.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            int byteIndex = (int)_numByteIndex.Value;
            int bitInByte = (int)_numStartBitInByte.Value;
            int length = (int)_numLength.Value;
            int globalStartBit = byteIndex * 8 + bitInByte;

            if (globalStartBit + length > _dlc * 8)
            {
                MessageBox.Show(this, $"Сигнал выходит за пределы DLC ({_dlc} байт).", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!NumberParseHelper.TryParseDouble(_txtFactor.Text, out double factor))
            {
                MessageBox.Show(this, "Factor: неверный формат.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtFactor.Focus();
                return;
            }
            if (factor == 0) factor = 1;

            if (!NumberParseHelper.TryParseDouble(_txtOffset.Text, out double offset))
            {
                MessageBox.Show(this, "Offset: неверный формат.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtOffset.Focus();
                return;
            }

            bool isSigned = _cmbRawType.SelectedIndex == 0;
            bool littleEndian = !_rbMotorola.Checked;

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
