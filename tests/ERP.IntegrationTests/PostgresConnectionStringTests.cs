using ERP.Persistence;

namespace ERP.IntegrationTests;

/// <summary>
/// Render, Heroku, Fly and Neon all hand out a postgres:// URL; Npgsql only understands key/value
/// pairs. Getting this wrong means the deployment cannot reach its database at all, so it is
/// worth pinning — especially the parts that are easy to lose: the port default, percent-encoded
/// passwords, and the TLS settings managed Postgres requires.
/// </summary>
public class PostgresConnectionStringTests
{
    [Fact]
    public void A_postgres_url_becomes_an_npgsql_key_value_string()
    {
        var result = PostgresConnectionString.Normalize(
            "postgresql://nautilus:s3cret@dpg-abc123.oregon-postgres.render.com:5432/nautilus_db");

        Assert.Contains("Host=dpg-abc123.oregon-postgres.render.com", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Database=nautilus_db", result);
        Assert.Contains("Username=nautilus", result);
        Assert.Contains("Password=s3cret", result);
        Assert.Contains("SSL Mode=Require", result);
    }

    [Fact]
    public void A_url_without_a_port_defaults_to_5432()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@host/db");
        Assert.Contains("Port=5432", result);
    }

    /// <summary>Generated passwords routinely contain characters that must be escaped in a URL.</summary>
    [Fact]
    public void A_percent_encoded_password_is_decoded()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p%40ss%2Fword@host:5432/db");
        Assert.Contains("Password=p@ss/word", result);
    }

    /// <summary>A hand-written key/value string must pass through untouched.</summary>
    [Fact]
    public void A_key_value_string_is_left_alone()
    {
        const string original = "Host=localhost;Port=5432;Database=ERP;Username=postgres;Password=postgres";
        Assert.Equal(original, PostgresConnectionString.Normalize(original));
    }
}
