using System.Collections.Generic;
using System.Linq;

namespace LogisticsApp.Core.Specifications;

public enum SpecificationLevel
{
    Warning,
    Error
}

public sealed class SpecificationMessage
{
    public string Message { get; }
    public SpecificationLevel Level { get; }

    public SpecificationMessage(string message, SpecificationLevel level)
    {
        Message = message;
        Level = level;
    }
}

public sealed class SpecificationResult
{
    public bool IsValid => !Messages.Any(m => m.Level == SpecificationLevel.Error);
    public IReadOnlyList<SpecificationMessage> Messages { get; }

    private SpecificationResult(List<SpecificationMessage> messages)
    {
        Messages = messages;
    }

    public static SpecificationResult Success() => new(new List<SpecificationMessage>());

    public static SpecificationResult Fail(string message) => new(new List<SpecificationMessage> { new(message, SpecificationLevel.Error) });

    public static SpecificationResult Warn(string message) => new(new List<SpecificationMessage> { new(message, SpecificationLevel.Warning) });

    public static SpecificationResult Combine(IEnumerable<SpecificationResult> results)
    {
        return new SpecificationResult(results.SelectMany(r => r.Messages).ToList());
    }
}

public interface ISpecification<in T>
{
    SpecificationResult IsSatisfiedBy(T entity);
}