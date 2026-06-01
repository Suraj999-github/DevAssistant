using DevAssistant.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

namespace DevAssistant.Web.Controllers
{
    public sealed class ChatController : Controller
    {
        private readonly IAgentService _agent;
        private readonly ILogger<ChatController> _logger;

        // Per-session chat history — in a real app, store in IDistributedCache or session
        private static readonly Dictionary<string, ChatHistory> _sessions = new();

        public ChatController(IAgentService agent, ILogger<ChatController> logger)
        {
            _agent = agent;
            _logger = logger;
        }

        public IActionResult Index() => View();

        [HttpGet("/chat/stream")]
        public async Task Stream(
            [FromQuery] string message,
            [FromQuery] string? systemPrompt,
            [FromQuery] string? sessionId,
            CancellationToken ct)
        {
            _logger.LogInformation("[Chat] Stream: {Message}", message);

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no");

            try
            {
                await foreach (var token in _agent.StreamChatAsync(message, systemPrompt, ct))
                {
                    var payload = $"data: {JsonSerializer.Serialize(new { token })}\n\n";
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), ct);
                    await Response.Body.FlushAsync(ct);
                }

                await Response.Body.WriteAsync(
                    Encoding.UTF8.GetBytes("data: [DONE]\n\n"), ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Chat] Client disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Chat] Stream error");
                var err = $"data: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(err), ct);
            }
        }
    }
}
