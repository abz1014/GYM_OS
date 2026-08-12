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
/// The schema is dropped and rebuilt on every factory instantiation so tests never see leftover
/// state from a previous run.
///
/// EnsureCreated builds the schema straight from the EF model and skips migrations entirely, so
/// anything a migration adds that the model cannot express — the overpayment guard trigger — would
/// exist in production and be silently absent from every test here. Migrate() would fix that and was
/// tried; it makes each of the six fixtures replay the whole migration history against a freshly
/// dropped database, which turned a 42-second suite into one that had not finished in ten minutes.
/// So the schema is still built the fast way and the guards are applied on top, from BillingGuards —
/// the one thing EnsureCreated cannot know about, added back explicitly.
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
        db.Database.ExecuteSqlRaw(BillingGuards.OverpaymentTriggerSql);

        return host;
    }
}
