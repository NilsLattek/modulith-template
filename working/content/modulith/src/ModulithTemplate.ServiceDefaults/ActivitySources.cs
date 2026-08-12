namespace ModulithTemplate.ServiceDefaults;

/// <summary>
/// Names of this solution's own <see cref="System.Diagnostics.ActivitySource"/>s, each subscribed to
/// by <c>ConfigureOpenTelemetry</c>.
/// </summary>
/// <remarks>
/// A source whose name was never passed to <c>AddSource</c> is silently ignored: every
/// <c>StartActivity</c> call on it returns <see langword="null"/> and its spans never reach the
/// exporter, with nothing in the logs to say so. These constants exist so a source and its
/// subscription cannot drift apart — create an <c>ActivitySource</c> from one of them and it is
/// already registered. Third-party source names (<c>"Npgsql"</c> and the like) do not belong here;
/// they are the other library's to change, not ours.
/// </remarks>
public static class ActivitySources
{
    /// <summary>
    /// Covers the span that <c>LoggingBehaviour</c> starts for every message the mediator dispatches,
    /// under which the handler's database calls appear.
    /// </summary>
    public const string Mediator = "ModulithTemplate.Web.Mediator";
}
