using ERP.Domain.Common;
using ERP.Domain.Sales;

namespace ERP.UnitTests.Domain;

public class InvoiceTests
{
    private static Invoice DraftWithLine(decimal qty = 10, decimal price = 5m, decimal rate = 15m)
    {
        var invoice = new Invoice { IssueDate = new DateOnly(2026, 7, 1) };
        invoice.AddLine(Guid.NewGuid(), "Widget", qty, price, rate);
        return invoice;
    }

    [Fact]
    public void Totals_apply_tax_per_line()
    {
        var invoice = DraftWithLine(qty: 10, price: 5m, rate: 15m); // net 50, tax 7.50, total 57.50

        Assert.Equal(50m, invoice.SubTotal);
        Assert.Equal(7.50m, invoice.TaxTotal);
        Assert.Equal(57.50m, invoice.Total);
        Assert.Equal(57.50m, invoice.Balance);
    }

    [Fact]
    public void Cannot_issue_an_empty_invoice()
    {
        var invoice = new Invoice { IssueDate = new DateOnly(2026, 7, 1) };
        Assert.Throws<DomainException>(() => invoice.Issue());
    }

    [Fact]
    public void Cannot_edit_after_issue()
    {
        var invoice = DraftWithLine();
        invoice.Issue();
        Assert.Throws<DomainException>(() => invoice.AddLine(Guid.NewGuid(), "x", 1, 1, 0));
    }

    [Fact]
    public void Partial_then_full_payment_advances_status_and_balance()
    {
        var invoice = DraftWithLine(qty: 10, price: 10m, rate: 0m); // total 100
        invoice.Issue();

        invoice.ApplyPayment(40m);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(60m, invoice.Balance);

        invoice.ApplyPayment(60m);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.Balance);
    }

    [Fact]
    public void Cannot_overpay()
    {
        var invoice = DraftWithLine(qty: 1, price: 10m, rate: 0m); // total 10
        invoice.Issue();
        Assert.Throws<DomainException>(() => invoice.ApplyPayment(11m));
    }

    [Fact]
    public void Cannot_pay_a_draft_invoice()
    {
        var invoice = DraftWithLine();
        Assert.Throws<DomainException>(() => invoice.ApplyPayment(1m));
    }

    [Fact]
    public void Cannot_void_after_payment()
    {
        var invoice = DraftWithLine(qty: 1, price: 10m, rate: 0m);
        invoice.Issue();
        invoice.ApplyPayment(5m);
        Assert.Throws<DomainException>(() => invoice.Void());
    }

    [Fact]
    public void Fiscal_status_defaults_to_not_submitted()
        => Assert.Equal(FiscalStatus.NotSubmitted, DraftWithLine().FiscalStatus);
}
