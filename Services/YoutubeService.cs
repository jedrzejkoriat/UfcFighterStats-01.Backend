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
            // Use apiKey1 for the first half of fighters, and apiKey2 for the second half of fighters
            string apiKey = firstHalf ? this.apiKey1 : this.apiKey2;

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = this.GetType().ToString()
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.MaxResults = 50;

            // Search for "FIGHTER NAME full fight"
            searchListRequest.Q = $"{fullName} full fight";

            // Seach for videos fitted to polish regions
            searchListRequest.RegionCode = "PL";

            var searchListResponse = await searchListRequest.ExecuteAsync();

            List<string> videos = new List<string>();

            // Loop through whole Items list or until 5 videos are found
            for (int i = 0, videoCounter = 0; i < searchListResponse.Items.Count && videoCounter < 5; i++)
            {
                if (searchListResponse.Items[i].Id.Kind == "youtube#video")
                {
                    videos.Add($"https://www.youtube.com/watch?v={searchListResponse.Items[i].Id.VideoId}");
                    videoCounter++;
                }

                // Limit to 5 videos
                if (videoCounter >= 5) return videos;
            }

            return videos;
        }
    }
}
