using DevAssistant.Models;
using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevAssistant.Web.Controllers
{
    public sealed class TestsController : Controller
    {
        private readonly IAgentService _api;
        private readonly ILogger<TestsController> _logger;

        public TestsController(IAgentService api, ILogger<TestsController> logger)
        {
            _api = api;
            _logger = logger;
        }

        public IActionResult Index() =>
            View(new TestRunnerViewModel(null, false, "./workspace"));

        [HttpPost]
        public async Task<IActionResult> Run(
            [FromForm] string? filter, CancellationToken ct)
        {
            _logger.LogInformation("[Tests] Running with filter: {Filter}", filter ?? "none");
            var summary = await _api.RunTestsAsync(filter, ct);
            return View("Index", new TestRunnerViewModel(summary, false, "./workspace"));
        }

        [HttpGet("/tests/results")]
        public async Task<IActionResult> Results(
            [FromQuery] string? filter, CancellationToken ct)
        {
            var summary = await _api.RunTestsAsync(filter, ct);
            return PartialView("_TestResults", summary);
        }
    }
}
