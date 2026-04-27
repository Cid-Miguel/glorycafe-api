using MediatR;

namespace GloryCafe.Application.Admin.Products.Commands.DeleteProduct;

public record DeleteProductCommand(int Id) : IRequest;
