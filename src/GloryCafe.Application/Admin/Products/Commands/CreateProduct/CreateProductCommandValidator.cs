using FluentValidation;

namespace GloryCafe.Application.Admin.Products.Commands.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(9999.99m);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}
