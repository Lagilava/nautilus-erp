namespace ERP.Application.Features.Sales;

/// <summary>
/// Builds human-readable document numbers like <c>SO-000123</c>. The caller supplies the
/// next sequence (typically a count of existing rows + 1). Simple and readable; a
/// gap-free, concurrency-safe sequence is a later hardening concern.
/// </summary>
internal static class DocumentNumber
{
    public static string For(string prefix, int sequence) => $"{prefix}-{sequence:D6}";
}
