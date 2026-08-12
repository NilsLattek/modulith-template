using FluentResults;

using FluentValidation;
using FluentValidation.Results;

using Mediator;

using ModulithTemplate.SharedKernel.Web.Errors;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for a message before its handler, and turns
/// any rule failure into a failed result of the handler's own response type — so a handler only
/// ever sees input that already passed its command's or query's basic checks.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// This is DTO-level validation only — shape checks such as not-empty, length and range, declared
/// on the command in its own <c>*CommandValidator</c>. Business rules stay in the domain, where
/// the entity enforces them for every caller; a rule that needs an entity's state or a lookup does
/// not belong in a validator.
/// <para>
/// The <typeparamref name="TResponse"/> constraint mirrors <see cref="ExceptionBehaviour{TMessage, TResponse}"/>:
/// the failures have to be reported as the response type itself, so a message whose handler
/// returns something other than <c>Result</c>/<c>Result&lt;T&gt;</c> is not validated at all.
/// Every handler in this solution returns a result, which is what keeps that from being a silent
/// hole.
/// </para>
/// <para>
/// A message with no registered validator resolves an empty <see cref="IEnumerable{T}"/> and goes
/// straight through, so validation stays opt-in per command or query.
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
