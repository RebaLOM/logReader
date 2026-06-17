using System.Globalization;
using System.Linq;
using logReader;
using static logReader.BitMath;

namespace logReader.UI
{
    internal sealed class DbcSignalEditForm : Form
    {
        private readonly TextBox _txtName = new();
        private readonly ComboBox _cmbType = new();
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
        private readonly Button _btnOk = new();
        private readonly Button _btnCancel = new();

        private readonly int _messageDlc;
        private readonly IReadOnlyList<string> _existingSignalNames;

        public DbcSignal Signal { get; private set; }

        public DbcSignalEditForm(
            DbcSignal? initial,
            int messageDlc,
            IEnumerable<string>? existingSignalNames = null)
        {
            _messageDlc = Math.Clamp(messageDlc, 1, 8);
            _existingSignalNames = existingSignalNames != null
                ? existingSignalNames.ToList()
                : Array.Empty<string>();
            Signal = initial != null ? Clone(initial) : new DbcSignal();

            Text = "Signal Details";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 275);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromSignal(Signal);

            _numLength.ValueChanged += (_, _) => RecalcHexBounds();
            _cmbType.SelectedIndexChanged += (_, _) => RecalcHexBounds();

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void BuildLayout()
        {
            Controls.Add(MakeLabel("Name:", 12, 14));
            _txtName.Location = new Point(70, 11);
            _txtName.Size = new Size(390, 23);
            Controls.Add(_txtName);

            Controls.Add(MakeLabel("Type:", 12, 47));
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Items.AddRange(new object[] { "int (знаковый)", "uint (беззнаковый)" });
            _cmbType.Location = new Point(70, 44);
            _cmbType.Size = new Size(160, 23);
            Controls.Add(_cmbType);

            Controls.Add(MakeLabel("Byte Index:", 245, 47));
            _numByteIndex.Minimum = 0;
            _numByteIndex.Maximum = _messageDlc - 1;
            _numByteIndex.Location = new Point(325, 44);
            _numByteIndex.Size = new Size(55, 23);
            Controls.Add(_numByteIndex);

            Controls.Add(MakeLabel("Start Bit:", 395, 47));
            _numStartBitInByte.Minimum = 0;
            _numStartBitInByte.Maximum = 7;
            _numStartBitInByte.Location = new Point(455, 44);
            _numStartBitInByte.Size = new Size(50, 23);
            Controls.Add(_numStartBitInByte);

            Controls.Add(MakeLabel("Length:", 12, 80));
            _numLength.Minimum = 1;
            _numLength.Maximum = 64;
            _numLength.Value = 8;
            _numLength.Location = new Point(70, 77);
            _numLength.Size = new Size(60, 23);
            Controls.Add(_numLength);
            Controls.Add(MakeLabel("Bits", 135, 80));

            Controls.Add(MakeLabel("Min Val: 0x", 165, 80));
            _txtMinHex.Location = new Point(242, 77);
            _txtMinHex.Size = new Size(70, 23);
            Controls.Add(_txtMinHex);

            Controls.Add(MakeLabel("Max Val: 0x", 325, 80));
            _txtMaxHex.Location = new Point(402, 77);
            _txtMaxHex.Size = new Size(60, 23);
            Controls.Add(_txtMaxHex);

            Controls.Add(MakeLabel("Offset:", 12, 113));
            _txtOffset.Location = new Point(70, 110);
            _txtOffset.Size = new Size(80, 23);
            Controls.Add(_txtOffset);

            Controls.Add(MakeLabel("Factor:", 165, 113));
            _txtFactor.Location = new Point(220, 110);
            _txtFactor.Size = new Size(90, 23);
            Controls.Add(_txtFactor);

            Controls.Add(MakeLabel("Unit:", 325, 113));
            _txtUnit.Location = new Point(370, 110);
            _txtUnit.Size = new Size(90, 23);
            Controls.Add(_txtUnit);

            Controls.Add(MakeLabel("Byte Order:", 12, 150));
            _rbIntel.Text = "Intel — little-endian";
            _rbIntel.Location = new Point(12, 170);
            _rbIntel.AutoSize = true;
            Controls.Add(_rbIntel);

            _rbMotorola.Text = "Motorola — big-endian";
            _rbMotorola.Location = new Point(12, 194);
            _rbMotorola.AutoSize = true;
            Controls.Add(_rbMotorola);

            _btnOk.Text = "OK";
            _btnOk.Location = new Point(340, 230);
            _btnOk.Size = new Size(80, 30);
            _btnOk.Click += (_, _) => OnOk();
            Controls.Add(_btnOk);

            _btnCancel.Text = "Отмена";
            _btnCancel.Location = new Point(425, 230);
            _btnCancel.Size = new Size(80, 30);
            _btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);
        }

        private void RecalcHexBounds()
        {
            var tmp = new DbcSignal
            {
                Length = (int)_numLength.Value,
                IsSigned = _cmbType.SelectedIndex == 0
            };
            ComputeRawRange(tmp.Length, tmp.IsSigned, out long rawMin, out long rawMax);
            _txtMinHex.Text = FormatHex(rawMin, tmp.Length);
            _txtMaxHex.Text = FormatHex(rawMax, tmp.Length);
        }

        private static Label MakeLabel(string text, int x, int y)
            => new() { Text = text, Location = new Point(x, y), AutoSize = true };

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
            _txtMinHex.Text = FormatHex(rawMin, s.Length);
            _txtMaxHex.Text = FormatHex(rawMax, s.Length);
        }

        private void OnOk()
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Введите имя сигнала.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            if (name.Any(char.IsWhiteSpace))
            {
                MessageBox.Show(this, "Имя сигнала не должно содержать пробелов.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }
            if (!DbcLineParser.IsValidSymbolName(name))
            {
                MessageBox.Show(this,
                    "Недопустимое имя сигнала. " + DbcLineParser.SymbolNameRulesHint,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }
            if (_existingSignalNames.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Сигнал с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
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
                MessageBox.Show(this,
                    $"Сигнал выходит за пределы DLC ({_messageDlc} байт).",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!NumberParseHelper.TryParseOrDefault(_txtFactor.Text, 1.0, out double factor))
            {
                MessageBox.Show(this, "Factor: неверный формат числа.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtFactor.Focus();
                return;
            }
            if (double.IsNaN(factor) || double.IsInfinity(factor))
            {
                MessageBox.Show(this, "Factor: значение должно быть конечным числом.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtFactor.Focus();
                return;
            }
            if (!NumberParseHelper.TryParseOrDefault(_txtOffset.Text, 0.0, out double offset))
            {
                MessageBox.Show(this, "Offset: неверный формат числа.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtOffset.Focus();
                return;
            }
            if (double.IsNaN(offset) || double.IsInfinity(offset))
            {
                MessageBox.Show(this, "Offset: значение должно быть конечным числом.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtOffset.Focus();
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
                Receiver = Signal.Receiver ?? "Vector__XXX"
            };

            ComputeRawRange(Signal.Length, Signal.IsSigned, out long rawMin, out long rawMax);
            (Signal.Min, Signal.Max) = DbcPhysicalValue.PhysicalBoundsFromRaw(
                rawMin, rawMax, Signal.Factor, Signal.Offset);

            DialogResult = DialogResult.OK;
            Close();
        }

        private static DbcSignal Clone(DbcSignal s) => new()
        {
            Name = s.Name,
            StartBit = s.StartBit,
            Length = s.Length,
            IsLittleEndian = s.IsLittleEndian,
            IsSigned = s.IsSigned,
            Factor = s.Factor,
            Offset = s.Offset,
            Min = s.Min,
            Max = s.Max,
            Unit = s.Unit,
            Receiver = s.Receiver
        };
    }
}
