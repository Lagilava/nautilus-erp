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

    // CORS for the SPA frontend. Origins come from config ("Cors:AllowedOrigins");
    // AllowCredentials is required so the SignalR hub can authenticate.
    const string SpaCorsPolicy = "SpaCors";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? new[] { "http://localhost:5173", "http://localhost:3000" };
    builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    // API surface + OpenAPI/Swagger (with a bearer scheme so the UI can call secured endpoints).
    // Enums serialize as their names (e.g. "Issued") so the API is self-documenting for the SPA.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
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

    // Liveness/readiness, including a real database connectivity probe.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ERP.Persistence.ApplicationDbContext>("database");

    // Compress API responses (JSON/reports) for lower bandwidth.
    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

    // Rate limiting: stricter on the auth surface (credential-stuffing / brute-force
    // defence), lenient elsewhere. Partitioned per client IP. Limits are configurable.
    var authPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 20);
    var generalPerMinute = builder.Configuration.GetValue("RateLimiting:GeneralPerMinute", 200);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var isAuth = ctx.Request.Path.StartsWithSegments("/api/auth");
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                (isAuth ? "auth:" : "gen:") + ip,
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = isAuth ? authPerMinute : generalPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        });
    });

    // Behind Render/any reverse proxy, TLS terminates at the edge and the app is reached over
    // plain HTTP. Without this, Request.IsHttps is always false (redirect loop) and
    // Connection.RemoteIpAddress is the proxy — collapsing every client into ONE rate-limit
    // partition, which turns the brute-force defence below into a self-inflicted DoS.
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                                   | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;

        // The platform's proxy IP is not stable, so we cannot allowlist it. This trusts
        // X-Forwarded-* unconditionally, which is only safe because the edge overwrites those
        // headers. Never run this container directly exposed to the internet.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Fail fast in Production on unsafe bootstrap secrets rather than shipping the dev defaults.
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    {
        var signingKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
        if (signingKey.Length < 32 || signingKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a strong secret (>= 32 chars) supplied via secrets/env in non-Development environments.");

        // A deployment reachable from the internet must not be seeded with a password that
        // lives in source control. Either supply a real secret, or seed no administrator.
        var seedPassword = builder.Configuration["Seed:AdminPassword"];
        if (!string.IsNullOrEmpty(seedPassword) && (seedPassword.Length < 12 || seedPassword.Contains("Admin#")))
            throw new InvalidOperationException(
                "Seed:AdminPassword must be a strong secret supplied via secrets/env in non-Development environments, or omitted entirely.");

        // If a real SMTP host is configured, it must be configured completely — a half-set SMTP
        // section would otherwise fail silently at send time (inside a retried Hangfire job)
        // instead of at boot, and every password-reset/notification email would quietly vanish.
        var smtpHost = builder.Configuration["Smtp:Host"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            var smtpPassword = builder.Configuration["Smtp:Password"];
            var smtpFrom = builder.Configuration["Smtp:FromAddress"];
            if (string.IsNullOrEmpty(smtpPassword) || string.IsNullOrEmpty(smtpFrom))
                throw new InvalidOperationException(
                    "Smtp:Host is set, so Smtp:FromAddress and Smtp:Password (supplied via the Smtp__Password " +
                    "environment variable) are both required in non-Development environments.");
        }
    }

    var app = builder.Build();

    // Apply migrations and seed baseline data (roles + bootstrap admin) on startup.
    using (var scope = app.Services.CreateScope())
    {
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.MigrateAsync();
        await initialiser.SeedAsync();

        // Demo data: on by default in Development, and opt-in elsewhere. Set
        // "DemoData:Seed": false to start Development with an empty database.
        if (builder.Configuration.GetValue("DemoData:Seed", app.Environment.IsDevelopment()))
        {
            Log.Information("Seeding demo data for demonstration...");
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var demoLogger = loggerFactory.CreateLogger("DemoDataSeeder");
            await ERP.Persistence.DemoDataSeeder.SeedAsync(
                scope.ServiceProvider,
                demoLogger);
        }
    }

    // FIRST: everything downstream (HTTPS redirect, rate-limit partitioning, login-history IPs)
    // reads the scheme and client IP this middleware restores from the proxy's headers.
    app.UseForwardedHeaders();

    app.UseResponseCompression();
    app.UseExceptionHandler();

    // Baseline security headers on every response. Note these decorate API responses only —
    // the SPA's HTML is served by nginx, which carries its own CSP (see client/nginx.conf).
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // The API returns JSON, never markup, so a maximally restrictive policy costs nothing
        // and contains anything that manages to get reflected. Exempt Development, where the
        // same host serves the Swagger UI's own scripts and styles.
        if (!app.Environment.IsDevelopment())
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        await next();
    });

    // Rate limiting is disabled under the Testing environment so the auth-heavy integration
    // tests (many logins from one IP) stay deterministic.
    if (!app.Environment.IsEnvironment("Testing"))
        app.UseRateLimiter();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP Platform API v1"));
        // Friendly landing: the API has no root page, so point developers at Swagger.
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    }
    else
    {
        // Only enforce HTTPS outside Development; the dev http profile has no HTTPS port,
        // which otherwise logs "Failed to determine the https port for redirect".
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseCors(SpaCorsPolicy);

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ERP.Infrastructure.Notifications.NotificationsHub>("/hubs/notifications");

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
