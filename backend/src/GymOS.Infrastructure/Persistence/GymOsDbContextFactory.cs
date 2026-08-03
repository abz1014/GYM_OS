using GymOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GymOS.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef migrations add/database update` at design time, so migrations can be
/// authored without needing the full API host (and its real DI container) to build first. Not
/// used at runtime — GymOsDbContext is otherwise always constructed via AddInfrastructure's DI
/// registration, with real ITenantProvider/ICurrentUserService/IDateTimeProvider implementations.
/// </summary>
public class GymOsDbContextFactory : IDesignTimeDbContextFactory<GymOsDbContext>
{
    public GymOsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GYMOS_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=gymos_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<GymOsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new GymOsDbContext(optionsBuilder.Options, new DesignTimeTenantProvider(), new DesignTimeCurrentUserService(), new DesignTimeDateTimeProvider());
    }

    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid? TenantId => null;
        public Guid? BranchId => null;
    }

    private class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public Guid? BranchId => null;
        public string? Email => null;
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => [];
        public bool IsAuthenticated => false;
        public bool HasPermission(string permissionCode) => false;
    }

    private class DesignTimeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
