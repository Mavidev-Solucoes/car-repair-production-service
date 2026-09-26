using FluentValidation;

namespace Application.WorkOrders.Commands.StartWork;

public sealed class StartWorkCommandValidator : AbstractValidator<StartWorkCommand>
{
    public StartWorkCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
    }
}
