namespace logReader.Tests;

public class DstConnectBlockStateTests
{
    private const int P = 0;

    private static TrcBlockDetectionResult ReferenceDetection(
        double periodMs,
        string referenceId = "0CFF001",
        double originTimeMs = 0,
        int firstBlockMessageIndex = 1)
        => new()
        {
            BlockPeriodMs = periodMs,
            AnchorMessageIndex = 1,
            AnchorTimeMs = originTimeMs,
            BlockOriginTimeMs = originTimeMs,
            FirstBlockMessageIndex = firstBlockMessageIndex,
            ReferenceCanId = referenceId,
            BlockCoverage = 0.85,
            UsedGapFallback = false,
            JitterToleranceMs = 3,
            Confidence = 0.85
        };

    private static TrcBlockDetectionResult GapFallback(
        double thresholdMs,
        int firstBlockMessageIndex = 1,
        double originTimeMs = 0,
        double periodMs = 20,
        double jitterMs = 3)
        => new()
        {
            AnchorMessageIndex = 1,
            FirstBlockMessageIndex = firstBlockMessageIndex,
            BlockOriginTimeMs = originTimeMs,
            UsedGapFallback = true,
            GapThresholdMs = thresholdMs,
            BlockPeriodMs = periodMs,
            JitterToleranceMs = jitterMs
        };

    [Fact]
    public void Incomplete_block_not_in_snapshot()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20), columnCount: 1);

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter(P, 1);
        tracker.Finish(1);

        Assert.Single(tracker.Rows);
        Assert.Equal(1, tracker.Rows[0].Values[P]);
    }

    [Fact]
    public void Tracker_one_row_per_block()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20), columnCount: 1);

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter(P, 1);
        tracker.OnFrameStart(2, 5, "0CFF002");
        tracker.OnFrameStart(3, 20, "0CFF001");
        tracker.UpdateParameter(P, 2);
        tracker.OnFrameStart(4, 25, "0CFF002");
        tracker.Finish(25);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal(1, tracker.Rows[0].Values[P]);
        Assert.Equal(2, tracker.Rows[1].Values[P]);
    }

    [Fact]
    public void Hold_value_across_blocks()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20), columnCount: 1);

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter(P, 1);
        tracker.OnFrameStart(2, 20, "0CFF001");
        tracker.Finish(25);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal(1, tracker.Rows[0].Values[P]);
        Assert.Equal(1, tracker.Rows[1].Values[P]);
    }

    [Fact]
    public void No_rows_before_first_data()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20), columnCount: 1);

        tracker.OnFrameStart(1, 363914.7, "0CFF002");
        tracker.OnFrameStart(2, 363915.0, "0CFF003");
        Assert.Empty(tracker.Rows);
    }

    [Fact]
    public void Gap_fallback_splits_on_period_grid_not_on_inter_frame_pause()
    {
        // Раньше UsedGapFallback резал блок по паузе между кадрами → «случайный» Step.
        // Сетка 20 мс: кадры 0…15 — один блок, 20… — следующий; пауза 9 мс внутри слота не режет.
        var tracker = new DstConnectBlockTracker(
            new DstConnectOptions { BlockPeriodMs = 20 },
            GapFallback(thresholdMs: 5),
            columnCount: 1);

        tracker.OnFrameStart(1, 0, "A");
        tracker.UpdateParameter(P, 1);
        tracker.OnFrameStart(2, 0.3, "B");
        tracker.OnFrameStart(3, 0.6, "C");
        tracker.OnFrameStart(4, 10, "D"); // пауза > GapThreshold, но тот же слот 0
        tracker.UpdateParameter(P, 2);
        tracker.OnFrameStart(5, 20.0, "A");
        tracker.UpdateParameter(P, 3);
        tracker.Finish(25);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal(20, tracker.Rows[0].StepMs, precision: 3);
        Assert.Equal(40, tracker.Rows[1].StepMs, precision: 3);
        Assert.Equal(2, tracker.Rows[0].Values[P]);
        Assert.Equal(3, tracker.Rows[1].Values[P]);
    }

    [Fact]
    public void Gap_fallback_step_deltas_match_block_period()
    {
        var tracker = new DstConnectBlockTracker(
            new DstConnectOptions { BlockPeriodMs = 20 },
            GapFallback(5, originTimeMs: 0.4),
            columnCount: 1);

        for (int block = 0; block < 5; block++)
        {
            double t0 = 0.4 + block * 20;
            tracker.OnFrameStart(block * 3 + 1, t0, "A");
            tracker.UpdateParameter(P, block);
            tracker.OnFrameStart(block * 3 + 2, t0 + 6, "B");
            tracker.OnFrameStart(block * 3 + 3, t0 + 14, "C");
        }
        tracker.Finish(0.4 + 5 * 20);

        Assert.Equal(5, tracker.Rows.Count);
        for (int i = 1; i < tracker.Rows.Count; i++)
            Assert.Equal(20, tracker.Rows[i].StepMs - tracker.Rows[i - 1].StepMs, precision: 3);
    }

    [Fact]
    public void Mid_block_leading_frames_not_counted_as_block()
    {
        const double period = 20;
        const double origin = 1000;
        const double recordStart = origin + 7;
        var detection = new TrcBlockDetectionResult
        {
            BlockPeriodMs = period,
            AnchorMessageIndex = 1,
            AnchorTimeMs = recordStart,
            BlockOriginTimeMs = origin,
            FirstBlockMessageIndex = 16,
            ReferenceCanId = "0CFF001",
            BlockCoverage = 0.8,
            UsedGapFallback = false,
            JitterToleranceMs = 3,
            Confidence = 0.8
        };

        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), detection, columnCount: 1);

        for (int i = 1; i < 16; i++)
        {
            tracker.OnFrameStart(i, recordStart + (i - 1) * 0.3, "0CFF002");
            tracker.UpdateParameter(P, 9);
        }

        Assert.Empty(tracker.Rows);

        tracker.OnFrameStart(16, origin + period, "0CFF001");
        tracker.UpdateParameter(P, 10);
        tracker.Finish(origin + period);

        Assert.Single(tracker.Rows);
        Assert.Equal(10, tracker.Rows[0].Values[P]);
    }

    [Fact]
    public void Block_complete_uses_last_frame_time_not_grid()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20), columnCount: 1);

        tracker.OnFrameStart(1, 3.1, "0CFF001");
        tracker.UpdateParameter(P, 1);
        tracker.OnFrameStart(2, 5.7, "0CFF002");
        tracker.UpdateParameter(P, 2);
        tracker.OnFrameStart(3, 22.4, "0CFF001");
        tracker.Finish(22.4);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal(5.7, tracker.Rows[0].StepMs, precision: 3);
        Assert.Equal(22.4, tracker.Rows[1].StepMs, precision: 3);
    }
}
