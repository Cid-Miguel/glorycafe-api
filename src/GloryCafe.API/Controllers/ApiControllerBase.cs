using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetService<ISender>()
            ?? throw new InvalidOperationException("ISender not registered.");
}
