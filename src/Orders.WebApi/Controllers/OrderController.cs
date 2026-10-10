using Joseco.DDD.Core.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Application.Orders.CreateOrder;
using Orders.Application.Orders.UseCases.AnnulOrder;
using Orders.Application.Orders.UseCases.CancelOrder;
using Orders.Application.Orders.UseCases.Drafts;
using Orders.Application.Orders.UseCases.GetOrderById;
using Orders.Application.Orders.UseCases.GetOrders;
using Nur.Store2026.Access.Orders.Permissions;
using Orders.Domain.Orders;
using Orders.WebApi.Infraestructure;

namespace Orders.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The price of every item is taken from the catalog data; only product and quantity are sent.</summary>
    /// <param name="CustomerName">Optional: the customer named on the sales note.</param>
    public record CreateOrderRequest(ICollection<CreateOrderItem> Items, string? CustomerName = null, string? CustomerDocument = null);

    public record CreatedOrder(Guid Id);

    /// <summary>A draft may start empty; the lines can be sent now or later.</summary>
    public record DraftRequest(ICollection<CreateOrderItem>? Items = null, string? CustomerName = null, string? CustomerDocument = null);

    public record DraftItemsRequest(ICollection<CreateOrderItem> Items);

    public record DraftCustomerRequest(string? CustomerName, string? CustomerDocument);

    public record AnnulRequest(string Reason);

    /// <summary>Creates a sales note (nota de venta) in the name of the authenticated sales advisor.</summary>
    [HttpPost]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> Post([FromBody] CreateOrderRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new CreateOrderCommand(userId, request.Items, request.CustomerName, request.CustomerDocument));

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedOrder(result.Value))
            : this.ToProblem(result.Error);
    }

    /// <summary>Opens a sales note that is still being written: nothing is reserved and no charge order exists yet.</summary>
    [HttpPost("draft")]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> CreateDraft([FromBody] DraftRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(
            new CreateDraftCommand(userId, request.Items, request.CustomerName, request.CustomerDocument));

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedOrder(result.Value))
            : this.ToProblem(result.Error);
    }

    /// <summary>Replaces the lines of a draft (only the advisor who wrote it).</summary>
    [HttpPut("{id:guid}/items")]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> UpdateDraftItems(Guid id, [FromBody] DraftItemsRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new UpdateDraftItemsCommand(id, userId, request.Items));
        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}/customer")]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> UpdateDraftCustomer(Guid id, [FromBody] DraftCustomerRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new UpdateDraftCustomerCommand(id, userId, request.CustomerName, request.CustomerDocument));
        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }

    /// <summary>Completes the draft: it is priced with the current prices and the sale starts (stock reservation, then the charge order).</summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> ConfirmDraft(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new ConfirmDraftCommand(id, userId));
        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }

    /// <summary>Throws a draft away.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = OrdersPermission.OrderCreate)]
    public async Task<IActionResult> DiscardDraft(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new DiscardDraftCommand(id, userId));
        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }

    /// <summary>
    /// Manual intervention: annuls a paid sale whose goods nobody picked up. Releases the reserved stock and flags the sale
    /// for a refund (the money is handled outside the system).
    /// </summary>
    [HttpPost("{id:guid}/annul")]
    [Authorize(Policy = OrdersPermission.OrderOverride)]
    public async Task<IActionResult> Annul(Guid id, [FromBody] AnnulRequest request)
    {
        Result<Guid> result = await _mediator.Send(new AnnulOrderCommand(id, request.Reason));
        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }

    /// <summary>A sales note of the authenticated user (users with the orders.order:admin permission can read any).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = OrdersPermission.OrderRead)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<OrderDto> result = await _mediator.Send(new GetOrderByIdQuery(id, userId, User.HasPermission(OrdersPermission.OrderAdmin)));

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>The orders of the authenticated user, newest first (orders.order:admin gets every order).</summary>
    [HttpGet]
    [Authorize(Policy = OrdersPermission.OrderRead)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null, [FromQuery] int? paidBeforeDays = null)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<PagedOrders> result = await _mediator.Send(new GetOrdersQuery(userId, User.HasPermission(OrdersPermission.OrderAdmin), page, pageSize, status, paidBeforeDays));

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>Cancels an unpaid order: releases its reserved stock and cancels its pending payment.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = OrdersPermission.OrderCancel)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new CancelOrderCommand(id, userId, User.HasPermission(OrdersPermission.OrderAdmin)));

        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }
}
