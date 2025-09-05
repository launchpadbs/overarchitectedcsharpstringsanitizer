namespace FlashAssessment.Domain.Words;

public sealed class SensitiveWord
{
    public long SensitiveWordId { get; init; }
    public required string Word { get; init; }
    public required string NormalizedWord { get; init; }
    public string? Category { get; init; }
    public byte? Severity { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime CreatedUtc { get; init; }
    public required byte[] RowVersion { get; init; }
}


