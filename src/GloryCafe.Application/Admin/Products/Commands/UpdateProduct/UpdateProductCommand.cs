using MediatR;

namespace GloryCafe.Application.Admin.Products.Commands.UpdateProduct;

public record UpdateProductCommand(
    int Id,
    string Name,
    string Description,
    decimal Price,
    int CategoryId,
    bool IsAvailable) : IRequest;
