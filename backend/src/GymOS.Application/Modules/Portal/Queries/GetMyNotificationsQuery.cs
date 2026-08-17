using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// What the gym has actually said to this member — expiry warnings, failed-payment notices, "your
/// coach has written", class reminders — most recent first.
///
/// **Why this reads ScheduledNotification and not NotificationLog.** Both tables look like
/// candidates and only one is usable. NotificationLog holds the rendered text that went out, but it
/// addresses a recipient by bare string (an email address or a phone number) and carries no member
/// link at all: matching a member to their own log rows would mean matching on address, which is
/// wrong the moment two people share a family email and is a data leak the first time it happens.
/// ScheduledNotification carries <c>RecipientMemberId</c> — an actual foreign key — so "mine" is a
/// fact here rather than a guess.
///
/// The cost of that choice is that the content lives one hop away on the template, unrendered. The
/// dispatch job substitutes placeholders at send time and writes the result only into the log, so
/// the member's own name is the one substitution reproduced here — without it every notification in
/// this feed opens with a literal "Hi {{FirstName}}". Placeholders that depend on the related entity
/// (an expiry date, an asset name) still resolve only in the delivered copy; making them resolve
/// here too would mean reimplementing the dispatch job's resolver in a read query, and it would go
/// stale the first time either side changed alone.
/// </summary>
public record GetMyNotificationsQuery : IQuery<List<MyNotificationDto>>;

public class GetMyNotificationsQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyNotificationsQuery, List<MyNotificationDto>>
{
    /// <summary>Enough to cover "what have I been told lately" without becoming an archive.</summary>
    private const int Take = 20;

    public async Task<List<MyNotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var member = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        var scheduled = await db.ScheduledNotifications.AsNoTracking()
            .Where(n => n.RecipientMemberId == memberId
                        // A cancelled notification was called back before it was sent — the member
                        // was never told this, so the feed must not claim they were.
                        && n.Status != ScheduledNotificationStatus.Cancelled)
            .Select(n => new
            {
                n.Id,
                n.ScheduledFor,
                Subject = n.NotificationTemplate!.Subject,
                Body = n.NotificationTemplate.BodyTemplate,
                Channel = (NotificationChannel?)n.NotificationTemplate.Channel,
            })
            .ToListAsync(cancellationToken);

        // ScheduledFor is a DateTimeOffset: SQLite cannot compare or order it, so the "not yet due"
        // cut and the newest-first sort both happen here. A member's own notification rows are a
        // small set — the same trade every DateTimeOffset query in this codebase makes.
        return scheduled
            // A notification scheduled for next Tuesday has not happened yet. Listing it now would
            // hand the member a renewal warning days before the gym decided to give it to them.
            .Where(n => n.ScheduledFor <= now && n.Channel is not null)
            .OrderByDescending(n => n.ScheduledFor)
            .Take(Take)
            .Select(n => new MyNotificationDto(
                n.Id,
                Personalise(n.Subject, member?.FirstName, member?.LastName),
                // An empty body is a template with nothing but a subject — surfaced as null so the
                // UI can render a title-only notice rather than a message that looks truncated.
                string.IsNullOrWhiteSpace(n.Body) || HasUnresolvedPlaceholder(Personalise(n.Body, member?.FirstName, member?.LastName))
                    ? null
                    : Personalise(n.Body, member?.FirstName, member?.LastName),
                n.Channel!.Value,
                n.ScheduledFor))
            .ToList();
    }

    private static string Personalise(string text, string? firstName, string? lastName)
        => text
            .Replace("{{FirstName}}", firstName ?? string.Empty)
            .Replace("{{LastName}}", lastName ?? string.Empty);

    /*
     * Anything still wearing {{braces}} after personalisation is a value this query cannot resolve.
     *
     * Only the member's own name lives on the row; the rest — {{ClassName}}, {{StartsAt}},
     * {{ExpiryDate}} — are filled from the related entity by the dispatch job when the message is
     * actually delivered, and are still literal template syntax here. Verified live: a class
     * reminder read "reminder: {{ClassName}} starts {{StartsAt}}. See you there!".
     *
     * Showing a member template source is worse than showing them nothing: it is not information,
     * it looks broken, and it breaks the rule this codebase holds everywhere else — never render a
     * value that isn't backed by real data. The subject alone ("Your class is coming up") is
     * complete and true, so an unresolvable body is dropped and the notice renders title-only,
     * which the UI already handles for templates that have no body at all.
     */
    private static bool HasUnresolvedPlaceholder(string text)
        => text.Contains("{{", StringComparison.Ordinal);
}
