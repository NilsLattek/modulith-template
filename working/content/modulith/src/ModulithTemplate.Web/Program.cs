using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Features.Payments.Web;
using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Outbox;
using ModulithTemplate.SharedKernel.Web;
using ModulithTemplate.SharedKernel.Outbox.Data;
using ModulithTemplate.Web.Components;

using Underground.Outbox.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Aspire's ServiceDefaults: OpenTelemetry tracing, metrics and logging, health checks, service
// discovery and HTTP resilience. First, so telemetry is in place before anything else registers.
builder.AddServiceDefaults();

// Owns the shared outbox table's DDL. Registered here rather than by a feature because
// update-database.sh and CI's migration checks enumerate contexts from this container.
builder.Services.AddOutboxDbContext(builder.Configuration);
builder.Services.AddOutboxServices<OutboxContext>(_ => { });

builder.ConfigureOrdersFeature();
builder.ConfigurePaymentsFeature();

builder.Services.AddMediator(options =>
{
    // Scoped, not the library default of Singleton: handlers inject repositories bound to a scoped
    // DbContext. That makes an @injected IMediator resolve from the Blazor circuit scope, which
    // would hold one DbContext open for the connection — hence IScopedMediator for components.
    options.ServiceLifetime = ServiceLifetime.Scoped;

    // Outermost first, so LoggingBehaviour sees a uniform Result outcome (ExceptionBehaviour below
    // it has already converted any exception), and ValidationBehaviour wraps the handler alone.
    options.PipelineBehaviors =
    [
        typeof(LoggingBehaviour<,>),
        typeof(ExceptionBehaviour<,>),
        typeof(ValidationBehaviour<,>),
    ];
});

// Components send database work through this, never a directly-injected IMediator: it opens a scope
// per message, so no DbContext outlives the operation that needed it.
builder.Services.AddScopedMediator();

// Each feature's Web project also calls AddValidation, for its own [ValidatableType] form models.
builder.Services.AddValidation();

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
