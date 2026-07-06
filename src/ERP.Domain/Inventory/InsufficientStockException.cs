namespace ERP.Domain.Inventory;

/// <summary>
/// Thrown when an issue/adjustment/transfer would drive on-hand quantity negative.
/// A domain invariant violation, mapped to a business failure by the application layer.
/// </summary>
public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(decimal requested, decimal available)
        : base($"Insufficient stock: requested {requested}, available {available}.")
    {
        Requested = requested;
        Available = available;
    }

    public decimal Requested { get; }
    public decimal Available { get; }
}
