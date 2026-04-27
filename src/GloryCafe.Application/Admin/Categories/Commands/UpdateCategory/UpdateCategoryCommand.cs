using MediatR;

namespace GloryCafe.Application.Admin.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(int Id, string Name, int DisplayOrder) : IRequest;
