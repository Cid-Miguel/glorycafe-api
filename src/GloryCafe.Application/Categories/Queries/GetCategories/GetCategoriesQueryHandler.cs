using GloryCafe.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Categories.Queries.GetCategories;

public class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoriesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
