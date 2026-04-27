using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, int>
{
    private readonly IApplicationDbContext _db;

    public CreateCategoryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var nameTaken = await _db.Categories
            .AnyAsync(c => c.Name == name, cancellationToken);

        if (nameTaken)
            throw new ConflictException($"A category named '{name}' already exists.");

        var category = new Category
        {
            Name = name,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
