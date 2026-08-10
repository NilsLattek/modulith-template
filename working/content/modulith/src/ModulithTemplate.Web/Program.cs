using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Web.Behaviours;
using ModulithTemplate.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Aspire's ServiceDefaults: OpenTelemetry tracing, metrics and logging, health checks, service
// discovery and HTTP resilience. First, so telemetry is in place before anything else registers.
builder.AddServiceDefaults();

builder.ConfigureOrdersFeature();

builder.Services.AddMediator(options =>
{
    // Scoped, not the library default of Singleton: handlers inject the feature repositories,
    // which are bound to a scoped DbContext. That makes IMediator scoped too, so an @injected
    // one resolves from the Blazor circuit scope — which lives as long as the user's connection
    // and would hold a single DbContext open for it. Hence WithNewScopeAsync per operation.
    options.ServiceLifetime = ServiceLifetime.Scoped;

    // Ordered outermost-first. LoggingBehaviour therefore observes a uniform Result outcome for
    // handlers returning Result/Result<T>, because ExceptionBehaviour has already converted any
    // exception below it. ValidationBehaviour sits innermost, so a handler never runs on invalid
    // input, and a validator that throws is still converted by ExceptionBehaviour above it.
    options.PipelineBehaviors =
    [
        typeof(LoggingBehaviour<,>),
        typeof(ExceptionBehaviour<,>),
        typeof(ValidationBehaviour<,>),
    ];
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Maps /health and /alive — in the Development environment only, because they are
// unauthenticated. See the security note in ServiceDefaults/Extensions.cs.
app.MapDefaultEndpoints();

#pragma warning disable S6966 // Awaitable method should be used
app.Run();
#pragma warning restore S6966 // Awaitable method should be used
