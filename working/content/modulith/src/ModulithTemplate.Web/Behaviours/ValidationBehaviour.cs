using FluentResults;

using FluentValidation;
using FluentValidation.Results;

using Mediator;

using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for a message before its handler, turning any
/// rule failure into a failed result of the handler's own response type.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// DTO-level shape checks only — not-empty, length, range. Business rules stay in the domain, where
/// the entity enforces them for every caller.
/// <para>
/// The <typeparamref name="TResponse"/> constraint mirrors <see cref="ExceptionBehaviour{TMessage, TResponse}"/>:
/// failures are reported as the response type itself, so a handler returning anything other than a
/// result is <b>not validated at all</b>. A message with no registered validator likewise passes
/// straight through, which keeps validation opt-in per command or query.
/// </para>
/// </remarks>
/// <param name="validators">Every validator registered for <typeparamref name="TMessage"/>.</param>
internal sealed class ValidationBehaviour<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : ResultBase<TResponse>, new()
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure>? failures = null;
        var context = new ValidationContext<TMessage>(message);

        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(context, cancellationToken);
            if (!validation.IsValid)
            {
                (failures ??= []).AddRange(validation.Errors);
            }
        }

        if (failures is null)
        {
            return await next(message, cancellationToken);
        }

        return new TResponse().WithErrors(
            failures.Select(failure => new ValidationError(failure.PropertyName, failure.ErrorMessage)));
    }
}
