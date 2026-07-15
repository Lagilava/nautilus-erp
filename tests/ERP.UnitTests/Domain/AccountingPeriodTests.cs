using ERP.Domain.Accounting;
using ERP.Domain.Common;

namespace ERP.UnitTests.Domain;

public class AccountingPeriodTests
{
    [Fact]
    public void Close_sets_closed_flag_and_metadata()
    {
        var period = new AccountingPeriod { Year = 2026, Month = 7 };
        var closedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        period.Close("admin@erp.local", closedAt);

        Assert.True(period.IsClosed);
        Assert.Equal("admin@erp.local", period.ClosedBy);
        Assert.Equal(closedAt, period.ClosedAt);
    }

    [Fact]
    public void Cannot_close_an_already_closed_period()
    {
        var period = new AccountingPeriod { Year = 2026, Month = 7 };
        period.Close("admin@erp.local", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => period.Close("someone-else", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Contains_matches_dates_within_the_calendar_month()
    {
        var period = new AccountingPeriod { Year = 2026, Month = 7 };

        Assert.True(period.Contains(new DateOnly(2026, 7, 1)));
        Assert.True(period.Contains(new DateOnly(2026, 7, 31)));
        Assert.False(period.Contains(new DateOnly(2026, 6, 30)));
        Assert.False(period.Contains(new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void Reopen_throws_unless_closed()
    {
        var period = new AccountingPeriod { Year = 2026, Month = 7 };
        Assert.Throws<DomainException>(() => period.Reopen());
    }

    [Fact]
    public void Reopen_clears_closed_metadata()
    {
        var period = new AccountingPeriod { Year = 2026, Month = 7 };
        period.Close("admin@erp.local", DateTimeOffset.UtcNow);

        period.Reopen();

        Assert.False(period.IsClosed);
        Assert.Null(period.ClosedBy);
        Assert.Null(period.ClosedAt);
    }
}
