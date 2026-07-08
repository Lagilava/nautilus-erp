namespace ERP.Persistence;

/// <summary>
/// Migrations are engine-specific — identity columns, <c>rowversion</c>, index and type syntax
/// all differ — so each provider owns a separate migration set. EF discovers migrations by
/// scanning an assembly for classes attributed to this context, which means two sets cannot live
/// in the same assembly without colliding. Hence one project per engine.
///
/// SQL Server's set stays in ERP.Persistence itself, where it has always lived, so existing
/// databases keep their <c>__EFMigrationsHistory</c> intact.
/// </summary>
public static class MigrationsAssemblies
{
    public const string Postgres = "ERP.Persistence.Migrations.Postgres";
}
