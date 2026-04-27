using GloryCafe.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Products.Queries.GetProducts;

public class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking();

        if (request.CategoryId is int categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        return await query
            .OrderBy(p => p.Category.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.ImageUrl,
                p.IsAvailable,
                p.CategoryId,
                p.Category.Name))
            .ToListAsync(cancellationToken);
    }
}
