using GymOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GymOS.Api.IntegrationTests.TestSupport;

/// <summary>
/// Boots the real API (real Postgres via Npgsql, real Hangfire storage, real middleware pipeline)
/// against a dedicated "gymos_test" database — never gymos_dev, which holds the demo seed data
/// this session relies on for manual verification. appsettings.Testing.json points GymOsDb there.
/// The schema is dropped and recreated on every factory instantiation so tests never see leftover
/// state from a previous run.
/// </summary>
public class GymOsWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        return host;
    }
}
