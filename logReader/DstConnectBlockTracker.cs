namespace logReader
{
    public sealed class DstConnectSnapshotRow
    {
        public double StepMs { get; init; }
        public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
    }

    // Last-known по завершённым блокам; одна строка CSV на блок.
    public sealed class DstConnectBlockTracker
    {
        private readonly DstConnectOptions _options;
        private readonly TrcBlockDetectionResult _detection;
        private readonly HashSet<double> _emittedSteps = new();
        private readonly List<DstConnectSnapshotRow> _rows = new();
        private readonly Dictionary<string, string> _globalState = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _currentBlock = new(StringComparer.Ordinal);

        private bool _blockOpen;
        private double _lastBlockStartTimeMs = double.NaN;
        private double _previousTimeMs = double.NaN;
        private int _framesInCurrentBlock;
        private int _currentBlockSlot = -1;

        public DstConnectBlockTracker(DstConnectOptions options, TrcBlockDetectionResult detection)
        {
            _options = options;
            _detection = detection;
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
                    if (!_detection.UsesReferenceRecurrence && !_detection.UsedGapFallback && _detection.BlockPeriodMs > 0)
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

        public void UpdateParameter(string key, string value)
        {
            if (!_blockOpen)
                return;

            _currentBlock[key] = value;
        }

        public void Finish(double lastTimeMs)
        {
            if (_blockOpen)
                CommitBlock(GetBlockCommitTimeMs(lastTimeMs));
        }

        private void CommitBlock(double blockCompleteTimeMs)
        {
            foreach (var kv in _currentBlock)
                _globalState[kv.Key] = kv.Value;
            _currentBlock.Clear();

            if (_globalState.Count == 0)
                return;

            TryEmitSnapshot(blockCompleteTimeMs);
        }

        private void TryEmitSnapshot(double stepMs)
        {
            if (_globalState.Count == 0)
                return;

            if (!_emittedSteps.Add(stepMs))
                return;

            _rows.Add(new DstConnectSnapshotRow
            {
                StepMs = stepMs,
                Values = new Dictionary<string, string>(_globalState, StringComparer.Ordinal)
            });
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
            if (!double.IsNaN(_previousTimeMs))
                return _previousTimeMs;

            return fallbackTimeMs ?? _lastBlockStartTimeMs;
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
