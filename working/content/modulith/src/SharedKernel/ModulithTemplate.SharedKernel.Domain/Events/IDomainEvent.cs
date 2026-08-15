using Mediator;

namespace ModulithTemplate.SharedKernel.Domain.Events;

/// <summary>
/// Marks something domain-significant that happened inside one feature.
/// </summary>
/// <remarks>
/// A domain event is an <b>internal</b> mechanism, not a contract: it lives in its feature's
/// <c>Domain/Events/</c> folder, may speak in the feature's own domain language, and is never
/// referenced by another feature. Its purpose is to let an aggregate record that something happened
/// without knowing who reacts.
/// <para>
/// A domain event is dispatched <b>inside</b> the transaction that saved the aggregate that raised
/// it — same feature, same <c>DbContext</c> — so a handler's writes commit or roll back together
/// with the change that triggered them.
/// </para>
/// <para>
/// A domain event with no registered handler is reported by the mediator's source generator as
/// <c>MSG0005</c> ("message without any registered handler") and fails the <c>-warnaserror</c> build.
/// The diagnostic is raised in the <b>host</b> compilation, so a <c>#pragma</c> on the event cannot
/// silence it: a raised domain event needs at least one handler under
/// <c>Application/DomainEventHandlers/</c>. If nothing reacts to it yet, it is not yet worth raising.
/// </para>
/// </remarks>
public interface IDomainEvent : INotification;
