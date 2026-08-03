using GymOS.Domain.Billing;
using Shouldly;

namespace GymOS.Domain.Tests.Billing;

public class InvoiceTests
{
    [Fact]
    public void AmountPaid_sums_only_completed_payments()
    {
        var invoice = new Invoice { TotalAmount = 300m };
        invoice.Payments.Add(new Payment { Amount = 100m, Status = PaymentStatus.Completed });
        invoice.Payments.Add(new Payment { Amount = 50m, Status = PaymentStatus.Pending });
        invoice.Payments.Add(new Payment { Amount = 25m, Status = PaymentStatus.Failed });
        invoice.Payments.Add(new Payment { Amount = 75m, Status = PaymentStatus.Completed });

        invoice.AmountPaid.ShouldBe(175m);
    }

    [Fact]
    public void AmountOutstanding_is_total_minus_completed_payments()
    {
        var invoice = new Invoice { TotalAmount = 300m };
        invoice.Payments.Add(new Payment { Amount = 120m, Status = PaymentStatus.Completed });

        invoice.AmountOutstanding.ShouldBe(180m);
    }

    [Fact]
    public void AmountOutstanding_is_zero_once_fully_paid()
    {
        var invoice = new Invoice { TotalAmount = 300m };
        invoice.Payments.Add(new Payment { Amount = 300m, Status = PaymentStatus.Completed });

        invoice.AmountOutstanding.ShouldBe(0m);
    }
}
