using ERP.Domain.Common;

namespace ERP.Domain.Documents;

/// <summary>
/// A file uploaded against another record (e.g. a supplier invoice PDF, a goods-receipt photo).
/// Attached polymorphically via <see cref="EntityType"/> + <see cref="EntityId"/> rather than a
/// foreign key per module, so any entity can carry attachments without a schema change.
/// The file bytes themselves live in file storage; this row is only the metadata index.
/// </summary>
public class Attachment : AuditableEntity
{
    /// <summary>Discriminator naming the owning entity, e.g. "SupplierInvoice", "GoodsReceipt".</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Opaque key used to retrieve the file from <see cref="IFileStorage"/>.</summary>
    public string StorageKey { get; set; } = string.Empty;
}
