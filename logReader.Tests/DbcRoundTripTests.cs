namespace logReader.Tests;

public class DbcRoundTripTests
{
    private const string VehicleDbc =
        "VERSION \"\"\n" +
        "\n" +
        "NS_ :\n" +
        "    CM_\n" +
        "    BA_DEF_\n" +
        "\n" +
        "BS_:\n" +
        "\n" +
        "BU_: ECU1 ECU2\n" +
        "\n" +
        "BO_ 2364539904 EngineData: 8 ECU1\n" +
        " SG_ EngSpeed : 0|16@1+ (0.125,0) [0|8031.875] \"rpm\"  ECU2\n" +
        " SG_ Mode M : 16|2@1+ (1,0) [0|3] \"\" ECU2\n" +
        " SG_ MuxA m0 : 24|8@1+ (1,0) [0|255] \"\" ECU2\n" +
        " SG_ MuxB m1 : 24|8@1+ (1,0) [0|255] \"\" ECU2\n" +
        " SG_ Temp : 32|32@1- (1,0) [-100|100] \"degC\" ECU2\n" +
        "\n" +
        "BO_ 256 Status: 2 ECU2\n" +
        " SG_ Flag : 0|1@1+ (1,0) [0|1] \"\" ECU1\n" +
        "\n" +
        "CM_ BO_ 2364539904 \"Engine frame\";\n" +
        "CM_ SG_ 2364539904 EngSpeed \"Actual engine speed,\n" +
        "measured at crankshaft\";\n" +
        "CM_ SG_ 256 Flag \"Status flag\";\n" +
        "BA_DEF_ BO_ \"GenMsgCycleTime\" INT 0 65535;\n" +
        "BA_ \"GenMsgCycleTime\" BO_ 2364539904 100;\n" +
        "BA_ \"GenMsgCycleTime\" BO_ 256 1000;\n" +
        "VAL_ 2364539904 Mode 0 \"Off\" 1 \"Idle\" 2 \"Run\" ;\n" +
        "SIG_VALTYPE_ 2364539904 Temp : 1;\n";

    [Fact]
    public void Saving_without_changes_keeps_the_file_byte_identical()
    {
        using var dir = new TempDir();
        string path = dir.Write("vehicle.dbc", VehicleDbc);

        var db = DbcFile.ReadDatabase(path);
        DbcFile.WriteDatabase(path, db);

        Assert.Equal(VehicleDbc, File.ReadAllText(path));
        Assert.Equal(5, db.Messages[0].Signals.Count);
    }

    [Fact]
    public void Editing_a_signal_keeps_comments_attributes_and_value_tables()
    {
        using var dir = new TempDir();
        string path = dir.Write("vehicle.dbc", VehicleDbc);

        var db = DbcFile.ReadDatabase(path);
        db.Messages[0].Signals[0].Name = "EngineSpeed";
        db.Messages[0].Signals[0].Factor = 0.25;
        DbcFile.WriteDatabase(path, db);
        string text = File.ReadAllText(path);

        Assert.Contains(" SG_ EngineSpeed : 0|16@1+ (0.25,0)", text);
        Assert.Contains("CM_ SG_ 2364539904 EngineSpeed \"Actual engine speed,\nmeasured at crankshaft\";", text);
        Assert.Contains(" SG_ Mode M : 16|2@1+", text);
        Assert.Contains(" SG_ MuxB m1 : 24|8@1+", text);
        Assert.Contains("VAL_ 2364539904 Mode 0 \"Off\"", text);
        Assert.Contains("BA_ \"GenMsgCycleTime\" BO_ 2364539904 100;", text);
        Assert.Contains("SIG_VALTYPE_ 2364539904 Temp : 1;", text);
        Assert.Contains("BU_: ECU1 ECU2", text);
    }

    [Fact]
    public void Deleting_a_message_removes_only_its_references()
    {
        using var dir = new TempDir();
        string path = dir.Write("vehicle.dbc", VehicleDbc);

        var db = DbcFile.ReadDatabase(path);
        db.Messages.RemoveAt(1);
        DbcFile.WriteDatabase(path, db);
        string text = File.ReadAllText(path);

        Assert.DoesNotContain("BO_ 256", text);
        Assert.DoesNotContain("CM_ SG_ 256", text);
        Assert.DoesNotContain("BO_ 256 1000", text);
        Assert.Contains("CM_ BO_ 2364539904 \"Engine frame\";", text);
    }

    [Fact]
    public void Changing_message_id_rewrites_references_and_new_messages_are_added()
    {
        using var dir = new TempDir();
        string path = dir.Write("vehicle.dbc", VehicleDbc);

        var db = DbcFile.ReadDatabase(path);
        db.Messages[1].Id = 0x101;
        db.Messages.Add(new DbcMessage { Name = "Added", Id = 0x200, IsExtended = false, Dlc = 1,
            Signals = { new DbcSignal { Name = "X", StartBit = 0, Length = 8 } } });
        DbcFile.WriteDatabase(path, db);
        var reread = DbcFile.ReadDatabase(path);
        string text = File.ReadAllText(path);

        Assert.Contains("CM_ SG_ 257 Flag \"Status flag\";", text);
        Assert.Contains("BA_ \"GenMsgCycleTime\" BO_ 257 1000;", text);
        Assert.Equal(new[] { "EngineData", "Status", "Added" }, reread.Messages.Select(m => m.Name));
        Assert.True(text.IndexOf("BO_ 512 Added", StringComparison.Ordinal) < text.IndexOf("CM_ BO_", StringComparison.Ordinal));
    }

    [Fact]
    public void Removing_a_signal_drops_its_comment_and_value_table()
    {
        using var dir = new TempDir();
        string path = dir.Write("vehicle.dbc", VehicleDbc);

        var db = DbcFile.ReadDatabase(path);
        db.Messages[0].Signals.RemoveAll(s => s.Name == "Mode");
        DbcFile.WriteDatabase(path, db);
        string text = File.ReadAllText(path);

        Assert.DoesNotContain("VAL_ 2364539904 Mode", text);
        Assert.Contains("CM_ SG_ 2364539904 EngSpeed", text);
    }

    [Fact]
    public void Multiplexed_and_float_signals_are_decoded()
    {
        using var dir = new TempDir();
        var device = DeviceFiles.LoadDevices(dir.Write("vehicle.dbc", VehicleDbc)).First();
        int muxA = Array.IndexOf(device.Headers, "MuxA");
        int muxB = Array.IndexOf(device.Headers, "MuxB");
        int temp = Array.IndexOf(device.Headers, "Temp");
        int[] FrameFor(int mode, int value, float t)
        {
            var bytes = new int[8];
            bytes[2] = mode;
            bytes[3] = value;
            byte[] f = BitConverter.GetBytes(t);
            for (int i = 0; i < 4; i++) bytes[4 + i] = f[i];
            return bytes;
        }

        device.SetPayload(FrameFor(0, 11, 21.5f));
        device.Decode();
        device.SetPayload(FrameFor(1, 22, -3.25f));
        device.Decode();

        Assert.Equal(11, device.Values[muxA]);
        Assert.Equal(22, device.Values[muxB]);
        Assert.Equal(-3.25, device.Values[temp]);
    }

    [Fact]
    public void Malformed_message_line_does_not_break_loading_and_is_preserved()
    {
        using var dir = new TempDir();
        string content = "BO_ 99999999999 Broken: 8 X\nBO_ 256 Ok: 1 X\n SG_ A : 0|8@1+ (1,0) [0|255] \"\" X\n";
        string path = dir.Write("broken.dbc", content);

        var db = DbcFile.ReadDatabase(path);
        DbcFile.WriteDatabase(path, db);

        Assert.Single(db.Messages);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void Dbf_value_descriptions_are_preserved_and_message_count_updated()
    {
        using var dir = new TempDir();
        string content =
            "[NUMBER_OF_MESSAGES] 1\n\n" +
            "[START_MSG] Msg,256,8,1,0,X,\n" +
            "[START_SIGNALS] Sig,8,1,0,U,255,0,1,0,1,,,\n" +
            "[VALUE_DESCRIPTION] \"Off\",0\n" +
            "[END_MSG]\n\n" +
            "[START_DESC_MSG]\n256 S \"Comment\";\n[END_DESC_MSG]\n";
        string path = dir.Write("db.dbf", content);

        var db = DbfFile.ReadDatabase(path);
        db.Messages.Add(new DbcMessage { Name = "New", Id = 0x300, IsExtended = false, Dlc = 1 });
        DbfFile.WriteDatabase(path, db);
        string text = File.ReadAllText(path);

        Assert.Contains("[NUMBER_OF_MESSAGES] 2", text);
        Assert.Contains("[VALUE_DESCRIPTION] \"Off\",0", text);
        Assert.Contains("256 S \"Comment\";", text);
        Assert.Contains("[START_MSG] New,768,1,0,0,X,", text);
    }
}
