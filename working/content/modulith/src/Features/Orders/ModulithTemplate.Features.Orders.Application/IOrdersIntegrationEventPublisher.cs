using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Application;

/// <summary>
/// Publishes the Orders feature's integration events, through the Orders <c>DbContext</c>.
/// </summary>
/// <remarks>
/// The per-feature marker is what binds the registration to this feature's own context, exactly as
/// <see cref="IOrdersRepository{T}"/> does; registering the shared
/// <see cref="IIntegrationEventPublisher"/> would let the last feature registered win for every
/// feature, and events would be staged on a sibling's connection.
/// </remarks>
public interface IOrdersIntegrationEventPublisher : IIntegrationEventPublisher;
