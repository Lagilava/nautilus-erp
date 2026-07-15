using ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Accounting;

public sealed class AccountConfiguration : AuditableEntityConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);
        builder.ToTable("Accounts");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).HasConversion<int>();
    }
}

public sealed class JournalEntryConfiguration : AuditableEntityConfiguration<JournalEntry>
{
    public override void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("JournalEntries");

        builder.Property(x => x.Reference).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.PreparedBy).HasMaxLength(64);
        builder.Property(x => x.PostedBy).HasMaxLength(64);

        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.EntryDate);
        builder.HasIndex(x => x.SourceDocumentId);
        builder.Ignore(x => x.TotalDebits);
        builder.Ignore(x => x.TotalCredits);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Debit).HasPrecision(18, 2);
        builder.Property(x => x.Credit).HasPrecision(18, 2);
        builder.Property(x => x.Memo).HasMaxLength(500);
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 6);

        builder.HasIndex(x => x.AccountId);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class AccountingPeriodConfiguration : AuditableEntityConfiguration<AccountingPeriod>
{
    public override void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        base.Configure(builder);
        builder.ToTable("AccountingPeriods");

        builder.Property(x => x.ClosedBy).HasMaxLength(64);
        builder.HasIndex(x => new { x.Year, x.Month }).IsUnique();
        builder.Ignore(x => x.StartDate);
        builder.Ignore(x => x.EndDate);
    }
}

public sealed class BankStatementLineConfiguration : AuditableEntityConfiguration<BankStatementLine>
{
    public override void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        base.Configure(builder);
        builder.ToTable("BankStatementLines");

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Source).HasConversion<int>();

        builder.HasIndex(x => x.StatementDate);
        builder.HasIndex(x => x.MatchedJournalLineId);
        builder.Ignore(x => x.IsMatched);
    }
}

public sealed class ReconciliationConfiguration : AuditableEntityConfiguration<Reconciliation>
{
    public override void Configure(EntityTypeBuilder<Reconciliation> builder)
    {
        base.Configure(builder);
        builder.ToTable("Reconciliations");

        builder.Property(x => x.MatchedBy).HasMaxLength(64);
        builder.HasIndex(x => x.BankStatementLineId);
        builder.HasIndex(x => x.MatchedJournalLineId);
    }
}
