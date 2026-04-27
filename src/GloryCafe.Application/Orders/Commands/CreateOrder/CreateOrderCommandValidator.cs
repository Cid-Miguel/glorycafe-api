using FluentValidation;

namespace GloryCafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public const int MinimumPickupMinutes = 10;
    public const int MaximumPickupHours = 24;

    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerFirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.CustomerLastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CustomerPhone) || !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithName("Contact")
            .WithMessage("At least one contact method (phone or email) is required.");

        When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail), () =>
        {
            RuleFor(x => x.CustomerEmail!)
                .EmailAddress().WithMessage("Email format is invalid.")
                .MaximumLength(200);
        });

        When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone), () =>
        {
            RuleFor(x => x.CustomerPhone!)
                .Matches(@"^\+?[0-9 \-]{6,20}$")
                .WithMessage("Phone format is invalid.");
        });

        RuleFor(x => x.EstimatedPickupTime)
            .Must(BeAtLeastMinimumMinutesAhead)
            .WithMessage($"Pickup time must be at least {MinimumPickupMinutes} minutes from now.")
            .Must(BeWithinMaximumWindow)
            .WithMessage($"Pickup time cannot be more than {MaximumPickupHours} hours from now.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required.")
            .Must(items => items != null && items.Count > 0)
            .WithMessage("At least one item is required.")
            .Must(items => items == null || items.Count <= 50)
            .WithMessage("An order cannot contain more than 50 line items.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(99).WithMessage("Quantity per line cannot exceed 99.");
        });
    }

    private static bool BeAtLeastMinimumMinutesAhead(DateTime pickup)
        => pickup.ToUniversalTime() >= DateTime.UtcNow.AddMinutes(MinimumPickupMinutes);

    private static bool BeWithinMaximumWindow(DateTime pickup)
        => pickup.ToUniversalTime() <= DateTime.UtcNow.AddHours(MaximumPickupHours);
}
