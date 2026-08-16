using Mediator;

namespace ModulithTemplate.SharedKernel.Domain.Events;

/// <summary>
/// Marks something domain-significant that happened inside one feature.
/// </summary>
/// <remarks>
/// An <b>internal</b> mechanism, not a contract: it lives in its feature's <c>Domain/Events/</c>
/// folder, speaks the feature's own domain language, and is never referenced by another feature. It
/// is dispatched inside the transaction that saved the aggregate, so a handler's writes commit or
/// roll back with the change that triggered them.
/// <para>
/// An event with no handler fails the build as <c>MSG0005</c>, raised in the <b>host</b>
/// compilation — a <c>#pragma</c> on the event itself cannot silence it. If nothing reacts to it
/// yet, it is not yet worth raising.
/// </para>
/// </remarks>
public interface IDomainEvent : INotification;
