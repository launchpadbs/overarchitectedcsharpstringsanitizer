namespace FlashAssessment.Application.Words.Dto;

public sealed class CreateSensitiveWordRequestDto
{
    /// <summary>
    /// The word to mask when encountered during sanitization.
    /// </summary>
    public required string Word { get; init; }
    /// <summary>
    /// Optional category to group related words.
    /// </summary>
    public string? Category { get; init; }
    /// <summary>
    /// Optional severity score (0..5).
    /// </summary>
    public byte? Severity { get; init; }
    /// <summary>
    /// Whether the word is active (enabled) on creation.
    /// </summary>
    public bool? IsActive { get; init; }
}

public sealed class UpdateSensitiveWordRequestDto
{
    /// <summary>
    /// Updated word value.
    /// </summary>
    public required string Word { get; init; }
    /// <summary>
    /// Updated category (optional).
    /// </summary>
    public string? Category { get; init; }
    /// <summary>
    /// Updated severity (optional).
    /// </summary>
    public byte? Severity { get; init; }
    /// <summary>
    /// Whether the word remains active.
    /// </summary>
    public bool IsActive { get; init; } = true;
    /// <summary>
    /// Row version for optimistic concurrency.
    /// </summary>
    public required byte[] RowVersion { get; init; }
}

public sealed class SensitiveWordResponseDto
{
    /// <summary>
    /// Identifier of the word.
    /// </summary>
    public required long SensitiveWordId { get; init; }
    /// <summary>
    /// The word as entered (original casing).
    /// </summary>
    public required string Word { get; init; }
    /// <summary>
    /// Lower-cased invariant form used for matching.
    /// </summary>
    public required string NormalizedWord { get; init; }
    /// <summary>
    /// Optional category.
    /// </summary>
    public string? Category { get; init; }
    /// <summary>
    /// Optional severity score (0..5).
    /// </summary>
    public byte? Severity { get; init; }
    /// <summary>
    /// Whether the word is currently active.
    /// </summary>
    public bool IsActive { get; init; }
    /// <summary>
    /// Row version for optimistic concurrency.
    /// </summary>
    public required byte[] RowVersion { get; init; }
}

public sealed class ListWordsQueryDto
{
    /// <summary>
    /// Optional search across Word and Category.
    /// </summary>
    public string? Search { get; init; }
    /// <summary>
    /// Filter by active status.
    /// </summary>
    public bool? IsActive { get; init; }
    /// <summary>
    /// 1-based page number.
    /// </summary>
    public int Page { get; init; } = 1;
    /// <summary>
    /// Page size (capped by the server for safety).
    /// </summary>
    public int PageSize { get; init; } = 50;
}

public sealed class PagedResultDto<T>
{
    /// <summary>
    /// The items for the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }
    /// <summary>
    /// The total number of records matching the query.
    /// </summary>
    public required int TotalCount { get; init; }
}


