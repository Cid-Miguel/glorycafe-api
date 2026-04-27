using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Application.Products.Queries.GetProducts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Products.Queries.GetProductById;

public class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ProductDto> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.ImageUrl,
                p.IsAvailable,
                p.CategoryId,
                p.Category.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Domain.Entities.Product), request.Id);

        return product;
    }
}
