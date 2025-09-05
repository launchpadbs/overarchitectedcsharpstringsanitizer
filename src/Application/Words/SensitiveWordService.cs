using System.Data;
using FlashAssessment.Application.Common;
using FlashAssessment.Application.Words.Dto;
using FlashAssessment.Domain.Words;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FlashAssessment.Application.Words;

public sealed class SensitiveWordService : ISensitiveWordService
{
    private readonly ISensitiveWordRepository _repository;
    private readonly IValidator<CreateSensitiveWordRequestDto> _createValidator;
    private readonly IValidator<UpdateSensitiveWordRequestDto> _updateValidator;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<SensitiveWordService> _logger;
    private readonly IActiveWordsProvider _activeWordsProvider;

    public SensitiveWordService(
        ISensitiveWordRepository repository,
        IValidator<CreateSensitiveWordRequestDto> createValidator,
        IValidator<UpdateSensitiveWordRequestDto> updateValidator,
        ISqlConnectionFactory connectionFactory,
        ILogger<SensitiveWordService> logger,
        IActiveWordsProvider activeWordsProvider)
    {
        _repository = repository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _activeWordsProvider = activeWordsProvider;
    }

    public async Task<long> CreateAsync(CreateSensitiveWordRequestDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = new SensitiveWord
        {
            Word = dto.Word,
            NormalizedWord = dto.Word.ToLowerInvariant(),
            Category = dto.Category,
            Severity = dto.Severity,
            IsActive = dto.IsActive ?? true,
            CreatedUtc = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };

        using var conn = _connectionFactory.CreateOpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var id = await _repository.CreateAsync(entity, tx, cancellationToken);
            tx.Commit();
            _logger.LogInformation("Created SensitiveWord {Id}", id);
            await _activeWordsProvider.InvalidateAsync();
            return id;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* ignore rollback failures */ }
            throw;
        }
    }

    public async Task<SensitiveWordResponseDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<PagedResultDto<SensitiveWordResponseDto>> ListAsync(ListWordsQueryDto query, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _repository.ListAsync(query.Search, query.IsActive, CapPage(query.Page), CapPageSize(query.PageSize), cancellationToken);
        return new PagedResultDto<SensitiveWordResponseDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = total
        };
    }

    public async Task UpdateAsync(long id, UpdateSensitiveWordRequestDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = new SensitiveWord
        {
            SensitiveWordId = id,
            Word = dto.Word,
            NormalizedWord = dto.Word.ToLowerInvariant(),
            Category = dto.Category,
            Severity = dto.Severity,
            IsActive = dto.IsActive,
            CreatedUtc = DateTime.UtcNow, // not used on update
            RowVersion = dto.RowVersion
        };

        using var conn = _connectionFactory.CreateOpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            await _repository.UpdateAsync(entity, dto.RowVersion, tx, cancellationToken);
            tx.Commit();
            _logger.LogInformation("Updated SensitiveWord {Id}", id);
            await _activeWordsProvider.InvalidateAsync();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var conn = _connectionFactory.CreateOpenConnection();
        using var tx = conn.BeginTransaction();
        bool deleted;
        try
        {
            deleted = await _repository.DeleteAsync(id, tx, cancellationToken);
            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
        if (deleted)
        {
            _logger.LogInformation("Deleted SensitiveWord {Id}", id);
            await _activeWordsProvider.InvalidateAsync();
        }
        return deleted;
    }

    private static int CapPage(int page) => page < 1 ? 1 : page;
    private static int CapPageSize(int size) => size < 1 ? 1 : Math.Min(size, 100);

    private static SensitiveWordResponseDto Map(SensitiveWord w) => new()
    {
        SensitiveWordId = w.SensitiveWordId,
        Word = w.Word,
        NormalizedWord = w.NormalizedWord,
        Category = w.Category,
        Severity = w.Severity,
        IsActive = w.IsActive,
        RowVersion = w.RowVersion
    };
}


