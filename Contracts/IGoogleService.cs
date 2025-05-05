namespace UfcStatsAPI.Contracts
{
    public interface IGoogleService
    {
        Task<string> GetSherdogLinkAsync(string fighterName);
    }
}
