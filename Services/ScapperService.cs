using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using UfcStatsAPI.Contracts;
using Newtonsoft.Json;
using System.Globalization;

namespace UfcStatsAPI.Services
{
	/* 
	 Known issues:

	-Fighters without wikipedia page excluded (~3) (probably won't fix)
	-Fighters not grouped in ranking order (flagicon +2/3 lines above is ranking - add to dictionary)
	-Scrap top 3 youtube links (maybe some youtube API)


	 */
	public class ScrapperService : IScrapperService
	{
		private static readonly HttpClient httpClient = new HttpClient();

		public async Task<string> GetRankedFightersJsonAsync()
		{
			var rankingTables = await ScrapUfcRankings();
			var sherdogLinksDictionary = await ScrapSherdogLinks(rankingTables);
			var scrappedFighterStats = await ConstructJson(sherdogLinksDictionary);

			return JsonConvert.SerializeObject(scrappedFighterStats, Newtonsoft.Json.Formatting.Indented);
		}

		// Scrapping weightclass ranking tables from UFC_rankings wikipedia page
		private async static Task<List<HtmlNode>> ScrapUfcRankings()
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
		private async static Task<Dictionary<string, List<string>>> ScrapSherdogLinks(List<HtmlNode> rankingTables)
		{
			// All UFC man weightclasses
			List<string> weightClassNames = ["Heavyweight", "Light Heavyweight", "Middleweight", "Welterweight", "Lightweight", "Featherweight", "Bantamweight", "Flyweight"];
			Dictionary<string, List<string>> weightClasses = new Dictionary<string, List<string>>();

			for (int i = 0; i < rankingTables.Count; i++)
			{
				List<string> sherdogLinks = new List<string>();

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
						// Searching for ranking two lines above the flagicon keyword or three lines above if the fighter is champion/interim champion
						var numberRanking = Regex.Match(lines[j - 2], @"<th>(\d+) </th>");
						var interimChampionRanking = Regex.Match(lines[j - 3], @">IC<");
						var championRanking = Regex.Match(lines[j - 3], @">C<");

						string ranking = "";

						// Checking for success
						if (numberRanking.Success) ranking = numberRanking.ToString().Substring(4, 2);
						else if (interimChampionRanking.Success) ranking = "1";
						else if (championRanking.Success) ranking = "2";

						// Searching the link two lines below the flagicon keyword
						var figtherWikipediaLink = Regex.Match(lines[j + 2], @"/wiki/[^""]+");
						if (figtherWikipediaLink.Success)
						{
							await semaphore.WaitAsync();
							tasks.Add(Task.Run(async () =>
							{
								// Searching for sherdog link on fighters wikipedia page
								string? fighterSherdogLink = await ScrapSingleSherdogLink(figtherWikipediaLink.ToString());

								// If sherdog link was found the fighter is added (otherwise he is ignored)
								if (fighterSherdogLink != null)
								{
									if (fighterSherdogLink.Contains(" &#39;"))
									{
										fighterSherdogLink = fighterSherdogLink.Replace("&#39;", "");
									}
									sherdogLinks.Add(fighterSherdogLink.ToString());
								}
							}));
						}
						// Iplement scrapping from google
					}
				}

				await Task.WhenAll(tasks);
				// Add key (weightclass name) and value (list of sherdog links) to dictionary
				weightClasses.Add(weightClassNames[i], sherdogLinks);
			}
			return weightClasses;
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

		private async static Task<Dictionary<string, object>> ScrapSherdogStats(string url, int id)
		{
			// Downloading fighter sherdog page content
			var response = await httpClient.GetStringAsync("https://www.sherdog.com/fighter/" + url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			Dictionary<string, object> fighter = new Dictionary<string, object>();

			fighter.Add("id", id);

			// Age, Height
			var bioHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			for (int i = 0; i < bioHtml.Length; i++)
			{
				if (bioHtml[i].Contains("AGE"))
				{
					// Age
					int age = Convert.ToInt32(Regex.Match(bioHtml[i + 1], @"<td><b>(\d+)").ToString().Substring(7));
					fighter.Add("Age", age);
				}
				if (bioHtml[i].Contains("HEIGHT"))
				{
					// Height
					int height = Convert.ToInt32(Regex.Match(bioHtml[i], @"(\d+\.\d+)\s*cm").ToString().Substring(0, 3));
					fighter.Add("Height", height);
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
					fighter.Add("Country", country);
				}
				if (fighterTitleHtml[i].Contains("fn"))
				{
					// Name and Surname
					string name = Regex.Match(fighterTitleHtml[i], @"fn(.*?)<").ToString().TrimEnd('<').Substring(4);
					fighter.Add("Name", name);
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

					fighter.Add("Nickname", nickname);
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
					fighter.Add("Wins", wins);
				}
				if (winsHtml[i].Contains("em> TKO"))
				{
					// Wins by KO/TKO
					int winKo = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("WinKO", winKo);
				}
				if (winsHtml[i].Contains(">SUBMISSIONS"))
				{
					// Wins by Submission
					int winSub = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("WinSUB", winSub);
				}
				if (winsHtml[i].Contains(">DECISIONS"))
				{
					// Wins by Decision
					int winDec = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("WinDEC", winDec);
				}
				if (winsHtml[i].Contains(">OTHERS"))
				{
					// Other wins
					winOthers = Convert.ToInt32(Regex.Match(winsHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
				}
			}

			// Add fields that sometimes are not present as zeros
			fighter.Add("WinOTH", winOthers);

			// Losses, LossesKo, LossesSub, LossesDec, LossesOthers, NoContest
			var lossesHtml = recordHtml.SelectSingleNode("//div[@class='loses']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			// Initialize fields that sometimes are not present
			int loseOthers = 0;
			int noContest = 0;

			for (int i = 0; i < lossesHtml.Count(); i++)
			{
				if (lossesHtml[i].Contains(">Losses<"))
				{
					// Loses
					int losses = Convert.ToInt32(Regex.Match(lossesHtml[i + 1], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("Losses", losses);
				}
				if (lossesHtml[i].Contains("em> TKO"))
				{
					// Lose by KO/TKO
					int winKo = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("LoseKO", winKo);
				}
				if (lossesHtml[i].Contains(">SUBMISSIONS"))
				{
					// Lose by Submission
					int loseSub = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("LoseSUB", loseSub);
				}
				if (lossesHtml[i].Contains(">DECISIONS"))
				{
					// Lose by Decision
					int loseDec = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
					fighter.Add("LoseDEC", loseDec);
				}
				if (lossesHtml[i].Contains(">OTHERS"))
				{
					// Other loses
					loseOthers = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
				}
				if (lossesHtml[i].Contains("winloses nc"))
				{
					// No contest
					noContest = Convert.ToInt32(Regex.Match(lossesHtml[i + 2], @">(\d+)<").ToString().Trim('<').Substring(1));
				}
			}

			// Add fields that sometimes are not present as zeros
			fighter.Add("LoseOTH", loseOthers);
			fighter.Add("NoContest", noContest);

			// Fights: Result, Opponent, Event, Date, Method, Round, Time
			var fightHistoryHtml = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='module fight_history']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			List<Dictionary<string, string>> fightHistory = new List<Dictionary<string, string>>();
			for (int i = 12; i < fightHistoryHtml.Count(); i++)
			{
				if (fightHistoryHtml[i].Contains("<tr>"))
				{
					Dictionary<string, string> dictionary = new Dictionary<string, string>();

					// Fight result
					dictionary.Add("result", Regex.Match(fightHistoryHtml[i + 1], @">(\w+)<").ToString().Trim('<').Substring(1));

					// Opponent
					dictionary.Add("opponent", Regex.Match(fightHistoryHtml[i + 2], @">([^<]+)<").ToString().Trim('<').Substring(1));

					// Event Name
					dictionary.Add("eventName", Regex.Match(fightHistoryHtml[i + 3], ">([^<]+)<").ToString().Trim('<').Substring(1));

					// Date
					string scrappedDate = Regex.Match(fightHistoryHtml[i + 3], @"[A-Za-z]{3} / \d{2} / \d{4}").ToString();
					DateTime date = DateTime.ParseExact(scrappedDate, "MMM / dd / yyyy", CultureInfo.InvariantCulture);
					dictionary.Add("date", date.ToString("dd-MM-yyyy"));

					// Method
					dictionary.Add("method", Regex.Match(fightHistoryHtml[i + 4], "b>([^<]+)<").ToString().Trim('<').Substring(2));

					// Round of stoppage
					dictionary.Add("round", Regex.Match(fightHistoryHtml[i + 6], @">(\w+)<").ToString().Trim('<').Substring(1));

					// Time of stoppage
					dictionary.Add("time", Regex.Match(fightHistoryHtml[i + 7], ">([^<]+)<").ToString().Trim('<').Substring(1));


					fightHistory.Add(dictionary);
					i += 8;
				}
			}

			fighter.Add("fightHistory", fightHistory);

			return fighter;
		}

		// Create a json structure (see template.json)
		private async static Task<Dictionary<string, List<Dictionary<string, object>>>> ConstructJson(Dictionary<string, List<string>> sherdogLinksDictionary)
		{
			// Initialize dictionary representing the json
			Dictionary<string, List<Dictionary<string, object>>> json = new Dictionary<string, List<Dictionary<string, object>>>();

			// Looping through weightclasses
			foreach (var weightClass in sherdogLinksDictionary)
			{
				// Initialize weightclass fighters dictionary
				List<Dictionary<string, object>> weightClassDictionary = new List<Dictionary<string, object>>();

				// Limit the tasks to weightClass.Count (around 15-16 fighters)
				int maxDegreeOfParallelism = weightClass.Value.Count;

				SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
				List<Task> tasks = new List<Task>();

				int id = 0;
				// Looping through each fighter in weightClass list
				foreach (var fighter in weightClass.Value)
				{
					// Add task
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							int localId = id++;
							// Scrap fighter from sherdog
							var fighterDictionary = await ScrapSherdogStats(fighter, localId);

							// Add fighter to weightclass
							weightClassDictionary.Add(fighterDictionary);
						}
						catch (Exception ex)
						{
							Console.WriteLine(fighter);
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
				json.Add(weightClass.Key, weightClassDictionary);
			}

			return json;
		}
	}
}
