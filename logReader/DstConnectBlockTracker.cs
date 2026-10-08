namespace logReader
{
    // Снимок last-known значений на конец блока. Values[i] — колонка i (NaN — значения ещё не было).
    public sealed class DstConnectSnapshotRow
    {
        public DstConnectSnapshotRow(double stepMs, double[] values)
        {
            StepMs = stepMs;
            Values = values;
        }

        public double StepMs { get; }
        public double[] Values { get; }
    }

    // Last-known по завершённым блокам; одна строка CSV на блок.
    public sealed class DstConnectBlockTracker
    {
        private readonly DstConnectOptions _options;
        private readonly TrcBlockDetectionResult _detection;
        private readonly HashSet<double> _emittedSteps = new();
        private readonly List<DstConnectSnapshotRow> _rows = new();
        private readonly double[] _globalState;
        private readonly double[] _currentBlock;
        private readonly bool[] _currentBlockSet;
        private readonly List<int> _currentBlockColumns = new();
        private bool _hasGlobalState;

        private bool _blockOpen;
        private double _lastBlockStartTimeMs = double.NaN;
        private double _previousTimeMs = double.NaN;
        private int _framesInCurrentBlock;
        private int _currentBlockSlot = -1;

        public DstConnectBlockTracker(DstConnectOptions options, TrcBlockDetectionResult detection, int columnCount)
        {
            _options = options;
            _detection = detection;
            _globalState = new double[columnCount];
            _currentBlock = new double[columnCount];
            _currentBlockSet = new bool[columnCount];
            Array.Fill(_globalState, double.NaN);
        }

        public IReadOnlyList<DstConnectSnapshotRow> Rows => _rows;

        public void OnFrameStart(int messageIndex, double timeMs, string id)
        {
            if (messageIndex < _detection.FirstBlockMessageIndex)
                return;

            if (IsBlockStart(messageIndex, timeMs, id))
            {
                if (_blockOpen)
                    CommitBlock(GetBlockCommitTimeMs());
                else
                {
                    _blockOpen = true;
                    // Слот нужен и для запасной сетки (UsedGapFallback), не только для «чистых» слотов.
                    if (!_detection.UsesReferenceRecurrence && _detection.BlockPeriodMs > 0)
                    {
                        _currentBlockSlot = TrcBlockDetector.ComputeSlot(
                            timeMs,
                            _detection.BlockOriginTimeMs,
                            _detection.BlockPeriodMs,
                            GetJitterToleranceMs());
                    }
                }

                _lastBlockStartTimeMs = timeMs;
                _framesInCurrentBlock = 0;
            }

            _framesInCurrentBlock++;
            _previousTimeMs = timeMs;
        }

        public void UpdateParameter(int column, double value)
        {
            if (!_blockOpen)
                return;

            if (!_currentBlockSet[column])
            {
                _currentBlockSet[column] = true;
                _currentBlockColumns.Add(column);
            }
            _currentBlock[column] = value;
        }

        public void Finish(double lastTimeMs)
        {
            if (_blockOpen)
                CommitBlock(GetBlockCommitTimeMs(lastTimeMs));
        }

        private void CommitBlock(double blockCompleteTimeMs)
        {
            foreach (int column in _currentBlockColumns)
            {
                _globalState[column] = _currentBlock[column];
                _currentBlockSet[column] = false;
            }
            if (_currentBlockColumns.Count > 0)
                _hasGlobalState = true;
            _currentBlockColumns.Clear();

            if (!_hasGlobalState || !_emittedSteps.Add(blockCompleteTimeMs))
                return;

            _rows.Add(new DstConnectSnapshotRow(blockCompleteTimeMs, (double[])_globalState.Clone()));
        }

        private bool IsBlockStart(int messageIndex, double timeMs, string id)
        {
            if (messageIndex < _detection.AnchorMessageIndex)
                return false;

            return TrcBlockDetector.IsBlockStart(
                messageIndex,
                timeMs,
                id,
                double.IsNaN(_previousTimeMs) ? timeMs : _previousTimeMs,
                _blockOpen ? _framesInCurrentBlock : 0,
                _lastBlockStartTimeMs,
                _blockOpen,
                _detection,
                ref _currentBlockSlot,
                GetJitterToleranceMs());
        }

        private double GetBlockCommitTimeMs(double? fallbackTimeMs = null)
        {
            double raw = !double.IsNaN(_previousTimeMs)
                ? _previousTimeMs
                : fallbackTimeMs ?? _lastBlockStartTimeMs;

            // Запасная сетка: Step = конец слота периода (20, 40, …), а не время последнего кадра.
            if (_detection.UsedGapFallback && _detection.BlockPeriodMs > 0)
                return SnapToPeriodEnd(raw, _detection.BlockOriginTimeMs, _detection.BlockPeriodMs);

            return raw;
        }

        private static double SnapToPeriodEnd(double timeMs, double originTimeMs, double periodMs)
        {
            double offset = timeMs - originTimeMs;
            if (offset < 0)
                return originTimeMs + periodMs;

            int slot = (int)Math.Floor(offset / periodMs);
            return originTimeMs + (slot + 1) * periodMs;
        }

        private double GetJitterToleranceMs()
        {
            if (_options.JitterToleranceMs > 0)
                return _options.JitterToleranceMs;
            if (_detection.JitterToleranceMs > 0)
                return _detection.JitterToleranceMs;
            return TrcBlockDetector.ComputeDefaultJitter(_detection.BlockPeriodMs > 0
                ? _detection.BlockPeriodMs
                : _options.BlockPeriodMs);
        }
    }
}
