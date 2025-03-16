using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

namespace UfcStatsAPI.Services
{
	public class WikipediaService
	{
		private static readonly HttpClient httpClient = new HttpClient();

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
									if (fighterSherdogLink.Contains("&#39;"))
									{
										fighterSherdogLink = fighterSherdogLink.Replace("&#39;", "");
									}
									sherdogLinks.Add(fighterSherdogLink.ToString());
								}
							}));
						}
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

		private async static Task<Dictionary<string, object>> ScrapSherdogStats(string url)
		{
			// Downloading fighter sherdog page content
			var response = await httpClient.GetStringAsync("https://www.sherdog.com/fighter/" + url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			Dictionary<string, object> fighter = new Dictionary<string, object>();

			// Age, Height
			var bio = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			for (int i = 0; i < bio.Length; i++)
			{
				if (bio[i].Contains("AGE"))
				{
					// Age
					int age = Convert.ToInt32(Regex.Match(bio[i + 1], @"<td><b>(\d+)").ToString().Substring(7));
					fighter.Add("Age", age);
				}
				if (bio[i].Contains("HEIGHT"))
				{
					// Height
					int height = Convert.ToInt32(Regex.Match(bio[i], @"(\d+\.\d+)\s*cm").ToString().Substring(0, 3));
					fighter.Add("Height", height);
				}
			}

			// Name + Surname, Nickname, Country
			var fighterTitle = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='fighter-title']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			for (int i = 0; i < fighterTitle.Length; i++)
			{
				if (fighterTitle[i].Contains("fighter-nationality"))
				{
					// Country
					string country = Regex.Match(fighterTitle[i + 2], @">(.*?)<").ToString().TrimEnd('<').Substring(1);
					fighter.Add("Country", country);
				}
				if (fighterTitle[i].Contains("fn"))
				{
					// Name and Surname
					string name = Regex.Match(fighterTitle[i], @"fn(.*?)<").ToString().TrimEnd('<').Substring(4);
					fighter.Add("Name", name);
				}
				if (fighterTitle[i].Contains("nickname"))
				{
					// Nickname
					string nickname = Regex.Match(fighterTitle[i], @"<em>(.*?)<").ToString().TrimEnd('<').Substring(4);
					fighter.Add("Nickname", nickname);
				}
			}

			// Wins, WinsKo, WinsSub, WinsDec, WinsOthers
			var record = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");

			var winsHtml = record.SelectSingleNode("//div[@class='wins']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

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

			// Add fields that sometimes are not present
			fighter.Add("WinOTH", winOthers);

			// Losses, LossesKo, LossesSub, LossesDec, LossesOthers, NoContest
			var lossesHtml = record.SelectSingleNode("//div[@class='loses']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

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

			// Add fields that sometimes are not present
			fighter.Add("LoseOTH", loseOthers);
			fighter.Add("NoContest", noContest);

			// Fights: Result, Opponent, Event, Date, Method, Round, Time
			var fightHistory = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='module fight_history']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			return fighter;
		}

		// Create a json structure (see template.json)
		private async static Task<Dictionary<string, List<Dictionary<string, object>>>> CreateJson(Dictionary<string, List<string>> sherdogLinksDictionary)
		{
			// Initialize dictionary representing the json
			Dictionary<string, List<Dictionary<string, object>>> json = new Dictionary<string, List<Dictionary<string, object>>>();

			// Looping through weighclasses
			foreach (var weightClass in sherdogLinksDictionary)
			{
				// Initialize weightclass fighters dictionary
				List<Dictionary<string, object>> weightClassDictionary = new List<Dictionary<string, object>>();

				// Limit the tasks to weightClass.Count (around 15-16 fighters)
				int maxDegreeOfParallelism = weightClass.Value.Count;

				SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
				List<Task> tasks = new List<Task>();

				// Looping through each fighter in weightClass list
				foreach (var fighter in weightClass.Value)
				{
					// Add task
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							// Scrap fighter from sherdog
							var fighterDictionary = await ScrapSherdogStats(fighter);

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
