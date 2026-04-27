using MediatR;

namespace GloryCafe.Application.Admin.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(string Name, int DisplayOrder) : IRequest<int>;
