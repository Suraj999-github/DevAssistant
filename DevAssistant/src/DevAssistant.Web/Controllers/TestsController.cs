using DevAssistant.Models;
using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevAssistant.Web.Controllers
{
    public sealed class TestsController : Controller
    {
        private readonly IAgentService _agent;
        private readonly ILogger<TestsController> _logger;

        public TestsController(IAgentService agent, ILogger<TestsController> logger)
        {
            _agent = agent;
            _logger = logger;
        }

        public IActionResult Index() =>
            View(new TestRunnerViewModel(null, false, "./workspace"));

        [HttpPost]
        public async Task<IActionResult> Run(
            [FromForm] string? filter, CancellationToken ct)
        {
            _logger.LogInformation("[Tests] Running with filter: {Filter}", filter ?? "none");
            var summary = await _agent.RunTestsAsync(filter, ct);
            return View("Index", new TestRunnerViewModel(summary, false, "./workspace"));
        }

        [HttpGet("/tests/results")]
        public async Task<IActionResult> Results(
            [FromQuery] string? filter, CancellationToken ct)
        {
            var summary = await _agent.RunTestsAsync(filter, ct);
            return PartialView("_TestResults", summary);
        }

        [HttpPost("cancel")]
        public IActionResult Cancel()
        {
            _agent.CancelTestRun();
            return Ok(new { cancelled = true });
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            return Ok(new { isRunning = _agent.IsTestRunning });
        }
    }
}
