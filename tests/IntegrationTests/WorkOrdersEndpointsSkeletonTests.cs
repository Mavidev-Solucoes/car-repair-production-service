using Xunit;

namespace IntegrationTests;

public sealed class WorkOrdersEndpointsSkeletonTests
{
    [Fact(Skip = "Integration skeleton: implement once MongoDB and messaging test fixtures are available.")]
    public Task CreateWorkOrder_ShouldReturnCreated()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Integration skeleton: implement once MongoDB and messaging test fixtures are available.")]
    public Task StartCompleteFailFlow_ShouldReturnNoContent()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Integration skeleton: implement once MongoDB and messaging test fixtures are available.")]
    public Task ListWorkOrders_ShouldReturnOk()
    {
        return Task.CompletedTask;
    }
}
