using GymOS.API.Authorization;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Application.Modules.Billing.Dtos;
using GymOS.Application.Modules.Billing.Queries;
using GymOS.Domain.Billing;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/invoices")]
public class BillingController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Billing.View)]
    public async Task<ActionResult<PagedList<InvoiceListItemDto>>> List(
        [FromQuery] Guid? memberId, [FromQuery] InvoiceStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetInvoicesQuery(memberId, status, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Billing.View)]
    public async Task<ActionResult<InvoiceDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetInvoiceByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Billing.CreateInvoice)]
    public async Task<ActionResult<Guid>> Create(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/payments")]
    [RequirePermission(PermissionCodes.Billing.RecordPayment)]
    public async Task<ActionResult<Guid>> RecordPayment(Guid id, RecordPaymentCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { InvoiceId = id }, cancellationToken));

    [HttpPost("payments/{paymentId:guid}/refund")]
    [RequirePermission(PermissionCodes.Billing.IssueRefund)]
    public async Task<ActionResult<Guid>> Refund(Guid paymentId, IssueRefundCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { PaymentId = paymentId }, cancellationToken));

    /// <summary>
    /// Voiding is raising an invoice in reverse, so it reuses billing.create_invoice rather than
    /// inventing a code: whoever may bring a charge into existence may withdraw one that should
    /// never have existed. It is explicitly NOT record_payment or issue_refund — this command
    /// refuses to run once any money is involved, and those are the two roles that handle money.
    /// </summary>
    [HttpPost("~/api/billing/invoices/{id:guid}/void")]
    [RequirePermission(PermissionCodes.Billing.CreateInvoice)]
    public async Task<IActionResult> Void(Guid id, VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { InvoiceId = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Rooted at /api/billing rather than under this controller's /api/invoices prefix because it
    /// answers a different question — "who owes us and why did the card fail" is the Monday-morning
    /// chase list, not a view of the invoice ledger.
    /// </summary>
    [HttpGet("~/api/billing/dunning")]
    [RequirePermission(PermissionCodes.Billing.View)]
    public async Task<ActionResult<List<DunningAttemptDto>>> Dunning(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDunningAttemptsQuery(), cancellationToken));
}
