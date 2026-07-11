using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Fiscalization;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Notifications;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Storage;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure;

/// <summary>Registers infrastructure services: clock, current-user, JWT tokens, and bearer authentication.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IAppUrls, AppUrls>();

        // Segregation-of-duties policy ("Sod" section). Enforced unless explicitly relaxed.
        services.AddSingleton(configuration.GetSection("Sod").Get<ERP.Application.Common.Security.SoDOptions>()
                              ?? new ERP.Application.Common.Security.SoDOptions());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        // Fiji fiscalization boundary — stub until a verified FRCS/VMS adapter exists.
        services.AddScoped<IFiscalizationService, NullFiscalizationService>();

        // Report rendering (CSV/Excel/PDF) and tax-invoice documents.
        services.AddSingleton<IReportExporter, Reporting.ReportExporter>();
        services.AddSingleton<IInvoiceDocumentRenderer, Reporting.InvoiceDocumentRenderer>();

        AddJwtAuthentication(services, configuration);
        AddNotifications(services, configuration);
        AddStorage(services, configuration);

        return services;
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        // Local-disk provider today. A cloud provider (S3-compatible/Azure Blob) can be
        // selected here later by branching on a "Storage:Provider" setting, the same way
        // AddNotifications branches on "Smtp:Host" — callers depend only on IFileStorage.
        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
    }

    private static void AddNotifications(IServiceCollection services, IConfiguration configuration)
    {
        // Real-time notifications over SignalR.
        services.AddSignalR();
        services.AddScoped<IRealtimeNotifier, SignalRNotificationPublisher>();

        // Background email queue via Hangfire (in-memory storage — no external dependency).
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
        services.AddHangfireServer();

        services.AddScoped<IEmailQueue, HangfireEmailQueue>();
        services.AddScoped<EmailDispatchJob>();

        // Real SMTP delivery when "Smtp:Host" is configured; otherwise the logging stub, which
        // keeps local development free of an external dependency.
        services.Configure<Notifications.SmtpSettings>(configuration.GetSection(Notifications.SmtpSettings.SectionName));
        if (!string.IsNullOrWhiteSpace(configuration[$"{Notifications.SmtpSettings.SectionName}:Host"]))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, LoggingEmailSender>();
    }

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
    }
}
