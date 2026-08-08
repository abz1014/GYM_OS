namespace GymOS.Domain.Billing;

/// <summary>
/// What state an invoice is in, given what has been paid, what has been given back, and the date.
///
/// This exists because the answer was previously computed in three places that each saw part of the
/// picture, and they disagreed in a way that lost money:
///
///  - recording a payment set Paid/PartiallyPaid and never considered the due date;
///  - issuing a refund set <see cref="InvoiceStatus.Refunded"/> UNCONDITIONALLY, so a one-cent refund
///    on a $130 invoice marked the whole thing refunded;
///  - the nightly overdue job only re-flagged Issued and PartiallyPaid, so nothing could ever bring
///    that invoice back.
///
/// The combination was terminal: a partially refunded invoice left every actionable queue while the
/// balance was still owed, and no role in the system had a transition that could retrieve it. Two
/// invoices in the demo data reached that state, holding $114.99 nobody would ever chase.
///
/// **Refunded means the money went back, not that a refund happened.** It is reserved for the case
/// where refunds cover the whole invoice — a terminal state where nothing is held and nothing is
/// owed. Anything less falls through to the ordinary rules, which is what keeps a live debt visible:
/// refund $40 of a $40 payment against a $100 invoice and the invoice is Overdue again, correctly,
/// because the member still owes $100.
/// </summary>
public static class InvoiceStatusPolicy
{
    /// <param name="totalAmount">The invoice total.</param>
    /// <param name="completedPayments">Sum of completed payments, before refunds.</param>
    /// <param name="completedRefunds">Sum of completed refunds against those payments.</param>
    /// <param name="dueDate">The invoice due date.</param>
    /// <param name="today">The current date, passed in rather than read, so this stays pure.</param>
    public static InvoiceStatus Derive(
        decimal totalAmount, decimal completedPayments, decimal completedRefunds, DateOnly dueDate, DateOnly today)
    {
        // Everything the gym took has been given back, and then some. Terminal.
        if (completedRefunds > 0 && completedRefunds >= totalAmount)
        {
            return InvoiceStatus.Refunded;
        }

        var netPaid = completedPayments - completedRefunds;

        if (netPaid >= totalAmount)
        {
            return InvoiceStatus.Paid;
        }

        // Past due beats "partially paid": a half-paid invoice a month past its date is a collections
        // problem, and calling it PartiallyPaid is what kept it out of the overdue queue.
        if (dueDate < today)
        {
            return InvoiceStatus.Overdue;
        }

        return netPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued;
    }

    /// <summary>
    /// What is still owed: the invoice total less what has been paid and not given back.
    ///
    /// Lives beside <see cref="Derive"/> because it is the same three inputs answering the same
    /// question, and the two disagreeing is precisely the class of bug that let a refunded invoice
    /// report different money on the list and the detail screen.
    ///
    /// Can go negative on invoices that were overpaid before the ceiling existed. Left signed rather
    /// than clamped to zero: a negative balance is real, wrong, and worth being visible.
    /// </summary>
    public static decimal Outstanding(decimal totalAmount, decimal completedPayments, decimal completedRefunds)
        => totalAmount - (completedPayments - completedRefunds);

    /// <summary>
    /// Statuses this policy owns. Draft and Cancelled are deliberately excluded: nothing in the
    /// system sets either, and a derivation that could silently overwrite a manual cancellation
    /// would be a worse bug than the one this class fixes.
    /// </summary>
    public static bool IsDerivable(InvoiceStatus status) =>
        status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid
            or InvoiceStatus.Overdue or InvoiceStatus.Refunded;
}
