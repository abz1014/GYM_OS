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

/*
 * `--migrate` and `--seed` are exempt from both startup guards below, and that exemption is the whole
 * reason this flag exists.
 *
 * Both guards protect an API that is about to SERVE HTTP: one stops it signing real tokens with a
 * published key, the other stops it going live unreachable by its own frontend. Neither risk exists
 * for a command that opens a database connection, applies migrations and exits. But `builder.Build()`
 * has to run before those branches can be reached, so the guards were executing first — meaning a
 * pre-deploy migration would abort with "Refusing to start ... with no non-localhost CORS origin".
 *
 * A schema migration failing on a CORS setting is the kind of error that sends you to the wrong layer
 * for an hour. Worse, on Railway the pre-deploy command runs BEFORE the frontend exists, so on a
 * first deploy there is no origin to give it — the guard would block the very migration needed to
 * make the app deployable at all.
 *
 * The web path is untouched: `dotnet GymOS.API.dll` with no arguments still gets both guards.
 */
var isCliCommand = args.Contains("--migrate") || args.Contains("--seed");

if (!placeholderIsAcceptable && !isCliCommand && signingKey.StartsWith("CHANGE_ME", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"Refusing to start in '{builder.Environment.EnvironmentName}' with the placeholder Jwt:SigningKey " +
        "from appsettings.json. Set the Jwt__SigningKey environment variable to a real secret before deploying.");
}

/*
 * Length, not just presence — and this one is checked in EVERY environment, unlike the placeholder
 * guard above.
 *
 * HS256 signs with Encoding.UTF8.GetBytes(SigningKey) and requires 256 bits of key material.
 * Microsoft.IdentityModel throws IDX10653 for a shorter key and IDX10703 for an empty one, and it
 * throws at the moment a token is SIGNED — not at startup.
 *
 * That timing is what makes it vicious. The app boots clean, /health reports Healthy, a wrong
 * password returns a correct 401, and a nonexistent account returns a correct 401 — because not one
 * of those paths signs anything. The single line that does runs immediately AFTER a password is
 * verified, so the only visible symptom is that correct credentials produce a 500 while incorrect
 * ones behave perfectly. Every signal a deploy dashboard shows you says the service is fine.
 *
 * This is not hypothetical: the first GymOS deployment set Jwt__SigningKey to the literal string
 * "<your generated secret>" — the instruction, pasted as the value. 23 bytes. It is not null, and it
 * does not begin with "CHANGE_ME", so both existing guards passed it through, and the only way to
 * find it was to read a stack trace.
 *
 * A secret that cannot sign is as broken in Development as in Production, so there is no environment
 * in which letting it through helps. CLI commands are exempt on the same reasoning as the guards
 * above: --migrate and --seed never issue a token, and a migration must not be blocked by a setting
 * it does not use.
 */
var signingKeyBytes = Encoding.UTF8.GetByteCount(signingKey);

if (!isCliCommand && signingKeyBytes < 32)
{
    throw new InvalidOperationException(
        $"Jwt:SigningKey is {signingKeyBytes} bytes; HS256 requires at least 32 (256 bits). " +
        "Generate one with `openssl rand -base64 48`. A shorter key starts the API successfully and " +
        "then fails only on the first CORRECT login, which is the hardest possible way to find it.");
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
if (!placeholderIsAcceptable && !isCliCommand &&
    (allowedOrigins.Length == 0 || allowedOrigins.All(o => o.Contains("localhost"))))
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

    // Dispose before returning, or this log line may never be printed. The console logger writes from
    // a background queue that is flushed on disposal, and a bare `return` from top-level statements
    // ends the process without disposing the host. On a deploy platform that produces a green
    // pre-deploy step with no output — indistinguishable from a migration that did nothing.
    await app.DisposeAsync();
    return;
}

if (args.Contains("--seed"))
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<DemoDataSeeder>();

    /*
     * `--seed --pilot` builds the small, fully-loginable gym instead of the 300-member sales demo
     * (see SeedProfile). A flag rather than a config setting: the profile is a property of the one
     * time you populate a database, not of how the service runs, and a setting left behind in an
     * environment would silently decide the shape of the NEXT empty database somebody points at it.
     */
    var profile = args.Contains("--pilot") ? SeedProfile.Pilot : SeedProfile.Demo;
    await seeder.SeedAsync(profile);
    await app.DisposeAsync();
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
