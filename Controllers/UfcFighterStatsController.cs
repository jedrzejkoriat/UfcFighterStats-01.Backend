using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UfcStatsAPI.Contracts;
using System.Threading.Tasks;
using System.IO;

namespace UfcStatsAPI.Controllers
{
	[Route("")]
	[ApiController]
	public class UfcFighterStatsController : ControllerBase
	{
		private readonly IScrapperService scrapperService;
        private readonly ILogger<UfcFighterStatsController> logger;

        public UfcFighterStatsController(IScrapperService scrapperService, ILogger<UfcFighterStatsController> logger)
		{
			this.scrapperService = scrapperService;
            this.logger = logger;
        }

		[HttpGet]
        public async Task<IActionResult> GetUFCStats()
		{
			logger.LogInformation("UFC Stats requiested");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ufcfighterdata.json");
			string json = await System.IO.File.ReadAllTextAsync(filePath);
			return Ok(json);
		}
	}
}
