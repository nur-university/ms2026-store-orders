using Joseco.DDD.Core.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Application.Orders.CreateOrder;
using Orders.Application.Orders.UseCases.CancelOrder;
using Orders.Application.Orders.UseCases.GetOrderById;
using Orders.Application.Orders.UseCases.GetOrders;
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
    public record CreateOrderRequest(ICollection<CreateOrderItem> Items);

    public record CreatedOrder(Guid Id);

    /// <summary>Places an order for the authenticated user.</summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateOrderRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new CreateOrderCommand(userId, request.Items));

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedOrder(result.Value))
            : this.ToProblem(result.Error);
    }

    /// <summary>An order of the authenticated user (staff can read any order).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<OrderDto> result = await _mediator.Send(new GetOrderByIdQuery(id, userId, User.IsStaff()));

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>The orders of the authenticated user, newest first (staff get every order).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<PagedOrders> result = await _mediator.Send(new GetOrdersQuery(userId, User.IsStaff(), page, pageSize));

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>Cancels an unpaid order: releases its reserved stock and cancels its pending payment.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        Result<Guid> result = await _mediator.Send(new CancelOrderCommand(id, userId, User.IsStaff()));

        return result.IsSuccess ? Ok(new CreatedOrder(result.Value)) : this.ToProblem(result.Error);
    }
}
