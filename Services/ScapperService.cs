using System.Globalization;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UfcStatsAPI.Contracts;
using UfcStatsAPI.Model;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Playwright;
using UfcStatsAPI.Helpers;

namespace UfcStatsAPI.Services
{
    public class ScrapperService : IScrapperService
    {
        private readonly ILogger<ScrapperService> _logger;
        private readonly IYoutubeService _youtubeService;
        private readonly IGoogleService _googleService;
        private readonly IWikipediaService _wikipediaService;
        private readonly ISherdogService _sherdogService;

        public ScrapperService(
            ILogger<ScrapperService> logger,
            IYoutubeService youtubeService,
            IGoogleService googleService,
            ISherdogService sherdogService,
            IWikipediaService wikipediaService)
        {
            _logger = logger;
            _youtubeService = youtubeService;
            _googleService = googleService;
            _wikipediaService = wikipediaService;
            _sherdogService = sherdogService;
        }

        /// <summary>
        /// Scrapes statistics, basic information, and fight history for all ranked fighters from Wikipedia and Sherdog.
        /// Retrieves YouTube video links for each fighter using the YouTube API.
        /// Uses the Google API to get Sherdog links for fighters not found on Wikipedia.
        /// </summary>
        /// <returns>A list of WeightClassModel objects, each containing a list of Fighters and their data.</returns>
        public async Task<string> ScrapUFCRankedFighterAsync()
        {
            // Get weight class tables from Wikipedia
            List<HtmlNode> wikipediaWeightClassTables = await _wikipediaService.GetWeightClassTablesAsync();
            _logger.LogInformation("Weight class tables fetched from Wikipedia");

            // List of weight class names and list of weight class models
            List<string> weightClassNames = ["Heavyweight", "Light Heavyweight", "Middleweight", "Welterweight", "Lightweight", "Featherweight", "Bantamweight", "Flyweight"];
            List<WeightClassModel> weightClassModels = new List<WeightClassModel>();

            // Iterate through each weight class table
            for (int i = 0; i < wikipediaWeightClassTables.Count; i++)
            {
                // WeightClassModel with assigned weightclass name
                WeightClassModel weightClassModel = new WeightClassModel { Name = weightClassNames[i] };

                // Split the table HTML into lines
                string[] tableSplitToLines = wikipediaWeightClassTables[i].OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

                // Semaphore and task configuration
                int maxFightersAtOnce = 1;
                SemaphoreSlim semaphore = new SemaphoreSlim(maxFightersAtOnce);
                List<Task> tasks = new List<Task>();

                // Iterate through each line in the weight class table
                for (int j = 0; j < tableSplitToLines.Length; j++)
                {
                    // Search for 'flagicon' - unique keyword for each fighter entry
                    if (tableSplitToLines[j].Contains("flagicon"))
                    {
                        // Get the Fighter Wikipedia Link two lines below the 'flagicon' keyword
                        var wikipediaLink = Regex.Match(tableSplitToLines[j + 2], @"/wiki/[^""]+");

                        // Get the ranking from two lines above the 'flagicon' keyword
                        string ranking = _wikipediaService.GetRanking(tableSplitToLines[j - 2]);
                        _logger.LogInformation("1. Ranking fetched from wikipedia");

                        // Initialize sherdogLink field
                        string? sherdogLink = null;

                        try
                        {
                            // Check if fighter wikipedia link was found
                            if (wikipediaLink.Success)
                            {
                                try
                                {
                                    // Get sherdog link from wikipedia page
                                    sherdogLink = await _wikipediaService.GetSherdogLinkAsync(wikipediaLink.ToString());
                                    _logger.LogInformation("2. Sherdog link fetched from wikipedia: {sherdogLink}", sherdogLink);
                                }
                                catch
                                {
                                    // Get sherdog link from google search if fighter sherdog link was not found on wikipedia
                                    sherdogLink = await _googleService.GetSherdogLinkAsync(tableSplitToLines[j + 2]);
                                    _logger.LogInformation("2. Sherdog link fetched from google: {sherdogLink}", sherdogLink);
                                }
                            }
                            else
                            {
                                // Get sherdog link from google search if fighter wikipedia link was not found
                                sherdogLink = await _googleService.GetSherdogLinkAsync(tableSplitToLines[j + 2]);
                                _logger.LogInformation("2. Sherdog link fetched from google: {sherdogLink}", sherdogLink);
                            }

                            // Check if sherdog link was found
                            if (!string.IsNullOrEmpty(sherdogLink))
                            {
                                // Handle edge cases in sherdog links
                                sherdogLink = EdgeCaseHelper.HandleEdgeCases(sherdogLink);

                                // Wait for semaphore to be available
                                await semaphore.WaitAsync();

                                // Start new task to scrap fighter data
                                tasks.Add(Task.Run(async () =>
                                {
                                    int maxRetries = 3;
                                    int attempt = 0;

                                    while (attempt < maxRetries)
                                    {
                                        attempt++;
                                        try
                                        {
                                            // Split the weight classes into two halves to use different youtube API keys
                                            bool firstHalf = i >= 4 ? false : true;

                                            int timeout = 50000;
                                            var scrapTask = _sherdogService.ScrapStatsAsync(sherdogLink, ranking, firstHalf);
                                            var completedTask = await Task.WhenAny(scrapTask, Task.Delay(timeout));


                                            FighterModel fighter = null;
                                            if (completedTask == scrapTask)
                                            {
                                                fighter = await scrapTask;
                                            }
                                            else
                                            {
                                                _logger.LogWarning("Timeout while scraping fighter: {sherdogLink}", sherdogLink);
                                                throw new Exception("Timeout while scraping fighter");
                                            }

                                            try
                                            {
                                                // Get youtube videos for each fighter
                                                fighter.YoutubeVideos = await this._youtubeService.GetFighterYoutubeVideos(fighter.Name, firstHalf);
                                                _logger.LogInformation("4. Youtube videos fetched: {sherdogLink}", sherdogLink);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError("Error fetching videos for fighter: " + fighter.Name);
                                            }

                                            // Check if fighter was scrapped successfully
                                            if (fighter != null)
                                            {
                                                // Add fighter to the weight class model
                                                weightClassModel.Fighters.Add(fighter);
                                                _logger.LogInformation("=======================SCRAPPED: {sherdogLink}==========================", sherdogLink);
                                                break;
                                            }
                                            else
                                            {
                                                this._logger.LogError("Fighter not scrapped: {sherdogLink}", sherdogLink);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            this._logger.LogError("Error scrapping fighter data: " + sherdogLink);
                                        }
                                    }
                                    semaphore.Release();
                                }));
                            }
                            else
                            {
                                this._logger.LogWarning("Fighter sherdog link was not found: " + tableSplitToLines[j + 2]);
                            }
                        }
                        catch (Exception ex)
                        {
                            this._logger.LogError("Error getting sherdog link for fighter: " + tableSplitToLines[j + 2]);
                        }
                    }
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                // Sort the fighters by ranking
                weightClassModel.Fighters = weightClassModel.Fighters.OrderBy(f => f.Ranking).ToList();

                // Add the weight class model to the list
                weightClassModels.Add(weightClassModel);
            }

            return JsonSerializer.Serialize(weightClassModels, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
