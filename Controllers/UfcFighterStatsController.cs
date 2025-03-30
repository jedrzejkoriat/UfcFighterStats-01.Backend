using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UfcStatsAPI.Contracts;
using System.Text.Json;
using System;

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
			var stats = await scrapperService.GetRankedFightersJsonAsync();
			return Ok(stats);
		}
	}
}
