using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Relex.Api.Features.Orders.Dtos;
using Xunit;

namespace Relex.Tests;

public class OrderTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client;

    public OrderTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OrderLifecycle_IntegrationTest()
    {
        // 1. Create Order
        var createRequest = new CreateOrderRequest
        {
            LocationCode = "LOC-0001", 
            ProductCode = "PROD-00001", 
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Quantity = 10, 
            SubmittedBy = "test-runner"
        };

        var createResponse = await _client.PostAsJsonAsync("/orders", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        createdOrder.Should().NotBeNull();
        createdOrder!.Id.Should().NotBeEmpty();
        createdOrder.Status.Should().Be("Pending");
        var orderId = createdOrder.Id;

        // 2. Get Order
        var getResponse = await _client.GetAsync($"/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedOrder = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        fetchedOrder!.Id.Should().Be(orderId);
        fetchedOrder.LocationCode.Should().Be("LOC-0001");

        // 3. Update Order
        var updateRequest = new UpdateOrderRequest
        {
            Quantity = 20,
            Status = "Confirmed"
        };
        var updateResponse = await _client.PutAsJsonAsync($"/orders/{orderId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Update
        var getAfterUpdate = await _client.GetFromJsonAsync<OrderDto>($"/orders/{orderId}");
        getAfterUpdate!.Quantity.Should().Be(20);
        getAfterUpdate.Status.Should().Be("Confirmed");

        // 4. List Orders
        var listResponse = await _client.GetFromJsonAsync<ListOrdersResponse>("/orders?page=1&pageSize=10");
        listResponse.Should().NotBeNull();
        listResponse!.Data.Should().Contain(o => o.Id == orderId);
        listResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);

        // 5. Delete Order - Only "Pending" can be deleted, so it is expected to fail first
        var failDeleteResponse = await _client.DeleteAsync($"/orders/{orderId}");
        failDeleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Cannot delete Confirmed orders");

        // Reset to "Pending" to allow delete
        await _client.PutAsJsonAsync($"/orders/{orderId}", new UpdateOrderRequest { Quantity = 20, Status = "Pending" });

        // Now Delete
        var deleteResponse = await _client.DeleteAsync($"/orders/{orderId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Deletion
        var getAfterDelete = await _client.GetAsync($"/orders/{orderId}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
