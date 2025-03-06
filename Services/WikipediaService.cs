using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

namespace UfcStatsAPI.Services
{
	public class WikipediaService
	{
		private static readonly HttpClient httpClient = new HttpClient();
		private async Task ScrapUfcRankingsPage()
		{
			var url = "https://en.wikipedia.org/wiki/UFC_rankings";


			var response = await httpClient.GetStringAsync(url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			var tables = htmlDoc.DocumentNode.SelectNodes("//table");

			var desiredTables = new List<HtmlNode>();

			for (int i = 3; i < 11; i++)
			{
				desiredTables.Add(tables[i]);
			}

			/*
			 * 3 - heavyweight
			 * 4 - light heavyweight
			 * 5 - middleweight
			 * 6 - welterweight
			 * 7 - lightweight
			 * 8 - featherweight
			 * 9 - bantamweight
			 * 10 - flyweight
			 */
			List<string> tablesNames = ["heavyweight", "light heavyweight", "middleweight", "welterweight", "lightweight", "featherweight", "bantamweight", "flyweight"];

			Dictionary<string, List<string>> weightClasses = new Dictionary<string, List<string>>();

			string pattern = @"/wiki/[^""]+";

			for (int i = 0; i < desiredTables.Count; i++)
			{
				List<string> fighters = new List<string>();

				string tableHtml = desiredTables[i].OuterHtml;
				string[] lines = tableHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

				for (int j = 0; j < lines.Length; j++)
				{
					if (lines[j].Contains("flagicon"))
					{
						var match = Regex.Match(lines[j + 2], pattern);
						if (match.Success)
						{
							fighters.Add(match.ToString());
						}
					}
				}

				weightClasses.Add(tablesNames[i], fighters);
			}
		}
	}
}
