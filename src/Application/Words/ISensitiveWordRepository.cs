using System.Data;
using FlashAssessment.Domain.Words;

namespace FlashAssessment.Application.Words;

public interface ISensitiveWordRepository
{
    Task<long> CreateAsync(SensitiveWord word, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<SensitiveWord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SensitiveWord> Items, int TotalCount)> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task UpdateAsync(SensitiveWord word, byte[] rowVersion, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, IDbTransaction? tx = null, CancellationToken cancellationToken = default);
}


