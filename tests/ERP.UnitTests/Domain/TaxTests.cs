using ERP.Domain.Taxation;

namespace ERP.UnitTests.Domain;

public class TaxTests
{
    private static Tax StandardVatWithHistory()
    {
        var tax = new Tax { Code = "VAT", Name = "Fiji VAT", Treatment = TaxTreatment.Standard };
        tax.Rates.Add(new TaxRate
        {
            Percentage = 12.5m,
            EffectiveFrom = new DateOnly(2016, 1, 1),
            EffectiveTo = new DateOnly(2022, 3, 31)
        });
        tax.Rates.Add(new TaxRate
        {
            Percentage = 15.0m,
            EffectiveFrom = new DateOnly(2022, 4, 1)
        });
        return tax;
    }

    [Fact]
    public void GetRateOn_returns_the_rate_in_force_on_the_date()
    {
        var tax = StandardVatWithHistory();

        Assert.Equal(12.5m, tax.GetRateOn(new DateOnly(2020, 6, 1)));
        Assert.Equal(15.0m, tax.GetRateOn(new DateOnly(2026, 7, 7)));
    }

    [Fact]
    public void GetRateOn_returns_zero_before_any_rate_applies()
        => Assert.Equal(0m, StandardVatWithHistory().GetRateOn(new DateOnly(2010, 1, 1)));

    [Fact]
    public void GetRateOn_is_zero_for_zero_rated_and_exempt_regardless_of_rows()
    {
        var zeroRated = new Tax { Treatment = TaxTreatment.ZeroRated };
        zeroRated.Rates.Add(new TaxRate { Percentage = 99m, EffectiveFrom = new DateOnly(2000, 1, 1) });

        var exempt = new Tax { Treatment = TaxTreatment.Exempt };

        Assert.Equal(0m, zeroRated.GetRateOn(new DateOnly(2026, 1, 1)));
        Assert.Equal(0m, exempt.GetRateOn(new DateOnly(2026, 1, 1)));
    }
}
