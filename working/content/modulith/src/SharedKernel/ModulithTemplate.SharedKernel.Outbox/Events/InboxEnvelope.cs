using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>An Integration Event addressed to one Consumer: the payload of one <c>shared.inbox</c> row.</summary>
/// <remarks>
/// The library dispatches a stored type name to exactly one handler, so the Consumer's context is a type
/// argument only to make that name distinct per Consumer. The name embeds each type argument's assembly
/// version, which is why <c>Directory.Build.props</c> pins <c>AssemblyVersion</c>.
/// </remarks>
/// <typeparam name="TConsumerContext">The consuming feature's context.</typeparam>
/// <typeparam name="TEvent">The event delivered.</typeparam>
/// <param name="Event">The event delivered.</param>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "It names the Consumer in the stored type name.")]
public sealed record InboxEnvelope<TConsumerContext, TEvent>(TEvent Event)
    where TConsumerContext : DbContext
    where TEvent : IIntegrationEvent;
