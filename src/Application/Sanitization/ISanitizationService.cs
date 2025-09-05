namespace FlashAssessment.Application.Sanitization;

public interface ISanitizationService
{
    Task<SanitizeResponseDto> SanitizeAsync(SanitizeRequestDto request, CancellationToken cancellationToken = default);
}


