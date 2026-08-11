using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// A member is never demoted. TotalXp follows the ledger and may fall when an undo withdraws a
/// session; PeakXp is a ratchet, and Level derives from the ratchet.
///
/// The split exists because both properties are load-bearing and they conflict: the projection has to
/// keep summing to its ledger (there is a rebuild test enforcing exactly that), and a rank earned over
/// months must not evaporate because someone corrected a mis-tap thirty seconds after making it.
/// </summary>
public class MemberProgressionRatchetTests
{
    private static MemberProgression At(long xp)
    {
        var p = new MemberProgression();
        p.SetTotalXp(xp);
        return p;
    }

    [Fact]
    public void Level_rises_with_xp_as_before()
    {
        At(0).Level.ShouldBe(1);
        At(100).Level.ShouldBe(2);   // cumulative threshold for level 2
        At(300).Level.ShouldBe(3);
    }

    [Fact]
    public void Undoing_a_session_lowers_the_total_but_never_the_level()
    {
        var p = At(320);          // level 3
        p.Level.ShouldBe(3);

        p.SetTotalXp(240);        // an undo withdrew 80: back under the level-3 threshold of 300

        p.TotalXp.ShouldBe(240, "the ledger is the truth about what is currently banked");
        p.PeakXp.ShouldBe(320, "but the high-water mark is what was actually reached");
        p.Level.ShouldBe(3, "and the member keeps the level they earned");
    }

    [Fact]
    public void Re_earning_past_the_peak_moves_the_peak_again()
    {
        var p = At(320);
        p.SetTotalXp(240);        // undo
        p.AddXp(200);             // 440 — past the old peak of 320

        p.TotalXp.ShouldBe(440);
        p.PeakXp.ShouldBe(440, "the ratchet advances whenever the total exceeds it");
        p.Level.ShouldBe(3);      // thresholds are 100 / 300 / 600 / 1000, so 440 is still level 3
    }

    /// <summary>
    /// Undo re-sums the remaining ledger and pushes the result through SetTotalXp, so the peak has to
    /// survive a total that arrives already reduced — not just one that is reduced step by step.
    /// (A full rebuild is the deliberate exception and goes through RebuildTo; see below.)
    /// </summary>
    [Fact]
    public void A_resum_of_a_shortened_ledger_does_not_lower_the_peak()
    {
        var p = At(1000);         // level 5
        p.Level.ShouldBe(5);

        p.SetTotalXp(950);        // undo re-summed a ledger missing the session it withdrew

        p.PeakXp.ShouldBe(1000);
        p.Level.ShouldBe(5);
    }

    /// <summary>
    /// The deliberate exception. A rebuild repairs a projection that has drifted from its ledger, and
    /// drift goes both ways — a row inflated to 9999 against a true 150 has to come DOWN. If the
    /// ratchet applied here the corruption would outlive the tool built to fix it, so RebuildTo sets
    /// the peak authoritatively while SetTotalXp continues to protect the member-facing path.
    /// </summary>
    [Fact]
    public void A_rebuild_is_authoritative_and_may_lower_the_level()
    {
        var p = At(9999);
        p.Level.ShouldBe(XpPolicy.LevelForXp(9999).Level);

        p.RebuildTo(150);

        p.TotalXp.ShouldBe(150);
        p.PeakXp.ShouldBe(150, "the rebuild is the truth, peak included");
        p.Level.ShouldBe(XpPolicy.LevelForXp(150).Level);
    }

    [Fact]
    public void A_negative_total_is_clamped_and_still_cannot_demote()
    {
        var p = At(600);
        p.SetTotalXp(-50);

        p.TotalXp.ShouldBe(0);
        p.PeakXp.ShouldBe(600);
        p.Level.ShouldBe(XpPolicy.LevelForXp(600).Level);
    }
}
