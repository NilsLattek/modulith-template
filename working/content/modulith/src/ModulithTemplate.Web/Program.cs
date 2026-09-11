using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Features.Payments.Web;
using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Outbox;
using ModulithTemplate.SharedKernel.Outbox.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;
using ModulithTemplate.Web.Components;

using Underground.Outbox.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Aspire's ServiceDefaults: OpenTelemetry tracing, metrics and logging, health checks, service
// discovery and HTTP resilience. First, so telemetry is in place before anything else registers.
builder.AddServiceDefaults();

// Owns the shared outbox table's DDL. Registered here rather than by a feature because
// update-database.sh and CI's migration checks enumerate contexts from this container.
builder.Services.AddOutboxDbContext(builder.Configuration);

// The worker that claims and delivers staged rows. AddOutboxServices comes from the outbox source
// generator, which the library requires to be referenced by the DI root — so this call can only be
// made here, not from Shared.Outbox.
builder.Services.AddOutboxServices<OutboxContext>(_ => { });

// After AddOutboxServices, deliberately: it replaces that generator's dispatcher, which can only
// route to handler classes naming a concrete message type. See IntegrationEventRepublisher.
builder.Services.AddIntegrationEventDelivery();

builder.ConfigureOrdersFeature();
builder.ConfigurePaymentsFeature();

builder.Services.AddMediator(options =>
{
    // Scoped, not the library default of Singleton: handlers inject repositories bound to a scoped
    // DbContext. That makes an @injected IMediator resolve from the Blazor circuit scope, which
    // would hold one DbContext open for the connection — hence WithNewScopeAsync per operation.
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
