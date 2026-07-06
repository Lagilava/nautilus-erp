namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Abstracts the system clock so time-dependent logic (token expiry, audit stamps)
/// is deterministic in tests. Implemented in Infrastructure.
/// </summary>
public interface IDateTime
{
    DateTimeOffset UtcNow { get; }
}
