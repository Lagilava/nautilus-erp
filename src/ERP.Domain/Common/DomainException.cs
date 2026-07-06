namespace ERP.Domain.Common;

/// <summary>
/// Raised when an operation violates a domain invariant or an illegal state transition is
/// attempted (e.g. paying a draft invoice). The application layer maps these to business
/// failures rather than 500s.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
