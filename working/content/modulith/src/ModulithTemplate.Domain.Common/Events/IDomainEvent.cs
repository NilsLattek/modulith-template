using Mediator;

namespace ModulithTemplate.Domain.Common.Events;

/// <summary>
/// Marks something domain-significant that happened inside one feature.
/// </summary>
/// <remarks>
/// A domain event is an <b>internal</b> mechanism, not a contract: it lives in its feature's
/// <c>Domain/Events/</c> folder, may speak in the feature's own domain language, and is never
/// referenced by another feature. Its purpose is to let an aggregate record that something happened
/// without knowing who reacts.
/// <para>
/// Unlike <c>IIntegrationEvent</c>, a domain event is dispatched <b>inside</b> the
/// transaction that saved the aggregate that raised it — same feature, same <c>DbContext</c> — so a
/// handler's writes commit or roll back together with the change that triggered them.
/// </para>
/// <para>
/// Translating one into a published fact is a deliberate step: a handler in the same feature's
/// Application layer enqueues the corresponding <c>IIntegrationEvent</c>. That translation is
/// what keeps domain language out of the published contract.
/// </para>
/// </remarks>
public interface IDomainEvent : INotification;
