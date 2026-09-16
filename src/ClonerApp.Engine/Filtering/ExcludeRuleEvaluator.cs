using System.Text.Json;
using System.Text.RegularExpressions;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Filtering;

public static class ExcludeRuleEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<ExcludeRule> ParseRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ExcludeRule>();

        try
        {
            return JsonSerializer.Deserialize<List<ExcludeRule>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<ExcludeRule>();
        }
    }

    public static string SerializeRules(IEnumerable<ExcludeRule> rules) =>
        JsonSerializer.Serialize(rules.ToList(), JsonOptions);

    /// <summary>
    /// Returns true when the candidate should be excluded (skipped).
    /// </summary>
    public static bool ShouldExclude(MediaCandidate candidate, IReadOnlyList<ExcludeRule> rules)
    {
        if (rules.Count == 0)
            return false;

        var result = EvaluateSingle(candidate, rules[0]);
        for (var i = 1; i < rules.Count; i++)
        {
            var next = EvaluateSingle(candidate, rules[i]);
            result = rules[i].JoinWithPrevious == RuleJoin.And
                ? result && next
                : result || next;
        }

        return result;
    }

    public static bool ShouldExclude(MediaCandidate candidate, string? rulesJson) =>
        ShouldExclude(candidate, ParseRules(rulesJson));

    private static bool EvaluateSingle(MediaCandidate candidate, ExcludeRule rule)
    {
        var fieldValue = GetFieldValue(candidate, rule.Field) ?? string.Empty;
        var expected = rule.Value ?? string.Empty;

        return rule.Operator switch
        {
            ExcludeOperator.Contains =>
                fieldValue.Contains(expected, StringComparison.OrdinalIgnoreCase),
            ExcludeOperator.Equals =>
                string.Equals(fieldValue, expected, StringComparison.OrdinalIgnoreCase),
            ExcludeOperator.DoesNotContain =>
                !fieldValue.Contains(expected, StringComparison.OrdinalIgnoreCase),
            ExcludeOperator.MatchesRegex =>
                TryRegexIsMatch(fieldValue, expected, out var matched) && matched,
            ExcludeOperator.DoesNotMatchRegex =>
                TryRegexIsMatch(fieldValue, expected, out var matchedNeg) && !matchedNeg,
            _ => false
        };
    }

    private static string? GetFieldValue(MediaCandidate candidate, ExcludeField field) =>
        field switch
        {
            ExcludeField.FileName => GetFileName(candidate.Url),
            ExcludeField.PageTitle => candidate.SourcePageTitle,
            ExcludeField.Tag => candidate.SourceTag,
            ExcludeField.Url => candidate.Url,
            _ => null
        };

    private static string GetFileName(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var name = Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? string.Empty : Uri.UnescapeDataString(name);
        }
        catch
        {
            return Path.GetFileName(url) ?? string.Empty;
        }
    }

    /// <summary>
    /// Returns false when the pattern is invalid (caller should treat the rule as non-matching / skip).
    /// </summary>
    private static bool TryRegexIsMatch(string input, string pattern, out bool isMatch)
    {
        isMatch = false;
        if (string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            isMatch = Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
