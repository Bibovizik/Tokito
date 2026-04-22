using System.Text.Json;

namespace Tokito.Services.Games;

internal static class GameJsonSerializer
{
    public static string? Serialize(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var element = value.Value;
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.GetRawText();
    }

    public static JsonElement? Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
