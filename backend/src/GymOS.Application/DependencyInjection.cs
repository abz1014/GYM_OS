using System.Reflection;
using FluentValidation;
using GymOS.Application.Common.Behaviors;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Application.Modules.Migration.EntityHandlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GymOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // Order = execution order, outermost first.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantScopeBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BranchScopeBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        services.AddScoped<IImportEntityHandler, MemberImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, TrainerImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, EquipmentImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, InventoryImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, LeadImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, MembershipImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, AttendanceImportEntityHandler>();
        services.AddScoped<IImportEntityHandler, PaymentImportEntityHandler>();

        // Member Experience Engine: shared idempotent write-paths used by the domain-event handlers.
        services.AddScoped<IMemberXpService, MemberXpService>();
        services.AddScoped<IWorkoutProgressionService, WorkoutProgressionService>();
        services.AddScoped<IAchievementService, AchievementService>();

        return services;
    }
}
