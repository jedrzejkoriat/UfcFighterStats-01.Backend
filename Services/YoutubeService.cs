using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
    public class YoutubeService : IYoutubeService
    {
        private readonly string apiKey1;
        private readonly string apiKey2;

        public YoutubeService(IConfiguration configuration)
        {
            this.apiKey1 = configuration.GetSection("Youtube")["ApiKey1"];
            this.apiKey2 = configuration.GetSection("Youtube")["ApiKey2"];
        }

        public async Task<List<string>> GetFighterYoutubeVideos(string? fullName, bool firstHalf)
        {
            string currentApiKey = "";

            if (firstHalf) currentApiKey = this.apiKey1;
            else currentApiKey = this.apiKey2;

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = currentApiKey,
                ApplicationName = this.GetType().ToString()
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = $"{fullName} full fight";
            searchListRequest.MaxResults = 50;
            searchListRequest.RegionCode = "PL";

            var searchListResponse = await searchListRequest.ExecuteAsync();

            List<string> videos = new List<string>();

            int videoCounter = 0;
            foreach (var searchResult in searchListResponse.Items)
            {
                if (searchResult.Id.Kind == "youtube#video")
                {
                    // Tworzenie URL do filmu i dodanie go do listy
                    string videoUrl = $"https://www.youtube.com/watch?v={searchResult.Id.VideoId}";
                    videos.Add(videoUrl);
                    videoCounter++;
                }

                if (videoCounter >= 5)
                {
                    break;
                }
            }

            return videos;
        }
    }
}
