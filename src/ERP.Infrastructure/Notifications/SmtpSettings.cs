namespace ERP.Infrastructure.Notifications;

/// <summary>
/// SMTP configuration, bound from the "Smtp" section. Only <see cref="Host"/> is required for
/// the app to switch from the logging stub to a real sender — see
/// <see cref="DependencyInjection.AddNotifications"/>. <see cref="Password"/> is a secret and
/// must be supplied as an environment variable (Smtp__Password), never committed to
/// appsettings.json, matching how Jwt:SigningKey and Seed:AdminPassword are handled.
/// </summary>
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Nautilus ERP";

    /// <summary>STARTTLS on 587 (the common case), implicit TLS on 465, or none for local relays.</summary>
    public bool UseStartTls { get; set; } = true;
}
