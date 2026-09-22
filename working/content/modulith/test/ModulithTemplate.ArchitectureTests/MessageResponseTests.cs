using System.Reflection;

using Mediator;

using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces that no message hands an aggregate back to its caller.
/// </summary>
/// <remarks>
/// A message is sent in a scope of its own, so the <c>DbContext</c> that loaded the aggregate is
/// disposed before the caller reads it — a Blazor component renders it against a dead context.
/// Only <see cref="AggregateRoot"/> is rejected: value objects carry no tracking and are a
/// legitimate part of a DTO.
/// </remarks>
public class MessageResponseTests
{
    [Fact]
    public void Message_responses_expose_no_aggregate()
    {
        var messages = ApplicationMessages();

        // Guard: no messages would make the rule pass vacuously.
        Assert.NotEmpty(messages);

        var violations = messages
            .Select(message => (message.Message, Aggregate: FindAggregate(message.Response, [])))
            .Where(found => found.Aggregate is not null)
            .Select(found => $"{found.Message.Name} returns {found.Aggregate!.Name}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "A message must return a DTO, never an aggregate: " + string.Join("; ", violations));
    }

    /// <summary>
    /// The message markers a handler responds to. Siblings, not a hierarchy: Mediator derives
    /// <c>IQuery&lt;T&gt;</c> from <c>IMessage</c>, not from <c>IRequest&lt;T&gt;</c>, so matching
    /// only the latter would find nothing.
    /// </summary>
    private static readonly Type[] MessageMarkers =
        [typeof(IRequest<>), typeof(ICommand<>), typeof(IQuery<>)];

    /// <summary>Every message declared in a feature's Application layer, with its response type.</summary>
    private static (Type Message, Type Response)[] ApplicationMessages() =>
        [.. SolutionAssemblies.FeatureAssemblies
            .Where(assembly => assembly.GetName().Name!.EndsWith(".Application", StringComparison.Ordinal))
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                    && MessageMarkers.Contains(contract.GetGenericTypeDefinition()))
                .Select(contract => (type, contract.GetGenericArguments()[0])))];

    /// <summary>The aggregate a response exposes, directly or through a property, if any.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="seen">Types already walked, so a cycle terminates.</param>
    /// <returns>The offending aggregate, or <see langword="null"/>.</returns>
    private static Type? FindAggregate(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return null;
        }

        // Result<T>, IReadOnlyList<T>, T[]: the wrapper is irrelevant, what it carries is not.
        foreach (var argument in Unwrap(type))
        {
            if (FindAggregate(argument, seen) is { } nested)
            {
                return nested;
            }
        }

        if (typeof(AggregateRoot).IsAssignableFrom(type))
        {
            return type;
        }

        // Only this solution's types are walked further: a framework type cannot reach an aggregate.
        return SolutionAssemblies.FeatureAssemblies.Contains(type.Assembly)
            ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => FindAggregate(property.PropertyType, seen))
                .FirstOrDefault(found => found is not null)
            : null;
    }

    /// <summary>What a wrapper carries: an array's element, or a generic type's arguments.</summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The carried types, empty if it carries none.</returns>
    private static Type[] Unwrap(Type type) => type switch
    {
        { IsArray: true } => [type.GetElementType()!],
        { IsGenericType: true } => type.GetGenericArguments(),
        _ => [],
    };
}
