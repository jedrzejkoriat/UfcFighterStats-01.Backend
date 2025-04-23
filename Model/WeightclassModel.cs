
using System.Text.Json.Serialization;

namespace UfcStatsAPI.Model
{
    public class WeightClassModel
    {
        [JsonPropertyName("weightClass")]
        public string? Name { get; set; }

        [JsonPropertyName("fighters")]
        public List<FighterModel> Fighters { get; set; }= new List<FighterModel>();
    }
}
