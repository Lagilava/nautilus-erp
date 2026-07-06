using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>System-clock implementation of <see cref="IDateTime"/>.</summary>
public sealed class DateTimeService : IDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
