using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;
using ClonerApp.Engine.Filtering;

namespace ClonerApp.Engine.Tests;

public class ExcludeRuleEvaluatorTests
{
    private static MediaCandidate Candidate(
        string url = "https://example.com/photos/my_file.jpg",
        string? title = "Gallery",
        string? tag = "img") =>
        new()
        {
            Url = url,
            SourcePageUrl = "https://example.com/page",
            SourcePageTitle = title,
            SourceTag = tag,
            Extension = "jpg",
            IsImage = true
        };

    [Fact]
    public void EmptyRules_NeverExclude()
    {
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(Candidate(), Array.Empty<ExcludeRule>()));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(Candidate(), (string?)null));
    }

    [Fact]
    public void Contains_CaseInsensitive()
    {
        var rules = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Contains,
                Value = "MY_FILE"
            }
        };
        Assert.True(ExcludeRuleEvaluator.ShouldExclude(Candidate(), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/clean.jpg"), rules));
    }

    [Fact]
    public void Equals_MatchesExactFileName()
    {
        var rules = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Equals,
                Value = "my_file.jpg"
            }
        };
        Assert.True(ExcludeRuleEvaluator.ShouldExclude(Candidate(), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/other.jpg"), rules));
    }

    [Fact]
    public void DoesNotContain_ExcludesWhenAbsent()
    {
        var rules = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.DoesNotContain,
                Value = "_"
            }
        };
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(Candidate(), rules));
        Assert.True(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/plain.jpg"), rules));
    }

    [Fact]
    public void MatchesRegex_AndInvalidRegexDoesNotExclude()
    {
        var ok = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.Url,
                Operator = ExcludeOperator.MatchesRegex,
                Value = @"photos/.+\.jpg$"
            }
        };
        Assert.True(ExcludeRuleEvaluator.ShouldExclude(Candidate(), ok));

        var bad = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.Url,
                Operator = ExcludeOperator.MatchesRegex,
                Value = "[invalid"
            }
        };
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(Candidate(), bad));
    }

    [Fact]
    public void DoesNotMatchRegex_InvalidPatternDoesNotExclude()
    {
        var bad = new[]
        {
            new ExcludeRule
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.DoesNotMatchRegex,
                Value = "(unclosed"
            }
        };
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(Candidate(), bad));
    }

    [Fact]
    public void FileNameContainsUnderscore_Or_Hyphen_ExcludesBoth()
    {
        var rules = new List<ExcludeRule>
        {
            new()
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Contains,
                Value = "_"
            },
            new()
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Contains,
                Value = "-",
                JoinWithPrevious = RuleJoin.Or
            }
        };

        Assert.True(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/a_b.jpg"), rules));
        Assert.True(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/a-b.jpg"), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/ab.jpg"), rules));
    }

    [Fact]
    public void And_RequiresBoth()
    {
        var rules = new List<ExcludeRule>
        {
            new()
            {
                Field = ExcludeField.Tag,
                Operator = ExcludeOperator.Equals,
                Value = "img"
            },
            new()
            {
                Field = ExcludeField.PageTitle,
                Operator = ExcludeOperator.Contains,
                Value = "Gallery",
                JoinWithPrevious = RuleJoin.And
            }
        };

        Assert.True(ExcludeRuleEvaluator.ShouldExclude(Candidate(), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate(title: "Other", tag: "img"), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate(tag: "a"), rules));
    }

    [Fact]
    public void LeftToRight_OrThenAnd()
    {
        // A OR B AND C  =>  (A OR B) AND C
        var rules = new List<ExcludeRule>
        {
            new()
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Contains,
                Value = "_"
            },
            new()
            {
                Field = ExcludeField.FileName,
                Operator = ExcludeOperator.Contains,
                Value = "-",
                JoinWithPrevious = RuleJoin.Or
            },
            new()
            {
                Field = ExcludeField.Tag,
                Operator = ExcludeOperator.Equals,
                Value = "img",
                JoinWithPrevious = RuleJoin.And
            }
        };

        Assert.True(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/a_b.jpg", tag: "img"), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/a_b.jpg", tag: "a"), rules));
        Assert.False(ExcludeRuleEvaluator.ShouldExclude(
            Candidate("https://example.com/ab.jpg", tag: "img"), rules));
    }

    [Fact]
    public void SerializeRoundTrip()
    {
        var rules = new List<ExcludeRule>
        {
            new() { Field = ExcludeField.Url, Operator = ExcludeOperator.Contains, Value = "cdn" },
            new()
            {
                Field = ExcludeField.Tag,
                Operator = ExcludeOperator.Equals,
                Value = "css",
                JoinWithPrevious = RuleJoin.Or
            }
        };
        var json = ExcludeRuleEvaluator.SerializeRules(rules);
        var parsed = ExcludeRuleEvaluator.ParseRules(json);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(ExcludeField.Url, parsed[0].Field);
        Assert.Equal(RuleJoin.Or, parsed[1].JoinWithPrevious);
    }
}
