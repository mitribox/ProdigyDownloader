using ClonerApp.Core.Enums;

namespace ClonerApp.Core.Models;

public sealed class ExcludeRule
{
    public ExcludeField Field { get; set; } = ExcludeField.FileName;
    public ExcludeOperator Operator { get; set; } = ExcludeOperator.Contains;
    public string Value { get; set; } = string.Empty;
    /// <summary>How this rule combines with the previous one. Ignored for the first rule.</summary>
    public RuleJoin JoinWithPrevious { get; set; } = RuleJoin.Or;
}
