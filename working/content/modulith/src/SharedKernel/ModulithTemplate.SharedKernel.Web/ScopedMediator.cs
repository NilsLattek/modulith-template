using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Web.Extensions;

namespace ModulithTemplate.SharedKernel.Web;

/// <summary>Resolves a mediator from a fresh scope for each message.</summary>
/// <remarks>
/// Singleton: it closes over the scope factory and keeps no state of its own.
/// </remarks>
/// <param name="scopeFactory">Creates the per-message scope.</param>
internal sealed class ScopedMediator(IServiceScopeFactory scopeFactory) : IScopedMediator
{
    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> message, CancellationToken cancellationToken = default) =>
        scopeFactory.WithNewScopeAsync(services =>
            services.GetRequiredService<IMediator>().Send(message, cancellationToken));

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(
        ICommand<TResponse> message, CancellationToken cancellationToken = default) =>
        scopeFactory.WithNewScopeAsync(services =>
            services.GetRequiredService<IMediator>().Send(message, cancellationToken));

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(
        IQuery<TResponse> message, CancellationToken cancellationToken = default) =>
        scopeFactory.WithNewScopeAsync(services =>
            services.GetRequiredService<IMediator>().Send(message, cancellationToken));
}
