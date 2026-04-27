using MediatR;

namespace GloryCafe.Application.Admin.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    int CategoryId,
    bool IsAvailable) : IRequest<int>;
