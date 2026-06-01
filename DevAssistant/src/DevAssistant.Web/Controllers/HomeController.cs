using DevAssistant.Models;
using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevAssistant.Web.Controllers
{
    public sealed class HomeController : Controller
    {
        private readonly IAgentService _agent;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAgentService agent, ILogger<HomeController> logger)
        {
            _agent = agent;
            _logger = logger;
        }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var report = await _agent.GetHealthAsync(ct);

            // Map HealthReport (Core) → HealthStatus (Web view model)
            var vm = new HealthStatus(
                OllamaOnline: report.OllamaOnline,
                QdrantOnline: report.QdrantOnline,
                OllamaModel: report.OllamaModel,
                OllamaVersion: report.OllamaVersion,
                MemoryEntryCount: 0,   // wired in Step 7 (memory)
                CheckedAt: report.CheckedAt);

            return View(vm);
        }

        [HttpGet("/api/health-simple")]
        public async Task<IActionResult> HealthSimple(CancellationToken ct)
        {
            var report = await _agent.GetHealthAsync(ct);
            return Ok(new
            {
                report.OllamaOnline,
                report.QdrantOnline,
                report.OllamaModel,
                report.CheckedAt
            });
        }
    }
}
