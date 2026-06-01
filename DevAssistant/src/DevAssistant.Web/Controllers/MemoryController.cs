using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevAssistant.Web.Controllers
{
    public sealed class MemoryController : Controller
    {
        private readonly IAgentService _api;

        public MemoryController(IAgentService api) => _api = api;

        public async Task<IActionResult> Index(
            [FromQuery] string? query, CancellationToken ct)
        {
            var vm = await _api.GetMemoryAsync(ct);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var results = await _api.SearchMemoryAsync(query, 10, ct);
                return View(vm with { SearchResults = results, SearchQuery = query });
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Add(
            [FromForm] string content, CancellationToken ct)
        {
            await _api.AddMemoryAsync(content, ct);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(
            [FromForm] string id, CancellationToken ct)
        {
            await _api.DeleteMemoryAsync(id, ct);
            return RedirectToAction(nameof(Index));
        }
    }
}
