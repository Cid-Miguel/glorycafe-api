using GloryCafe.Application.Orders.Commands.CreateOrder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GloryCafe.API.Controllers;

public class OrdersController : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting("orders")]
    [ProducesResponseType(typeof(CreateOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateOrderResult>> Create(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = result.OrderId }, result);
    }
}
