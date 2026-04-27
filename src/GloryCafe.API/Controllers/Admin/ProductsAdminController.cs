using GloryCafe.API.Infrastructure;
using GloryCafe.Application.Admin.Products.Commands.CreateProduct;
using GloryCafe.Application.Admin.Products.Commands.DeleteProduct;
using GloryCafe.Application.Admin.Products.Commands.SetProductImage;
using GloryCafe.Application.Admin.Products.Commands.UpdateProduct;
using GloryCafe.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers.Admin;

[Route("api/admin/products")]
public class ProductsAdminController : AdminControllerBase
{
    private readonly IFileStorage _fileStorage;

    public ProductsAdminController(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return Created($"/api/products/{id}", new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(
            id,
            request.Name,
            request.Description,
            request.Price,
            request.CategoryId,
            request.IsAvailable);
        await Mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadValidator.MaxBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var (ok, error, extension) = ImageUploadValidator.Validate(file);
        if (!ok)
            return ValidationProblem(error!);

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveAsync(stream, extension!, "products", cancellationToken);

        try
        {
            await Mediator.Send(new SetProductImageCommand(id, url), cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(url, cancellationToken);
            throw;
        }

        return Ok(new { imageUrl = url });
    }

    [HttpDelete("{id:int}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(int id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new SetProductImageCommand(id, null), cancellationToken);
        return NoContent();
    }

    private ActionResult ValidationProblem(string detail)
    {
        return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["File"] = new[] { detail }
        }));
    }
}

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int CategoryId,
    bool IsAvailable);
