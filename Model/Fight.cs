using System.Text.Json.Serialization;

namespace UfcStatsAPI.Model
{
    public class Fight
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("opponent")]
        public string? Opponent { get; set; }

        [JsonPropertyName("eventName")]
        public string? EventName { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("round")]
        public int? Round { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }
    }
}
