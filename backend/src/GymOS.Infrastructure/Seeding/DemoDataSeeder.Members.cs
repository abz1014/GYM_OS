using GymOS.Domain.Attendance;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    private async Task<List<Member>> SeedMembersAsync(Guid tenantId, List<Branch> branches, List<MembershipPlan> plans, CancellationToken cancellationToken)
    {
        var members = new List<Member>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rng = new Random(1001);

        for (var i = 1; i <= 300; i++)
        {
            var isMale = rng.Next(2) == 0;
            var firstName = isMale ? _faker.Name.FirstName(Bogus.DataSets.Name.Gender.Male) : _faker.Name.FirstName(Bogus.DataSets.Name.Gender.Female);
            var lastName = _faker.Name.LastName();
            var branch = branches[rng.Next(branches.Count)];
            var joinDate = today.AddDays(-rng.Next(0, 420));

            var member = new Member
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                MemberCode = $"MBR-{i:D5}",
                FirstName = firstName,
                LastName = lastName,
                Email = _faker.Internet.Email(firstName, lastName).ToLowerInvariant(),
                Phone = _faker.Phone.PhoneNumber("(###) ###-####"),
                DateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(50, DateTime.UtcNow.AddYears(-18))),
                Gender = isMale ? "Male" : "Female",
                Address = $"{_faker.Address.StreetAddress()}, {_faker.Address.City()}",
                JoinDate = joinDate,
                QrCodeToken = Guid.NewGuid().ToString("N")
            };

            var statusRoll = rng.Next(100);
            var status = statusRoll switch
            {
                < 75 => MemberStatus.Active,
                < 85 => MemberStatus.Frozen,
                < 95 => MemberStatus.Expired,
                _ => MemberStatus.Cancelled
            };
            member.Status = status;

            var plan = plans[rng.Next(plans.Count)];
            DateOnly startDate;
            DateOnly endDate;
            MemberMembershipStatus membershipStatus;
            DateOnly? freezeStart = null;
            DateOnly? freezeEnd = null;

            switch (status)
            {
                case MemberStatus.Active when rng.Next(100) < 8:
                    // ~8% of active members expire within the next week, so the dashboard's
                    // "expiring soon" widget always has something to show.
                    endDate = today.AddDays(rng.Next(1, 7));
                    startDate = endDate.AddDays(-plan.DurationDays);
                    membershipStatus = MemberMembershipStatus.Active;
                    break;
                case MemberStatus.Active:
                    (startDate, endDate) = RollForwardToFuture(joinDate, plan.DurationDays, today);
                    membershipStatus = MemberMembershipStatus.Active;
                    break;
                case MemberStatus.Frozen:
                    (startDate, endDate) = RollForwardToFuture(joinDate, plan.DurationDays, today);
                    freezeStart = today.AddDays(-rng.Next(1, 10));
                    freezeEnd = today.AddDays(rng.Next(1, 20));
                    membershipStatus = MemberMembershipStatus.Frozen;
                    break;
                case MemberStatus.Expired:
                    endDate = today.AddDays(-rng.Next(1, 60));
                    startDate = endDate.AddDays(-plan.DurationDays);
                    membershipStatus = MemberMembershipStatus.Expired;
                    break;
                default:
                    startDate = joinDate;
                    endDate = startDate.AddDays(plan.DurationDays);
                    membershipStatus = MemberMembershipStatus.Cancelled;
                    break;
            }

            db.MemberMemberships.Add(new MemberMembership
            {
                MemberId = member.Id,
                MembershipPlanId = plan.Id,
                StartDate = startDate,
                EndDate = endDate,
                Status = membershipStatus,
                AutoRenew = rng.Next(100) < 60,
                FreezeStartDate = freezeStart,
                FreezeEndDate = freezeEnd,
                PricePaid = plan.Price,
                Currency = plan.Currency
            });

            if (i % 3 == 0)
            {
                db.EmergencyContacts.Add(new EmergencyContact
                {
                    MemberId = member.Id,
                    Name = _faker.Name.FullName(),
                    Relationship = _faker.PickRandom("Spouse", "Parent", "Sibling", "Friend"),
                    Phone = _faker.Phone.PhoneNumber("(###) ###-####")
                });
            }

            if (i % 5 == 0)
            {
                db.MemberMeasurements.Add(new MemberMeasurement
                {
                    MemberId = member.Id,
                    MeasuredOn = today.AddDays(-rng.Next(1, 30)),
                    WeightKg = (decimal)Math.Round(60 + rng.NextDouble() * 40, 1),
                    BodyFatPercentage = (decimal)Math.Round(10 + rng.NextDouble() * 20, 1)
                });
            }

            if (i % 10 == 0)
            {
                db.MedicalNotes.Add(new MedicalNote
                {
                    MemberId = member.Id,
                    Note = _faker.PickRandom("No known allergies.", "Mild asthma — has inhaler on file.", "Previous knee injury, avoid high-impact.", "Cleared for all activity levels."),
                    RecordedAt = DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 90))
                });
            }

            if (i % 8 == 0)
            {
                db.ProgressPhotos.Add(new ProgressPhoto
                {
                    MemberId = member.Id,
                    PhotoUrl = $"https://picsum.photos/seed/member-{i}/400/600",
                    TakenAt = DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 60))
                });
            }

            db.Members.Add(member);
            members.Add(member);
        }

        await db.SaveChangesAsync(cancellationToken);
        return members;
    }

    /// <summary>
    /// Links the "member@titanfitness.demo" login to a real seeded Member row (Member.UserId is
    /// otherwise never populated by seeding) so the self-service portal has real attendance,
    /// membership status, and invoice history to show instead of an empty state. Picked after
    /// attendance/invoices are seeded so the chosen member demonstrably has both, rather than
    /// guessing a fixed index against the member-seeding RNG's output.
    /// </summary>
    private async Task LinkDemoMemberAccountAsync(Dictionary<string, User> demoUsers, Guid preferredBranchId, CancellationToken cancellationToken)
    {
        // Base eligibility: an active member with real attendance + billing history so the portal
        // dashboard has something to show.
        var eligible = db.Members.IgnoreQueryFilters()
            .Where(m => m.Status == MemberStatus.Active)
            .Where(m => db.AttendanceRecords.IgnoreQueryFilters().Any(a => a.MemberId == m.Id))
            .Where(m => db.Invoices.IgnoreQueryFilters().Any(inv => inv.MemberId == m.Id));

        // Prefer a member in the branch that actually has group classes, so the demo member can see
        // and book classes; fall back to any eligible member if that branch has none.
        var candidate = await eligible.Where(m => m.BranchId == preferredBranchId).OrderBy(m => m.MemberCode).FirstOrDefaultAsync(cancellationToken)
            ?? await eligible.OrderBy(m => m.MemberCode).FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            logger.LogWarning("No eligible member found to link to the demo Member login — portal will show an empty state.");
            return;
        }

        candidate.UserId = demoUsers[RoleNames.Member].Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (DateOnly Start, DateOnly End) RollForwardToFuture(DateOnly startDate, int durationDays, DateOnly today)
    {
        var end = startDate.AddDays(durationDays);
        while (end < today)
        {
            startDate = end;
            end = startDate.AddDays(durationDays);
        }

        return (startDate, end);
    }

    private async Task SeedAttendanceAsync(Guid tenantId, List<Branch> branches, List<Member> members, CancellationToken cancellationToken)
    {
        var rng = new Random(2002);
        var attendingMembers = members.Where(m => m.Status is MemberStatus.Active or MemberStatus.Frozen).ToList();
        if (attendingMembers.Count == 0)
        {
            return;
        }

        // Weighted toward two realistic gym peaks: early morning and early evening.
        int[] peakHours = [6, 7, 8, 17, 18, 19, 20];

        for (var i = 0; i < 500; i++)
        {
            var member = attendingMembers[rng.Next(attendingMembers.Count)];
            var daysAgo = rng.Next(0, 60);
            var hour = rng.Next(100) < 70 ? peakHours[rng.Next(peakHours.Length)] : rng.Next(5, 22);
            var baseDate = DateTimeOffset.UtcNow.AddDays(-daysAgo);
            // Built explicitly as a UTC DateTimeOffset rather than via .Date (which downgrades to
            // DateTime and then implicitly reattaches the machine's LOCAL offset on assignment —
            // Npgsql rejects any non-zero offset for timestamptz columns).
            var checkIn = new DateTimeOffset(baseDate.Year, baseDate.Month, baseDate.Day, hour, rng.Next(0, 60), 0, TimeSpan.Zero);

            var record = new AttendanceRecord
            {
                TenantId = tenantId,
                BranchId = member.BranchId,
                MemberId = member.Id,
                CheckInAt = checkIn,
                Method = rng.Next(100) < 85 ? AttendanceMethod.QrSimulated : AttendanceMethod.Manual
            };

            if (rng.Next(100) < 80)
            {
                record.CheckOutAt = checkIn.AddMinutes(rng.Next(30, 120));
            }

            db.AttendanceRecords.Add(record);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInvoicesAndPaymentsAsync(
        Guid tenantId, List<Branch> branches, List<Member> members, Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var rng = new Random(3003);
        var receptionist = demoUsers[RoleNames.Receptionist];
        var candidates = members.Where(m => m.Status != MemberStatus.Cancelled).OrderBy(_ => rng.Next()).Take(100).ToList();

        var sequence = 1;
        foreach (var member in candidates)
        {
            var issueDate = member.JoinDate.AddDays(rng.Next(0, 300));
            if (issueDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                issueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-rng.Next(0, 30));
            }

            var dueDate = issueDate.AddDays(7);
            var membership = await db.MemberMemberships.IgnoreQueryFilters().Where(mm => mm.MemberId == member.Id).FirstOrDefaultAsync(cancellationToken);
            var lineAmount = membership?.PricePaid ?? 49.99m;

            var invoice = new Invoice
            {
                TenantId = tenantId,
                BranchId = member.BranchId,
                MemberId = member.Id,
                InvoiceNumber = $"INV-{issueDate.Year}-{sequence++:D6}",
                IssueDate = issueDate,
                DueDate = dueDate,
                Subtotal = lineAmount,
                TaxAmount = 0,
                DiscountAmount = 0,
                TotalAmount = lineAmount,
                Currency = "USD",
                Status = InvoiceStatus.Issued
            };

            invoice.Lines.Add(new InvoiceLine
            {
                ItemType = InvoiceLineItemType.MembershipFee,
                Description = "Membership fee",
                Quantity = 1,
                UnitPrice = lineAmount
            });

            var paymentRoll = rng.Next(100);
            if (paymentRoll < 80)
            {
                var payment = new Payment
                {
                    Method = _faker.PickRandom(PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.BankTransfer),
                    Amount = lineAmount,
                    PaidAt = issueDate.ToDateTime(TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc),
                    ReceivedByUserId = receptionist.Id,
                    Status = PaymentStatus.Completed
                };
                invoice.Payments.Add(payment);
                invoice.Status = InvoiceStatus.Paid;

                if (rng.Next(100) < 5)
                {
                    payment.GatewayTransactionId = $"DEMO-TXN-{Guid.NewGuid():N}";
                    invoice.Status = InvoiceStatus.Refunded;
                    db.Refunds.Add(new Refund
                    {
                        Payment = payment,
                        Amount = lineAmount,
                        Reason = "Member requested cancellation within cooling-off period.",
                        RefundedAt = DateTimeOffset.UtcNow.AddDays(-rng.Next(1, 10)),
                        Status = RefundStatus.Completed
                    });
                }
            }
            else if (paymentRoll < 95)
            {
                invoice.Payments.Add(new Payment
                {
                    Method = PaymentMethod.Cash,
                    Amount = Math.Round(lineAmount / 2, 2),
                    PaidAt = issueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    ReceivedByUserId = receptionist.Id,
                    Status = PaymentStatus.Completed
                });
                invoice.Status = InvoiceStatus.PartiallyPaid;
            }
            else
            {
                invoice.Status = dueDate < DateOnly.FromDateTime(DateTime.UtcNow) ? InvoiceStatus.Overdue : InvoiceStatus.Issued;
            }

            db.Invoices.Add(invoice);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
