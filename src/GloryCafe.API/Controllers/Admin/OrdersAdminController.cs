using GloryCafe.Application.Admin.Orders.Commands.UpdateOrderStatus;
using GloryCafe.Application.Admin.Orders.Queries.GetAdminOrderById;
using GloryCafe.Application.Admin.Orders.Queries.GetAdminOrders;
using GloryCafe.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers.Admin;

[Route("api/admin/orders")]
public class OrdersAdminController : AdminControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AdminOrderSummaryDto>>> GetAll(
        [FromQuery] OrderStatus? status,
        CancellationToken cancellationToken)
    {
        var orders = await Mediator.Send(new GetAdminOrdersQuery(status), cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOrderDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await Mediator.Send(new GetAdminOrderByIdQuery(id), cancellationToken);
        return Ok(order);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateOrderStatusCommand(id, request.Status), cancellationToken);
        return NoContent();
    }
}

public record UpdateOrderStatusRequest(OrderStatus Status);
