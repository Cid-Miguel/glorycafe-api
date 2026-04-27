using GloryCafe.Application.Products.Queries.GetProducts;
using MediatR;

namespace GloryCafe.Application.Products.Queries.GetProductById;

public record GetProductByIdQuery(int Id) : IRequest<ProductDto>;
