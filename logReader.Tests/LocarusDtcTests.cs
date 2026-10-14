using System.Globalization;
using logReader.Processing;

namespace logReader.Tests;

public sealed class LocarusDtcTests
{
    private static readonly DtcCode[] Six = [new(32770, 0), new(524287, 4), new(1, 19), new(332340, 0), new(0, 31), new(249259, 0)];

    [Fact]
    public void Six_slots_decode_in_protocol_order_including_both_cross_frame_boundaries()
    {
        var decoder = new LocarusDtcDecoder();
        var frames = Frames(Six, 6);
        Assert.False(decoder.OnFrame(frames[0].Id, frames[0].Bytes, 0)!.IsComplete);
        var firstThree = decoder.OnFrame(frames[1].Id, frames[1].Bytes, 1)!;
        Assert.Equal(Six.Take(3), firstThree.Slots.Take(3).Select(s => s.Code!.Value));
        Assert.All(firstThree.Slots.Skip(3), s => Assert.False(s.IsKnown));
        var complete = decoder.OnFrame(frames[2].Id, frames[2].Bytes, 2)!;
        Assert.True(complete.IsComplete);
        Assert.Equal(6, complete.ReportedCount);
        Assert.Equal(Six, complete.Slots.Select(s => s.Code!.Value));
        Assert.Equal("SPN 32770 / FMI 0", complete.Slots[0].Text);
    }

    [Fact]
    public void Zero_count_requires_fresh_bytes_for_all_six_slots_before_confirming_empty_list()
    {
        var frames = Frames([], 0);
        var decoder = new LocarusDtcDecoder();
        var snapshot = decoder.OnFrame(frames[0].Id, frames[0].Bytes, 0)!;
        Assert.False(snapshot.IsComplete);
        Assert.All(snapshot.Slots, s => Assert.False(s.IsKnown));
        snapshot = decoder.OnFrame(frames[1].Id, frames[1].Bytes, 1)!;
        Assert.False(snapshot.IsComplete);
        Assert.All(snapshot.Slots.Take(3), s => Assert.Equal(DtcSlot.Empty, s));
        Assert.All(snapshot.Slots.Skip(3), s => Assert.False(s.IsKnown));
        snapshot = decoder.OnFrame(frames[2].Id, frames[2].Bytes, 2)!;
        Assert.True(snapshot.IsComplete);
        Assert.All(snapshot.Slots, s => { Assert.True(s.IsKnown); Assert.Null(s.Code); Assert.Equal("", s.Text); });
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Count_is_quality_metadata_and_cannot_hide_nonzero_codes_outside_its_prefix(int count, bool complete)
    {
        DtcCode[] codes = [new(0, 0), new(0, 0), new(0, 0), Six[0], new(0, 0), new(0, 0)];
        var decoder = new LocarusDtcDecoder();
        Feed(decoder, Frames(codes, count), 0);
        var snapshot = decoder.SnapshotAt(2);
        Assert.Equal(complete, snapshot.IsComplete);
        Assert.Equal(Six[0], snapshot.Slots[3].Code);
        Assert.Equal(count, snapshot.ReportedCount);
        Assert.All(snapshot.Slots.Where((_, index) => index != 3), s => Assert.Equal(DtcSlot.Empty, s));
    }

    [Fact]
    public void Two_parts_confirm_first_code_but_do_not_confirm_completeness_of_all_six_slots()
    {
        var frames = Frames([Six[0]], 1);
        var decoder = new LocarusDtcDecoder();
        decoder.OnFrame(frames[0].Id, frames[0].Bytes, 0);
        var partial = decoder.OnFrame(frames[1].Id, frames[1].Bytes, 1)!;
        Assert.False(partial.IsComplete);
        Assert.Equal(Six[0], partial.Slots[0].Code);
        Assert.All(partial.Slots.Skip(3), s => Assert.False(s.IsKnown));
        Assert.True(decoder.OnFrame(frames[2].Id, frames[2].Bytes, 2)!.IsComplete);
    }

    [Fact]
    public void New_header_and_logical_boundary_prevent_fragment_reuse()
    {
        var frames = Frames(Six, 6);
        var decoder = new LocarusDtcDecoder();
        Feed(decoder, frames, 0);
        decoder.OnFrame(frames[0].Id, frames[0].Bytes, 100);
        var partial = decoder.OnFrame(frames[2].Id, frames[2].Bytes, 101)!;
        Assert.False(partial.IsComplete);
        Assert.False(partial.Slots[0].IsKnown);
        decoder.ResetCycle();
        Assert.Null(decoder.OnFrame(frames[1].Id, frames[1].Bytes, 102));
        Assert.False(decoder.SnapshotAt(102).Slots[0].IsKnown);
    }

    [Fact]
    public void Duplicate_continuation_after_completed_cycle_is_ignored()
    {
        var decoder = new LocarusDtcDecoder();
        Feed(decoder, Frames(Six.Take(3).ToArray(), 3), 0);
        var different = Frames([new DtcCode(111, 31)], 1);
        Assert.Null(decoder.OnFrame(different[1].Id, different[1].Bytes, 100));
        Assert.Equal(Six[0], decoder.SnapshotAt(100).Slots[0].Code);
    }

    [Fact]
    public void Timeout_and_backwards_time_invalidate_previous_state()
    {
        var decoder = new LocarusDtcDecoder(2000);
        Feed(decoder, Frames([Six[0]], 1), 0);
        Assert.NotNull(decoder.SnapshotAt(2002).Slots[0].Code);
        Assert.All(decoder.SnapshotAt(2003).Slots, s => Assert.False(s.IsKnown));
        Feed(decoder, Frames([Six[0]], 1), 4000);
        Assert.All(decoder.SnapshotAt(3999).Slots, s => Assert.False(s.IsKnown));
    }

    [Fact]
    public void Timeout_also_applies_to_pending_cross_frame_cycle()
    {
        var frames = Frames(Six, 6);
        var decoder = new LocarusDtcDecoder(100);
        decoder.OnFrame(frames[0].Id, frames[0].Bytes, 0);
        decoder.OnFrame(frames[1].Id, frames[1].Bytes, 80);
        Assert.Null(decoder.OnFrame(frames[2].Id, frames[2].Bytes, 150));
        Assert.False(decoder.SnapshotAt(150).IsComplete);
    }

    [Fact]
    public void Logical_empty_row_retains_confirmed_state_only_until_timeout()
    {
        var decoder = new LocarusDtcDecoder(100);
        Feed(decoder, Frames([Six[0]], 1), 0);
        decoder.ResetCycle();
        Assert.Equal(Six[0], decoder.SnapshotAt(50).Slots[0].Code);
        decoder.ResetCycle();
        Assert.All(decoder.SnapshotAt(103).Slots, s => Assert.False(s.IsKnown));
    }

    [Fact]
    public void More_than_six_errors_and_invalid_fragments_cannot_confirm_absence()
    {
        var decoder = new LocarusDtcDecoder();
        var frames = Frames(Six, 8);
        Feed(decoder, frames, 0);
        var snapshot = decoder.SnapshotAt(3);
        Assert.False(snapshot.IsComplete);
        Assert.Equal(8, snapshot.ReportedCount);
        Assert.All(snapshot.Slots, s => Assert.NotNull(s.Code));
        decoder.OnFrame(frames[0].Id, frames[0].Bytes, 100);
        decoder.OnFrame(frames[1].Id, frames[1].Bytes, 101);
        snapshot = decoder.OnFrame(frames[2].Id, [0, 0, 0], 102)!;
        Assert.False(snapshot.IsComplete);
        Assert.All(snapshot.Slots.Skip(3), s => Assert.False(s.IsKnown));
        Assert.Equal(Six[0], snapshot.Slots[0].Code);
    }

    [Fact]
    public void Vendor_codes_are_integer_pairs_and_preserve_list_order()
    {
        var snapshot = DtcAnalysisService.ParseVendorSnapshot(0, "3", "523106.0,32770.04|523055.19|");
        Assert.True(snapshot.IsComplete);
        Assert.Equal(new DtcCode(523106, 0), snapshot.Slots[0].Code);
        Assert.Equal(new DtcCode(32770, 4), snapshot.Slots[1].Code);
        Assert.Equal(new DtcCode(523055, 19), snapshot.Slots[2].Code);
        Assert.All(snapshot.Slots.Skip(3), s => Assert.Equal(DtcSlot.Empty, s));
        Assert.True(DtcAnalysisService.ParseVendorSnapshot(0, "0", "-").IsComplete);
    }

    [Theory]
    [InlineData("2", "32770.0")]
    [InlineData("1", "32770.0|524288.4")]
    [InlineData("1", "32770.32")]
    [InlineData("0", "32770.0")]
    [InlineData("8", "1.0|2.0|3.0|4.0|5.0|6.0")]
    public void Inconsistent_vendor_count_or_code_stays_incomplete(string count, string errors)
    {
        var snapshot = DtcAnalysisService.ParseVendorSnapshot(0, count, errors);
        Assert.False(snapshot.IsComplete);
        Assert.Contains(snapshot.Slots, s => !s.IsKnown || s.Code != null);
    }

    [Fact]
    public void Moving_slots_is_one_episode_but_confirmed_absence_and_gaps_split_it()
    {
        using var dir = new TempDir();
        string path = dir.Write("locarus.csv", "time;ErrorsQuantity;Err\n" +
            "2026-07-06T06:19:49Z;2;1.0|2.4\n" +
            "2026-07-06T06:19:50Z;2;2.4|1.0\n" +
            "2026-07-06T06:19:51Z;0;-\n" +
            "2026-07-06T06:19:52Z;1;1.0\n" +
            "2026-07-06T06:20:00Z;1;1.0\n");
        var result = DtcAnalysisService.Analyze(path);
        Assert.Equal(5, result.Observations.Count);
        var one = result.Summaries.Single(s => s.Code == new DtcCode(1, 0));
        Assert.Equal(3, one.IntervalCount);
        Assert.Equal("Присутствует в последнем снимке", one.State);
        var first = result.Episodes.First(e => e.Code == one.Code);
        Assert.Equal("2026-07-06T06:19:51Z", first.FirstAbsent);
        Assert.Equal("2026-07-06T06:19:50Z", first.LastSeen);
    }

    [Fact]
    public void Partial_lists_do_not_create_false_disappearances_and_fixed_slots_cover_many_codes()
    {
        using var dir = new TempDir();
        string path = dir.Write("locarus.csv", "time;ErrorsQuantity;Err\n" +
            "2026-07-06T06:19:49Z;1;99.0\n" +
            "2026-07-06T06:19:50Z;8;1.0|2.4|3.0|4.0|5.0|6.0\n" +
            "2026-07-06T06:19:51Z;1;99.0\n");
        var result = DtcAnalysisService.Analyze(path);
        Assert.Equal(7, result.Summaries.Count);
        Assert.All(result.Observations, o => Assert.Equal(6, o.Snapshot.Slots.Count));
        Assert.Equal(1, result.Summaries.Single(s => s.Code == new DtcCode(99, 0)).IntervalCount);
        Assert.False(result.Observations[1].Snapshot.IsComplete);
    }

    [Fact]
    public void Repeated_incomplete_snapshots_do_not_extend_unconfirmed_code_indefinitely()
    {
        using var dir = new TempDir();
        string path = dir.Write("locarus.csv", "time;ErrorsQuantity;Err\n" +
            "2026-07-06T06:19:49Z;1;99.0\n" +
            "2026-07-06T06:19:50Z;8;1.0\n" +
            "2026-07-06T06:19:51Z;8;1.0\n" +
            "2026-07-06T06:19:52Z;8;1.0\n" +
            "2026-07-06T06:19:53Z;1;99.0\n");
        var result = DtcAnalysisService.Analyze(path);
        Assert.Equal(2, result.Summaries.Single(s => s.Code == new DtcCode(99, 0)).IntervalCount);
        var episode = result.Episodes.First(e => e.Code == new DtcCode(99, 0));
        Assert.Null(episode.FirstAbsent);
        Assert.Equal("Пропуск диагностических данных", episode.EndReason);
    }

    [Theory]
    [InlineData("trc")]
    [InlineData("asc")]
    [InlineData("txt")]
    [InlineData("matrix.csv")]
    [InlineData("step.csv")]
    public void Analysis_reads_each_existing_can_format(string format)
    {
        using var dir = new TempDir();
        var frames = Frames([Six[0]], 1);
        string Hex(int[] data) => string.Join(" ", data.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        string content = format switch
        {
            "trc" => string.Join('\n', frames.Select((f, i) => $"{i + 1}) {i} Rx {f.Id} 8 {Hex(f.Bytes)}")),
            "asc" => "base hex timestamps absolute\n" + string.Join('\n', frames.Select((f, i) => $"0.00{i} 1 {f.Id}x Rx d 8 {Hex(f.Bytes)}")),
            "txt" => string.Join('\n', frames.Select((f, i) => $"2026.07.06 06:19:49.00{i} - {f.Id} 8 {Hex(f.Bytes)}")),
            "matrix.csv" => $";18FE2603;18FE2503;18FE2703\n06:19:49;{Hex(frames[1].Bytes).Replace(" ", "")};{Hex(frames[0].Bytes).Replace(" ", "")};{Hex(frames[2].Bytes).Replace(" ", "")}\n",
            _ => $"1;06:19:49;18FE2603;1;{string.Join(';', frames[1].Bytes)}\n;06:19:49;18FE2503;1;{string.Join(';', frames[0].Bytes)}\n;06:19:49;18FE2703;1;{string.Join(';', frames[2].Bytes)}\n"
        };
        string path = dir.Write("input." + format, content);
        var result = DtcAnalysisService.Analyze(path);
        Assert.Contains(result.Observations, o => o.Snapshot.IsComplete && o.Snapshot.Slots[0].Code == Six[0]);
        Assert.Equal(Six[0], Assert.Single(result.Summaries).Code);
    }

    [Fact]
    public void Nonempty_corrupt_matrix_anchor_invalidates_preceding_completed_snapshot()
    {
        using var dir = new TempDir();
        var frames = Frames([Six[0]], 1);
        string Hex(int[] data) => string.Concat(data.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        string path = dir.Write("matrix.csv", ";18FE2503;18FE2603;18FE2703\n" +
            $"06:19:49;{Hex(frames[0].Bytes)};{Hex(frames[1].Bytes)};{Hex(frames[2].Bytes)}\n" +
            $"06:19:50;not-hex;{Hex(frames[1].Bytes)};{Hex(frames[2].Bytes)}\n");
        var result = DtcAnalysisService.Analyze(path);
        Assert.True(result.Observations[0].Snapshot.IsComplete);
        Assert.All(result.Observations[1].Snapshot.Slots, s => Assert.False(s.IsKnown));
        Assert.Equal("Недостаточно данных", Assert.Single(result.Summaries).State);
        Assert.Null(Assert.Single(result.Episodes).FirstAbsent);
    }

    [Theory]
    [InlineData("trc")]
    [InlineData("asc")]
    [InlineData("canfd.asc")]
    [InlineData("txt")]
    public void Corrupt_raw_cycle_anchor_prevents_cross_cycle_codes_and_next_cycle_recovers(string format)
    {
        using var dir = new TempDir();
        var oldFrames = Frames(Six, 6);
        DtcCode[] nextCodes = Enumerable.Range(100, 6).Select(spn => new DtcCode(spn, 0)).ToArray();
        var nextFrames = Frames(nextCodes, 6);
        var input = new[]
        {
            (Frame: oldFrames[0], Ms: 0, Broken: false),
            (Frame: oldFrames[1], Ms: 1, Broken: false),
            (Frame: nextFrames[0], Ms: 100, Broken: true),
            (Frame: nextFrames[1], Ms: 101, Broken: false),
            (Frame: nextFrames[2], Ms: 102, Broken: false),
            (Frame: nextFrames[0], Ms: 200, Broken: false),
            (Frame: nextFrames[1], Ms: 201, Broken: false),
            (Frame: nextFrames[2], Ms: 202, Broken: false)
        };
        string[] lines = input.Select((item, index) =>
        {
            string[] data = item.Frame.Bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)).ToArray();
            if (item.Broken) data[0] = "GG";
            string hex = string.Join(' ', data);
            string offset = (item.Ms / 1000d).ToString("0.000", CultureInfo.InvariantCulture);
            return format switch
            {
                "trc" => $"{index + 1}) {item.Ms} Rx {item.Frame.Id} 8 {hex}",
                "asc" => $"{offset} 1 {item.Frame.Id}x Rx d 8 {hex}",
                "canfd.asc" => $"{offset} CANFD 1 Rx {item.Frame.Id}x 0 0 8 8 {hex}",
                _ => $"2026.07.06 00:00:00.{item.Ms:000} - {item.Frame.Id} 8 {hex}"
            };
        }).ToArray();
        string path = dir.Write("input." + format,
            (format.EndsWith("asc", StringComparison.Ordinal) ? "base hex timestamps absolute\n" : "") + string.Join('\n', lines));
        var result = DtcAnalysisService.Analyze(path);
        Assert.Equal(8, result.Observations.Count);
        Assert.Contains("GG", result.Observations[2].Snapshot.RawData);
        Assert.All(result.Observations.Skip(2).Take(3), observation =>
        {
            Assert.Null(observation.Snapshot.ReportedCount);
            Assert.All(observation.Snapshot.Slots, slot => Assert.False(slot.IsKnown));
        });
        Assert.All(result.Observations.Take(7), o => Assert.False(o.Snapshot.IsComplete));
        Assert.True(result.Observations[^1].Snapshot.IsComplete);
        Assert.Equal(nextCodes, result.Observations[^1].Snapshot.Slots.Select(s => s.Code!.Value));
    }

    [Theory]
    [InlineData("1) 0 Rx 18FE2503 8 RTR")]
    [InlineData("1 0 RR 18FE2503 Rx 8")]
    [InlineData("1) invalid Rx 18FE2503 8 GG")]
    [InlineData("1) 0 Error 18FE2503 8 GG")]
    [InlineData("1) 0 Rx 18FE2403 8 GG")]
    public void Trc_corruption_fallback_ignores_remote_status_and_unrelated_frames(string line)
        => Assert.False(DtcFrameBoundary.TryTrc(line, out _, out _, out _));

    [Theory]
    [InlineData("0.000 1 18FE2503x Rx r 8")]
    [InlineData("invalid 1 18FE2503x Rx d 8 GG")]
    [InlineData("0.000 1 18FE2503x ErrorFrame d 8 GG")]
    [InlineData("0.000 1 18FE2403x Rx d 8 GG")]
    public void Asc_corruption_fallback_ignores_remote_status_and_unrelated_frames(string line)
        => Assert.False(DtcFrameBoundary.TryAsc(line, AscFileFormat.Default, out _, out _));

    [Fact]
    public void Canfox_corruption_fallback_ignores_remote_frames()
        => Assert.False(DtcFrameBoundary.TryCanfox("2026.07.06 00:00:00.100 - 18FE2503 8 RTR", out _, out _, out _));

    [Theory]
    [InlineData("2;06:19:50;18FE2503;1;0\n")]
    [InlineData("2;not-a-time;18FE2503;1;0;0;0;0;0;0;0;0\n")]
    public void Short_anchor_or_invalid_clock_in_legacy_step_cannot_retain_previous_known_state(string broken)
    {
        using var dir = new TempDir();
        var frames = Frames([Six[0]], 1);
        string path = dir.Write("legacy.csv", $"1;06:19:49;18FE2503;1;{string.Join(';', frames[0].Bytes)}\n" +
            $";06:19:49;18FE2603;1;{string.Join(';', frames[1].Bytes)}\n" +
            $";06:19:49;18FE2703;1;{string.Join(';', frames[2].Bytes)}\n" + broken);
        var result = DtcAnalysisService.Analyze(path);
        Assert.True(result.Observations[0].Snapshot.IsComplete);
        Assert.All(result.Observations[1].Snapshot.Slots, s => Assert.False(s.IsKnown));
        Assert.Equal("Недостаточно данных", Assert.Single(result.Summaries).State);
    }

    [Fact]
    public void Folder_analysis_resets_state_and_honors_cancellation()
    {
        using var dir = new TempDir();
        dir.Write("a.csv", "time;ErrorsQuantity;Err\n2026-07-06T06:19:49Z;1;32770.0\n");
        dir.Write("b.csv", "time;ErrorsQuantity;Err\n2026-07-06T06:19:49Z;0;-\n");
        var result = DtcAnalysisService.Analyze(dir.Path);
        Assert.Single(result.Summaries);
        Assert.Equal("Присутствует в последнем снимке", result.Summaries[0].State);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DtcAnalysisService.Analyze(dir.Path, context: new ProcessingContext(cancellationToken: cancellation.Token)));
    }

    [Fact]
    public void Provided_dst_traces_have_1038_empty_trace_cycles_and_15_fmi_zero_counterrotation_cycles()
    {
        string? root = Environment.GetEnvironmentVariable("LOGER_DTC_TEST_DATA_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;
        Assert.True(Directory.Exists(root), "LOGER_DTC_TEST_DATA_DIR должен указывать непосредственно на папку DST.");
        Assert.Equal(9, Directory.GetFiles(root, "*.trc").Length);
        var counterrotation = DtcAnalysisService.Analyze(Path.Combine(root, "контрвращение_085008.trc"));
        Assert.Equal(new DtcCode(32770, 0), Assert.Single(counterrotation.Summaries).Code);
        Assert.Equal(15, counterrotation.Observations.Count(o => o.Snapshot.IsComplete && o.Snapshot.Slots[0].Code == new DtcCode(32770, 0)));
        Assert.Equal(118, counterrotation.Observations.Count(o => o.Snapshot.IsComplete));
        var trace = DtcAnalysisService.Analyze(Path.Combine(root, "trace_2025-10-16T06_45_04_085033.trc"));
        Assert.Empty(trace.Summaries);
        Assert.Equal(1038, trace.Observations.Count(o => o.Snapshot.IsComplete));
        Assert.All(trace.Observations, o => Assert.All(o.Snapshot.Slots, s => Assert.Null(s.Code)));
        // Independently counted directly from the raw FE25/26/27 bytes. The
        // driving trace has missing/orphan fragments: only 608 of 614 cycles
        // have all three parts, so those incomplete rows must stay incomplete.
        (string File, int CompleteCycles)[] emptyTraces =
        [
            ("мтс15е-1_085623.trc", 588),
            ("мтс15е-горка-вверх_085611.trc", 159),
            ("мтс15е-горка-спуск-торможение_085600.trc", 120),
            ("мтс15е-езда_085258.trc", 608),
            ("мтс15е-подъемвгорку_085433.trc", 761),
            ("мтс15е-спуск-рекуперация_085413.trc", 183),
            ("торможение_085545.trc", 149)
        ];
        foreach (var expected in emptyTraces)
        {
            var result = DtcAnalysisService.Analyze(Path.Combine(root, expected.File));
            Assert.Empty(result.Summaries);
            Assert.Equal(expected.CompleteCycles, result.Observations.Count(o => o.Snapshot.IsComplete));
            Assert.All(result.Observations, o => Assert.Equal(6, o.Snapshot.Slots.Count));
        }
    }

    private static void Feed(LocarusDtcDecoder decoder, (string Id, int[] Bytes)[] frames, double start)
    {
        for (int i = 0; i < frames.Length; i++) decoder.OnFrame(frames[i].Id, frames[i].Bytes, start + i);
    }

    internal static (string Id, int[] Bytes)[] Frames(DtcCode[] codes, int count)
    {
        int[] packed = new int[18];
        for (int i = 0; i < codes.Length; i++)
        {
            packed[3 * i] = codes[i].Spn & 255;
            packed[3 * i + 1] = (codes[i].Spn >> 8) & 255;
            packed[3 * i + 2] = ((codes[i].Spn >> 16) << 5) | codes[i].Fmi;
        }
        int[] first = new int[8];
        first[3] = count;
        first[6] = packed[0];
        first[7] = packed[1];
        return [("18FE2503", first), ("18FE2603", packed[2..10]), ("18FE2703", packed[10..18])];
    }
}
