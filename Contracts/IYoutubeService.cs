namespace UfcStatsAPI.Contracts
{
    public interface IYoutubeService
    {
        Task<List<string>> GetFighterYoutubeVideos(string fullName);
    }
}
