using System.Text.Json.Serialization;

namespace UfcStatsAPI.Model
{
    public class FighterModel
    {
        [JsonPropertyName("ranking")]
        public int? Ranking { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("wins")]
        public int? Wins { get; set; }

        [JsonPropertyName("winKo")]
        public int? WinsKo { get; set; }

        [JsonPropertyName("winSub")]
        public int? WinsSub { get; set; }

        [JsonPropertyName("winDec")]
        public int? WinsDec { get; set; }

        [JsonPropertyName("winOth")]
        public int? WinsOth { get; set; }

        [JsonPropertyName("losses")]
        public int? Losses { get; set; }

        [JsonPropertyName("lossesKo")]
        public int? LossesKo { get; set; }

        [JsonPropertyName("lossesSub")]
        public int? LossesSub { get; set; }

        [JsonPropertyName("lossesDec")]
        public int? LossesDec { get; set; }

        [JsonPropertyName("lossesOth")]
        public int? LossesOth { get; set; }

        [JsonPropertyName("noContest")]
        public int? NoContest { get; set; }

        [JsonPropertyName("fightHistory")]
        public List<FightModel> FightHistory { get; set; } = new List<FightModel>();

        [JsonPropertyName("youtubeVideos")]
        public List<string> YoutubeVideos { get; set; } = new List<string>();
    }
}
