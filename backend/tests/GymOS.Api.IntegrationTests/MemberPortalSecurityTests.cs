using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymOS.Api.IntegrationTests.TestSupport;
using GymOS.Application.Modules.Auth.Commands;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Api.IntegrationTests;

/// <summary>
/// Regression coverage for a real cross-member data exposure found by manual verification: a
/// Member-role account (seeded with Dashboard/Attendance/Workouts/Nutrition.View, the staff-wide
/// view permissions) could read the executive dashboard, all 500 attendance records, and any
/// other member's workout/diet/water logs just by supplying their id — because those endpoints
/// trust a caller-supplied memberId and only check "does this role have View", never "is this
/// your own record". Fixed by giving the Member role Portal.View only, and a /api/me/* surface
/// that resolves "whose data" server-side from the JWT and accepts no id parameter at all.
/// </summary>
public class MemberPortalSecurityTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    [Fact]
    public async Task Portal_member_gets_403_on_the_staff_wide_dashboard_and_attendance_endpoints()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = await TestDataSeeder.SeedPortalMemberAsync(db);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, member.Email));

        (await client.GetAsync("/api/dashboard/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/attendance?page=1&pageSize=10")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Portal_member_reads_only_their_own_profile_and_attendance()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = await TestDataSeeder.SeedPortalMemberAsync(db);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, member.Email));

        var profileResponse = await client.GetAsync("/api/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<MeProfile>();
        profile!.id.ShouldBe(member.MemberId);

        var attendanceResponse = await client.GetAsync("/api/me/attendance?page=1&pageSize=10");
        attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var attendance = await attendanceResponse.Content.ReadFromJsonAsync<MePagedAttendance>();
        attendance!.totalCount.ShouldBe(1);
        attendance.items[0].memberId.ShouldBe(member.MemberId);
    }

    [Fact]
    public async Task A_memberId_query_parameter_smuggled_onto_the_portal_endpoint_is_ignored()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var attacker = await TestDataSeeder.SeedPortalMemberAsync(db);
        var victim = await TestDataSeeder.SeedPortalMemberAsync(db);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, attacker.Email));

        // The portal controller action has no memberId parameter to bind to — this proves that
        // structurally, not just by convention.
        var response = await client.GetAsync($"/api/me/attendance?memberId={victim.MemberId}&page=1&pageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var attendance = await response.Content.ReadFromJsonAsync<MePagedAttendance>();

        attendance!.items.ShouldAllBe(a => a.memberId == attacker.MemberId);
        attendance.items.ShouldNotContain(a => a.memberId == victim.MemberId);
    }

    [Fact]
    public async Task A_portal_user_with_no_linked_member_row_gets_404_not_someone_elses_data()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        // Portal.View granted, but no Member row links back to this user.
        var (_, _, email) = await TestDataSeeder.SeedUserWithPermissionsAsync(db, "portal.view");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, email));

        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }

    // Minimal local shapes — avoids pulling MemberDetailDto's full surface into the test just to
    // read two fields.
    private record MeProfile(Guid id);

    private record MeAttendanceItem(Guid memberId);

    private record MePagedAttendance(List<MeAttendanceItem> items, int totalCount);
}
