using MediatR;

namespace GloryCafe.Application.Categories.Queries.GetCategories;

public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;
