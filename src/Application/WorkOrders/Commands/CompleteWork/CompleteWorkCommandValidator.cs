using FluentValidation;

namespace Application.WorkOrders.Commands.CompleteWork;

public sealed class CompleteWorkCommandValidator : AbstractValidator<CompleteWorkCommand>
{
    public CompleteWorkCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
    }
}
