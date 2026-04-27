using MediatR;

namespace GloryCafe.Application.Admin.Products.Commands.SetProductImage;

public record SetProductImageCommand(int ProductId, string? ImageUrl) : IRequest;
