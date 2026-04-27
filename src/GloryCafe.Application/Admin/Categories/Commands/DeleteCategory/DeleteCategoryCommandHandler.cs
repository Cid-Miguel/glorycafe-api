using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
            throw new NotFoundException(nameof(Category), request.Id);

        var hasProducts = await _db.Products
            .AnyAsync(p => p.CategoryId == request.Id, cancellationToken);

        if (hasProducts)
            throw new ConflictException(
                $"Category '{category.Name}' has associated products and cannot be deleted. " +
                "Move or remove the products first.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
