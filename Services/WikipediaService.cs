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
							tasks.Add(Task.Run(async () => {
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

			/*for (int i = 0; i < bio.Length; i++)
			{
				if (bio[i].Contains("AGE"))
				{
					// BAD
					fighter.Add("Age", Convert.ToInt32(bio[i + 2].Substring(3, 2)));
				}
				if (bio[i].Contains("HEIGHT"))
				{
					// BAD
					fighter.Add("Height", Convert.ToInt32(bio[i + 4].Substring(2, 3)));
				}
			}*/

			// Name + Surname, Nickname, Country
			var fighterTitle = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='fighter-title']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			/*for (int i = 0; i < fighterTitle.Length; i++)
			{
				if (fighterTitle[i].Contains("fn"))
				{
					// Name
				}
				if (fighterTitle[i].Contains("nickname"))
				{
					// Nickname
				}
				if (fighterTitle[i].Contains("fighter-nationality"))
				{
					// Country
				}
			}*/

			// Wins, WinsKo, WinsSub, WinsDec, WinsOthers, Losses, LossesKo, LossesSub, LossesDec, LossesOthers, NoContest
			var record = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");

			var wins = record.SelectSingleNode("//div[@class='wins']");
			var loses = record.SelectSingleNode("//div[@class='loses']");



			// Fights: Result, Opponent, Event, Date, Method, Round, Time
			var fightHistory = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='module fight_history']").OuterHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

			return fighter;
		}

		private async static Task<Dictionary<string, List<Dictionary<string, object>>>> CreateJson(Dictionary<string, List<string>> sherdogLinksDictionary)
		{
			Dictionary<string, List<Dictionary<string, object>>> json = new Dictionary<string, List<Dictionary<string, object>>>();
			foreach (var weightClass in sherdogLinksDictionary)
			{
				List<Dictionary<string, object>> weightClassDictionary = new List<Dictionary<string, object>>();

				int maxDegreeOfParallelism = weightClass.Value.Count;
				SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
				List<Task> tasks = new List<Task>();

				foreach (var fighter in weightClass.Value)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							var fighterDictionary = await ScrapSherdogStats(fighter);
							weightClassDictionary.Add(fighterDictionary);
						}
						catch (Exception ex)
						{
							Console.WriteLine(fighter);
						}
						finally
						{
							semaphore.Release();
						}

					}));
				}

				await Task.WhenAll(tasks);
				json.Add(weightClass.Key, weightClassDictionary);
			}

			return json;
		}
	}
}
