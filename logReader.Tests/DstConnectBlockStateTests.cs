namespace logReader.Tests;

public class DstConnectBlockStateTests
{
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

    private static TrcBlockDetectionResult GapFallback(double thresholdMs, int firstBlockMessageIndex = 1)
        => new()
        {
            AnchorMessageIndex = 1,
            FirstBlockMessageIndex = firstBlockMessageIndex,
            BlockOriginTimeMs = 0,
            UsedGapFallback = true,
            GapThresholdMs = thresholdMs,
            BlockPeriodMs = 20
        };

    [Fact]
    public void Incomplete_block_not_in_snapshot()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20));

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter("p", "first");
        tracker.Finish(1);

        Assert.Single(tracker.Rows);
        Assert.Equal("first", tracker.Rows[0].Values["p"]);
    }

    [Fact]
    public void Tracker_one_row_per_block()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20));

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter("p", "v1");
        tracker.OnFrameStart(2, 5, "0CFF002");
        tracker.OnFrameStart(3, 20, "0CFF001");
        tracker.UpdateParameter("p", "v2");
        tracker.OnFrameStart(4, 25, "0CFF002");
        tracker.Finish(25);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal("v1", tracker.Rows[0].Values["p"]);
        Assert.Equal("v2", tracker.Rows[1].Values["p"]);
    }

    [Fact]
    public void Hold_value_across_blocks()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20));

        tracker.OnFrameStart(1, 0, "0CFF001");
        tracker.UpdateParameter("p", "v1");
        tracker.OnFrameStart(2, 20, "0CFF001");
        tracker.Finish(25);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal("v1", tracker.Rows[0].Values["p"]);
        Assert.Equal("v1", tracker.Rows[1].Values["p"]);
    }

    [Fact]
    public void No_rows_before_first_data()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20));

        tracker.OnFrameStart(1, 363914.7, "0CFF002");
        tracker.OnFrameStart(2, 363915.0, "0CFF003");
        Assert.Empty(tracker.Rows);
    }

    [Fact]
    public void Gap_fallback_splits_on_long_pause()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), GapFallback(5));

        tracker.OnFrameStart(1, 0, "A");
        tracker.UpdateParameter("p", "a");
        tracker.OnFrameStart(2, 0.3, "B");
        tracker.OnFrameStart(3, 0.6, "C");
        tracker.OnFrameStart(4, 10, "D");
        tracker.UpdateParameter("p", "b");
        tracker.Finish(10);

        Assert.Equal(2, tracker.Rows.Count);
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

        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), detection);

        for (int i = 1; i < 16; i++)
        {
            tracker.OnFrameStart(i, recordStart + (i - 1) * 0.3, "0CFF002");
            tracker.UpdateParameter("p", "partial");
        }

        Assert.Empty(tracker.Rows);

        tracker.OnFrameStart(16, origin + period, "0CFF001");
        tracker.UpdateParameter("p", "full");
        tracker.Finish(origin + period);

        Assert.Single(tracker.Rows);
        Assert.Equal("full", tracker.Rows[0].Values["p"]);
    }

    [Fact]
    public void Block_complete_uses_last_frame_time_not_grid()
    {
        var tracker = new DstConnectBlockTracker(new DstConnectOptions(), ReferenceDetection(20));

        tracker.OnFrameStart(1, 3.1, "0CFF001");
        tracker.UpdateParameter("p", "v1");
        tracker.OnFrameStart(2, 5.7, "0CFF002");
        tracker.UpdateParameter("p", "v2");
        tracker.OnFrameStart(3, 22.4, "0CFF001");
        tracker.Finish(22.4);

        Assert.Equal(2, tracker.Rows.Count);
        Assert.Equal(5.7, tracker.Rows[0].StepMs, precision: 3);
        Assert.Equal(22.4, tracker.Rows[1].StepMs, precision: 3);
    }
}
