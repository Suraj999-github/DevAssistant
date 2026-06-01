// src/DevAssistant.Web/Program.cs
using DevAssistant.Services;
using DevAssistant.Web.Services;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, svc, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(svc)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllersWithViews();

    // ── One line wires ALL Core services (KernelFactory, LlmChatService, etc.) ──
    builder.Services.AddAgentCore(builder.Configuration);

    // ── Web-layer adapter ─────────────────────────────────────────────────────
    builder.Services.AddScoped<IAgentService, AgentService>();
    builder.Services.AddScoped<IAgentApiClient, AgentApiClient>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
        app.UseExceptionHandler("/Home/Error");

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DevAssistant.Web crashed at startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}