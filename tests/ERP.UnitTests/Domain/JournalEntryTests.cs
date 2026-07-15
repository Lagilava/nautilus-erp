using ERP.Domain.Accounting;
using ERP.Domain.Common;

namespace ERP.UnitTests.Domain;

public class JournalEntryTests
{
    private static JournalEntry DraftEntry() => new()
    {
        EntryDate = new DateOnly(2026, 7, 1),
        Reference = "Manual"
    };

    [Fact]
    public void Post_succeeds_when_debits_equal_credits()
    {
        var entry = DraftEntry();
        entry.AddLine(Guid.NewGuid(), 100m, 0, "Debit");
        entry.AddLine(Guid.NewGuid(), 0, 100m, "Credit");

        entry.Post("poster@erp.local");

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal("poster@erp.local", entry.PostedBy);
    }

    [Fact]
    public void Post_throws_when_debits_do_not_equal_credits()
    {
        var entry = DraftEntry();
        entry.AddLine(Guid.NewGuid(), 100m, 0, "Debit");
        entry.AddLine(Guid.NewGuid(), 0, 90m, "Credit");

        Assert.Throws<DomainException>(() => entry.Post());
    }

    [Fact]
    public void Post_throws_when_already_posted()
    {
        var entry = DraftEntry();
        entry.AddLine(Guid.NewGuid(), 50m, 0);
        entry.AddLine(Guid.NewGuid(), 0, 50m);
        entry.Post();

        Assert.Throws<DomainException>(() => entry.Post());
    }

    [Fact]
    public void Post_throws_when_no_lines()
    {
        var entry = DraftEntry();
        Assert.Throws<DomainException>(() => entry.Post());
    }

    [Fact]
    public void AddLine_rejects_line_with_both_debit_and_credit()
    {
        var entry = DraftEntry();
        Assert.Throws<DomainException>(() => entry.AddLine(Guid.NewGuid(), 10m, 10m));
    }

    [Fact]
    public void AddLine_rejects_line_with_neither_debit_nor_credit()
    {
        var entry = DraftEntry();
        Assert.Throws<DomainException>(() => entry.AddLine(Guid.NewGuid(), 0, 0));
    }

    [Fact]
    public void Cannot_add_lines_after_posting()
    {
        var entry = DraftEntry();
        entry.AddLine(Guid.NewGuid(), 10m, 0);
        entry.AddLine(Guid.NewGuid(), 0, 10m);
        entry.Post();

        Assert.Throws<DomainException>(() => entry.AddLine(Guid.NewGuid(), 5m, 0));
    }

    [Fact]
    public void Void_throws_unless_posted()
    {
        var entry = DraftEntry();
        Assert.Throws<DomainException>(() => entry.Void());
    }

    [Fact]
    public void Void_marks_entry_voided_without_mutating_lines()
    {
        var entry = DraftEntry();
        entry.AddLine(Guid.NewGuid(), 20m, 0);
        entry.AddLine(Guid.NewGuid(), 0, 20m);
        entry.Post();

        var reversal = entry.BuildReversal(new DateOnly(2026, 7, 2));
        entry.Void();

        Assert.Equal(JournalEntryStatus.Voided, entry.Status);
        Assert.Equal(20m, entry.TotalDebits); // original lines untouched
        Assert.Equal(entry.Id, reversal.ReversalOfJournalEntryId);
        Assert.Equal(entry.Lines.Single(l => l.Debit > 0).AccountId,
            reversal.Lines.Single(l => l.Credit > 0).AccountId);
    }

    [Fact]
    public void Reversal_flips_debits_and_credits()
    {
        var entry = DraftEntry();
        var accA = Guid.NewGuid();
        var accB = Guid.NewGuid();
        entry.AddLine(accA, 30m, 0);
        entry.AddLine(accB, 0, 30m);
        entry.Post();

        var reversal = entry.BuildReversal(new DateOnly(2026, 7, 2));

        Assert.Equal(30m, reversal.Lines.Single(l => l.AccountId == accA).Credit);
        Assert.Equal(30m, reversal.Lines.Single(l => l.AccountId == accB).Debit);
        Assert.Equal(entry.TotalDebits, reversal.TotalCredits);
        Assert.Equal(entry.TotalCredits, reversal.TotalDebits);
    }
}
