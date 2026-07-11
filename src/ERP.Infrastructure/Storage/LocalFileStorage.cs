using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Storage;

/// <summary>
/// Stores files on local disk under the content root. Suitable for a single-instance
/// deployment; a multi-instance or ephemeral-filesystem deployment (e.g. most PaaS hosts)
/// needs a cloud provider (S3-compatible/Azure Blob) implementing the same <see cref="IFileStorage"/>
/// instead — see <see cref="DependencyInjection.AddStorage"/> for the config switch.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageSettings> settings, IWebHostEnvironment env)
    {
        var path = settings.Value.LocalPath;
        _root = Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        // The storage key is server-generated, never derived from the caller-supplied file
        // name, so there is no path-traversal surface from an attacker-chosen name.
        var extension = Path.GetExtension(fileName);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_root, storageKey);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);

        return storageKey;
    }

    public Task<StoredFile> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveExistingPath(storageKey);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(new StoredFile(stream, "application/octet-stream", storageKey));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a storage key to an on-disk path, rejecting any key that would escape the
    /// storage root (defense in depth — keys are server-generated, but never trust a path
    /// join with input that ultimately traces back to a client-suppliable id).
    /// </summary>
    private string ResolvePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!fullPath.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid storage key.");
        return fullPath;
    }

    private string ResolveExistingPath(string storageKey)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Stored file not found.", storageKey);
        return fullPath;
    }
}
