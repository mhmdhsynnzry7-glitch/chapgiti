namespace ChapGiti.Domain.Common;

/// <summary>A printing or input rule was violated, without an HTTP dependency.</summary>
public sealed class RuleViolation(string message) : Exception(message);
