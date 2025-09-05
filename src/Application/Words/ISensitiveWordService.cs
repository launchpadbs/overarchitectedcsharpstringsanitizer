using FlashAssessment.Application.Words.Dto;

namespace FlashAssessment.Application.Words;

public interface ISensitiveWordService
{
    Task<long> CreateAsync(CreateSensitiveWordRequestDto dto, CancellationToken cancellationToken = default);
    Task<SensitiveWordResponseDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PagedResultDto<SensitiveWordResponseDto>> ListAsync(ListWordsQueryDto query, CancellationToken cancellationToken = default);
    Task UpdateAsync(long id, UpdateSensitiveWordRequestDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}


