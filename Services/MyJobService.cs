using Quartz;
using UfcStatsAPI.Contracts;

namespace UfcStatsAPI.Services
{
	public class MyJobService : IJob
	{
		private readonly IScrapperService scrapperService;
        private readonly ILogger<MyJobService> logger;

        public MyJobService(IScrapperService scrapperService, ILogger<MyJobService> logger)
		{
			this.scrapperService = scrapperService;
            this.logger = logger;
        }

		public async Task Execute(IJobExecutionContext context)
		{
			logger.LogInformation("=========== START UFC STATS UPDATE ===========");

			string json = await scrapperService.GetRankedFighterStatsAsync();
			string filePath = "ufcfighterdata.json";

			if (File.Exists(filePath))
            {
                string backupFileName = $"Archive/ufcfighterdata_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                File.Move(filePath, backupFileName);
            }

            File.WriteAllText(filePath, json);

            logger.LogInformation("=========== END UFC STATS UPDATE ===========");
        }
	}
}
