using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Products.Commands.DeleteProduct;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;

    public DeleteProductCommandHandler(IApplicationDbContext db, IFileStorage fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Product), request.Id);

        var hasOrderHistory = await _db.OrderItems
            .AnyAsync(i => i.ProductId == request.Id, cancellationToken);

        if (hasOrderHistory)
            throw new ConflictException(
                $"Product '{product.Name}' has order history and cannot be deleted. " +
                "Set IsAvailable = false to hide it instead.");

        var imageUrl = product.ImageUrl;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(imageUrl))
            await _fileStorage.DeleteAsync(imageUrl, cancellationToken);
    }
}
