using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Products.Commands.SetProductImage;

public class SetProductImageCommandHandler : IRequestHandler<SetProductImageCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;

    public SetProductImageCommandHandler(IApplicationDbContext db, IFileStorage fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    public async Task Handle(SetProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Product), request.ProductId);

        var oldImage = product.ImageUrl;
        product.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl;

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldImage) && oldImage != product.ImageUrl)
            await _fileStorage.DeleteAsync(oldImage, cancellationToken);
    }
}
