namespace FlashAssessment.Application.Common;

public interface ILogSanitizer
{
    Task<string> SanitizeAsync(string input, CancellationToken cancellationToken = default);
}


