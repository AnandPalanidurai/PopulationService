using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Population.Data;
using Population.Dto;

namespace Population.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PopulationController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<PopulationController> _logger;

        public PopulationController(ILogger<PopulationController> logger,DataContext context)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery]string state)
        {
            _logger.LogInformation(string.Format("{1} – API endpoint called - /population?state={0}", state, DateTime.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss.fff")));
            var states = state.Split(',').Select(int.Parse).ToList(); 

            //1. Validate all states available in DB
            var allAvailableStates = new List<int>();
            allAvailableStates.AddRange(await _context.Actuals.Select(a => a.State).Distinct().ToListAsync());
            allAvailableStates.AddRange(await _context.Estimates.Select(a => a.State).Distinct().ToListAsync());

            bool isAnyStateNotAvailable = states.Where(s => !allAvailableStates.Any(x => x == s)).Any();
            if (isAnyStateNotAvailable) {
                return NotFound();
            }

            List<PopulationDto> populations = new List<PopulationDto>();

            //1. Lookup available stats in actuals table first
            populations.AddRange(await  _context.Actuals.Where(a=> states.Contains(a.State)).Select(a=> new PopulationDto { State = a.State, Population = a.ActualPopulation}).ToListAsync());

            //2. Lookup from estimates table for not available data in actuals
            var availableInActualsStates = populations.Select(p=> p.State).Distinct().ToList();
            var notAvailableInActualsStates = states.Except(availableInActualsStates);
            var estimatesForNotAvailableStates = await _context.Estimates.Where(a => notAvailableInActualsStates.Contains(a.State)).ToListAsync();
            populations.AddRange(estimatesForNotAvailableStates.GroupBy(a=> new { a.State }).Select(a => new PopulationDto { State = a.Key.State, Population = a.Sum(x=> Convert.ToDecimal(x.EstimatesPopulation)) }).ToList());

            return Ok(populations);
        }
    }
}
