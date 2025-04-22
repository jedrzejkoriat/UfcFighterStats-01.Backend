using System.Text.Json.Serialization;
using UfcStatsAPI.Model;

namespace UfcStatsAPI.Configuration
{

    [JsonSourceGenerationOptions(WriteIndented = true)]

    [JsonSerializable(typeof(Fight))]
    [JsonSerializable(typeof(List<Fight>))]
    [JsonSerializable(typeof(Fighter))]
    [JsonSerializable(typeof(List<Fighter>))]
    [JsonSerializable(typeof(Weightclass))]
    [JsonSerializable(typeof(List<Weightclass>))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
