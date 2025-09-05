using FlashAssessment.Application.Common;
using System.Text.RegularExpressions;
using FlashAssessment.Application.Words;

namespace FlashAssessment.Api.Support;

public sealed class LogSanitizer : ILogSanitizer
{
    private readonly IActiveWordsProvider _activeWordsProvider;

    public LogSanitizer(IActiveWordsProvider activeWordsProvider)
    {
        _activeWordsProvider = activeWordsProvider;
    }

    public async Task<string> SanitizeAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input)) return input;
        // Use case-insensitive, whole word regex to replace sensitive words with asterisks in logs
        var regex = await _activeWordsProvider.GetRegexAsync(wholeWordOnly: true, caseSensitive: false, cancellationToken);
        return regex.Replace(input, m => new string('*', m.Length));
    }
}


