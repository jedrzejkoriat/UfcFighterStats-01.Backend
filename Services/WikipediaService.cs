using HtmlAgilityPack;
using System;
using System.Text.RegularExpressions;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services;

public class WikipediaService : IWikipediaService
{
    private readonly HttpClient _httpClient;

    public WikipediaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Get sherdog link from fighter wikipedia page
    public async Task<string> GetSherdogLinkAsync(string wikipediaUrl)
    {
        try
        {
            // Get the content of the fighters wikipedia page and load it to HtmlDocument
            var response = await _httpClient.GetStringAsync("https://en.wikipedia.org" + wikipediaUrl);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // Get the infobox table which contains the sherdog link
            var table = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='infobox ib-martial-artist vcard']");

            // Scrap the Name and Surname
            var name = Regex.Match(table.OuterHtml, @"<span class=""fn"">([^<]+)<\/span>");

            // Scrap the Sherdog Link
            var match = Regex.Match(table.OuterHtml, "http://www\\.sherdog\\.com/fightfinder/fightfinder\\.asp\\?fighterID=\\d+");

            // Create the link to avoid the redirection
            if (match.Success && name.Success)
            {
                // Replace space between name and surname with '-'
                string modifiedName = name.ToString().Replace(' ', '-');

                // Trim the </span>
                modifiedName = modifiedName.Substring(0, modifiedName.Length - 7);

                // Trim the <span class="fn">
                int biggerSignIndex = modifiedName.LastIndexOf('>');
                modifiedName = modifiedName.Substring(biggerSignIndex + 1);

                // Trim the ID from the link
                int equalSignIndex = match.ToString().LastIndexOf('=');
                string fighterId = match.ToString().Substring(equalSignIndex + 1);

                return modifiedName + '-' + fighterId;
            }
            return null;
        }
        catch (Exception ex)
        {
            return "";
        }
    }

    // Get weight class tables from Wikipedia (from male Heavyweight to male Flyweight)
    public async Task<List<HtmlNode>> GetWeightClassTablesAsync()
    {
        // User-Agent with Google Chrome
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/116.0.0.0 Safari/537.36"
        );

        // Get the content of the UFC rankings wikipedia page and load it to HtmlDocument
        var response = await _httpClient.GetStringAsync("https://en.wikipedia.org/wiki/UFC_rankings");
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(response);

        // Get all tables from the page
        var tables = htmlDoc.DocumentNode.SelectNodes("//table");

        // List to store the target tables
        var weightClassRankings = new List<HtmlNode>();

        // Iterate through target tables (male Heavyweight to male Flyweight are located on 3-10 indexes)
        for (int i = 3; i < 11; i++)
        {
            weightClassRankings.Add(tables[i]);
        }

        return weightClassRankings;
    }

    // Scrap ranking and convert to number
    public string GetRanking(string line)
    {
        // Get ranking two lines above the flagicon keyword or three lines above if the fighter is champion/interim champion
        var numberRanking = Regex.Match(line, @"<th>(\d+)");
        var interimChampionRanking = Regex.Match(line, @">IC<");
        var championRanking = Regex.Match(line, @">C<");

        // Check for ranking success
        if (numberRanking.Success) return numberRanking.ToString().Substring(4, numberRanking.ToString().Length - 4);
        else if (interimChampionRanking.Success) return "1";
        else if (championRanking.Success) return "0";
        else throw new Exception("Ranking not found");
    }
}
