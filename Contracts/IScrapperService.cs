using UfcStatsAPI.Model;

namespace UfcStatsAPI.Contracts
{
	public interface IScrapperService
	{
		Task<string> GetRankedFighterStatsAsync();
	}
}
