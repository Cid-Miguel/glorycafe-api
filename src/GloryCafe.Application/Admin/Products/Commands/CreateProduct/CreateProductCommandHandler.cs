using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Products.Commands.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, int>
{
    private readonly IApplicationDbContext _db;

    public CreateProductCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException(nameof(Category), request.CategoryId);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            IsAvailable = request.IsAvailable,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
