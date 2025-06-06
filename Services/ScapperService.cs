﻿using System.Globalization;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UfcStatsAPI.Contracts;
using UfcStatsAPI.Model;
using System.Text.Json;

namespace UfcStatsAPI.Services
{
    public class ScrapperService : IScrapperService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<ScrapperService> logger;
        private readonly IYoutubeService youtubeService;
        private readonly IGoogleService googleService;

        public ScrapperService(HttpClient httpClient, ILogger<ScrapperService> logger, IYoutubeService youtubeService, IGoogleService googleService)
        {
            this.httpClient = httpClient;
            this.logger = logger;
            this.youtubeService = youtubeService;
            this.googleService = googleService;
        }

        public async Task<string> GetRankedFighterStatsAsync()
        {
            return JsonSerializer.Serialize(await ScrapDataAsync(), new JsonSerializerOptions { WriteIndented = true});
        }

        /// <summary>
        /// Scrapes statistics, basic information, and fight history for all ranked fighters from Wikipedia and Sherdog.
        /// Retrieves YouTube video links for each fighter using the YouTube API.
        /// Uses the Google API to get Sherdog links for fighters not found on Wikipedia.
        /// </summary>
        /// <returns>A list of WeightClassModel objects, each containing a list of Fighters and their data.</returns>
        private async Task<List<WeightClassModel>> ScrapDataAsync()
        {
            // Downloading tables from UFC_Rankings wikipedia page
            List<HtmlNode> wikipediaWeightClassTables = await GetWeightClassTablesFromWikipediaAsync();

            List<string> weightClassNames = ["Heavyweight", "Light Heavyweight", "Middleweight", "Welterweight", "Lightweight", "Featherweight", "Bantamweight", "Flyweight"];
            List<WeightClassModel> weightClassModels = new List<WeightClassModel>();

            // Looping through ranking tables (weightclasses)
            for (int i = 0; i < wikipediaWeightClassTables.Count; i++)
            {
                // Creating new weightClassModel with assigned weightclass name
                WeightClassModel weightClassModel = new WeightClassModel { Name = weightClassNames[i] };

                // Splitting html table to lines for easier looping
                string[] tableSplitToLines = wikipediaWeightClassTables[i].OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

                // List of tasks - max 16 fighters at once
                SemaphoreSlim semaphore = new SemaphoreSlim(16);
                List<Task> tasks = new List<Task>();

                // Looping through lines of html table
                for (int j = 0; j < tableSplitToLines.Length; j++)
                {
                    // Searching for flagicon - it is a unique word which is a part of pattern that easily helps finding links to fighters wikipedia pages
                    if (tableSplitToLines[j].Contains("flagicon"))
                    {
                        // Searching for the link two lines below the flagicon keyword
                        var wikipediaLink = Regex.Match(tableSplitToLines[j + 2], @"/wiki/[^""]+");

                        // Searching for the rankinkg two lines below the flagicon keyword
                        string ranking = GetRanking(tableSplitToLines[j - 2]);

                        string? sherdogLink = null;
                        try
                        {
                            if (wikipediaLink.Success)
                            {
                                // Use wikipedia link to get sherdog link
                                try { sherdogLink = await GetSherdogLinkFromWikipediaAsync(wikipediaLink.ToString()); }
                                // Use google search if sherdog link was not found inside wikipedia page
                                catch { sherdogLink = await GetSherdogLinkFromGoogleAsync(tableSplitToLines[j + 2]); }
                            }
                            // Use google search if wikipedia link was not found
                            else { sherdogLink = await GetSherdogLinkFromGoogleAsync(tableSplitToLines[j + 2]); }

                            if (!string.IsNullOrEmpty(sherdogLink))
                            {
                                // Handling edge cases - removing the ' from the link
                                if (sherdogLink.Contains("&#39;")) sherdogLink = sherdogLink.Replace("&#39;", "");

                                try
                                {
                                    bool firstHalf = i >= 4 ? false : true;

                                    // Wait if already 16 tasks are running
                                    await semaphore.WaitAsync();
                                    tasks.Add(Task.Run(async () =>
                                    {
                                        // Scrap fighter from sherdog page and add fighter to weightclass
                                        var fighter = await ScrapStatsFromSherdogAsync(sherdogLink, ranking, firstHalf);
                                        weightClassModel.Fighters.Add(fighter);
                                        this.logger.LogInformation($"Fighter scrapped: {fighter.Ranking}.{fighter.Name} '{fighter.Nickname}'");
                                    }));
                                }
                                catch (Exception ex)
                                {
                                    this.logger.LogError("Error scrapping fighter data: " + sherdogLink);
                                }
                            }
                            else
                            {
                                this.logger.LogWarning("Fighter sherdog link was not found: " + tableSplitToLines[j + 2]);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                }

                // Wait for all tasks to be finished
                await Task.WhenAll(tasks);

                // Sort the fighter list by ranking
                weightClassModel.Fighters = weightClassModel.Fighters.OrderBy(f => f.Ranking).ToList();

                // Add the weight class model to the list
                weightClassModels.Add(weightClassModel);
            }
            return weightClassModels;
        }

        // Getting sherdog link from fighter's wikipedia page
        private async Task<string> GetSherdogLinkFromWikipediaAsync(string wikipediaLink)
        {
            try
            {
                string sherdogLink = await ScrapSherdogLinkFromWikipediaAsync(wikipediaLink);
                return sherdogLink;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Error scrapping sherdog link from wikipedia: " + wikipediaLink);
                return "";
            }
        }

        // Getting sherdog link from google search
        private async Task<string> GetSherdogLinkFromGoogleAsync(string line)
        {
            try
            {
                // Scrap fighterName for google search
                string fighterName = Regex.Match(line, @"<td>(.*)").Groups[1].Value;
                string sherdogLink = await googleService.GetSherdogLinkAsync(fighterName);
                return sherdogLink;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Error scrapping sherdog link from google");
                return "";
            }
        }

        // Scrap ranking and convert to number
        private string GetRanking(string line)
        {
            // Searching for ranking two lines above the flagicon keyword or three lines above if the fighter is champion/interim champion
            var numberRanking = Regex.Match(line, @"<th>(\d+)");
            var interimChampionRanking = Regex.Match(line, @">IC<");
            var championRanking = Regex.Match(line, @">C<");

            // Checking for ranking success
            if (numberRanking.Success) return numberRanking.ToString().Substring(4, numberRanking.ToString().Length - 4);
            else if (interimChampionRanking.Success) return "1";
            else if (championRanking.Success) return "0";
            else throw new Exception("Ranking not found");
        }

        // Getting weight class tables from Wikipedia (from male Heavyweight to male Flyweight)
        private async Task<List<HtmlNode>> GetWeightClassTablesFromWikipediaAsync()
        {
            // Downloading content of the Wikipedia UFC_Rankings page
            var response = await httpClient.GetStringAsync("https://en.wikipedia.org/wiki/UFC_rankings");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // Select all tables from the page
            var tables = htmlDoc.DocumentNode.SelectNodes("//table");

            var weightClassRankings = new List<HtmlNode>();

            // Target tables are located on indexes 3-10 (male Heavyweight to male Flyweight)
            for (int i = 3; i < 11; i++)
            {
                weightClassRankings.Add(tables[i]);
            }

            return weightClassRankings;
        }

        // Scraper for Sherdog link from Wikipedia page
        private async Task<string?> ScrapSherdogLinkFromWikipediaAsync(string url)
        {
            // Downloading fighter wikipedia page content
            var response = await this.httpClient.GetStringAsync("https://en.wikipedia.org" + url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // This specific table contains sherdog link for each fighter
            var table = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='infobox ib-martial-artist vcard']");

            // Scrapping name and surname of the fighter
            var name = Regex.Match(table.OuterHtml, @"<span class=""fn"">([^<]+)<\/span>");

            // Scrapping the sherdog link of fighter
            var match = Regex.Match(table.OuterHtml, "http://www\\.sherdog\\.com/fightfinder/fightfinder\\.asp\\?fighterID=\\d+");

            // Creating the link to avoid the redirection
            if (match.Success && name.Success)
            {
                // Name is downloaded as <span class="fn">Jon Jones</span> - we replace space between Name and Surname to '-'
                string modifiedName = name.ToString().Replace(' ', '-');

                // Trimming the </span>
                modifiedName = modifiedName.Substring(0, modifiedName.Length - 7);

                // Trimming the <span class="fn">
                int biggerSignIndex = modifiedName.LastIndexOf('>');
                modifiedName = modifiedName.Substring(biggerSignIndex + 1);

                // Trimming the ID (27944) from link http://www.sherdog.com/fightfinder/fightfinder.asp?fighterID=27944
                int equalSignIndex = match.ToString().LastIndexOf('=');
                string fighterId = match.ToString().Substring(equalSignIndex + 1);

                // Returning ready sherdog link of fighter
                return modifiedName + '-' + fighterId;
            }
            return null;
        }

        // Scraper for fighter statistics and fight history from Sherdog page
        private async Task<FighterModel> ScrapStatsFromSherdogAsync(string url, string ranking, bool firstHalf)
        {
            // Downloading fighter sherdog page content
            var response = await httpClient.GetStringAsync("https://www.sherdog.com/fighter/" + url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            FighterModel fighter = new FighterModel();

            fighter.Ranking = Convert.ToInt32(ranking);

            // Age, Height
            var bioHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

            for (int i = 0; i < bioHtml.Length; i++)
            {
                if (bioHtml[i].Contains("AGE"))
                {
                    // Age
                    int age = Convert.ToInt32(Regex.Match(bioHtml[i + 1], @"<td><b>(\d+)").ToString().Substring(7));
                    fighter.Age = age;
                }
                if (bioHtml[i].Contains("birthDate"))
                {
                    // Birthdate
                    string birthdate = Regex.Match(bioHtml[i], @">([^<]+)</span>").ToString();
                    birthdate = birthdate.Substring(1, birthdate.Length - "</span>".Length - 1);
                    fighter.Birthdate = birthdate;
                }
                if (bioHtml[i].Contains("WEIGHT"))
                {
                    // Weight
                    string weight = Regex.Match(bioHtml[i], @"(\d+)\.").Groups[1].Value;
                    fighter.Weight = int.Parse(weight);
                }
                if (bioHtml[i].Contains("HEIGHT"))
                {
                    // Height
                    int height = Convert.ToInt32(Regex.Match(bioHtml[i], @"(\d+\.\d+)\s*cm").ToString().Substring(0, 3));
                    fighter.Height = height;
                }
                if (bioHtml[i].Contains("ASSOCIATION"))
                {
                    string association = Regex.Match(bioHtml[i + 1], @">([^<]+)<").ToString().Substring(1).TrimEnd('<');
                    fighter.Association = association;
                }
            }

            // Name + Surname, Nickname, Country
            var fighterTitleHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='fighter-title']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

            for (int i = 0; i < fighterTitleHtml.Length; i++)
            {
                if (fighterTitleHtml[i].Contains("fighter-nationality"))
                {
                    // Country
                    string country = Regex.Match(fighterTitleHtml[i + 2], @">(.*?)<").ToString().TrimEnd('<').Substring(1);
                    fighter.Country = country;
                }
                if (fighterTitleHtml[i].Contains("addressLocality"))
                {
                    // Region
                    string region = Regex.Match(fighterTitleHtml[i], @">([^<]+)<").ToString().Substring(1).TrimEnd('<');
                    fighter.Region = region;
                }
                if (fighterTitleHtml[i].Contains("fn"))
                {
                    // Name and Surname
                    string name = Regex.Match(fighterTitleHtml[i], @"fn(.*?)<").ToString().TrimEnd('<').Substring(4);
                    fighter.Name = name;
                }
                if (fighterTitleHtml[i].Contains("nickname"))
                {
                    // Nickname
                    Match match = Regex.Match(fighterTitleHtml[i], @"<em>(.*?)<");
                    string nickname = "";

                    if (match.Success)
                    {
                        nickname = match.ToString().TrimEnd('<').Substring(4);
                    }

                    fighter.Nickname = nickname;
                }
            }

            // Wins, WinsKo, WinsSub, WinsDec, WinsOthers
            var recordHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");

            var winsHtml = recordHtml.SelectSingleNode("//div[@class='wins']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

            // Initialize fields that sometimes are not present
            int winOthers = 0;

            for (int i = 0; i < winsHtml.Count(); i++)
            {
                if (winsHtml[i].Contains(">Wins<"))
                {
                    // Wins
                    int wins = Convert.ToInt32(Regex.Match(winsHtml[i + 1], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.Wins = wins;
                }
                if (winsHtml[i].Contains("em> TKO"))
                {
                    // Wins by KO/TKO
                    int winsKo = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.WinsKo = winsKo;
                }
                if (winsHtml[i].Contains(">SUBMISSIONS"))
                {
                    // Wins by Submission
                    int winSub = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.WinsSub = winSub;
                }
                if (winsHtml[i].Contains(">DECISIONS"))
                {
                    // Wins by Decision
                    int winDec = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.WinsDec = winDec;
                }
                if (winsHtml[i].Contains(">OTHERS"))
                {
                    // Other wins
                    winOthers = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                }
            }

            // Add fields that sometimes are not present as zeros
            fighter.WinsOth = winOthers;

            // Losses, LossesKo, LossesSub, LossesDec, LossesOthers, NoContest
            var lossesHtml = recordHtml.SelectSingleNode("//div[@class='loses']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

            // Initialize fields that sometimes are not present
            int lossesOthers = 0;
            int noContest = 0;

            for (int i = 0; i < lossesHtml.Count(); i++)
            {
                if (lossesHtml[i].Contains(">Losses<"))
                {
                    // Loses
                    int losses = Convert.ToInt32(Regex.Match(lossesHtml[i + 1], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.Losses = losses;
                }
                if (lossesHtml[i].Contains("em> TKO"))
                {
                    // Lose by KO/TKO
                    int lossesKo = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.LossesKo = lossesKo;
                }
                if (lossesHtml[i].Contains(">SUBMISSIONS"))
                {
                    // Lose by Submission
                    int lossesSub = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.LossesSub = lossesSub;
                }
                if (lossesHtml[i].Contains(">DECISIONS"))
                {
                    // Lose by Decision
                    int lossesDec = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                    fighter.LossesDec = lossesDec;
                }
                if (lossesHtml[i].Contains(">OTHERS"))
                {
                    // Other loses
                    lossesOthers = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                }
                if (lossesHtml[i].Contains("winloses nc"))
                {
                    // No contest
                    noContest = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
                }
            }

            // Add fields that sometimes are not present as zeros
            fighter.LossesOth = lossesOthers;
            fighter.NoContest = noContest;

            // Fights: Result, Opponent, Event, Date, Method, Round, Time
            var fightHistoryHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='module fight_history']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

            for (int i = 12; i < fightHistoryHtml.Count(); i++)
            {
                if (fightHistoryHtml[i].Contains("<tr>"))
                {
                    FightModel fight = new FightModel();
                    // Fight result
                    fight.Result = Regex.Match(fightHistoryHtml[i + 1], @">(\w+)<").ToString().Trim('<').Substring(1);

                    // Opponent
                    fight.Opponent = Regex.Match(fightHistoryHtml[i + 2], @">([^<]+)<").ToString().Trim('<').Substring(1);

                    // Event Name
                    fight.EventName = Regex.Match(fightHistoryHtml[i + 3], ">([^<]+)<").ToString().Trim('<').Substring(1);

                    // Date
                    string scrappedDate = Regex.Match(fightHistoryHtml[i + 3], @"[A-Za-z]{3} / \d{2} / \d{4}").ToString();
                    DateTime date = DateTime.ParseExact(scrappedDate, "MMM / dd / yyyy", CultureInfo.InvariantCulture);
                    fight.Date = date.ToString("dd-MM-yyyy");

                    // Method
                    fight.Method = Regex.Match(fightHistoryHtml[i + 4], "b>([^<]+)<").ToString().Trim('<').Substring(2);

                    // Round of stoppage
                    fight.Round = Convert.ToInt32(Regex.Match(fightHistoryHtml[i + 6], @">(\w+)<").ToString().Trim('<').Substring(1));

                    // Time of stoppage
                    fight.Time = Regex.Match(fightHistoryHtml[i + 7], ">([^<]+)<").ToString().Trim('<').Substring(1);

                    fighter.FightHistory.Add(fight);
                    i += 8;
                }
            }

            // Get youtube videos for each fighter
            try
            {
                fighter.YoutubeVideos = await this.youtubeService.GetFighterYoutubeVideos(fighter.Name, firstHalf);
            }
            catch (Exception ex)
            {
                logger.LogError("Error fetching videos for fighter: " + fighter.Name);
            }

            return fighter;
        }
    }
}
