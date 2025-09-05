using Microsoft.Extensions.Options;

namespace FlashAssessment.Application.Common;

public sealed class RequestLimitsOptionsValidator : IValidateOptions<RequestLimitsOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestLimitsOptions options)
    {
        if (options.MaxJsonBytes < 1024 || options.MaxJsonBytes > 5_000_000)
        {
            return ValidateOptionsResult.Fail("RequestLimits:MaxJsonBytes must be between 1KB and 5MB.");
        }
        return ValidateOptionsResult.Success;
    }
}


