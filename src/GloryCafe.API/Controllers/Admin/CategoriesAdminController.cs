using GloryCafe.Application.Admin.Categories.Commands.CreateCategory;
using GloryCafe.Application.Admin.Categories.Commands.DeleteCategory;
using GloryCafe.Application.Admin.Categories.Commands.UpdateCategory;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers.Admin;

[Route("api/admin/categories")]
public class CategoriesAdminController : AdminControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return Created($"/api/categories/{id}", new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateCategoryCommand(id, request.Name, request.DisplayOrder), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateCategoryRequest(string Name, int DisplayOrder);
