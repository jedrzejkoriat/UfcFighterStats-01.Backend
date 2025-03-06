using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

namespace UfcStatsAPI.Services
{
	public class WikipediaService
	{
		private static readonly HttpClient httpClient = new HttpClient();
		private async static Task<List<HtmlNode>> DownloadWeightClassRankings()
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

			return desiredTables;
		}

		private async static Task<Dictionary<string, List<string>>> ScrapSherdogLinks(List<HtmlNode> desiredTables)
		{

			List<string> tablesNames = ["heavyweight", "light heavyweight", "middleweight", "welterweight", "lightweight", "featherweight", "bantamweight", "flyweight"];

			Dictionary<string, List<string>> weightClasses = new Dictionary<string, List<string>>();

			string pattern = @"/wiki/[^""]+";

			for (int i = 0; i < desiredTables.Count; i++)
			{
				List<string> sherdogLinks = new List<string>();

				string tableHtml = desiredTables[i].OuterHtml;
				string[] lines = tableHtml.Split(new char[] { '\n' }, StringSplitOptions.None);

				for (int j = 0; j < lines.Length; j++)
				{
					if (lines[j].Contains("flagicon"))
					{
						var match = Regex.Match(lines[j + 2], pattern);
						if (match.Success)
						{
							string? sherdog = await ScrapSingleSherdogLink(match.ToString());
							if (sherdog != null)
							{
								await ScrapSherdogStats(sherdog);
								sherdogLinks.Add(sherdog.ToString());
							}
						}
					}
				}
				weightClasses.Add(tablesNames[i], sherdogLinks);
			}

			return weightClasses;
		}

		private async static Task<string?> ScrapSingleSherdogLink(string link)
		{
			var url = "https://en.wikipedia.org" + link;

			var response = await httpClient.GetStringAsync(url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			var table = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='infobox vcard']");

			var name = Regex.Match(table.OuterHtml, @"<span class=""fn"">([^<]+)<\/span>");
			var match = Regex.Match(table.OuterHtml, "http://www\\.sherdog\\.com/fightfinder/fightfinder\\.asp\\?fighterID=\\d+");

			if (match.Success && name.Success)
			{


				string modifiedName = name.ToString().Replace(' ', '-');
				modifiedName = modifiedName.Substring(0, modifiedName.Length - 7);
				int biggerSignIndex = modifiedName.LastIndexOf('>');
				modifiedName = modifiedName.Substring(biggerSignIndex + 1);

				int equalSignIndex = match.ToString().LastIndexOf('=');
				string fighterId = match.ToString().Substring(equalSignIndex + 1);

				return "https://www.sherdog.com/fighter/" + modifiedName + '-' + fighterId;
			}
			return null;
		}
		private async static Task ScrapSherdogStats(string url)
		{

			var response = await httpClient.GetStringAsync(url);

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(response);

			var bio = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='bio-holder']");
			var record = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='winsloses-holder']");
		}
	}
}
