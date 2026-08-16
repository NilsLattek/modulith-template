namespace ModulithTemplate.ServiceDefaults;

/// <summary>
/// Names of this solution's own <see cref="System.Diagnostics.ActivitySource"/>s, each subscribed to
/// by <c>ConfigureOpenTelemetry</c>.
/// </summary>
public static class ActivitySources
{
    /// <summary>
    /// Covers the span that <c>LoggingBehaviour</c> starts for every message the mediator dispatches,
    /// under which the handler's database calls appear.
    /// </summary>
    public const string Mediator = "ModulithTemplate.Web.Mediator";
}
