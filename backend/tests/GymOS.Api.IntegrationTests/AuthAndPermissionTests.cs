using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymOS.Api.IntegrationTests.TestSupport;
using GymOS.Application.Modules.Auth.Commands;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Api.IntegrationTests;

/// <summary>
/// Exercises the full real pipeline (TestServer -> ExceptionHandlingMiddleware -> JWT auth ->
/// PermissionResolutionMiddleware -> policy-based authorization -> controller -> MediatR) against
/// a real Postgres database, the same wiring the manual curl-based verification used throughout
/// this session's live testing — just automated and repeatable.
/// </summary>
public class AuthAndPermissionTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var (_, _, email) = await TestDataSeeder.SeedUserWithPermissionsAsync(db);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, "WrongPassword1", null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_a_usable_access_token()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var (_, _, email) = await TestDataSeeder.SeedUserWithPermissionsAsync(db);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/revenue");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_with_a_token_lacking_the_permission_returns_403()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        // Deliberately no permissions granted to this user's role.
        var (_, _, email) = await TestDataSeeder.SeedUserWithPermissionsAsync(db);

        var client = factory.CreateClient();
        var accessToken = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/reports/revenue");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Protected_endpoint_with_the_right_permission_returns_200()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var (_, _, email) = await TestDataSeeder.SeedUserWithPermissionsAsync(db, PermissionCodes.Reports.View);

        var client = factory.CreateClient();
        var accessToken = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/reports/revenue");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }
}
