using GloryCafe.Application.Categories.Queries.GetCategories;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers;

public class CategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Get(CancellationToken cancellationToken)
    {
        var categories = await Mediator.Send(new GetCategoriesQuery(), cancellationToken);
        return Ok(categories);
    }
}
