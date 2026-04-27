using MediatR;

namespace GloryCafe.Application.Products.Queries.GetProducts;

public record GetProductsQuery(int? CategoryId = null) : IRequest<IReadOnlyList<ProductDto>>;
