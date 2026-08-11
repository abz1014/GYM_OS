using GymOS.Application.Common.Interfaces;
using GymOS.Infrastructure.Identity;
using GymOS.Infrastructure.Messaging;
using GymOS.Infrastructure.Persistence;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GymOS.Application.Tests.TestSupport;

/// <summary>
/// Wires the real MediatR pipeline (DependencyInjection.AddApplication) against a SQLite
/// in-memory GymOsDbContext, using the real PasswordHasher/JwtTokenService/TotpService/
/// NoOpEmailSender rather than mocks. A test that calls SendAsync exercises the actual
/// TenantScope -> Logging -> Validation -> Transaction -> Audit pipeline and EF Core's global
/// query filters, not a stand-in for them.
///
/// Each SendAsync/CreateScope call gets its own DI scope (and its own GymOsDbContext instance)
/// sharing the same underlying SQLite connection, mirroring a real per-request DbContext
/// lifetime instead of hiding staleness bugs behind one long-lived context.
/// </summary>
public abstract class ApplicationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    protected FakeCurrentUserService CurrentUser { get; }

    protected FakeDateTimeProvider DateTimeProvider { get; }

    /// <summary>Captures the live-dashboard signals handlers raise, so commands that announce branch
    /// activity (CheckInCommand) are exercisable here instead of only through the API tests.</summary>
    protected FakeDashboardNotifier DashboardNotifier { get; }

    protected FakeCoachingNotifier CoachingNotifier { get; }

    protected FakePaymentGateway PaymentGateway { get; }

    protected ApplicationTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        CurrentUser = new FakeCurrentUserService();
        DateTimeProvider = new FakeDateTimeProvider();
        DashboardNotifier = new FakeDashboardNotifier();
        CoachingNotifier = new FakeCoachingNotifier();
        PaymentGateway = new FakePaymentGateway();

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<ICurrentUserService>(CurrentUser);
        services.AddSingleton<ITenantProvider>(CurrentUser);
        services.AddSingleton<IDateTimeProvider>(DateTimeProvider);
        services.AddSingleton<IDashboardNotifier>(DashboardNotifier);
        services.AddSingleton<ICoachingNotifier>(CoachingNotifier);
        services.AddSingleton<IPaymentGateway>(PaymentGateway);

        services.AddDbContext<GymOsDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<GymOsDbContext>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IJwtTokenService>(new JwtTokenService(Options.Create(new JwtSettings
        {
            SigningKey = "test-only-signing-key-at-least-32-characters-long",
            Issuer = "GymOS.Tests",
            Audience = "GymOS.Tests.Client",
            AccessTokenLifetimeMinutes = 20
        })));
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<ISmsSender, NoOpSmsSender>();
        services.AddScoped<IWhatsAppSender, NoOpWhatsAppSender>();
        services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();

        services.AddApplication();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<GymOsDbContext>().Database.EnsureCreated();
    }

    protected IServiceScope CreateScope() => _provider.CreateScope();

    /// <summary>Dispatches through the real ISender — the full pipeline runs, not just the handler.</summary>
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
