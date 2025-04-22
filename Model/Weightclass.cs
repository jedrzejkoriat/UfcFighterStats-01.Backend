
using System.Text.Json.Serialization;

namespace UfcStatsAPI.Model
{
    public class Weightclass
    {
        [JsonPropertyName("weightClass")]
        public string? Name { get; set; }

        [JsonPropertyName("fighters")]
        public List<Fighter> Fighters { get; set; }= new List<Fighter>();
    }
}
