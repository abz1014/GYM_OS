using System.Text;
using System.Text.Json.Serialization;
using GymOS.API.Authorization;
using GymOS.API.Middleware;
using GymOS.Application;
using GymOS.Infrastructure;
using GymOS.Infrastructure.BackgroundJobs;
using GymOS.Infrastructure.Persistence;
using GymOS.Infrastructure.RealTime;
using GymOS.Infrastructure.Seeding;
using GymOS.Shared;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

/*
 * Container platforms (Railway, Render, Fly, Cloud Run) hand the app its port in PORT and route
 * traffic there. Kestrel does not read PORT — it reads ASPNETCORE_URLS — so without this the
 * container listens on :8080, the platform health-checks the port it assigned, gets nothing, and
 * reports the deploy as failed with an application log that looks completely healthy.
 *
 * 0.0.0.0, not localhost: binding the loopback inside a container makes it unreachable from outside
 * it, which fails in exactly the same silent way.
 *
 * An explicit ASPNETCORE_URLS still wins, so local runs and `dotnet run` are untouched.
 */
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");
}

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
// Allow-list the two environments that legitimately carry the placeholder, rather than blocking only
// Production. The comment above always said "something other than Development/Testing", but the check
// named Production specifically — so a host set to Staging, Preview, or any other name would have
// started up signing real tokens with a key published in this file.
var placeholderIsAcceptable = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

if (!placeholderIsAcceptable && signingKey.StartsWith("CHANGE_ME", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"Refusing to start in '{builder.Environment.EnvironmentName}' with the placeholder Jwt:SigningKey " +
        "from appsettings.json. Set the Jwt__SigningKey environment variable to a real secret before deploying.");
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

/*
 * Same fail-loud rule as the signing key, for the opposite failure.
 *
 * An unset origins list does not open the API up — WithOrigins([]) allows nobody — it makes the
 * deployed frontend silently unable to call its own backend, surfacing as every request failing in
 * the browser with nothing wrong in the API logs. That is an afternoon of debugging the wrong layer.
 * The checked-in default is http://localhost:5173, which is correct locally and useless in
 * production, so a real deployment must set Cors__AllowedOrigins__0 to the site's origin.
 */
if (!placeholderIsAcceptable && (allowedOrigins.Length == 0 || allowedOrigins.All(o => o.Contains("localhost"))))
{
    throw new InvalidOperationException(
        $"Refusing to start in '{builder.Environment.EnvironmentName}' with no non-localhost CORS origin. " +
        "Set Cors__AllowedOrigins__0 to the deployed frontend's origin (e.g. https://your-app.vercel.app).");
}

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

/*
 * `--migrate` applies pending migrations and exits, matching `--seed`'s shape.
 *
 * A container platform gives you one start command and an empty database, so something has to
 * create the schema. The options were auto-migrating on every boot — which silently reshapes a
 * production database whenever someone redeploys, and races itself the moment there is more than
 * one instance — or making it an explicit, separately-invokable step. This is the explicit step:
 * run it once as a pre-deploy/release command, and the web process never touches the schema.
 */
if (args.Contains("--migrate"))
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Migrations applied.");
    return;
}

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
// RequireAuthorization on all three. The JWT was already being read off the query string for /hubs
// paths (see OnMessageReceived above) but nothing insisted on it, so any anonymous client could
// open a socket and — since the older two hubs take a group id from the caller — subscribe to a
// branch or tenant it had no claim to. The frontend already sends the token, so nothing client-side
// changes.
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();
app.MapHub<CoachingHub>("/hubs/coaching").RequireAuthorization();

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
