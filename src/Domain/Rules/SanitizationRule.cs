namespace FlashAssessment.Domain.Rules;

public sealed class SanitizationRule
{
    public long SanitizationRuleId { get; init; }
    public long WordId { get; init; }
    public bool WholeWordOnly { get; init; } = true;
    public bool CaseSensitive { get; init; } = false;
    public bool AllowInsideCompound { get; init; } = false;
    public bool IsActive { get; init; } = true;
    public required byte[] RowVersion { get; init; }
}


