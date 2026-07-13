namespace logReader
{
    public sealed class TrcBlockDetectionResult
    {
        public double BlockPeriodMs { get; init; }
        public int AnchorMessageIndex { get; init; }
        public string AnchorCanId { get; init; } = "";
        public double AnchorTimeMs { get; init; }
        public double BlockOriginTimeMs { get; init; }
        public int FirstBlockMessageIndex { get; init; }
        public string ReferenceCanId { get; init; } = "";
        public double BlockCoverage { get; init; }
        public double Confidence { get; init; }
        public bool UsedGapFallback { get; init; }
        public double GapThresholdMs { get; init; }
        public double JitterToleranceMs { get; init; }

        public bool UsesReferenceRecurrence => !string.IsNullOrEmpty(ReferenceCanId) && !UsedGapFallback;
    }

    // Блок = окно между повторами якорной целевой посылки (~T мс) с покрытием целевых ID.
    public static class TrcBlockDetector
    {
        private const int MaxScanFrames = 8000;
        private const int MinValidWindows = 3;

        public static TrcBlockDetectionResult Detect(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            int userAnchorMessageIndex,
            int userBlockPeriodMs,
            IReadOnlySet<string>? targetCanIds,
            Action<string>? log = null)
        {
            double periodMs = Math.Max(1, userBlockPeriodMs);
            double jitterMs = ComputeDefaultJitter(periodMs);

            if (frames.Count == 0)
            {
                return new TrcBlockDetectionResult
                {
                    BlockPeriodMs = periodMs,
                    AnchorMessageIndex = userAnchorMessageIndex > 0 ? userAnchorMessageIndex : 1,
                    JitterToleranceMs = jitterMs,
                    UsedGapFallback = true
                };
            }

            var (anchorMsgIdx, anchorTimeMs, anchorId) = ResolveAnchor(frames, userAnchorMessageIndex);
            bool manualAnchor = userAnchorMessageIndex > 0;
            int scanCount = Math.Min(frames.Count, MaxScanFrames);
            var scan = frames.Take(scanCount).ToList();
            var targets = NormalizeTargetIds(targetCanIds);
            double gapThreshold = ComputeGapThreshold(scan);

            if (manualAnchor)
            {
                log?.Invoke(
                    $"ДСТ: период {periodMs:F0} мс (допуск {jitterMs:F1} мс), " +
                    $"якорь — посылка №{anchorMsgIdx} ({anchorId}), целевых ID: {targets.Count}.");
                return new TrcBlockDetectionResult
                {
                    BlockPeriodMs = periodMs,
                    AnchorMessageIndex = anchorMsgIdx,
                    AnchorCanId = anchorId,
                    AnchorTimeMs = anchorTimeMs,
                    BlockOriginTimeMs = anchorTimeMs,
                    FirstBlockMessageIndex = anchorMsgIdx,
                    ReferenceCanId = anchorId,
                    BlockCoverage = 1,
                    Confidence = 1,
                    UsedGapFallback = false,
                    GapThresholdMs = gapThreshold,
                    JitterToleranceMs = jitterMs
                };
            }

            if (TryDetectByReferenceRecurrence(
                    scan, periodMs, jitterMs, targets, anchorMsgIdx,
                    out var recurrence))
            {
                log?.Invoke(
                    $"ДСТ: период {periodMs:F0} мс (допуск {jitterMs:F1} мс), " +
                    $"якорь ID {recurrence.ReferenceCanId}, покрытие {recurrence.BlockCoverage:F2}, " +
                    $"первая граница — посылка №{recurrence.FirstBlockMessageIndex}, целевых ID: {targets.Count}.");
                return new TrcBlockDetectionResult
                {
                    BlockPeriodMs = periodMs,
                    AnchorMessageIndex = anchorMsgIdx,
                    AnchorCanId = anchorId,
                    AnchorTimeMs = anchorTimeMs,
                    BlockOriginTimeMs = recurrence.BlockOriginTimeMs,
                    FirstBlockMessageIndex = recurrence.FirstBlockMessageIndex,
                    ReferenceCanId = recurrence.ReferenceCanId,
                    BlockCoverage = recurrence.BlockCoverage,
                    Confidence = recurrence.BlockCoverage,
                    UsedGapFallback = false,
                    GapThresholdMs = gapThreshold,
                    JitterToleranceMs = jitterMs
                };
            }

            log?.Invoke("ДСТ: повтор якорной посылки не подтверждён — запасной детект по временной сетке.");
            var (originMs, firstBlockMsgIdx, aligned) = TryAlignBlockOriginFallback(
                scan, periodMs, gapThreshold, anchorTimeMs, anchorMsgIdx, targets);

            if (!aligned)
            {
                originMs = anchorTimeMs;
                firstBlockMsgIdx = anchorMsgIdx;
            }

            log?.Invoke(
                $"ДСТ: период {periodMs:F0} мс, сетка от t={originMs:F1} мс, " +
                $"первая граница — посылка №{firstBlockMsgIdx}, целевых ID: {targets.Count}.");

            return new TrcBlockDetectionResult
            {
                BlockPeriodMs = periodMs,
                AnchorMessageIndex = anchorMsgIdx,
                AnchorCanId = anchorId,
                AnchorTimeMs = anchorTimeMs,
                BlockOriginTimeMs = originMs,
                FirstBlockMessageIndex = firstBlockMsgIdx,
                Confidence = aligned ? 0.5 : 0,
                UsedGapFallback = true,
                GapThresholdMs = gapThreshold,
                JitterToleranceMs = jitterMs
            };
        }

        public static double ComputeDefaultJitter(double periodMs)
            => Math.Max(0.5, periodMs * 0.15);

        public static bool IsBlockStart(
            int messageIndex,
            double timeMs,
            string canId,
            double previousTimeMs,
            int framesInCurrentBlock,
            double lastBlockStartTimeMs,
            bool blockOpen,
            TrcBlockDetectionResult detection,
            ref int currentSlot,
            double jitterToleranceMs)
        {
            if (messageIndex < detection.FirstBlockMessageIndex)
                return false;

            if (detection.UsesReferenceRecurrence)
            {
                return IsReferenceBlockStart(
                    messageIndex, timeMs, canId, lastBlockStartTimeMs, blockOpen, detection, jitterToleranceMs);
            }

            if (messageIndex == detection.FirstBlockMessageIndex)
            {
                if (!detection.UsedGapFallback && detection.BlockPeriodMs > 0)
                {
                    currentSlot = ComputeSlot(
                        timeMs,
                        detection.BlockOriginTimeMs,
                        detection.BlockPeriodMs,
                        jitterToleranceMs);
                }
                return true;
            }

            if (detection.UsedGapFallback)
                return IsGapBlockStart(timeMs, previousTimeMs, framesInCurrentBlock, detection.GapThresholdMs);

            if (detection.BlockPeriodMs <= 0)
                return false;

            int slot = ComputeSlot(
                timeMs,
                detection.BlockOriginTimeMs,
                detection.BlockPeriodMs,
                jitterToleranceMs);
            if (slot <= currentSlot)
                return false;

            currentSlot = slot;
            return true;
        }

        private static bool IsGapBlockStart(
            double timeMs,
            double previousTimeMs,
            int framesInCurrentBlock,
            double gapThresholdMs)
        {
            if (framesInCurrentBlock < 3)
                return false;

            return timeMs - previousTimeMs >= gapThresholdMs;
        }

        private static bool IsReferenceBlockStart(
            int messageIndex,
            double timeMs,
            string canId,
            double lastBlockStartTimeMs,
            bool blockOpen,
            TrcBlockDetectionResult detection,
            double jitterToleranceMs)
        {
            if (!canId.Equals(detection.ReferenceCanId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!blockOpen)
                return messageIndex == detection.FirstBlockMessageIndex;

            if (double.IsNaN(lastBlockStartTimeMs))
                return true;

            double delta = timeMs - lastBlockStartTimeMs;
            double period = detection.BlockPeriodMs;
            return delta >= period - jitterToleranceMs && delta <= period + jitterToleranceMs;
        }

        public static int ComputeSlot(double timeMs, double originTimeMs, double periodMs, double jitterToleranceMs)
        {
            if (periodMs <= 0)
                return 0;

            double offset = timeMs - originTimeMs;
            if (offset < -jitterToleranceMs)
                return -1;

            if (offset <= jitterToleranceMs)
                return 0;

            return (int)Math.Floor((offset + jitterToleranceMs) / periodMs);
        }

        public static (int MessageIndex, double TimeMs, string CanId) ResolveAnchor(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            int userAnchorMessageIndex)
        {
            if (userAnchorMessageIndex > 0)
            {
                foreach (var frame in frames)
                {
                    if (frame.MessageIndex == userAnchorMessageIndex)
                        return (userAnchorMessageIndex, frame.TimeMs, frame.Id);
                }
            }

            var first = frames[0];
            return (first.MessageIndex, first.TimeMs, first.Id);
        }

        private sealed record ReferenceRecurrenceResult(
            string ReferenceCanId,
            double BlockOriginTimeMs,
            int FirstBlockMessageIndex,
            double BlockCoverage);

        private static bool TryDetectByReferenceRecurrence(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            double periodMs,
            double jitterMs,
            HashSet<string> targetCanIds,
            int anchorMessageIndex,
            out ReferenceRecurrenceResult result)
        {
            result = new ReferenceRecurrenceResult("", 0, anchorMessageIndex, 0);
            var candidates = BuildReferenceCandidates(frames, targetCanIds);
            double minCoverage = DstConnectOptions.MinBlockCoverage;
            var allResults = new List<ReferenceRecurrenceResult>();

            foreach (string refId in candidates)
            {
                var detected = TryDetectCandidate(frames, refId, periodMs, jitterMs, targetCanIds, minCoverage);
                if (detected != null)
                    allResults.Add(detected);
            }

            if (allResults.Count > 0)
            {
                result = PickBestRecurrence(frames, allResults, periodMs, targetCanIds);
                return true;
            }

            return false;
        }

        private static ReferenceRecurrenceResult? TryDetectCandidate(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            string refId,
            double periodMs,
            double jitterMs,
            HashSet<string> targetCanIds,
            double minCoverage)
        {
            var refFrames = frames
                .Where(f => f.Id.Equals(refId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (refFrames.Count < MinValidWindows + 1)
                return null;

            int consecutiveValid = 0;
            var coverages = new List<double>();

            for (int i = 0; i < refFrames.Count - 1; i++)
            {
                double t0 = refFrames[i].TimeMs;
                double t1 = refFrames[i + 1].TimeMs;
                double delta = t1 - t0;

                if (Math.Abs(delta - periodMs) > jitterMs)
                {
                    consecutiveValid = 0;
                    coverages.Clear();
                    continue;
                }

                double coverage = ComputeWindowCoverage(frames, t0, t1, targetCanIds);
                if (coverage < minCoverage)
                {
                    consecutiveValid = 0;
                    coverages.Clear();
                    continue;
                }

                consecutiveValid++;
                coverages.Add(coverage);

                if (consecutiveValid >= MinValidWindows)
                {
                    int firstWindowStart = i - MinValidWindows + 1;
                    var firstRef = refFrames[firstWindowStart];
                    var best = new ReferenceRecurrenceResult(
                        refId,
                        firstRef.TimeMs,
                        firstRef.MessageIndex,
                        Median(coverages));
                    return RefineLeadingPartialBlock(frames, best, refFrames, periodMs, jitterMs, targetCanIds);
                }
            }

            return null;
        }

        private static ReferenceRecurrenceResult PickBestRecurrence(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            List<ReferenceRecurrenceResult> candidates,
            double periodMs,
            HashSet<string> targetCanIds)
        {
            int firstMessageIndex = frames[0].MessageIndex;
            var afterPartial = candidates
                .Where(c => c.FirstBlockMessageIndex > firstMessageIndex
                            || !IsLeadingPartialBlock(
                                frames, c.ReferenceCanId, c.BlockOriginTimeMs, periodMs, targetCanIds))
                .ToList();

            if (afterPartial.Count > 0)
                candidates = afterPartial;

            return candidates
                .OrderByDescending(c => c.BlockCoverage)
                .ThenBy(c => c.ReferenceCanId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.FirstBlockMessageIndex)
                .First();
        }

        private static ReferenceRecurrenceResult RefineLeadingPartialBlock(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            ReferenceRecurrenceResult result,
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> refFrames,
            double periodMs,
            double jitterMs,
            HashSet<string> targetCanIds)
        {
            if (frames.Count == 0)
                return result;

            var firstFrame = frames[0];
            if (result.FirstBlockMessageIndex != firstFrame.MessageIndex)
                return result;

            if (!IsLeadingPartialBlock(frames, result.ReferenceCanId, result.BlockOriginTimeMs, periodMs, targetCanIds))
                return result;

            for (int i = 1; i < refFrames.Count; i++)
            {
                var refFrame = refFrames[i];
                if (refFrame.MessageIndex <= firstFrame.MessageIndex)
                    continue;

                double delta = refFrame.TimeMs - result.BlockOriginTimeMs;
                if (delta < periodMs - jitterMs || delta > periodMs + jitterMs)
                    continue;

                return result with
                {
                    BlockOriginTimeMs = refFrame.TimeMs,
                    FirstBlockMessageIndex = refFrame.MessageIndex
                };
            }

            return result;
        }

        private static bool IsLeadingPartialBlock(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            string referenceCanId,
            double blockOriginTimeMs,
            double periodMs,
            HashSet<string> targetCanIds)
        {
            if (frames.Count == 0 || targetCanIds.Count == 0)
                return false;

            var firstFrame = frames[0];
            if (!firstFrame.Id.Equals(referenceCanId, StringComparison.OrdinalIgnoreCase))
                return true;

            double windowEnd = blockOriginTimeMs + periodMs;
            foreach (var frame in frames)
            {
                if (frame.TimeMs <= blockOriginTimeMs || frame.TimeMs >= windowEnd)
                    continue;
                if (!targetCanIds.Contains(frame.Id))
                    continue;
                if (string.Compare(frame.Id, referenceCanId, StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
            }

            return false;
        }

        private static List<string> BuildReferenceCandidates(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            HashSet<string> targetCanIds)
        {
            if (targetCanIds.Count > 0)
            {
                string? firstTarget = frames.FirstOrDefault(f => targetCanIds.Contains(f.Id)).Id;
                var byFrequency = frames
                    .Where(f => targetCanIds.Contains(f.Id))
                    .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToList();

                if (firstTarget != null && !byFrequency.Contains(firstTarget, StringComparer.OrdinalIgnoreCase))
                    byFrequency.Insert(0, firstTarget);
                else if (firstTarget != null)
                {
                    byFrequency.Remove(firstTarget);
                    byFrequency.Insert(0, firstTarget);
                }

                return byFrequency;
            }

            return frames
                .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToList();
        }

        private static double ComputeWindowCoverage(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            double windowStartMs,
            double windowEndMs,
            HashSet<string> targetCanIds)
        {
            if (targetCanIds.Count == 0)
                return 1;

            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var frame in frames)
            {
                if (frame.TimeMs < windowStartMs || frame.TimeMs >= windowEndMs)
                    continue;
                if (targetCanIds.Contains(frame.Id))
                    present.Add(frame.Id);
            }

            return (double)present.Count / targetCanIds.Count;
        }

        private static HashSet<string> NormalizeTargetIds(IReadOnlySet<string>? targetCanIds)
        {
            if (targetCanIds == null || targetCanIds.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(targetCanIds, StringComparer.OrdinalIgnoreCase);
        }

        private static (double OriginMs, int FirstBlockMessageIndex, bool Aligned) TryAlignBlockOriginFallback(
            IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames,
            double periodMs,
            double gapThresholdMs,
            double anchorTimeMs,
            int anchorMessageIndex,
            HashSet<string> targetCanIds)
        {
            double interBlockThreshold = Math.Max(gapThresholdMs, periodMs * 0.5);
            var boundaryTimes = new List<double>();
            var boundaryMessageIndices = new List<int>();
            int framesSinceBoundary = 0;

            for (int i = 1; i < frames.Count; i++)
            {
                double gap = frames[i].TimeMs - frames[i - 1].TimeMs;
                bool targetNear = targetCanIds.Count == 0
                    || targetCanIds.Contains(frames[i - 1].Id)
                    || targetCanIds.Contains(frames[i].Id);

                if (gap >= interBlockThreshold && framesSinceBoundary >= 3 && targetNear)
                {
                    boundaryTimes.Add(frames[i].TimeMs);
                    boundaryMessageIndices.Add(frames[i].MessageIndex);
                }

                if (gap >= interBlockThreshold)
                    framesSinceBoundary = 0;
                else
                    framesSinceBoundary++;
            }

            if (boundaryTimes.Count < MinValidWindows)
                return (anchorTimeMs, anchorMessageIndex, false);

            var origins = new List<double>();
            foreach (double boundaryTime in boundaryTimes)
            {
                double slots = Math.Round((boundaryTime - anchorTimeMs) / periodMs);
                origins.Add(boundaryTime - slots * periodMs);
            }

            return (Median(origins), boundaryMessageIndices[0], true);
        }

        private static double ComputeGapThreshold(IReadOnlyList<(int MessageIndex, double TimeMs, string Id)> frames)
        {
            if (frames.Count < 2)
                return 2.5;

            var gaps = new List<double>();
            for (int i = 1; i < frames.Count; i++)
                gaps.Add(frames[i].TimeMs - frames[i - 1].TimeMs);

            gaps.Sort();
            double p90 = Percentile(gaps, 0.90);
            double p95 = Percentile(gaps, 0.95);
            return Math.Max(1.0, (p90 + p95) / 2.0);
        }

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double index = (sorted.Count - 1) * p;
            int lo = (int)Math.Floor(index);
            int hi = (int)Math.Ceiling(index);
            if (lo == hi) return sorted[lo];
            double weight = index - lo;
            return sorted[lo] * (1 - weight) + sorted[hi] * weight;
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }
}
