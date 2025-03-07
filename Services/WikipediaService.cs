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

				for (int j = 0; j < lines.Length; j++)
				{
					// Searching for flagicon - it is a unique word which is a part of pattern that easily helps finding desired links to fighters wikipedia pages
					if (lines[j].Contains("flagicon"))
					{
						// Searching the link two lines below the flagicon keyword
						var figtherWikipediaLink = Regex.Match(lines[j + 2], @"/wiki/[^""]+");
						if (figtherWikipediaLink.Success)
						{
							// Searching for sherdog link on fighters wikipedia page
							string? fighterSherdogLink = await ScrapSingleSherdogLink(figtherWikipediaLink.ToString());

							// If sherdog link was found the fighter is added (otherwise he is ignored)
							if (fighterSherdogLink != null)
							{
								sherdogLinks.Add(fighterSherdogLink.ToString());
							}
						}
					}
				}
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

		private async static Task ScrapSherdogStats(string url)
		{
			// Downloading fighter sherdog page content
			var response = await httpClient.GetStringAsync("https://www.sherdog.com/fighter/" + url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			// Birthdate, age, height, weight, association, class
			var bio = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']");

			// Country, flag.png
			var nationality = htmlDoc.DocumentNode.SelectSingleNode("//div[@classes='fighter-nationality']");

			// Wins, win ko/tko, win submissions, win decisions, win others, losses, losses ko/tko, losses submissions, losses decisions, losses others, No contest
			var record = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");

			// Fights table: result, opponent, event, date, method, round, time
			var fightHistory = htmlDoc.DocumentNode.SelectSingleNode("//div[@classes='module fight_history']");
		}
	}
}
