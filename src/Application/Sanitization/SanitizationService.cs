using System.Diagnostics;
using System.Text;
using FlashAssessment.Application.Common;
using FlashAssessment.Application.Words;
using System.Text.RegularExpressions;

namespace FlashAssessment.Application.Sanitization;

public sealed class SanitizationService : ISanitizationService
{
    private readonly IActiveWordsProvider _activeWordsProvider;
    private readonly FlashAssessment.Application.Common.ISanitizationMetrics? _metrics;
    private static readonly ActivitySource Activity = new("FlashAssessment");

    public SanitizationService(IActiveWordsProvider activeWordsProvider, FlashAssessment.Application.Common.ISanitizationMetrics? metrics = null)
    {
        _activeWordsProvider = activeWordsProvider;
        _metrics = metrics;
    }

    public async Task<SanitizeResponseDto> SanitizeAsync(SanitizeRequestDto request, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SanitizationService.Sanitize", ActivityKind.Internal);
        activity?.SetTag("sanitize.strategy", request.Options?.Strategy.ToString());
        activity?.SetTag("sanitize.wholeWordOnly", request.Options?.WholeWordOnly ?? true);
        activity?.SetTag("sanitize.caseSensitive", request.Options?.CaseSensitive ?? false);
        activity?.SetTag("sanitize.textLength", request.Text?.Length ?? 0);

        var sw = Stopwatch.StartNew();
        var text = request.Text ?? string.Empty;
        var options = request.Options ?? new SanitizeOptionsDto();

        // Build or reuse compiled regex for active words with chosen flags
        Regex regex;
        try
        {
            regex = await _activeWordsProvider.GetRegexAsync(
                wholeWordOnly: options.WholeWordOnly ?? true,
                caseSensitive: options.CaseSensitive ?? false,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Graceful degradation: if cache/regex compilation fails, fall back to simple word boundary masking
            _metrics?.RecordError();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var degraded = await DegradedSanitizeAsync(text, options, cancellationToken);
            sw.Stop();
            _metrics?.RecordSanitizeDuration(sw.Elapsed.TotalMilliseconds);
            return degraded;
        }

        // For FullMask/FirstLastOnly, we can work in-place on a char array without growing
        // For FixedLength/Hash, replacement may change length; fallback to StringBuilder for those
        var matches = new List<MatchDto>();
        if (options.Strategy == MaskStrategy.FixedLength || options.Strategy == MaskStrategy.Hash)
        {
            var output = new StringBuilder(text);
            var m = regex.Match(text);
            while (m.Success)
            {
                ApplyMask(output, m.Index, m.Length, options, text);
                matches.Add(new MatchDto { Word = new string(text.AsSpan(m.Index, m.Length)), Start = m.Index, Length = m.Length });
                m = m.NextMatch();
            }
            sw.Stop();
            _metrics?.RecordSanitizeDuration(sw.Elapsed.TotalMilliseconds);
            _metrics?.IncrementMatches(matches.Count);
            var response = new SanitizeResponseDto
            {
                SanitizedText = output.ToString(),
                Matched = matches,
                ElapsedMs = sw.Elapsed.TotalMilliseconds
            };
            activity?.SetTag("sanitize.matches", matches.Count);
            return response;
        }
        else
        {
            var buffer = text.ToCharArray();
            var m2 = regex.Match(text);
            while (m2.Success)
            {
                // in-place mask on buffer
                if (options.Strategy == MaskStrategy.FullMask)
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    for (int i = 0; i < m2.Length; i++) buffer[m2.Index + i] = ch;
                }
                else // FirstLastOnly
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    if (m2.Length <= 2)
                    {
                        for (int i = 0; i < m2.Length; i++) buffer[m2.Index + i] = ch;
                    }
                    else
                    {
                        for (int i = 1; i < m2.Length - 1; i++) buffer[m2.Index + i] = ch;
                    }
                }
                matches.Add(new MatchDto { Word = new string(text.AsSpan(m2.Index, m2.Length)), Start = m2.Index, Length = m2.Length });
                m2 = m2.NextMatch();
            }
            sw.Stop();
            _metrics?.RecordSanitizeDuration(sw.Elapsed.TotalMilliseconds);
            _metrics?.IncrementMatches(matches.Count);
            var response = new SanitizeResponseDto
            {
                SanitizedText = new string(buffer),
                Matched = matches,
                ElapsedMs = sw.Elapsed.TotalMilliseconds
            };
            activity?.SetTag("sanitize.matches", matches.Count);
            return response;
        }

        // All code paths return above; this method should not reach here.
    }

    private static void ApplyMask(StringBuilder buffer, int start, int length, SanitizeOptionsDto options, string original)
    {
        switch (options.Strategy)
        {
            case MaskStrategy.FullMask:
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    for (int j = 0; j < length; j++) buffer[start + j] = ch;
                }
                break;
            case MaskStrategy.FirstLastOnly:
                if (length <= 2)
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    for (int j = 0; j < length; j++) buffer[start + j] = ch;
                }
                else
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    for (int j = 1; j < length - 1; j++) buffer[start + j] = ch;
                }
                break;
            case MaskStrategy.FixedLength:
                var fixedLen = options.FixedLength.GetValueOrDefault(3);
                var maskStr = new string((options.MaskCharacter ?? "*")[0], fixedLen);
                buffer.Remove(start, length);
                buffer.Insert(start, maskStr);
                break;
            case MaskStrategy.Hash:
                var hash = ComputeSha256Hex(original.Substring(start, length));
                var shortHash = hash[..Math.Min(8, hash.Length)];
                buffer.Remove(start, length);
                buffer.Insert(start, shortHash);
                break;
        }
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsAlphaNum(char c) => char.IsLetterOrDigit(c);

    private async Task<SanitizeResponseDto> DegradedSanitizeAsync(string text, SanitizeOptionsDto options, CancellationToken cancellationToken)
    {
        // Degraded mode with Aho-Corasick multi-pattern matching over the active word list
        var caseSensitive = options.CaseSensitive ?? false;
        var words = await _activeWordsProvider.GetActiveWordsAsync(caseSensitive, cancellationToken);
        var normalizedText = caseSensitive ? text : text.ToLowerInvariant();

        var matcher = new AhoCorasickMatcher(caseSensitive);
        foreach (var w in words)
        {
            if (!string.IsNullOrWhiteSpace(w)) matcher.AddPattern(caseSensitive ? w : w.ToLowerInvariant());
        }
        matcher.Build();

        var rawMatches = matcher.FindAll(normalizedText);

        // Optionally enforce whole-word boundaries using original text
        var filtered = new List<(int start, int length)>();
        foreach (var (start, length) in rawMatches)
        {
            if (options.WholeWordOnly.GetValueOrDefault(true))
            {
                var leftOk = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
                var end = start + length;
                var rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                if (!(leftOk && rightOk)) continue;
            }
            filtered.Add((start, length));
        }

        // De-overlap: prefer longest match at a given start; greedy by start asc, length desc
        var ordered = filtered
            .OrderBy(m => m.start)
            .ThenByDescending(m => m.length)
            .ToList();
        var selected = new List<(int start, int length)>();
        var lastEnd = -1;
        foreach (var m in ordered)
        {
            if (m.start >= lastEnd)
            {
                selected.Add(m);
                lastEnd = m.start + m.length;
            }
        }

        // Apply masking per strategy
        if (options.Strategy == MaskStrategy.FixedLength || options.Strategy == MaskStrategy.Hash)
        {
            var sb = new StringBuilder();
            var cursor = 0;
            foreach (var (start, length) in selected)
            {
                if (start > cursor) sb.Append(text, cursor, start - cursor);
                // temporary buffer to reuse ApplyMask
                var segment = new StringBuilder(text.Substring(start, length));
                ApplyMask(segment, 0, length, options, text);
                sb.Append(segment.ToString());
                cursor = start + length;
            }
            if (cursor < text.Length) sb.Append(text, cursor, text.Length - cursor);

            var resultMatches = selected.Select(m => new MatchDto { Word = new string(text.AsSpan(m.start, m.length)), Start = m.start, Length = m.length }).ToList();
            return new SanitizeResponseDto { SanitizedText = sb.ToString(), Matched = resultMatches, ElapsedMs = 0 };
        }
        else
        {
            var buffer = text.ToCharArray();
            foreach (var (start, length) in selected)
            {
                if (options.Strategy == MaskStrategy.FullMask)
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    for (int i = 0; i < length; i++) buffer[start + i] = ch;
                }
                else
                {
                    var ch = (options.MaskCharacter ?? "*")[0];
                    if (length <= 2)
                    {
                        for (int i = 0; i < length; i++) buffer[start + i] = ch;
                    }
                    else
                    {
                        for (int i = 1; i < length - 1; i++) buffer[start + i] = ch;
                    }
                }
            }
            var resultMatches = selected.Select(m => new MatchDto { Word = new string(text.AsSpan(m.start, m.length)), Start = m.start, Length = m.length }).ToList();
            return new SanitizeResponseDto { SanitizedText = new string(buffer), Matched = resultMatches, ElapsedMs = 0 };
        }
    }

    private sealed class AhoCorasickMatcher
    {
        private sealed class Node
        {
            public Dictionary<char, Node> Next { get; } = new();
            public Node? Fail { get; set; }
            public List<int> Outputs { get; } = new();
        }

        private readonly Node _root = new();
        private readonly bool _caseSensitive;

        public AhoCorasickMatcher(bool caseSensitive)
        {
            _caseSensitive = caseSensitive;
        }

        public void AddPattern(string pattern)
        {
            var node = _root;
            foreach (var ch in pattern)
            {
                if (!node.Next.TryGetValue(ch, out var next))
                {
                    next = new Node();
                    node.Next[ch] = next;
                }
                node = next;
            }
            node.Outputs.Add(pattern.Length);
        }

        public void Build()
        {
            var queue = new Queue<Node>();
            foreach (var kvp in _root.Next)
            {
                kvp.Value.Fail = _root;
                queue.Enqueue(kvp.Value);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var kvp in current.Next)
                {
                    var c = kvp.Key;
                    var child = kvp.Value;
                    queue.Enqueue(child);

                    var fail = current.Fail;
                    while (fail != null && !fail.Next.TryGetValue(c, out _))
                    {
                        fail = fail.Fail;
                    }
                    if (fail != null && fail.Next.TryGetValue(c, out var target))
                    {
                        child.Fail = target;
                        // merge outputs
                        foreach (var o in target.Outputs) child.Outputs.Add(o);
                    }
                    else
                    {
                        child.Fail = _root;
                    }
                }
            }
        }

        public List<(int start, int length)> FindAll(string text)
        {
            var results = new List<(int start, int length)>();
            var node = _root;
            for (int i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                Node? nextNode;
                while (node != null && !node.Next.TryGetValue(ch, out nextNode))
                {
                    node = node.Fail;
                }
                if (node == null)
                {
                    node = _root;
                    continue;
                }
                else
                {
                    if (!node.Next.TryGetValue(ch, out nextNode))
                    {
                        // no transition even after fail chain; stay at root
                        nextNode = _root;
                    }
                    node = nextNode;
                }
                foreach (var length in node.Outputs)
                {
                    results.Add((i - length + 1, length));
                }
            }
            return results;
        }
    }

    
}


