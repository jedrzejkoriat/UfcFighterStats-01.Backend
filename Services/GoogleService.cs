using Newtonsoft.Json;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
    public class GoogleService : IGoogleService
    {
        private readonly string apiKey;
        private readonly HttpClient httpClient;
        private readonly string searchEngineId;
        public GoogleService(HttpClient httpClient, IConfiguration configuration)
        {
            this.apiKey = configuration.GetSection("Google")["ApiKey"];
            this.searchEngineId = configuration.GetSection("Google")["SearchEngineId"];
            this.httpClient = httpClient;
        }

        public async Task<string> GetSherdogLinkAsync(string fighterName)
        {
            string apiKey = this.apiKey;
            string cx = this.searchEngineId;

            // Search for "FIGHTER NAME sherdog"
            string searchTerm = fighterName + " sherdog";

            string url = $"https://www.googleapis.com/customsearch/v1?q={searchTerm}&key={apiKey}&cx={cx}";

            var response = await httpClient.GetStringAsync(url);
            dynamic jsonReponse = JsonConvert.DeserializeObject(response);

            foreach(var item in jsonReponse.items)
            {
                // If link contains "sherdog.com" it is a valid link
                if (item.link.ToString().Contains("sherdog.com"))
                {
                    return item.link.ToString();
                }
            }

            // Throw exception if sherdog.com link was not found
            throw new Exception("No sherdog link found in google api");

        }
    }
}
