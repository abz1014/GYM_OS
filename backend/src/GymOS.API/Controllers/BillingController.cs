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
}
