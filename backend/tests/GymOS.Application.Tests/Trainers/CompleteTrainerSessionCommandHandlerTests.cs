using FluentValidation;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Trainers;

/// <summary>
/// CompleteTrainerSessionCommand's core business rule is a state-machine guard: only a Scheduled
/// session can be completed, so a session can't be completed twice or completed after cancellation.
/// </summary>
public class CompleteTrainerSessionCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Completing_a_scheduled_session_marks_it_completed_with_a_timestamp()
    {
        var (tenantId, sessionId) = await SeedAsync(TrainerSessionStatus.Scheduled);
        CurrentUser.TenantId = tenantId;

        await SendAsync(new CompleteTrainerSessionCommand(sessionId, "Great session"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var session = await db.TrainerSessions.SingleAsync(s => s.Id == sessionId);

        session.Status.ShouldBe(TrainerSessionStatus.Completed);
        session.CompletedAt.ShouldNotBeNull();
        session.Notes.ShouldBe("Great session");
    }

    [Fact]
    public async Task Completing_an_already_completed_session_is_rejected()
    {
        var (tenantId, sessionId) = await SeedAsync(TrainerSessionStatus.Completed);
        CurrentUser.TenantId = tenantId;

        var act = () => SendAsync(new CompleteTrainerSessionCommand(sessionId, null));

        (await Should.ThrowAsync<ValidationException>(act)).Message.ShouldContain("Only a scheduled session can be completed");
    }

    [Fact]
    public async Task Completing_a_cancelled_session_is_rejected()
    {
        var (tenantId, sessionId) = await SeedAsync(TrainerSessionStatus.Cancelled);
        CurrentUser.TenantId = tenantId;

        var act = () => SendAsync(new CompleteTrainerSessionCommand(sessionId, null));

        await Should.ThrowAsync<ValidationException>(act);
    }

    private async Task<(Guid TenantId, Guid SessionId)> SeedAsync(TrainerSessionStatus status)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var trainerUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Coach",
            LastName = "User"
        };
        db.Users.Add(trainerUser);

        var trainer = new Trainer
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = trainerUser.Id,
            Specialties = "Strength",
            CommissionRate = 20
        };
        db.Trainers.Add(trainer);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Client",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var assignment = new TrainerAssignment { TrainerId = trainer.Id, MemberId = member.Id, StartDate = new DateOnly(2025, 1, 1) };
        db.TrainerAssignments.Add(assignment);

        var session = new TrainerSession
        {
            TrainerAssignmentId = assignment.Id,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
            DurationMinutes = 60,
            Status = status
        };
        db.TrainerSessions.Add(session);

        await db.SaveChangesAsync();
        return (tenant.Id, session.Id);
    }
}
