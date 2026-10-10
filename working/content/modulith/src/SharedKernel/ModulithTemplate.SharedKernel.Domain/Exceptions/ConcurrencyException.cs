namespace ModulithTemplate.SharedKernel.Domain.Exceptions;

/// <summary>A row changed after the caller read it, so a change made from that stale copy is refused.</summary>
/// <remarks>Not a <see cref="BusinessException"/>: the caller reloads rather than shows a rule's message.</remarks>
public class ConcurrencyException : Exception
{
    public ConcurrencyException()
        : this("The data changed after it was read.")
    {
    }

    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
