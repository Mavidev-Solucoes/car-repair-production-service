using FluentValidation;

namespace Application.WorkOrders.Queries.GetWorkOrderById;

public sealed class GetWorkOrderByIdQueryValidator : AbstractValidator<GetWorkOrderByIdQuery>
{
    public GetWorkOrderByIdQueryValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
    }
}
