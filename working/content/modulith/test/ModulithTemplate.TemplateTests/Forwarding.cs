using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;

namespace ModulithTemplate.TemplateTests;

/// <summary>A forwarder of Orders' event to an external system, as a feature's plain notification handler.</summary>
/// <remarks>Inert unless a test registers a <see cref="Broker"/> for it to send to.</remarks>
public static class Forwarding
{
    /// <summary>Stands in for the external system.</summary>
    public sealed class Broker
    {
        /// <summary>Whether a send fails, as an unreachable broker's would.</summary>
        public bool Down { get; set; }

        /// <summary>The ids of the events sent.</summary>
        public ICollection<Guid> Sent { get; } = [];
    }

    /// <summary>Sends each event to the <see cref="Broker"/>.</summary>
    /// <param name="services">The provider of the outbox's processing scope.</param>
    public sealed class SomeEntityAddedForwarder(IServiceProvider services)
        : INotificationHandler<SomeEntityAddedIntegrationEvent>
    {
        /// <inheritdoc />
        public ValueTask Handle(SomeEntityAddedIntegrationEvent notification, CancellationToken cancellationToken)
        {
            var broker = services.GetService<Broker>();
            if (broker is { Down: true })
            {
                throw new InvalidOperationException("The broker is unreachable.");
            }

            broker?.Sent.Add(notification.EventId);
            return ValueTask.CompletedTask;
        }
    }
}
