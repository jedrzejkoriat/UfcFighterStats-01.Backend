using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UfcStatsAPI.Contracts;
using System.Threading.Tasks;
using System.IO;

namespace UfcStatsAPI.Controllers
{
	[Route("")]
	[ApiController]
	public class FigtherStatsController : ControllerBase
	{
        private readonly ILogger<FigtherStatsController> logger;

        public FigtherStatsController(ILogger<FigtherStatsController> logger)
		{
            this.logger = logger;
        }

		[HttpGet]
        public async Task<IActionResult> GetUfcFighterStats()
		{
			logger.LogInformation("UFC Stats requiested");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ufcfighterdata.json");
			string json = await System.IO.File.ReadAllTextAsync(filePath);
			return Ok(json);
		}

		[HttpGet("pulse")]
		public IActionResult GetPulse()
        {
            logger.LogInformation("Pulse requiested");
            return Ok("PULSE");
        }
    }
}
