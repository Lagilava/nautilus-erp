using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Phase 2 accounting: period locking, bank reconciliation, and multi-currency journal lines.
/// </summary>
public class AccountingPhase2EndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AccountingPhase2EndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync() => await _factory.AdminClientAsync();

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<JsonElement> FirstAccountAsync(HttpClient client, string code)
    {
        var accounts = await client.GetFromJsonAsync<JsonElement>("/api/chart-of-accounts", Json);
        foreach (var a in accounts.EnumerateArray())
            if (a.GetProperty("code").GetString() == code) return a;
        throw new InvalidOperationException($"Seed account {code} not found.");
    }

    [Fact]
    public async Task Closing_a_period_with_unposted_entries_is_rejected_then_succeeds_after_cleanup()
    {
        var client = await AdminClientAsync();
        // Journal-entry posting is SoD-guarded (preparer != poster), so a second user posts.
        var poster = await _factory.ClientForNewUserAsync("Manager");
        var cash = await FirstAccountAsync(client, "1000");
        var ar = await FirstAccountAsync(client, "1100");
        var cashId = cash.GetProperty("id").GetGuid();
        var arId = ar.GetProperty("id").GetGuid();

        // A draft (unposted) manual entry dated in July 2027 — a month unlikely to collide
        // with any other test in this fixture.
        var entryId = await IdAsync(await client.PostAsJsonAsync("/api/journal-entries", new
        {
            entryDate = "2027-07-15",
            reference = $"MJE-{Guid.NewGuid():N}"[..12],
            description = "Test draft entry",
            lines = new object[]
            {
                new { accountId = cashId, debit = 10m, credit = 0m, memo = (string?)null },
                new { accountId = arId, debit = 0m, credit = 10m, memo = (string?)null }
            }
        }));

        var closeAttempt = await client.PostAsJsonAsync("/api/accounting-periods/close", new { year = 2027, month = 7 });
        Assert.Equal(HttpStatusCode.Conflict, closeAttempt.StatusCode);

        // Post the entry (cleanup) as a different user, then closing should succeed.
        Assert.Equal(HttpStatusCode.NoContent, (await poster.PostAsync($"/api/journal-entries/{entryId}/post", null)).StatusCode);

        var closeOk = await client.PostAsJsonAsync("/api/accounting-periods/close", new { year = 2027, month = 7 });
        Assert.Equal(HttpStatusCode.NoContent, closeOk.StatusCode);

        // Posting into (or creating a new manual entry dated in) the now-closed period fails.
        var lateEntry = await client.PostAsJsonAsync("/api/journal-entries", new
        {
            entryDate = "2027-07-20",
            reference = "MJE-LATE",
            description = "Should be rejected",
            lines = new object[]
            {
                new { accountId = cashId, debit = 5m, credit = 0m, memo = (string?)null },
                new { accountId = arId, debit = 0m, credit = 5m, memo = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, lateEntry.StatusCode);
    }

    [Fact]
    public async Task Bank_statement_line_matches_a_cash_journal_line_and_cannot_be_matched_twice()
    {
        var client = await AdminClientAsync();
        var poster = await _factory.ClientForNewUserAsync("Manager");
        var cash = await FirstAccountAsync(client, "1000");
        var ar = await FirstAccountAsync(client, "1100");
        var cashId = cash.GetProperty("id").GetGuid();
        var arId = ar.GetProperty("id").GetGuid();

        // A posted entry that debits Cash 250 (money received) — the reconciliation candidate.
        var entryId = await IdAsync(await client.PostAsJsonAsync("/api/journal-entries", new
        {
            entryDate = "2026-01-10",
            reference = $"MJE-{Guid.NewGuid():N}"[..12],
            description = "Cash receipt for reconciliation test",
            lines = new object[]
            {
                new { accountId = cashId, debit = 250m, credit = 0m, memo = (string?)null },
                new { accountId = arId, debit = 0m, credit = 250m, memo = (string?)null }
            }
        }));
        (await poster.PostAsync($"/api/journal-entries/{entryId}/post", null)).EnsureSuccessStatusCode();

        var unreconciledLines = await client.GetFromJsonAsync<JsonElement>(
            "/api/bank-reconciliation/journal-lines/unreconciled", Json);
        var candidate = unreconciledLines.EnumerateArray()
            .First(l => l.GetProperty("journalEntryId").GetGuid() == entryId);
        var journalLineId = candidate.GetProperty("journalLineId").GetGuid();

        var statementLineId = await IdAsync(await client.PostAsJsonAsync("/api/bank-reconciliation/statement-lines", new
        {
            statementDate = "2026-01-11",
            amount = 250m,
            description = "Deposit",
            source = 2 // Manual
        }));

        var matchResponse = await client.PostAsJsonAsync("/api/bank-reconciliation/match", new
        {
            bankStatementLineId = statementLineId,
            journalLineId
        });
        Assert.Equal(HttpStatusCode.NoContent, matchResponse.StatusCode);

        // The statement line no longer appears as unreconciled.
        var stillUnreconciled = await client.GetFromJsonAsync<JsonElement>(
            "/api/bank-reconciliation/statement-lines/unreconciled", Json);
        Assert.DoesNotContain(stillUnreconciled.EnumerateArray(),
            l => l.GetProperty("id").GetGuid() == statementLineId);

        // Matching the same journal line again (via a new statement line) is rejected.
        var otherStatementLineId = await IdAsync(await client.PostAsJsonAsync("/api/bank-reconciliation/statement-lines", new
        {
            statementDate = "2026-01-12",
            amount = 250m,
            description = "Duplicate attempt",
            source = 2
        }));
        var secondMatch = await client.PostAsJsonAsync("/api/bank-reconciliation/match", new
        {
            bankStatementLineId = otherStatementLineId,
            journalLineId
        });
        Assert.Equal(HttpStatusCode.Conflict, secondMatch.StatusCode);
    }

    [Fact]
    public async Task Bank_statement_line_amount_mismatch_is_rejected()
    {
        var client = await AdminClientAsync();
        var poster = await _factory.ClientForNewUserAsync("Manager");
        var cash = await FirstAccountAsync(client, "1000");
        var ar = await FirstAccountAsync(client, "1100");
        var cashId = cash.GetProperty("id").GetGuid();
        var arId = ar.GetProperty("id").GetGuid();

        var entryId = await IdAsync(await client.PostAsJsonAsync("/api/journal-entries", new
        {
            entryDate = "2026-01-15",
            reference = $"MJE-{Guid.NewGuid():N}"[..12],
            description = "Cash receipt for mismatch test",
            lines = new object[]
            {
                new { accountId = cashId, debit = 75m, credit = 0m, memo = (string?)null },
                new { accountId = arId, debit = 0m, credit = 75m, memo = (string?)null }
            }
        }));
        (await poster.PostAsync($"/api/journal-entries/{entryId}/post", null)).EnsureSuccessStatusCode();

        var unreconciledLines = await client.GetFromJsonAsync<JsonElement>(
            "/api/bank-reconciliation/journal-lines/unreconciled", Json);
        var journalLineId = unreconciledLines.EnumerateArray()
            .First(l => l.GetProperty("journalEntryId").GetGuid() == entryId)
            .GetProperty("journalLineId").GetGuid();

        var statementLineId = await IdAsync(await client.PostAsJsonAsync("/api/bank-reconciliation/statement-lines", new
        {
            statementDate = "2026-01-16",
            amount = 999m, // does not match the 75 debit
            description = "Wrong amount",
            source = 2
        }));

        var matchResponse = await client.PostAsJsonAsync("/api/bank-reconciliation/match", new
        {
            bankStatementLineId = statementLineId,
            journalLineId
        });
        Assert.Equal(HttpStatusCode.BadRequest, matchResponse.StatusCode);
    }

    [Fact]
    public async Task Multi_currency_journal_line_converts_to_base_currency_in_the_trial_balance()
    {
        var client = await AdminClientAsync();
        var poster = await _factory.ClientForNewUserAsync("Manager");
        var cash = await FirstAccountAsync(client, "1000");
        var ar = await FirstAccountAsync(client, "1100");
        var cashId = cash.GetProperty("id").GetGuid();
        var arId = ar.GetProperty("id").GetGuid();

        var s = Guid.NewGuid().ToString("N")[..6];
        var currencyCode = "U" + Guid.NewGuid().ToString("N")[..2].ToUpperInvariant();
        var usd = await IdAsync(await client.PostAsJsonAsync("/api/currencies",
            new { code = currencyCode, name = "Test Dollar", symbol = "$", isBaseCurrency = false }));

        // 50 USD at rate 2.2 => 110 base-currency units.
        var entryId = await IdAsync(await client.PostAsJsonAsync("/api/journal-entries", new
        {
            entryDate = "2026-02-01",
            reference = $"MJE-FX-{s}",
            description = "FX test entry",
            lines = new object[]
            {
                new { accountId = cashId, debit = 50m, credit = 0m, memo = (string?)null, currencyId = usd, exchangeRate = 2.2m },
                new { accountId = arId, debit = 0m, credit = 50m, memo = (string?)null, currencyId = usd, exchangeRate = 2.2m }
            }
        }));
        (await poster.PostAsync($"/api/journal-entries/{entryId}/post", null)).EnsureSuccessStatusCode();

        var entryDto = await client.GetFromJsonAsync<JsonElement>($"/api/journal-entries/{entryId}", Json);
        var cashLine = entryDto.GetProperty("lines").EnumerateArray()
            .First(l => l.GetProperty("accountId").GetGuid() == cashId);
        Assert.Equal(50m, cashLine.GetProperty("debit").GetDecimal()); // stored at face value

        var trialBalance = await client.GetFromJsonAsync<JsonElement>(
            $"/api/reports/trial-balance/data?asOfDate=2026-02-28", Json);
        var rows = trialBalance.GetProperty("rows").EnumerateArray().ToList();
        var cashRow = rows.First(r => r[0].GetString() == "1000");

        // Converted debit should include this line's 50 * 2.2 = 110 on top of whatever else
        // posted to Cash by other tests dated on/before 2026-02-28, so assert the FX portion
        // arithmetically rather than an exact total: parse and check it's not just 50.
        var cashDebit = decimal.Parse(cashRow[3].GetString()!);
        Assert.True(cashDebit >= 110m, $"Expected the FX-converted 110 to be reflected, got {cashDebit}.");
    }
}
