using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Summary description for Class1
/// </summary>
public static class JsonFileReader
{
    /// <summary>
    /// Returns the deserialized object or null.
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static T Read<T>(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(text);
    }

    public class BotSettings
    {
        [JsonPropertyName("BotToken")]
        public string BotToken { get; set; }

        [JsonPropertyName("ServerDescriptions")]
        public Dictionary<String,String> ServerDescriptions { get; set; }

    }
}
