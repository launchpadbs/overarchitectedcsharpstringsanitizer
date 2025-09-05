using Microsoft.Extensions.Options;

namespace FlashAssessment.Application.Common;

public sealed class HealthOptionsValidator : IValidateOptions<HealthOptions>
{
    public ValidateOptionsResult Validate(string? name, HealthOptions options)
    {
        var errors = new List<string>();
        if (options.SqlTimeoutSeconds < 1 || options.SqlTimeoutSeconds > 120)
        {
            errors.Add("Health:SqlTimeoutSeconds must be between 1 and 120.");
        }
        if (options.RegexCheckMs < 50 || options.RegexCheckMs > 10_000)
        {
            errors.Add("Health:RegexCheckMs must be between 50 and 10000.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}


