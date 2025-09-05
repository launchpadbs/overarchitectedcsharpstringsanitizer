using Microsoft.Extensions.Options;

namespace FlashAssessment.Application.Words;

public sealed class ActiveWordsCacheOptionsValidator : IValidateOptions<ActiveWordsCacheOptions>
{
    public ValidateOptionsResult Validate(string? name, ActiveWordsCacheOptions options)
    {
        if (options.ActiveWordsMinutes <= 0 || options.ActiveWordsMinutes > 1440)
        {
            return ValidateOptionsResult.Fail("Caching.ActiveWordsMinutes must be between 1 and 1440 minutes.");
        }
        return ValidateOptionsResult.Success;
    }
}


