namespace ERP.Persistence;

/// <summary>
/// Managed Postgres providers (Render, Heroku, Fly, Neon) hand you a URL —
/// <c>postgresql://user:pass@host:5432/dbname</c> — but Npgsql expects key/value pairs. This
/// translates the former into the latter and passes anything else through untouched, so the
/// same configuration key works whether you paste the platform's URL or write your own string.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;

        var isUrl = connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                    || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        if (!isUrl) return connectionString;

        var uri = new Uri(connectionString);
        var credentials = uri.UserInfo.Split(':', 2);

        var username = Uri.UnescapeDataString(credentials[0]);
        var password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        // Managed Postgres requires TLS but presents a certificate the client cannot chain to a
        // known root, so verification must be relaxed while transport stays encrypted.
        return $"Host={uri.Host};Port={port};Database={database};Username={username};" +
               $"Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
}
