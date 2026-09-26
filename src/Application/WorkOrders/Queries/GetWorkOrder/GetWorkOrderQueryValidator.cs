using FluentValidation;

namespace Application.WorkOrders.Queries.GetWorkOrder;

public sealed class GetWorkOrderQueryValidator : AbstractValidator<GetWorkOrderQuery>
{
    public GetWorkOrderQueryValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
    }
}
