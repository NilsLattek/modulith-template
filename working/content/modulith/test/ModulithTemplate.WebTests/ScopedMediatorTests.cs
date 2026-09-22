using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Web;

using NSubstitute;

namespace ModulithTemplate.WebTests;

/// <summary>
/// Tests the one guarantee <see cref="IScopedMediator"/> exists to make: each message is handled in
/// a scope of its own, which is disposed once the message completes.
/// </summary>
public class ScopedMediatorTests
{
    /// <summary>A query with no handler; only the mediator substitute ever sees it.</summary>
    private sealed record ProbeQuery : IQuery<int>;

    /// <summary>Scoped probe recording whether its scope was disposed.</summary>
    private sealed class ScopeProbe : IDisposable
    {
        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task Send_handles_each_message_in_its_own_scope_and_disposes_it()
    {
        // Arrange: one probe per scope, captured as the mediator is resolved from that scope.
        var probes = new List<ScopeProbe>();
        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddScoped<IMediator>(provider =>
        {
            probes.Add(provider.GetRequiredService<ScopeProbe>());

            var mediator = Substitute.For<IMediator>();

            // CA2012 reads the arrangement as a stray ValueTask; it is NSubstitute's syntax for
            // configuring one, and the value is produced per call by the factory overload.
#pragma warning disable CA2012
            mediator.Send(Arg.Any<IQuery<int>>(), Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromResult(7));
#pragma warning restore CA2012
            return mediator;
        });
        services.AddScopedMediator();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IScopedMediator>();

        // Act
        var first = await mediator.Send(new ProbeQuery(), TestContext.Current.CancellationToken);
        var second = await mediator.Send(new ProbeQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(7, first);
        Assert.Equal(7, second);

        // A scope per message, not one shared by both — the circuit-lifetime DbContext this avoids.
        Assert.Equal(2, probes.Count);
        Assert.NotSame(probes[0], probes[1]);

        // Disposed by the time Send returned, so nothing it resolved outlives the message.
        Assert.All(probes, probe => Assert.True(probe.Disposed));
    }
}
