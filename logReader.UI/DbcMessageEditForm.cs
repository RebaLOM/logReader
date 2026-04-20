using System.Globalization;
using System.Linq;
using logReader;

namespace logReader.UI
{
    internal sealed class DbcMessageEditForm : Form
    {
        private readonly TextBox _txtName = new();
        private readonly TextBox _txtId = new();
        private readonly NumericUpDown _numDlc = new();
        private readonly RadioButton _rbStandard = new();
        private readonly RadioButton _rbExtended = new();
        private readonly DataGridView _grid = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnEdit = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnOk = new();
        private readonly Button _btnCancel = new();

        public DbcMessage Message { get; private set; }
        private readonly List<DbcSignal> _signals;

        public DbcMessageEditForm(DbcMessage? initial)
        {
            Message = initial != null ? Clone(initial) : new DbcMessage();
            _signals = new List<DbcSignal>(Message.Signals);

            Text = initial == null ? "Новая посылка (DBC)" : "Редактирование посылки (DBC)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(720, 460);
            ClientSize = new Size(720, 460);

            Icon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.Icon;

            BuildLayout();
            LoadFromMessage(Message);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void BuildLayout()
        {
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 6,
                Padding = new Padding(12, 12, 12, 6),
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("Имя"), 0, 0);
            _txtName.Dock = DockStyle.Fill;
            top.Controls.Add(_txtName, 1, 0);

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("ID (hex)"), 2, 0);
            _txtId.Dock = DockStyle.Fill;
            top.Controls.Add(_txtId, 3, 0);

            top.Controls.Add(MessageEditFormHelpers.MakeLabel("DLC"), 4, 0);
            _numDlc.Minimum = 1;
            _numDlc.Maximum = 8;
            _numDlc.Value = 8;
            _numDlc.Dock = DockStyle.Left;
            _numDlc.Width = 60;
            top.Controls.Add(_numDlc, 5, 0);

            var fmtPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            fmtPanel.Controls.Add(MessageEditFormHelpers.MakeLabel("Формат:"));
            _rbStandard.Text = "Standard (11-bit)";
            _rbStandard.AutoSize = true;
            _rbStandard.Margin = new Padding(8, 3, 8, 0);
            _rbExtended.Text = "Extended (29-bit)";
            _rbExtended.AutoSize = true;
            _rbExtended.Margin = new Padding(8, 3, 8, 0);
            fmtPanel.Controls.Add(_rbStandard);
            fmtPanel.Controls.Add(_rbExtended);

            var signalButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "Добавить сигнал";
            _btnAdd.AutoSize = true;
            _btnAdd.Click += (_, _) => AddSignal();
            _btnEdit.Text = "Изменить";
            _btnEdit.AutoSize = true;
            _btnEdit.Click += (_, _) => EditSignal();
            _btnDelete.Text = "Удалить";
            _btnDelete.AutoSize = true;
            _btnDelete.Click += (_, _) => DeleteSignal();
            signalButtons.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete });

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSignal(); };
            _grid.Columns.Add("Name", "Name");
            _grid.Columns.Add("ByteIdx", "Byte Idx");
            _grid.Columns.Add("StartBit", "Start Bit");
            _grid.Columns.Add("Length", "Length");
            _grid.Columns.Add("Type", "Type");
            _grid.Columns.Add("Factor", "Factor");
            _grid.Columns.Add("Offset", "Offset");
            _grid.Columns.Add("Unit", "Unit");
            _grid.Columns.Add("Order", "Order");
            _grid.Columns["Name"]!.FillWeight = 25;
            MessageEditFormHelpers.MakeGridColumnsNotSortable(_grid);

            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 6) };
            gridHost.Controls.Add(_grid);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(12),
                Height = 54,
                WrapContents = false
            };
            _btnOk.Text = "OK";
            _btnOk.Width = 100;
            _btnOk.Click += (_, _) => OnOk();
            _btnCancel.Text = "Отмена";
            _btnCancel.Width = 100;
            _btnCancel.DialogResult = DialogResult.Cancel;
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);

            Controls.Add(gridHost);
            Controls.Add(signalButtons);
            Controls.Add(fmtPanel);
            Controls.Add(top);
            Controls.Add(bottom);
        }

        private void LoadFromMessage(DbcMessage m)
        {
            _txtName.Text = m.Name;
            _txtId.Text = m.Id.ToString("X", CultureInfo.InvariantCulture);
            _numDlc.Value = Math.Clamp(m.Dlc, 1, 8);
            _rbExtended.Checked = m.IsExtended;
            _rbStandard.Checked = !m.IsExtended;
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            foreach (var s in _signals)
            {
                int byteIdx = s.Length > 0 ? s.StartBit / 8 : 0;
                int bitInByte = s.StartBit % 8;

                _grid.Rows.Add(
                    s.Name,
                    byteIdx.ToString(CultureInfo.InvariantCulture),
                    bitInByte.ToString(CultureInfo.InvariantCulture),
                    s.Length.ToString(CultureInfo.InvariantCulture),
                    s.IsSigned ? "int" : "unsigned",
                    s.Factor.ToString(CultureInfo.InvariantCulture),
                    s.Offset.ToString(CultureInfo.InvariantCulture),
                    s.Unit ?? "",
                    s.IsLittleEndian ? "Intel" : "Motorola");
            }
        }

        private int SelectedIndex()
        {
            if (_grid.CurrentRow == null) return -1;
            return _grid.CurrentRow.Index;
        }

        private void AddSignal()
        {
            using var dlg = new DbcSignalEditForm(null, (int)_numDlc.Value);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_signals.Any(x => x.Name.Equals(dlg.Signal.Name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Сигнал с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _signals.Add(dlg.Signal);
            RefreshGrid();
        }

        private void EditSignal()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _signals.Count) return;

            using var dlg = new DbcSignalEditForm(_signals[idx], (int)_numDlc.Value);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (_signals
                .Where((s, i) => i != idx)
                .Any(s => s.Name.Equals(dlg.Signal.Name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Сигнал с таким именем уже существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _signals[idx] = dlg.Signal;
            RefreshGrid();
            if (idx < _grid.Rows.Count) _grid.Rows[idx].Selected = true;
        }

        private void DeleteSignal()
        {
            int idx = SelectedIndex();
            if (idx < 0 || idx >= _signals.Count) return;

            var confirm = MessageBox.Show(
                this,
                $"Удалить сигнал '{_signals[idx].Name}'?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _signals.RemoveAt(idx);
            RefreshGrid();
        }

        private void OnOk()
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Введите имя посылки.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }
            if (name.Any(char.IsWhiteSpace))
            {
                MessageBox.Show(this, "Имя посылки не должно содержать пробелов.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            bool isExtended = _rbExtended.Checked;
            if (!MessageEditFormHelpers.TryParseHexId(_txtId.Text, isExtended, out uint id, out string idError))
            {
                MessageBox.Show(this, idError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtId.Focus();
                return;
            }

            if (_signals.Count == 0)
            {
                var cont = MessageBox.Show(
                    this,
                    "У посылки нет ни одного сигнала. Продолжить?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (cont != DialogResult.Yes) return;
            }

            int dlc = (int)_numDlc.Value;
            foreach (var s in _signals)
            {
                if (!SignalFitsInDlc(s, dlc))
                {
                    MessageBox.Show(
                        this,
                        $"Сигнал '{s.Name}' выходит за пределы DLC={dlc} байт.",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            Message = new DbcMessage
            {
                Name = name,
                Id = id,
                IsExtended = isExtended,
                Dlc = dlc,
                Transmitter = Message.Transmitter ?? "Vector__XXX",
                Signals = new List<DbcSignal>(_signals)
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool SignalFitsInDlc(DbcSignal s, int dlc)
        {
            int payloadBits = dlc * 8;
            if (s.Length <= 0 || s.Length > payloadBits) return false;

            if (s.IsLittleEndian)
            {
                return s.StartBit >= 0 && s.StartBit + s.Length <= payloadBits;
            }

            // Motorola: StartBit — MSB поля. Имитируем обход по DBC-правилу и
            // требуем, чтобы все биты остались внутри DLC.
            int bit = s.StartBit;
            for (int i = 0; i < s.Length; i++)
            {
                if (bit < 0 || bit >= payloadBits) return false;
                if ((bit % 8) == 0) bit += 15;
                else bit -= 1;
            }
            return true;
        }

        private static DbcMessage Clone(DbcMessage m)
        {
            var copy = new DbcMessage
            {
                Name = m.Name,
                Id = m.Id,
                IsExtended = m.IsExtended,
                Dlc = m.Dlc,
                Transmitter = m.Transmitter
            };
            foreach (var s in m.Signals)
            {
                copy.Signals.Add(new DbcSignal
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
                });
            }
            return copy;
        }
    }
}
