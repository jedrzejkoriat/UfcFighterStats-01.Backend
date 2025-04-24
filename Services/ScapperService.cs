using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using UfcStatsAPI.Contracts;
using Newtonsoft.Json;
using System.Globalization;
using UfcStatsAPI.Model;
using System.Runtime.CompilerServices;

namespace UfcStatsAPI.Services
{
	/* 
	-Scrap top 3 youtube links (maybe some youtube API)
	 */
	public class ScrapperService : IScrapperService
    {
		private static readonly HttpClient httpClient = new HttpClient();
        private readonly ILogger<ScrapperService> logger;
        private readonly IYoutubeService youtubeService;

        public ScrapperService(ILogger<ScrapperService> logger, IYoutubeService youtubeService)
		{
            this.logger = logger;
            this.youtubeService = youtubeService;
        }


		public async Task<string> GetRankedFightersJsonAsync()
		{
			var rankingTables = await ScrapUfcRankingTables();
			var sherdogLinksWeightClassModels = await ScrapSherdogLinksAndRankings(rankingTables);
			var scrappedFighterData = await ScrapFightersData(sherdogLinksWeightClassModels);

			return JsonConvert.SerializeObject(scrappedFighterData, Newtonsoft.Json.Formatting.Indented);
		}

		// Scrapping weightclass ranking tables from UFC_rankings wikipedia page
		private async Task<List<HtmlNode>> ScrapUfcRankingTables()
		{
			// Downloading wikipedia/UFC_rankings content
			var response = await httpClient.GetStringAsync("https://en.wikipedia.org/wiki/UFC_rankings");

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);
			var tables = htmlDoc.DocumentNode.SelectNodes("//table");

			var weightClassRankings = new List<HtmlNode>();

			// Our desired tables (All men weightclass rankinkgs) are located on 3-10 positions
			for (int i = 3; i < 11; i++)
			{
				weightClassRankings.Add(tables[i]);
			}

			return weightClassRankings;
		}

		// Scrapping sherdog link for each ranked fighter - returns a dictionary of weightclass as key and ranked fighter sherdog links as value
		private async Task<List<SherdogLinksWeightClassModel>> ScrapSherdogLinksAndRankings(List<HtmlNode> rankingTables)
		{
			// All UFC man weightclasses
			List<string> weightClassNames = ["Heavyweight", "Light Heavyweight", "Middleweight", "Welterweight", "Lightweight", "Featherweight", "Bantamweight", "Flyweight"];

			List<SherdogLinksWeightClassModel> sherdogLinksWeightClasses = new List<SherdogLinksWeightClassModel>();

            for (int i = 0; i < rankingTables.Count; i++)
			{
				List<SherdogLinksFighterModel> sherdogLinks = new List<SherdogLinksFighterModel>();

                // Splitting single table html to lines
                string[] lines = rankingTables[i].OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

				// List of tasks - max 16 fighters at one time
				SemaphoreSlim semaphore = new SemaphoreSlim(16);
				List<Task> tasks = new List<Task>();

				for (int j = 0; j < lines.Length; j++)
				{
					// Searching for flagicon - it is a unique word which is a part of pattern that easily helps finding desired links to fighters wikipedia pages
					if (lines[j].Contains("flagicon"))
					{
						// Searching the link two lines below the flagicon keyword
						var figtherWikipediaLink = Regex.Match(lines[j + 2], @"/wiki/[^""]+");
						if (figtherWikipediaLink.Success)
						{
							// Searching for ranking two lines above the flagicon keyword or three lines above if the fighter is champion/interim champion
							var numberRanking = Regex.Match(lines[j - 2], @"<th>(\d+)");
							var interimChampionRanking = Regex.Match(lines[j - 2], @">IC<");
							var championRanking = Regex.Match(lines[j - 2], @">C<");

							string ranking = "";

							// Checking for success
							if (numberRanking.Success) ranking = numberRanking.ToString().Substring(4, numberRanking.ToString().Length - 4);
							else if (interimChampionRanking.Success) ranking = "1";
							else if (championRanking.Success) ranking = "0";

							await semaphore.WaitAsync();
							tasks.Add(Task.Run(async () =>
							{

								// Searching for sherdog link on fighters wikipedia page
								string? fighterSherdogLink = await ScrapSingleSherdogLink(figtherWikipediaLink.ToString());

								// If sherdog link was found the fighter is added (otherwise he is ignored)
								if (fighterSherdogLink != null)
								{
									if (fighterSherdogLink.Contains("&#39;"))
									{
										fighterSherdogLink = fighterSherdogLink.Replace("&#39;", "");
                                    }
                                    this.logger.LogInformation($"{ranking}. {fighterSherdogLink}");
									sherdogLinks.Add(new SherdogLinksFighterModel { Ranking = ranking, SherdogLink = fighterSherdogLink.ToString() });
                        }
							}));
						}
						// Iplement scrapping from google
					}
				}

				await Task.WhenAll(tasks);
				// Add key (weightclass name) and value (list of sherdog links) to dictionary
				sherdogLinksWeightClasses.Add(new SherdogLinksWeightClassModel { Name = weightClassNames[i], Fighters = sherdogLinks });
                this.logger.LogInformation("================================================================");
			}
			return sherdogLinksWeightClasses;
		}

		private async static Task<string?> ScrapSingleSherdogLink(string url)
		{
			// Downloading fighter wikipedia page content
			var response = await httpClient.GetStringAsync("https://en.wikipedia.org" + url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			// This specific table contains sherdog link for each fighter
			var table = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='infobox vcard']");

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

        // Create a json structure (see template.json)
        private async Task<List<WeightClassModel>> ScrapFightersData(List<SherdogLinksWeightClassModel> sherdogLinksDictionary)
        {
            List<WeightClassModel> json = new List<WeightClassModel>();

            // Looping through weightclasses
			for (int i = 0; i < sherdogLinksDictionary.Count; i++)
            {
				if (i == 4)
				{
					await Task.Delay(600000);
				}
                // Initialize weightclass fighters dictionary
                WeightClassModel weightClassEntity = new WeightClassModel { Name = sherdogLinksDictionary[i].Name };

                // Limit the tasks to weightClass.Count (around 15-16 fighters)
                int maxDegreeOfParallelism = sherdogLinksDictionary[i].Fighters.Count;

                SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
                List<Task> tasks = new List<Task>();

                int id = 0;
                // Looping through each fighter in weightClass list
                foreach (var fighter in sherdogLinksDictionary[i].Fighters)
                {
                    // Add task
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            int localId = id++;
                            // Scrap fighter from sherdog
                            var fighterEntity = await ScrapSherdogStats(fighter.SherdogLink, fighter.Ranking);

                            // Add fighter to weightclass
                            weightClassEntity.Fighters.Add(fighterEntity);
                        }
                        catch (Exception ex)
                        {
							this.logger.LogError("Error scrapping fighter data: " + fighter.SherdogLink);
                        }
                        finally
                        {
                            // Release task
                            semaphore.Release();
                        }
                    }));
                }

                // Stop until all tasks are done
                await Task.WhenAll(tasks);

                // Add weightClass name as key and 
                json.Add(weightClassEntity);
            }

            return json;
        }

        private async static Task<FighterModel> ScrapSherdogStats(string url, string ranking)
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
				if (bioHtml[i].Contains("HEIGHT"))
				{
					// Height
					int height = Convert.ToInt32(Regex.Match(bioHtml[i], @"(\d+\.\d+)\s*cm").ToString().Substring(0, 3));
					fighter.Height = height;
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

			return fighter;
		}
	}
}
