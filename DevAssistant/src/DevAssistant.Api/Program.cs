using DevAssistant.Api.Configuration;
using DevAssistant.Api.Services;
using DevAssistant.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ─── Bootstrap Serilog before the host builds ────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("═══════════════════════════════════════════════════");
    Log.Information("   Dev Assistant Agent — Environment Check          ");
    Log.Information("═══════════════════════════════════════════════════");

    // ─── Build the .NET Generic Host ─────────────────────────────────────────
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        .ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json",
                optional: true, reloadOnChange: true);
            cfg.AddEnvironmentVariables();
        })
        .ConfigureServices((ctx, services) =>
        {
            // Register strongly-typed options
            services.Configure<AgentOptions>(
                ctx.Configuration.GetSection(AgentOptions.SectionName));

            // Register our services
            services.AddSingleton<IKernelFactory, KernelFactory>();
            services.AddSingleton<ILlmChatService, LlmChatService>();

            // Register a named HttpClient for Ollama health checks
            services.AddHttpClient("ollama", (sp, client) =>
            {
                var options = ctx.Configuration
                    .GetSection(AgentOptions.SectionName)
                    .Get<AgentOptions>()!;
                client.BaseAddress = options.OllamaUri;
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // Register a named HttpClient for Qdrant health checks
            services.AddHttpClient("qdrant", (sp, client) =>
            {
                var options = ctx.Configuration
                    .GetSection(AgentOptions.SectionName)
                    .Get<AgentOptions>()!;
                client.BaseAddress = options.QdrantUri;
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // Register the startup health checker
            services.AddTransient<EnvironmentHealthChecker>();

            // The demo runner
            services.AddTransient<Step2Demo>();
        })
        .Build();

    // ─── Run the health check ─────────────────────────────────────────────────
    var checker = host.Services.GetRequiredService<EnvironmentHealthChecker>();
    await checker.RunAsync();

    var demo = host.Services.GetRequiredService<Step2Demo>();
    await demo.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
