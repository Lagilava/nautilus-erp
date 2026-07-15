using ERP.Domain.Accounting;
using ERP.Domain.Common;

namespace ERP.UnitTests.Domain;

public class BankStatementLineTests
{
    [Fact]
    public void Match_sets_matched_journal_line()
    {
        var line = new BankStatementLine { StatementDate = new DateOnly(2026, 7, 1), Amount = 100m };
        var journalLineId = Guid.NewGuid();

        line.Match(journalLineId);

        Assert.True(line.IsMatched);
        Assert.Equal(journalLineId, line.MatchedJournalLineId);
    }

    [Fact]
    public void Cannot_match_an_already_matched_line()
    {
        var line = new BankStatementLine { StatementDate = new DateOnly(2026, 7, 1), Amount = 100m };
        line.Match(Guid.NewGuid());

        Assert.Throws<DomainException>(() => line.Match(Guid.NewGuid()));
    }

    [Fact]
    public void Cannot_unmatch_a_line_that_is_not_matched()
    {
        var line = new BankStatementLine { StatementDate = new DateOnly(2026, 7, 1), Amount = 100m };
        Assert.Throws<DomainException>(() => line.Unmatch());
    }

    [Fact]
    public void Unmatch_clears_the_match_so_it_can_be_rematched()
    {
        var line = new BankStatementLine { StatementDate = new DateOnly(2026, 7, 1), Amount = 100m };
        line.Match(Guid.NewGuid());

        line.Unmatch();
        Assert.False(line.IsMatched);

        var secondJournalLineId = Guid.NewGuid();
        line.Match(secondJournalLineId); // does not throw now
        Assert.Equal(secondJournalLineId, line.MatchedJournalLineId);
    }
}
