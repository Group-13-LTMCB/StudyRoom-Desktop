using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyRoom.Core.Models;

public sealed class WebSocketMessage
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    public static WebSocketMessage Create<T>(string action, string roomId, T data)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        using var doc = JsonDocument.Parse(bytes);
        return new WebSocketMessage
        {
            Action = action,
            RoomId = roomId,
            Data = doc.RootElement.Clone()
        };
    }

    public T? GetData<T>(JsonSerializerOptions? options = null)
    {
        if (Data.ValueKind == JsonValueKind.Undefined || Data.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<T>(Data.GetRawText(), options);
    }
}
