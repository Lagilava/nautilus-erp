using ERP.Domain.Common;

namespace ERP.Domain.Organization;

/// <summary>
/// A stock-holding location belonging to a <see cref="Branch"/>. Inventory levels and
/// movements (Milestone 4) are tracked per warehouse.
/// </summary>
public class Warehouse : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;
}
