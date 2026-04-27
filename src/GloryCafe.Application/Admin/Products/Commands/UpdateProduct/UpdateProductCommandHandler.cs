using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Products.Commands.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            throw new NotFoundException(nameof(Product), request.Id);

        if (product.CategoryId != request.CategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (!categoryExists)
                throw new NotFoundException(nameof(Category), request.CategoryId);

            product.CategoryId = request.CategoryId;
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.IsAvailable = request.IsAvailable;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
