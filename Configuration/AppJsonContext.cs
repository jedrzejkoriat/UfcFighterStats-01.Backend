using System.Text.Json.Serialization;
using UfcStatsAPI.Model;

namespace UfcStatsAPI.Configuration
{

    [JsonSourceGenerationOptions(WriteIndented = true)]

    [JsonSerializable(typeof(FightModel))]
    [JsonSerializable(typeof(List<FightModel>))]
    [JsonSerializable(typeof(FighterModel))]
    [JsonSerializable(typeof(List<FighterModel>))]
    [JsonSerializable(typeof(WeightClassModel))]
    [JsonSerializable(typeof(List<WeightClassModel>))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
