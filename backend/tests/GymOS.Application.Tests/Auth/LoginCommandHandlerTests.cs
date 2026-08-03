using GymOS.Application.Modules.Auth.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Identity;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Shouldly;

namespace GymOS.Application.Tests.Auth;

/// <summary>
/// LoginCommand is [AllowAnonymous] and runs before any JWT/tenant context exists, which is
/// exactly the gap the Phase 5 recheck found in audit logging (see AuditLogWriter). These tests
/// exercise the real pipeline end to end, including that self-audit call.
/// </summary>
public class LoginCommandHandlerTests : ApplicationTestBase
{
    private const string CorrectPassword = "Correct@12345";

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await SeedUserAsync("wrongpw@example.com", mfaEnabled: false);

        var act = () => SendAsync(new LoginCommand("wrongpw@example.com", "NotThePassword1", null));

        (await Should.ThrowAsync<UnauthorizedAccessException>(act)).Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Unknown_email_is_rejected_with_the_same_generic_message()
    {
        var act = () => SendAsync(new LoginCommand("nobody@example.com", CorrectPassword, null));

        (await Should.ThrowAsync<UnauthorizedAccessException>(act)).Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Correct_password_with_MFA_disabled_succeeds()
    {
        await SeedUserAsync("nomfa@example.com", mfaEnabled: false);

        var result = await SendAsync(new LoginCommand("nomfa@example.com", CorrectPassword, null));

        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MFA_enabled_without_a_code_is_rejected()
    {
        await SeedUserAsync("mfa1@example.com", mfaEnabled: true);

        var act = () => SendAsync(new LoginCommand("mfa1@example.com", CorrectPassword, null));

        (await Should.ThrowAsync<UnauthorizedAccessException>(act)).Message.ShouldBe("A valid MFA code is required.");
    }

    [Fact]
    public async Task MFA_enabled_with_the_wrong_code_is_rejected()
    {
        await SeedUserAsync("mfa2@example.com", mfaEnabled: true);

        var act = () => SendAsync(new LoginCommand("mfa2@example.com", CorrectPassword, "000000"));

        (await Should.ThrowAsync<UnauthorizedAccessException>(act)).Message.ShouldBe("A valid MFA code is required.");
    }

    [Fact]
    public async Task MFA_enabled_with_the_correct_code_succeeds()
    {
        var (_, _, secret) = await SeedUserAsync("mfa3@example.com", mfaEnabled: true);
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var result = await SendAsync(new LoginCommand("mfa3@example.com", CorrectPassword, code));

        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Successful_login_writes_a_redacted_audit_entry_despite_running_pre_auth()
    {
        var (tenantId, userId, _) = await SeedUserAsync("audited@example.com", mfaEnabled: false);

        await SendAsync(new LoginCommand("audited@example.com", CorrectPassword, null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        // AuditBehavior can't see a TenantId for anonymous commands, so this row only exists
        // because LoginCommandHandler calls AuditLogWriter directly once it resolves the user.
        var entry = await db.AuditLogs.IgnoreQueryFilters()
            .SingleAsync(a => a.Action == nameof(LoginCommand) && a.UserId == userId);

        entry.TenantId.ShouldBe(tenantId);
        entry.DataAfter.ShouldNotBeNull();
        entry.DataAfter.ShouldContain("audited@example.com");
        entry.DataAfter.ShouldNotContain(CorrectPassword);
        entry.DataAfter.ShouldContain("REDACTED");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_correct_password_on_the_sixth_try()
    {
        await SeedUserAsync("bruteforce@example.com", mfaEnabled: false);

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<UnauthorizedAccessException>(
                () => SendAsync(new LoginCommand("bruteforce@example.com", "WrongPassword", null)));
        }

        var act = () => SendAsync(new LoginCommand("bruteforce@example.com", CorrectPassword, null));

        (await Should.ThrowAsync<UnauthorizedAccessException>(act)).Message.ShouldStartWith("Too many failed login attempts.");
    }

    [Fact]
    public async Task A_successful_login_resets_the_failed_attempt_counter()
    {
        await SeedUserAsync("resets@example.com", mfaEnabled: false);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => SendAsync(new LoginCommand("resets@example.com", "WrongPassword", null)));
        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => SendAsync(new LoginCommand("resets@example.com", "WrongPassword", null)));

        await SendAsync(new LoginCommand("resets@example.com", CorrectPassword, null));

        // 3 more wrong attempts after a successful login is only 3, not 5 — proves the counter
        // was reset by the successful login rather than carrying over from the earlier failures.
        for (var i = 0; i < 3; i++)
        {
            await Should.ThrowAsync<UnauthorizedAccessException>(
                () => SendAsync(new LoginCommand("resets@example.com", "WrongPassword", null)));
        }

        var result = await SendAsync(new LoginCommand("resets@example.com", CorrectPassword, null));
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    private async Task<(Guid TenantId, Guid UserId, string MfaSecret)> SeedUserAsync(string email, bool mfaEnabled)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var secret = new TotpService().GenerateSecret();

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = new PasswordHasher().Hash(CorrectPassword),
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            MfaEnabled = mfaEnabled,
            MfaSecret = mfaEnabled ? secret : null
        };
        db.Users.Add(user);

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, secret);
    }
}
