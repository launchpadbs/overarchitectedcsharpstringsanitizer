using Microsoft.Extensions.Options;

namespace FlashAssessment.Application.Common.RateLimiting
{
    /// <summary>
    /// Validates RateLimitingOptions at startup.
    /// </summary>
    public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
    {
        public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail("RateLimiting options are missing.");
            }

            var results = new List<string>();
            ValidatePolicy(options.Sanitize, nameof(options.Sanitize), results);
            ValidatePolicy(options.WordsRead, nameof(options.WordsRead), results);
            ValidatePolicy(options.WordsWrite, nameof(options.WordsWrite), results);

            return results.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(results);
        }

        private static void ValidatePolicy(FixedWindowPolicyOptions policy, string name, List<string> results)
        {
            if (policy.PermitLimit <= 0 || policy.PermitLimit > 10000)
            {
                results.Add($"{name}.PermitLimit must be between 1 and 10000.");
            }
            if (policy.WindowSeconds < 1 || policy.WindowSeconds > 3600)
            {
                results.Add($"{name}.WindowSeconds must be between 1 and 3600.");
            }
            if (policy.QueueLimit < 0 || policy.QueueLimit > 10000)
            {
                results.Add($"{name}.QueueLimit must be between 0 and 10000.");
            }
        }
    }
}


