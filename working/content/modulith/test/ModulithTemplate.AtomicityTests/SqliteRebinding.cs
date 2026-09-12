using System.Reflection;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// Moves a registered context onto SQLite without restating how it was registered.
/// </summary>
/// <remarks>
/// The options the solution built are resolved as usual and then rebuilt with every non-provider
/// extension carried across — interceptors, replaced services, naming conventions and all — so the
/// behaviour under test is the behaviour <c>AddModuleDbContext</c> configured. Writing a fresh
/// options builder here instead would let a change to that registration pass these tests while
/// breaking the host.
/// </remarks>
internal static class SqliteRebinding
{
    private static readonly MethodInfo AddOrUpdate =
        typeof(IDbContextOptionsBuilderInfrastructure)
            .GetMethod(nameof(IDbContextOptionsBuilderInfrastructure.AddOrUpdateExtension))!;

    /// <summary>Re-registers <typeparamref name="TContext"/>'s options against a SQLite connection.</summary>
    /// <typeparam name="TContext">The context to move.</typeparam>
    /// <param name="services">The services the context is registered in.</param>
    /// <param name="connection">The open in-memory connection to serve it from.</param>
    /// <param name="interceptors">Interceptors to attach after the registered ones.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection OnSqlite<TContext>(
        this IServiceCollection services, SqliteConnection connection, params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var registered = services.Single(service => service.ServiceType == typeof(DbContextOptions<TContext>));
        services.Remove(registered);
        services.Add(new ServiceDescriptor(
            typeof(DbContextOptions<TContext>),
            provider => Rebuild(
                (DbContextOptions<TContext>)registered.ImplementationFactory!(provider),
                connection,
                interceptors),
            registered.Lifetime));

        return services;
    }

    private static DbContextOptions<TContext> Rebuild<TContext>(
        DbContextOptions<TContext> registered, SqliteConnection connection, IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        var infrastructure = (IDbContextOptionsBuilderInfrastructure)builder;

        foreach (var extension in registered.Extensions.Where(extension => !extension.Info.IsDatabaseProvider))
        {
            // The generic argument is the key the extension is stored under, so it must be the
            // extension's own type rather than the interface.
            AddOrUpdate.MakeGenericMethod(extension.GetType()).Invoke(infrastructure, [extension]);
        }

        // Last, so a recorder sees what the registered interceptors have already done to the save.
        return builder.UseSqlite(connection).AddInterceptors(interceptors).Options;
    }
}
