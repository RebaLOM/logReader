namespace logReader.Tests;

public class TrcBlockDetectorTests
{
    private static readonly HashSet<string> SyntheticTargetIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "0CFF001", "0CFF002", "0CFF003", "0CFF004", "0CFF005",
        "0CFF006", "0CFF007", "0CFF008", "0CFF009", "0CFF010",
        "0CFF011", "0CFF012"
    };

    [Fact]
    public void Detect_validates_block_by_reference_recurrence()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 80);
        var result = TrcBlockDetector.Detect(frames, 0, 20, SyntheticTargetIds);

        Assert.Equal(20, result.BlockPeriodMs);
        Assert.False(result.UsedGapFallback);
        Assert.Equal("0CFF001", result.ReferenceCanId);
        Assert.True(result.BlockCoverage >= DstConnectOptions.MinBlockCoverage);
        Assert.Equal(frames[0].TimeMs, result.BlockOriginTimeMs, precision: 1);
    }

    [Fact]
    public void Detect_uses_user_period_not_auto()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 80);
        var result = TrcBlockDetector.Detect(frames, 0, userBlockPeriodMs: 20, SyntheticTargetIds);

        Assert.Equal(20, result.BlockPeriodMs);
    }

    [Fact]
    public void Detect_skips_leading_partial_block()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 80, recordStartOffsetMs: 3);
        var result = TrcBlockDetector.Detect(frames, 0, 20, SyntheticTargetIds);

        Assert.False(result.UsedGapFallback);
        Assert.True(result.FirstBlockMessageIndex > 1);
        Assert.Equal("0CFF001", result.ReferenceCanId);
    }

    [Fact]
    public void Detect_tolerates_missing_ids()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 80, dropEveryNthId: 4);
        var result = TrcBlockDetector.Detect(frames, 0, 20, SyntheticTargetIds);

        Assert.False(result.UsedGapFallback);
        Assert.True(result.BlockCoverage >= DstConnectOptions.MinBlockCoverage);
    }

    [Fact]
    public void Detect_respects_anchor_message_number()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 80);
        var result = TrcBlockDetector.Detect(frames, userAnchorMessageIndex: 25, 20, SyntheticTargetIds);

        Assert.Equal(25, result.AnchorMessageIndex);
        Assert.Equal(frames[24].Id, result.AnchorCanId);
        Assert.Equal(25, result.FirstBlockMessageIndex);
    }

    [Fact]
    public void IsBlockStart_reference_recurrence_detects_periodic_ref()
    {
        var detection = new TrcBlockDetectionResult
        {
            BlockPeriodMs = 20,
            FirstBlockMessageIndex = 1,
            ReferenceCanId = "0CFF001",
            UsedGapFallback = false,
            JitterToleranceMs = 3
        };

        int slot = 0;
        Assert.True(TrcBlockDetector.IsBlockStart(1, 100, "0CFF001", 99, 0, double.NaN, false, detection, ref slot, 3));
        Assert.False(TrcBlockDetector.IsBlockStart(2, 105, "0CFF002", 100, 5, 100, true, detection, ref slot, 3));
        Assert.True(TrcBlockDetector.IsBlockStart(3, 120, "0CFF001", 105, 10, 100, true, detection, ref slot, 3));
    }

    [Fact]
    public void ComputeDefaultJitter_scales_with_period()
    {
        Assert.Equal(3, TrcBlockDetector.ComputeDefaultJitter(20), precision: 3);
    }

    [Fact]
    public void Synthetic_grid_block_count_matches_period()
    {
        var frames = BuildTimeGrid(periodMs: 20, idsPerBlock: 12, blocks: 50);
        var detection = TrcBlockDetector.Detect(frames, 0, 20, SyntheticTargetIds);
        Assert.True(detection.UsesReferenceRecurrence);

        int blocks = CountBlocks(frames, detection);
        Assert.InRange(blocks, 45, 52);
    }

    [Fact]
    public void Real_trc_examples_with_user_period_20ms()
    {
        string examplesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "trc examples");

        if (!Directory.Exists(examplesDir))
            return;

        foreach (string file in Directory.EnumerateFiles(examplesDir, "*.trc"))
        {
            var frames = LoadTrcFrames(file, maxFrames: 5000);
            if (frames.Count < 200)
                continue;

            var targetIds = frames
                .Select(f => f.Id)
                .Where(id => id.StartsWith("0CFF", StringComparison.OrdinalIgnoreCase)
                             || id.StartsWith("1801", StringComparison.OrdinalIgnoreCase)
                             || id.StartsWith("1802", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = TrcBlockDetector.Detect(frames, 0, 20, targetIds);
            Assert.Equal(20, result.BlockPeriodMs);
        }
    }

    private static int CountBlocks(
        List<(int MessageIndex, double TimeMs, string Id)> frames,
        TrcBlockDetectionResult detection)
    {
        int blocks = 0;
        int slot = -1;
        double prev = double.NaN;
        int framesInBlock = 0;
        double lastStart = double.NaN;
        bool blockOpen = false;
        double jitter = detection.JitterToleranceMs > 0 ? detection.JitterToleranceMs : 3;

        foreach (var frame in frames)
        {
            if (TrcBlockDetector.IsBlockStart(
                    frame.MessageIndex,
                    frame.TimeMs,
                    frame.Id,
                    double.IsNaN(prev) ? frame.TimeMs : prev,
                    framesInBlock,
                    lastStart,
                    blockOpen,
                    detection,
                    ref slot,
                    jitter))
            {
                blocks++;
                framesInBlock = 0;
                blockOpen = true;
                lastStart = frame.TimeMs;
            }

            framesInBlock++;
            prev = frame.TimeMs;
        }

        return blocks;
    }

    private static List<(int MessageIndex, double TimeMs, string Id)> BuildTimeGrid(
        double periodMs,
        int idsPerBlock,
        int blocks,
        int noiseEvery = 0,
        double recordStartOffsetMs = 0,
        int dropEveryNthId = 0)
    {
        var list = new List<(int, double, string)>();
        int idx = 1;
        double t = 1000;
        double recordStart = t + recordStartOffsetMs;

        for (int b = 0; b < blocks; b++)
        {
            double blockStart = t + b * periodMs;
            for (int i = 0; i < idsPerBlock; i++)
            {
                double timeMs = blockStart + i * 0.3;
                if (timeMs < recordStart - 0.001)
                    continue;

                string id = $"0CFF{(i + 1):D3}";
                if (dropEveryNthId > 0 && (i + 1) % dropEveryNthId == 0)
                    continue;

                list.Add((idx++, timeMs, id));

                if (noiseEvery > 0 && idx % noiseEvery == 0)
                    list.Add((idx++, timeMs + 0.1, "1FEEFF00"));
            }
        }

        return list;
    }

    private static List<(int MessageIndex, double TimeMs, string Id)> LoadTrcFrames(string path, int maxFrames)
    {
        var frames = new List<(int, double, string)>();
        foreach (string line in File.ReadLines(path))
        {
            if (frames.Count >= maxFrames)
                break;

            if (!line.Contains(")"))
                continue;

            int paren = line.IndexOf(')');
            if (paren < 2)
                continue;

            if (!int.TryParse(line.AsSpan(0, paren), out int msgNum))
                continue;

            var parts = line[(paren + 1)..].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 4)
                continue;

            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double timeMs))
                continue;

            string id = parts[3].Trim();
            frames.Add((msgNum, timeMs, id));
        }

        return frames;
    }
}
