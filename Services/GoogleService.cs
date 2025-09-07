using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
    public class GoogleService : IGoogleService
    {
        private readonly HttpClient httpClient;
        private readonly string apiKey;
        private readonly string searchEngineId;

        public GoogleService(HttpClient httpClient, IConfiguration configuration)
        {
            this.httpClient = httpClient;
            this.apiKey = configuration.GetSection("Google")["ApiKey"];
            this.searchEngineId = configuration.GetSection("Google")["SearchEngineId"];
        }

        // Getting sherdog link from google search
        public async Task<string> GetSherdogLinkAsync(string line)
        {
            try
            {
                // Scrap fighterName for google search
                string fighterName = Regex.Match(line, @"<td>(.*)").Groups[1].Value;

                // Set Google API Keys
                string apiKey = this.apiKey;
                string cx = this.searchEngineId;

                // Search for "FIGHTER NAME sherdog"
                string searchTerm = fighterName + " sherdog";
                string url = $"https://www.googleapis.com/customsearch/v1?q={searchTerm}&key={apiKey}&cx={cx}";

                // Make the HTTP request and parse the response
                var response = await httpClient.GetStringAsync(url);
                dynamic jsonReponse = JsonConvert.DeserializeObject(response);

                // Iterate through items from the response
                foreach (var item in jsonReponse.items)
                {
                    // Search for valid link that contains "sherdog.com"
                    if (item.link.ToString().Contains("sherdog.com"))
                    {
                        return item.link.ToString();
                    }
                }

                throw new Exception("No sherdog link found in google api");
            }
            catch (Exception ex)
            {
                return "";
            }
        }
    }
}
