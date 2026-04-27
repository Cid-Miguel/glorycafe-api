using GloryCafe.Application.Products.Queries.GetProductById;
using GloryCafe.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Controllers;

public class ProductsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Get(
        [FromQuery] int? categoryId,
        CancellationToken cancellationToken)
    {
        var products = await Mediator.Send(new GetProductsQuery(categoryId), cancellationToken);
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await Mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return Ok(product);
    }
}
