namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// The integration event types this application can deliver, keyed by the name stored on the row.
/// </summary>
/// <remarks>
/// An outbox row holds a JSON payload and its type's full name; republishing needs the CLR type
/// back. Each feature contributes its own through <c>Configure&lt;Name&gt;Application</c>, the only
/// layer permitted to reference a <c>Contracts</c> project.
/// <para>
/// The stored type name is a wire contract: renaming or moving an event orphans the rows already
/// written under its old name.
/// </para>
/// </remarks>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, Type> _byTypeName = new(StringComparer.Ordinal);

    /// <summary>The registered type names, in no particular order.</summary>
    public IReadOnlyCollection<string> TypeNames => _byTypeName.Keys;

    /// <summary>Registers <typeparamref name="TEvent"/> as deliverable.</summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    public void Add<TEvent>() where TEvent : class, IIntegrationEvent =>
        _byTypeName[NameOf<TEvent>()] = typeof(TEvent);

    /// <summary>Resolves the type an outbox row's stored type name refers to.</summary>
    /// <param name="typeName">The row's type name.</param>
    /// <returns>The event type, or <see langword="null"/> if nothing registered it.</returns>
    public Type? Find(string typeName) =>
        _byTypeName.TryGetValue(typeName, out var eventType) ? eventType : null;

    /// <summary>The name an outbox row carries for <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    /// <returns>The type's full name.</returns>
    public static string NameOf<TEvent>() where TEvent : class, IIntegrationEvent =>
        typeof(TEvent).FullName!;
}
