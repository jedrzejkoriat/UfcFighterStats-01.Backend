using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UfcStatsAPI.Contracts;
using System.Threading.Tasks;
using System.IO;

namespace UfcStatsAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class UfcFighterStatsController : ControllerBase
	{
		private readonly IScrapperService scrapperService;

		public UfcFighterStatsController(IScrapperService scrapperService)
		{
			this.scrapperService = scrapperService;
		}

		[HttpGet]
		public async Task<IActionResult> GetRaknedFightersStats()
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ufcfighterdata.json");
			string json = await System.IO.File.ReadAllTextAsync(filePath);
			return Ok(json);
		}
	}
}
