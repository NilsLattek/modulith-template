using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Web.Common.Extensions;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for the <c>ValueTask</c> overloads of <see cref="ServiceScopeExtensions"/>.</summary>
public class ServiceScopeExtensionsTests
{
    /// <summary>Scoped probe recording whether its scope was disposed.</summary>
    public sealed class ScopedProbe : IDisposable
    {
        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task WithNewScopeAsync_with_a_value_task_action_returns_its_value_and_disposes_the_scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        ScopedProbe? captured = null;

        // Act
        var result = await scopeFactory.WithNewScopeAsync(sp =>
        {
            captured = sp.GetRequiredService<ScopedProbe>();
            return ValueTask.FromResult(42);
        });

        // Assert
        Assert.Equal(42, result);
        Assert.True(captured!.Disposed);
    }

    [Fact]
    public async Task WithNewScopeAsync_with_a_value_task_action_that_returns_nothing_disposes_the_scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        ScopedProbe? captured = null;

        // Act
        await scopeFactory.WithNewScopeAsync(sp =>
        {
            captured = sp.GetRequiredService<ScopedProbe>();
            return ValueTask.CompletedTask;
        });

        // Assert
        Assert.True(captured!.Disposed);
    }
}
