using Quartz;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
	public class MyJobService : IJob
	{
		private readonly IScrapperService scrapperService;

		public MyJobService(IScrapperService scrapperService)
		{
			this.scrapperService = scrapperService;
		}

		public async Task Execute(IJobExecutionContext context)
		{
			string json = await scrapperService.GetRankedFightersStatisticsAsync();
			string filePath = "ufcfighterdata.json";

			File.WriteAllText(filePath, json);
		}
	}
}
