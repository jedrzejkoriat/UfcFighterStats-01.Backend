namespace UfcStatsAPI.Contracts
{
	public interface IScrapperService
	{
		Task<string> GetRankedFightersJsonAsync();
	}
}
