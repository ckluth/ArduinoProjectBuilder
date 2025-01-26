using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArduinoProjectBuilder;

public static class Serializer
{
    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T obj) where T : class
    {
        var jsonString = JsonSerializer.Serialize(obj, options);
        return jsonString;
    }

    public static T? Deserialize<T>(string str) where T : class
    {
        var result = JsonSerializer.Deserialize<T>(str, options);
        return result;
    }

}