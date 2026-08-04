using GymOS.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.Property(i => i.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(3).IsRequired();

        builder.HasOne(i => i.Member).WithMany().HasForeignKey(i => i.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Lines).WithOne(l => l.Invoice).HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Payments).WithOne(p => p.Invoice).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Reminders).WithOne(r => r.Invoice).HasForeignKey(r => r.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(i => i.AmountPaid);
        builder.Ignore(i => i.AmountOutstanding);
    }
}

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Ignore(l => l.LineTotal);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasMany<Refund>().WithOne(r => r.Payment).HasForeignKey(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
    }
}

public class RecurringBillingAttemptConfiguration : IEntityTypeConfiguration<RecurringBillingAttempt>
{
    public void Configure(EntityTypeBuilder<RecurringBillingAttempt> builder)
    {
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.LastFailureReason).HasMaxLength(500);

        builder.HasOne(a => a.MemberMembership).WithMany().HasForeignKey(a => a.MemberMembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Member).WithMany().HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Invoice).WithMany().HasForeignKey(a => a.InvoiceId).OnDelete(DeleteBehavior.Restrict);

        // The daily job scans for "pending and due today or earlier" — index exactly that pair.
        builder.HasIndex(a => new { a.Status, a.NextAttemptDate });
    }
}
