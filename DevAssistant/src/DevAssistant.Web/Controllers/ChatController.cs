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

        //[HttpGet("/chat/stream")]
        //public async Task Stream(
        //    [FromQuery] string message,
        //    [FromQuery] string? systemPrompt,
        //    [FromQuery] string? sessionId,
        //    CancellationToken ct)
        //{
        //    _logger.LogInformation("[Chat] Stream: {Message}", message);

        //    Response.Headers.Append("Content-Type", "text/event-stream");
        //    Response.Headers.Append("Cache-Control", "no-cache");
        //    Response.Headers.Append("X-Accel-Buffering", "no");

        //    try
        //    {
        //        await foreach (var token in _agent.StreamChatAsync(message, systemPrompt, ct))
        //        {
        //            var payload = $"data: {JsonSerializer.Serialize(new { token })}\n\n";
        //            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), ct);
        //            await Response.Body.FlushAsync(ct);
        //        }

        //        await Response.Body.WriteAsync(
        //            Encoding.UTF8.GetBytes("data: [DONE]\n\n"), ct);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.LogInformation("[Chat] Client disconnected");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "[Chat] Stream error");
        //        var err = $"data: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
        //        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(err), ct);
        //    }
        //}
        // src/DevAssistant.Web/Controllers/ChatController.cs
        [HttpGet("/chat/stream")]
        public async Task Stream(
            [FromQuery] string message,
            [FromQuery] string? systemPrompt,
            CancellationToken browserCt)
        {
            _logger.LogInformation("[Chat] Stream: {Message}", message);

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no");
            Response.Headers.Append("Connection", "keep-alive");

            // ── Agent runs with its own token — completely independent ────────────
            using var agentCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            _logger.LogInformation(
                "[Chat] Starting background agent task (independent of HTTP request)");

            var agentTask = Task.Run(
                () => _agent.CollectResponseAsync(message, systemPrompt, agentCts.Token),
                CancellationToken.None);   // ← Task.Run itself not cancellable

            // ── Keepalive loop — runs until agent completes or browser drops ──────
            try
            {
                while (!agentTask.IsCompleted)
                {
                    // Send SSE comment — browsers ignore it but it keeps TCP alive
                    var keepalive = Encoding.UTF8.GetBytes(": keepalive\n\n");
                    await Response.Body.WriteAsync(keepalive, browserCt);
                    await Response.Body.FlushAsync(browserCt);

                    _logger.LogDebug("[Chat] Keepalive sent — agent still running");

                    await Task.Delay(TimeSpan.FromSeconds(15), browserCt)
                        .ContinueWith(_ => { }); // absorb cancellation
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "[Chat] Browser disconnected during keepalive — agent continues in background");
            }

            // ── Agent finished — stream the result ───────────────────────────────
            if (agentTask.IsCompletedSuccessfully)
            {
                var response = await agentTask;
                _logger.LogInformation(
                    "[Chat] Agent complete — streaming {Len} chars to browser",
                    response.Length);

                const int chunkSize = 40;
                for (var i = 0; i < response.Length; i += chunkSize)
                {
                    var chunk = response[i..Math.Min(i + chunkSize, response.Length)];
                    var payload = $"data: {JsonSerializer.Serialize(new { token = chunk })}\n\n";
                    try
                    {
                        await Response.Body.WriteAsync(
                            Encoding.UTF8.GetBytes(payload), CancellationToken.None);
                        await Response.Body.FlushAsync(CancellationToken.None);
                    }
                    catch
                    {
                        break; // browser disconnected during streaming — ok
                    }
                    await Task.Delay(8, CancellationToken.None);
                }
            }
            else if (agentTask.IsFaulted)
            {
                _logger.LogError(agentTask.Exception, "[Chat] Agent task faulted");
                var err = $"data: {JsonSerializer.Serialize(new { error = agentTask.Exception?.Message })}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(err), CancellationToken.None);
            }

            await Response.Body.WriteAsync(
                Encoding.UTF8.GetBytes("data: [DONE]\n\n"), CancellationToken.None);
        }
        private async Task StreamResponseAsync(string response, CancellationToken ct)
        {
            const int chunkSize = 40;
            for (var i = 0; i < response.Length; i += chunkSize)
            {
                if (ct.IsCancellationRequested) break;
                var chunk = response[i..Math.Min(i + chunkSize, response.Length)];
                var payload = $"data: {JsonSerializer.Serialize(new { token = chunk })}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), ct);
                await Response.Body.FlushAsync(ct);
                await Task.Delay(8, ct);
            }
        }
    }
}
