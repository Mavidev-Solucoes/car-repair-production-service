using FluentValidation;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.ServiceJobId).NotEmpty();
        RuleFor(x => x.ServiceJobName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ServiceJobDescription).MaximumLength(500);
    }
}
