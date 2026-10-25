using System.Text.Json;
using System.Text.Json.Serialization;

namespace logReader.UI;

internal sealed class DisplaySettingsStore
{
    private const long MaximumFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 8,
        Converters =
        {
            new JsonStringEnumConverter<DtcDisplayFormat>(),
            new JsonStringEnumConverter<DtcInclusionMode>()
        }
    };

    private readonly string _path;

    internal DisplaySettingsStore(string? path = null)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LOGER", "Classic", "display-settings.json"));
    }

    internal DisplaySettings Load()
    {
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumFileBytes) return new();
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = JsonOptions.MaxDepth });
            var settings = document.RootElement.Deserialize<DisplaySettings>(JsonOptions)?.Normalized() ?? new();
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var properties = document.RootElement.EnumerateObject().ToArray();
                bool hasMode = properties.Any(property => property.Name.Equals("dtcInclusion", StringComparison.OrdinalIgnoreCase));
                var legacy = properties.FirstOrDefault(property => property.Name.Equals("autoIncludeDtc", StringComparison.OrdinalIgnoreCase));
                if (!hasMode && legacy.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    settings = settings with { DtcInclusion = legacy.Value.GetBoolean() ? DtcInclusionMode.Auto : DtcInclusionMode.Never };
            }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    internal void Save(DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string json = JsonSerializer.Serialize(settings.Normalized(), JsonOptions);
        SafeFileWriter.Write(_path, temporaryPath => File.WriteAllText(temporaryPath, json));
    }
}
