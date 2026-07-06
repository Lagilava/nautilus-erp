using ERP.API.Common;
using ERP.Application;
using ERP.Infrastructure;
using ERP.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

// Bootstrap logger so failures during startup are captured before the full config is read.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ERP API host");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog reads its sinks (console + rolling file) from configuration.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Application + Infrastructure + Persistence composition.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence(builder.Configuration);

    // RFC 7807 problem-details for consistent error payloads.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // API surface + OpenAPI/Swagger (with a bearer scheme so the UI can call secured endpoints).
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var scheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };
        options.AddSecurityDefinition("Bearer", scheme);
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            [scheme] = Array.Empty<string>()
        });
    });

    // Liveness/readiness. Data-store checks are added in later milestones once the
    // persistence layer exists; keeping the wiring here means we only extend, never bolt on.
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Apply migrations and seed baseline data (roles + bootstrap admin) on startup.
    using (var scope = app.Services.CreateScope())
    {
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.MigrateAsync();
        await initialiser.SeedAsync();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP Platform API v1"));
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Simple health endpoint returning a small JSON payload rather than the default plaintext.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
                totalDurationMs = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(payload);
        }
    });

    app.Run();
}
// HostAbortedException is thrown intentionally by WebApplicationFactory (integration
// tests) and EF design-time tooling after configuration — it must not be treated as a crash.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ERP API host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed so ERP.IntegrationTests can bootstrap the host via WebApplicationFactory<Program>.
public partial class Program { }
