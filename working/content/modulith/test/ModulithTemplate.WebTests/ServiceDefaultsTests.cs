using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ModulithTemplate.WebTests;

/// <summary>
/// Registration smoke tests for the Aspire ServiceDefaults copy in
/// <c>ModulithTemplate.ServiceDefaults</c>. These exist to catch a package upgrade that
/// silently stops registering telemetry or the health checks, not to test Aspire's own code.
/// </summary>
public class ServiceDefaultsTests
{
    private static WebApplicationBuilder CreateBuilder(string environmentName) =>
        WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });

    /// <summary>The route patterns of every endpoint mapped on <paramref name="app"/> so far.</summary>
    private static List<string> MappedRoutes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToList();

    [Fact]
    public async Task AddServiceDefaults_always_registers_the_self_health_check_tagged_live()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();

        // Act
        await using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

        // Assert
        var self = Assert.Single(
            options.Value.Registrations,
            registration => string.Equals(registration.Name, "self", StringComparison.Ordinal));
        Assert.Contains("live", self.Tags, StringComparer.Ordinal);
    }

    [Fact]
    public async Task AddServiceDefaults_always_registers_the_tracer_and_meter_providers()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();

        // Act
        await using var app = builder.Build();

        // Assert
        Assert.NotNull(app.Services.GetService<TracerProvider>());
        Assert.NotNull(app.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task MapDefaultEndpoints_in_development_maps_health_and_alive()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();
        await using var app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert
        var routes = MappedRoutes(app);
        Assert.Contains("/health", routes, StringComparer.Ordinal);
        Assert.Contains("/alive", routes, StringComparer.Ordinal);
    }

    [Fact]
    public async Task MapDefaultEndpoints_outside_development_maps_nothing()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production);
        builder.AddServiceDefaults();
        await using var app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert
        var routes = MappedRoutes(app);
        Assert.DoesNotContain("/health", routes, StringComparer.Ordinal);
        Assert.DoesNotContain("/alive", routes, StringComparer.Ordinal);
    }
}
