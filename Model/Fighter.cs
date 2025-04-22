using System.Text.Json.Serialization;

namespace UfcStatsAPI.Model
{
    public class Fighter
    {
        [JsonPropertyName("Ranking")]
        public int? Ranking { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("Country")]
        public string? Country { get; set; }

        [JsonPropertyName("Age")]
        public int? Age { get; set; }

        [JsonPropertyName("Height")]
        public int? Height { get; set; }

        [JsonPropertyName("Wins")]
        public int? Wins { get; set; }

        [JsonPropertyName("WinKO")]
        public int? WinKO { get; set; }

        [JsonPropertyName("WinSUB")]
        public int? WinSUB { get; set; }

        [JsonPropertyName("WinDEC")]
        public int? WinDEC { get; set; }

        [JsonPropertyName("WinOTH")]
        public int? WinOTH { get; set; }

        [JsonPropertyName("Losses")]
        public int? Losses { get; set; }

        [JsonPropertyName("LoseKO")]
        public int? LoseKO { get; set; }

        [JsonPropertyName("LoseSUB")]
        public int? LoseSUB { get; set; }

        [JsonPropertyName("LoseDEC")]
        public int? LoseDEC { get; set; }

        [JsonPropertyName("LoseOTH")]
        public int? LoseOTH { get; set; }

        [JsonPropertyName("NoContest")]
        public int? NoContest { get; set; }

        [JsonPropertyName("fightHistory")]
        public List<Fight> FightHistory { get; set; } = new List<Fight>();
    }
}
