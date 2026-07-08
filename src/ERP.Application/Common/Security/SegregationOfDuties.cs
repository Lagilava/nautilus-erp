using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Common.Security;

/// <summary>
/// The conflicting-duty pairs this system enforces. Each rule prevents one person from
/// performing two steps of the same transaction chain — the classic procure-to-pay fraud
/// loop is: raise a purchase order, approve it, "receive" the goods, approve the supplier's
/// bill, then pay it.
/// </summary>
public enum SoDRule
{
    /// <summary>You may not approve a purchase order you raised.</summary>
    PurchaseOrderApproval,

    /// <summary>You may not receive goods against a purchase order you raised or approved.</summary>
    GoodsReceipt,

    /// <summary>You may not approve a supplier invoice you entered, or one for goods you received.</summary>
    SupplierInvoiceApproval,

    /// <summary>You may not pay a supplier invoice you approved.</summary>
    SupplierPayment,

    /// <summary>You may not void a customer invoice you issued.</summary>
    InvoiceVoid,
}

/// <summary>
/// Configuration for segregation of duties. Enforced by default; a very small business may
/// disable individual rules (with the audit trail as compensating control) rather than being
/// unable to operate at all.
/// </summary>
public sealed class SoDOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Names of <see cref="SoDRule"/> values to switch off.</summary>
    public List<string> DisabledRules { get; set; } = [];
}

public interface ISegregationOfDuties
{
    /// <summary>
    /// Fails when the current user is one of <paramref name="conflictingActors"/> — the people
    /// who performed an earlier, conflicting step on this same document chain. Null/blank
    /// actors are ignored (e.g. records created by seeding or a background job).
    /// </summary>
    Result Ensure(SoDRule rule, string message, params string?[] conflictingActors);
}

internal sealed class SegregationOfDuties : ISegregationOfDuties
{
    private readonly ICurrentUserService _currentUser;
    private readonly SoDOptions _options;
    private readonly ILogger<SegregationOfDuties> _logger;

    public SegregationOfDuties(
        ICurrentUserService currentUser, SoDOptions options, ILogger<SegregationOfDuties> logger)
    {
        _currentUser = currentUser;
        _options = options;
        _logger = logger;
    }

    public Result Ensure(SoDRule rule, string message, params string?[] conflictingActors)
    {
        if (!_options.Enabled || _options.DisabledRules.Contains(rule.ToString(), StringComparer.OrdinalIgnoreCase))
            return Result.Success();

        var actor = _currentUser.UserId?.ToString();
        if (string.IsNullOrEmpty(actor))
            return Result.Success(); // system/background work has no conflicting duty

        var conflict = conflictingActors.Any(a => !string.IsNullOrEmpty(a) && a == actor);
        if (!conflict)
            return Result.Success();

        // A blocked attempt is itself a signal worth keeping.
        _logger.LogWarning(
            "Segregation of duties: user {UserId} blocked by rule {Rule}. {Message}", actor, rule, message);

        return Result.Failure(Error.Forbidden(message));
    }
}
