using UfcStatsAPI.Model;

namespace UfcStatsAPI.Contracts;

public interface ISherdogService
{
    Task<FighterModel> ScrapStatsAsync(string url, string ranking, bool firstHalf);
}
