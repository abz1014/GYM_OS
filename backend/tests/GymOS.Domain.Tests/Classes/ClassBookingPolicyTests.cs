using GymOS.Domain.Classes;
using Shouldly;

namespace GymOS.Domain.Tests.Classes;

/// <summary>
/// The capacity rule decides whether a new booking confirms or waitlists, and whether a
/// cancellation frees a spot — the core of the booking state machine. Pure, so it's tested here
/// directly (the full book/cancel/promote flow through the DB is covered by an application test).
/// </summary>
public class ClassBookingPolicyTests
{
    [Theory]
    [InlineData(ClassBookingStatus.Booked, true)]
    [InlineData(ClassBookingStatus.CheckedIn, true)]
    [InlineData(ClassBookingStatus.Waitlisted, false)]
    [InlineData(ClassBookingStatus.NoShow, false)]
    [InlineData(ClassBookingStatus.Cancelled, false)]
    public void Only_booked_and_checked_in_occupy_a_spot(ClassBookingStatus status, bool occupies)
    {
        ClassBookingPolicy.Occupies(status).ShouldBe(occupies);
    }

    [Fact]
    public void A_new_booking_confirms_while_spots_remain()
    {
        ClassBookingPolicy.StatusForNewBooking(occupiedCount: 9, capacity: 10).ShouldBe(ClassBookingStatus.Booked);
    }

    [Fact]
    public void A_new_booking_waitlists_once_the_session_is_full()
    {
        ClassBookingPolicy.StatusForNewBooking(occupiedCount: 10, capacity: 10).ShouldBe(ClassBookingStatus.Waitlisted);
    }

    [Fact]
    public void Cancelling_from_a_full_session_opens_a_spot_for_the_waitlist()
    {
        // Was 10/10, one cancels → 9 occupied → a waitlisted member can be promoted.
        ClassBookingPolicy.CanPromoteFromWaitlist(occupiedCountAfterCancel: 9, capacity: 10).ShouldBeTrue();
    }

    [Fact]
    public void Cancelling_from_an_under_full_session_does_not_force_a_promotion()
    {
        // A session that was never full has no waitlist to promote from.
        ClassBookingPolicy.CanPromoteFromWaitlist(occupiedCountAfterCancel: 5, capacity: 10).ShouldBeTrue();
        ClassBookingPolicy.CanPromoteFromWaitlist(occupiedCountAfterCancel: 10, capacity: 10).ShouldBeFalse();
    }
}
