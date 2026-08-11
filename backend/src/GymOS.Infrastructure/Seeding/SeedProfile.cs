namespace GymOS.Infrastructure.Seeding;

/// <summary>
/// Which shape of gym the seeder builds.
///
/// The two profiles answer different questions. <see cref="Demo"/> answers "does this look like a
/// real gym to a buyer" — 300 members with 400 days of history behind them, which is what makes
/// revenue, retention cohorts, at-risk lists and the coaching flags non-zero. <see cref="Pilot"/>
/// answers "can I drive this myself" — a small roster where EVERY member has a login, because in the
/// demo profile only two of three hundred do, and you cannot manually test a member experience you
/// cannot sign in to.
///
/// They are separate rather than one being tuned into the other because the trade is real and goes
/// both ways: the pilot gym's dashboards are thin by construction, and no amount of history fixes
/// three hundred members nobody can log in as.
/// </summary>
public enum SeedProfile
{
    /// <summary>300 members, 20 trainers, branch assignment at random. The sales demo.</summary>
    Demo,

    /// <summary>
    /// Ten members and two trainers per branch, every one of them with a login on the standard demo
    /// password. Emails are sequential and predictable (member1@…, trainer1@…) so a test script can
    /// name an account instead of looking one up.
    /// </summary>
    Pilot
}
