using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Fiscalization;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Notifications;
using ERP.Infrastructure.Services;
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

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        // Fiji fiscalization boundary — stub until a verified FRCS/VMS adapter exists.
        services.AddScoped<IFiscalizationService, NullFiscalizationService>();

        // Report rendering (CSV/Excel/PDF) and tax-invoice documents.
        services.AddSingleton<IReportExporter, Reporting.ReportExporter>();
        services.AddSingleton<IInvoiceDocumentRenderer, Reporting.InvoiceDocumentRenderer>();

        AddJwtAuthentication(services, configuration);
        AddNotifications(services);

        return services;
    }

    private static void AddNotifications(IServiceCollection services)
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
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<EmailDispatchJob>();
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
