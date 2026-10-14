using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using logReader.Processing;

namespace logReader.Tests;

public class DstConnectProcessorTests
{
    private static string Dbc(int presentMessages, int absentMessages)
    {
        var sb = new StringBuilder();
        for (int m = 0; m < presentMessages + absentMessages; m++)
        {
            uint id = m < presentMessages ? 0x0CFF0000u + (uint)m : 0x18FF0000u + (uint)m;
            sb.Append(CultureInfo.InvariantCulture, $"BO_ {0x80000000u | id} M{m}: 8 X\n");
            sb.Append(CultureInfo.InvariantCulture, $" SG_ S{m} : 0|8@1+ (1,0) [0|255] \"\" X\n");
        }
        return sb.ToString();
    }

    private static string Trc(int ids, int blocks)
    {
        var sb = new StringBuilder(";   Start time: 26.04.2023 10:00:00.000.0\n");
        int n = 1;
        for (int b = 0; b < blocks; b++)
            for (int i = 0; i < ids; i++)
                sb.Append(CultureInfo.InvariantCulture,
                    $"{n++,6})  {1000 + b * 20 + i * 0.3,10:F1}  Rx     {0x0CFF0000 + i:X8}  8  {b % 256:X2} 00 00 00 00 00 00 00\n");
        return sb.ToString();
    }

    [Fact]
    public void Devices_absent_from_log_do_not_disable_reference_block_detection()
    {
        using var dir = new TempDir();
        var devices = DeviceFiles.LoadDevices(dir.Write("vehicle.dbc", Dbc(presentMessages: 10, absentMessages: 30)));
        string trc = dir.Write("log.trc", Trc(ids: 10, blocks: 100));
        var log = new List<string>();

        var result = new DstConnectTrcProcessor().Process(trc, devices, dir.File("dst.csv"),
            new OutputSettings { Format = OutputFormat.CsvDstConnect }, new ProcessingContext(log.Add));

        Assert.True(result.Success);
        Assert.DoesNotContain(log, l => l.Contains("запасной детект", StringComparison.Ordinal));
        // :F2 в логе зависит от локали (1.00 / 1,00).
        Assert.Contains(log, l => Regex.IsMatch(l, @"покрытие 1[.,]00\b"));
        Assert.InRange(result.RowsWritten, 98, 101);
    }

    [Fact]
    public void Dst_csv_contains_only_columns_with_data_and_last_known_values()
    {
        using var dir = new TempDir();
        var devices = DeviceFiles.LoadDevices(dir.Write("vehicle.dbc", Dbc(presentMessages: 3, absentMessages: 2)));
        string trc = dir.Write("log.trc", Trc(ids: 3, blocks: 10));
        string output = dir.File("dst.csv");

        var result = new DstConnectTrcProcessor().Process(trc, devices, output,
            new OutputSettings { Format = OutputFormat.CsvDstConnect }, new ProcessingContext());

        Assert.True(result.Success);
        var lines = File.ReadAllLines(output);
        Assert.Equal("Time;Step;S0;S1;S2", lines[0]);
        // Time — часы:минуты:секунды.миллисекунды (Start time + StepMs).
        Assert.Matches(@"^10:00:01\.\d{3};", lines[1]);
        Assert.EndsWith(";0;0;0", lines[1]);
        Assert.EndsWith(";9;9;9", lines[^1]);
    }
}
