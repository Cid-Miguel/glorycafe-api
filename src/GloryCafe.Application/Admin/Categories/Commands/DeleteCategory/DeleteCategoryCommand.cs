using MediatR;

namespace GloryCafe.Application.Admin.Categories.Commands.DeleteCategory;

public record DeleteCategoryCommand(int Id) : IRequest;
