using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.WorkOrders.Queries.GetWorkOrder;
using Application.WorkOrders.Queries.ListWorkOrders;
using Domain.Common;
using Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests;

public sealed class WorkOrdersEndpointsSkeletonTests
{
    [Fact]
    public async Task CreateWorkOrder_ShouldReturnCreated()
    {
        await using var factory = new WorkOrdersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(Guid.NewGuid(), "Oil Change", "Replace engine oil"));

        var payload = await response.Content.ReadFromJsonAsync<CreatedWorkOrderResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task StartCompleteFlow_ShouldReturnNoContent()
    {
        await using var factory = new WorkOrdersApiFactory();
        using var client = factory.CreateClient();

        var workOrderId = await CreateWorkOrderAsync(client);

        var startResponse = await client.PostAsync($"/api/work-orders/{workOrderId}/start", content: null);
        var completeResponse = await client.PostAsync($"/api/work-orders/{workOrderId}/complete", content: null);
        var getResponse = await client.GetFromJsonAsync<WorkOrderDetailsResponse>($"/api/work-orders/{workOrderId}");

        Assert.Equal(HttpStatusCode.NoContent, startResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);
        Assert.NotNull(getResponse);
        Assert.Equal("Completed", getResponse.Status);
        Assert.NotNull(getResponse.CompletedAtUtc);
    }

    [Fact]
    public async Task FailWork_ShouldReturnNoContent()
    {
        await using var factory = new WorkOrdersApiFactory();
        using var client = factory.CreateClient();

        var workOrderId = await CreateWorkOrderAsync(client);
        await client.PostAsync($"/api/work-orders/{workOrderId}/start", content: null);

        var failResponse = await client.PostAsJsonAsync(
            $"/api/work-orders/{workOrderId}/fail",
            new FailWorkRequest("Missing spare part"));
        var getResponse = await client.GetFromJsonAsync<WorkOrderDetailsResponse>($"/api/work-orders/{workOrderId}");

        Assert.Equal(HttpStatusCode.NoContent, failResponse.StatusCode);
        Assert.NotNull(getResponse);
        Assert.Equal("Failed", getResponse.Status);
        Assert.Equal("Missing spare part", getResponse.FailureReason);
    }

    [Fact]
    public async Task ListWorkOrders_ShouldReturnOk()
    {
        await using var factory = new WorkOrdersApiFactory();
        using var client = factory.CreateClient();

        await CreateWorkOrderAsync(client, "Alignment");
        await CreateWorkOrderAsync(client, "Oil Change");

        var response = await client.GetAsync("/api/work-orders");
        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<WorkOrderSummaryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
    }

    private static async Task<Guid> CreateWorkOrderAsync(HttpClient client, string serviceJobName = "Oil Change")
    {
        var response = await client.PostAsJsonAsync(
            "/api/work-orders",
            new CreateWorkOrderRequest(Guid.NewGuid(), serviceJobName, $"{serviceJobName} description"));
        var payload = await response.Content.ReadFromJsonAsync<CreatedWorkOrderResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);

        return payload.Id;
    }

    private sealed record CreatedWorkOrderResponse(Guid Id);

    private sealed class WorkOrdersApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IServiceOrderJobRepository>();
                services.RemoveAll<IEventPublisher>();

                services.AddSingleton<IServiceOrderJobRepository, InMemoryServiceOrderJobRepository>();
                services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
            });
        }
    }

    private sealed class InMemoryServiceOrderJobRepository : IServiceOrderJobRepository
    {
        private readonly ConcurrentDictionary<Guid, ServiceOrderJob> _items = new();

        public Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
        {
            if (!_items.TryAdd(serviceOrderJob.Id, serviceOrderJob))
            {
                throw new InvalidOperationException($"Work order '{serviceOrderJob.Id}' already exists.");
            }

            return Task.CompletedTask;
        }

        public Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyCollection<ServiceOrderJob>> ListAsync(CancellationToken cancellationToken)
        {
            var items = _items.Values
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<ServiceOrderJob>>(items);
        }

        public Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
        {
            _items[serviceOrderJob.Id] = serviceOrderJob;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpEventPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent>(MessageEnvelope<TEvent> envelope, string routingKey, CancellationToken cancellationToken = default)
            where TEvent : class
            => Task.CompletedTask;
    }
}
