using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.SharedKernel.Web.Extensions;

/// <summary>
/// Helpers for running a piece of work inside a fresh dependency injection scope.
/// </summary>
/// <remarks>
/// In Blazor Server a DI scope lives for the whole circuit, so a scoped <c>DbContext</c> would
/// otherwise be shared — and concurrently used — across the entire user session.
/// </remarks>
public static class ServiceScopeExtensions
{
    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task that completes when the work and scope disposal have finished.</returns>
    public static async Task WithNewScopeAsync(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, Task> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <typeparam name="TResult">The type of value produced by <paramref name="action"/>.</typeparam>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task producing the value returned by <paramref name="action"/>.</returns>
    public static async Task<TResult> WithNewScopeAsync<TResult>(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task that completes when the work and scope disposal have finished.</returns>
    /// <remarks>
    /// The <see cref="ValueTask"/> overloads let a <c>IMediator.Send</c> call be passed directly,
    /// without a trailing <c>AsTask()</c>. The lambda's return type selects the overload.
    /// </remarks>
    public static async ValueTask WithNewScopeAsync(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <typeparam name="TResult">The type of value produced by <paramref name="action"/>.</typeparam>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task producing the value returned by <paramref name="action"/>.</returns>
    public static async ValueTask<TResult> WithNewScopeAsync<TResult>(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ValueTask<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
