using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
    public class YoutubeService : IYoutubeService
    {
        private readonly string apiKey;

        public YoutubeService(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<List<string>> GetFighterYoutubeVideos(string? fullName)
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = this.apiKey,
                ApplicationName = this.GetType().ToString()
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = $"{fullName} full fight";
            searchListRequest.MaxResults = 50;

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
