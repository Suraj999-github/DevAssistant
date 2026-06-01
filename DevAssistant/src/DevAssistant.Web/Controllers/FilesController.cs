using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevAssistant.Web.Controllers
{
    public sealed class FilesController : Controller
    {
        private readonly IAgentService _api;

        public FilesController(IAgentService api) => _api = api;

        public async Task<IActionResult> Index(
            [FromQuery] string path = ".",
            [FromQuery] string? selectedFile = null,
            CancellationToken ct = default)
        {
            var vm = await _api.GetFilesAsync(path, ct);

            if (selectedFile is not null)
            {
                var content = await _api.GetFileContentAsync(selectedFile, ct);
                return View(vm with { FileContent = content.Content, SelectedFile = selectedFile });
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Save(
            string path, string content, CancellationToken ct)
        {
            var success = await _api.WriteFileAsync(path, content, ct);
            TempData[success ? "Success" : "Error"] =
                success ? $"Saved {path}" : $"Failed to save {path}";
            return RedirectToAction(nameof(Index), new { selectedFile = path });
        }
    }
}
