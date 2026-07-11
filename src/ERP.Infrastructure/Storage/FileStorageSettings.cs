namespace ERP.Infrastructure.Storage;

/// <summary>Binds the "Storage" configuration section.</summary>
public sealed class FileStorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root directory files are written under when using the local-disk provider. Relative
    /// paths are resolved against the content root. Ignored by non-local providers.
    /// </summary>
    public string LocalPath { get; set; } = "App_Data/uploads";
}
