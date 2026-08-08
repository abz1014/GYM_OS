using System.Text;
using System.Text.Json.Serialization;
using GymOS.API.Authorization;
using GymOS.API.Middleware;
using GymOS.Application;
using GymOS.Infrastructure;
using GymOS.Infrastructure.BackgroundJobs;
using GymOS.Infrastructure.RealTime;
using GymOS.Infrastructure.Seeding;
using GymOS.Shared;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<GymOS.Infrastructure.HealthChecks.DatabaseHealthCheck>("database");

builder.Services.AddControllers()
    // Enums serialize as their string names (e.g. "Active") rather than raw ints — the frontend's
    // TypeScript types model every status/type field as a string union, not a magic number.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GymOS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT access token (from POST /api/auth/login) — no need to type \"Bearer \" first."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

var jwtSection = builder.Configuration.GetSection(GymOS.Infrastructure.Identity.JwtSettings.SectionName);
var signingKey = jwtSection[nameof(GymOS.Infrastructure.Identity.JwtSettings.SigningKey)]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// The checked-in appsettings.json fallback is a placeholder ("CHANGE_ME...") meant only for local
// Development, where appsettings.Development.json (gitignored) overrides it with a real value —
// see README "Production Deployment". Without this guard, deploying with ASPNETCORE_ENVIRONMENT
// unset or set to something other than Development/Testing and no Jwt__SigningKey environment
// variable would silently start the API signing real user tokens with a key visible to anyone who
// reads this public source file. Fail loudly instead of shipping that silently.
if (builder.Environment.IsProduction() && signingKey.StartsWith("CHANGE_ME", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Refusing to start in Production with the placeholder Jwt:SigningKey from appsettings.json. " +
        "Set the Jwt__SigningKey environment variable to a real secret before deploying.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection[nameof(GymOS.Infrastructure.Identity.JwtSettings.Issuer)],
            ValidateAudience = true,
            ValidAudience = jwtSection[nameof(GymOS.Infrastructure.Identity.JwtSettings.Audience)],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // WebSocket connections (SignalR) can't set an Authorization header, so accept the token via query string for hub paths only.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var code in PermissionCodes.All)
    {
        options.AddPolicy(code, policy => policy.Requirements.Add(new PermissionRequirement(code)));
    }
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (args.Contains("--seed"))
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseMiddleware<PermissionResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<MembershipExpiryCheckJob>("membership-expiry-check", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<MembershipExpiryTransitionJob>("membership-expiry-transition", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<InvoiceOverdueTransitionJob>("invoice-overdue-transition", job => job.RunAsync(CancellationToken.None), Cron.Daily);
// Weekly, not daily. Nothing about a two-year retention boundary is urgent to the day, and a job
// that deletes member correspondence should run as rarely as it can still do its job.
RecurringJob.AddOrUpdate<CoachMessageRetentionJob>("coach-message-retention", job => job.RunAsync(CancellationToken.None), Cron.Weekly);
RecurringJob.AddOrUpdate<BirthdayCheckJob>("birthday-check", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<MaintenanceDueCheckJob>("maintenance-due-check", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<LowStockCheckJob>("low-stock-check", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<FollowUpReminderCheckJob>("follow-up-reminder-check", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<ClassSessionGenerationJob>("class-session-generation", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<RecurringBillingJob>("recurring-billing", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<ChurnRiskWinBackJob>("churn-risk-winback", job => job.RunAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<LeadDripJob>("lead-drip", job => job.RunAsync(CancellationToken.None), Cron.Daily);
// Class reminders fire on the dispatch cadence, not daily — a 3-hour-ahead nudge is only useful in a narrow window.
RecurringJob.AddOrUpdate<ClassReminderJob>("class-reminder", job => job.RunAsync(CancellationToken.None), "*/5 * * * *");
RecurringJob.AddOrUpdate<NotificationDispatchJob>("notification-dispatch", job => job.RunAsync(CancellationToken.None), "*/5 * * * *");

app.Run();

// Exposes the top-level-statement Program for WebApplicationFactory<Program> in integration tests.
public partial class Program;
