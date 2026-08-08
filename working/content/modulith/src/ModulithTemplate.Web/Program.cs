using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Web.Behaviours;
using ModulithTemplate.Web.Components;

var builder = WebApplication.CreateBuilder(args);

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
    // exception below it.
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>)];
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

#pragma warning disable S6966 // Awaitable method should be used
app.Run();
#pragma warning restore S6966 // Awaitable method should be used
