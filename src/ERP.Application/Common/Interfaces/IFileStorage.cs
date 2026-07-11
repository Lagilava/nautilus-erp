namespace ERP.Application.Common.Interfaces;

/// <summary>A stored file's bytes plus the metadata needed to stream it back.</summary>
public sealed record StoredFile(Stream Content, string ContentType, string FileName);

/// <summary>
/// Persists and retrieves file content, keyed by an opaque storage key the caller doesn't
/// need to interpret. Implemented over the local disk in Infrastructure today; a cloud
/// provider (S3-compatible, Azure Blob) can be swapped in later via the same interface —
/// see <see cref="DependencyInjection"/> for the config-switched selection.
/// </summary>
public interface IFileStorage
{
    /// <summary>Saves the stream under a new, storage-generated key and returns that key.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<StoredFile> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
