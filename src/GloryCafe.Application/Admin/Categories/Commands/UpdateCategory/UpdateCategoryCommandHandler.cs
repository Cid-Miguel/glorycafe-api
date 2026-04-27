using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IApplicationDbContext _db;

    public UpdateCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
            throw new NotFoundException(nameof(Category), request.Id);

        var name = request.Name.Trim();

        var nameTaken = await _db.Categories
            .AnyAsync(c => c.Id != request.Id && c.Name == name, cancellationToken);

        if (nameTaken)
            throw new ConflictException($"A category named '{name}' already exists.");

        category.Name = name;
        category.DisplayOrder = request.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
