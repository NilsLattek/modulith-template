using System.Diagnostics;

using FluentResults;

using Mediator;

using ModulithTemplate.ServiceDefaults;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Traces and times every message passing through the mediator, and logs its outcome.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// Deliberately unconstrained on <typeparamref name="TResponse"/>, so that every message is
/// traced and logged regardless of what its handler returns — observability must never be
/// conditional on the shape of a response. Responses that happen to be results are inspected at
/// runtime so a failed result is reported at warning rather than information.
/// <para>
/// <b>The span.</b> Being the outermost behaviour, the activity started here spans the entire
/// operation: validation, the handler, and the integration events it published. Every span raised
/// below it — the Npgsql span of each query the handler issued, the HTTP client span of each call it
/// made — is a child of this one, and it in turn is a child of the ASP.NET Core request span. One
/// command or query is therefore one collapsible subtree in the monitoring system, and the log
/// records emitted here carry its trace and span id.
/// </para>
/// <para>
/// The status comes off the response rather than a <c>catch</c>, which is what
/// <see cref="ExceptionBehaviour{TMessage, TResponse}"/> sitting directly beneath makes possible:
/// exceptions from result-returning handlers have already become failed results by the time they
/// arrive. The exception it re-throws by design — <see cref="OperationCanceledException"/> — unwinds
/// through the <c>using</c> below, which still ends the span, leaving its status unset. That is the
/// intended reading: a cancelled operation neither succeeded nor failed.
/// </para>
/// </remarks>
internal sealed class LoggingBehaviour<TMessage, TResponse>(
    ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private static readonly ActivitySource ActivitySource = new(ActivitySources.Mediator);

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;

        using var activity = ActivitySource.StartActivity(messageType);
        activity?.SetTag("mediator.message.type", typeof(TMessage).FullName);

        BehaviourLog.Handling(logger, messageType);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (response is IResultBase { IsFailed: true } failed)
        {
            var errors = string.Join("; ", failed.Errors.Select(error => error.Message));
            BehaviourLog.Failed(logger, messageType, elapsedMilliseconds, errors);
            activity?.SetStatus(ActivityStatusCode.Error, errors);
        }
        else
        {
            BehaviourLog.Handled(logger, messageType, elapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        return response;
    }
}
