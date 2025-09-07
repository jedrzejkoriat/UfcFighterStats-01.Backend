using HtmlAgilityPack;

namespace UfcStatsAPI.Contracts;

public interface IWikipediaService
{
    string GetRanking(string line);
    Task<string> GetSherdogLinkAsync(string wikipediaLink);
    Task<List<HtmlNode>> GetWeightClassTablesAsync();
}
