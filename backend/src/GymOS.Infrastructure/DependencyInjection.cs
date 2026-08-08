using GymOS.Application.Common.Interfaces;
using GymOS.Infrastructure.Common;
using GymOS.Infrastructure.Identity;
using GymOS.Infrastructure.Messaging;
using GymOS.Infrastructure.Payments;
using GymOS.Infrastructure.Persistence;
using GymOS.Infrastructure.RealTime;
using GymOS.Infrastructure.Reports;
using GymOS.Infrastructure.Seeding;
using GymOS.Infrastructure.Storage;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GymOsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("GymOsDb")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<GymOsDbContext>());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<ICurrentUserService>());

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IPaymentGateway, NoOpPaymentGateway>();
        services.AddSingleton<IReportExporter, ClosedXmlReportExporter>();
        services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();

        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddScoped<ISmsSender, NoOpSmsSender>();
        services.AddScoped<IWhatsAppSender, NoOpWhatsAppSender>();
        services.AddScoped<IInAppSender, InAppSender>();

        var storageProvider = configuration.GetSection(StorageSettings.SectionName)[nameof(StorageSettings.Provider)];
        if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }
        else
        {
            services.AddSingleton<IObjectStorage, LocalDiskObjectStorage>();
        }

        services.AddSignalR();
        services.AddScoped<IDashboardNotifier, DashboardNotifier>();

        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<BackgroundJobs.MembershipExpiryCheckJob>();
        services.AddScoped<BackgroundJobs.MembershipExpiryTransitionJob>();
        services.AddScoped<BackgroundJobs.InvoiceOverdueTransitionJob>();
        services.AddScoped<BackgroundJobs.BirthdayCheckJob>();
        services.AddScoped<BackgroundJobs.MaintenanceDueCheckJob>();
        services.AddScoped<BackgroundJobs.LowStockCheckJob>();
        services.AddScoped<BackgroundJobs.FollowUpReminderCheckJob>();
        services.AddScoped<BackgroundJobs.ClassSessionGenerationJob>();
        services.AddScoped<BackgroundJobs.RecurringBillingJob>();
        services.AddScoped<BackgroundJobs.ChurnRiskWinBackJob>();
        services.AddScoped<BackgroundJobs.ClassReminderJob>();
        services.AddScoped<BackgroundJobs.NotificationDispatchJob>();
        services.AddScoped<INotificationSchedulerService, BackgroundJobs.NotificationSchedulerService>();

        var connectionString = configuration.GetConnectionString("GymOsDb");
        services.AddHangfire(hangfireConfig => hangfireConfig
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }
}
