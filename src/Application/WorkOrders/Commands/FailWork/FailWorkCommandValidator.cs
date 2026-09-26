using FluentValidation;

namespace Application.WorkOrders.Commands.FailWork;

public sealed class FailWorkCommandValidator : AbstractValidator<FailWorkCommand>
{
    public FailWorkCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
