using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Relex.Api.Features.Orders;
using Relex.Api.Features.Orders.Dtos;
using Relex.Api.Infrastructure;
using Relex.Domain;
using Xunit;

namespace Relex.Tests;

public class OrderUnitTests
{
    private readonly RelexDbContext _db;
    private readonly Mock<ILookupCache> _cacheMock;

    public OrderUnitTests()
    {
        var options = new DbContextOptionsBuilder<RelexDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new RelexDbContext(options);
        _cacheMock = new Mock<ILookupCache>();
    }

    [Fact]
    public async Task CreateOrder_InvalidQuantity_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest 
        { 
            Quantity = 0,
            LocationCode = "LOC", 
            ProductCode = "PROD", 
            SubmittedBy = "User" 
        };

        // Act
        var result = await CreateOrder.HandleAsync(request, _db, _cacheMock.Object, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("Quantity");
    }

    [Fact]
    public async Task CreateOrder_InvalidLocation_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest 
        { 
            Quantity = 10, 
            LocationCode = "INVALID", 
            ProductCode = "PROD", 
            SubmittedBy = "User" 
        };

        _cacheMock.Setup(x => x.GetLocationId("INVALID")).Returns((short?)null);

        // Act
        var result = await CreateOrder.HandleAsync(request, _db, _cacheMock.Object, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("Invalid Location Code");
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateOrderRequest 
        { 
            Quantity = 10, 
            LocationCode = "LOC-1", 
            ProductCode = "PROD-1", 
            SubmittedBy = "User",
            OrderDate = new DateOnly(2023, 1, 1)
        };

        _cacheMock.Setup(x => x.GetLocationId("LOC-1")).Returns(1);
        _cacheMock.Setup(x => x.GetProductId("PROD-1")).Returns(100);

        // Act
        var result = await CreateOrder.HandleAsync(request, _db, _cacheMock.Object, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<Created<OrderDto>>().Subject;
        created.Value.Should().NotBeNull();
        created.Value!.Status.Should().Be("Pending");
        var savedOrder = await _db.Orders.FirstOrDefaultAsync();
        savedOrder.Should().NotBeNull();
        savedOrder!.LocationId.Should().Be(1);
    }

    [Fact]
    public async Task DeleteOrder_PastOrder_ReturnsBadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var pastOrder = new Order
        {
            Id = orderId,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Status = OrderStatus.Pending,
            LocationId = 1,
            ProductId = 1,
            Quantity = 10,
            SubmittedBy = "User"
        };
        _db.Orders.Add(pastOrder);
        await _db.SaveChangesAsync();

        // Act
        var result = await DeleteOrder.HandleAsync(orderId, _db, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("past");
    }

    [Fact]
    public async Task DeleteOrder_ConfirmedOrder_ReturnsBadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var confirmedOrder = new Order
        {
            Id = orderId,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            Status = OrderStatus.Confirmed,
            LocationId = 1,
            ProductId = 1,
            Quantity = 10,
            SubmittedBy = "User"
        };
        _db.Orders.Add(confirmedOrder);
        await _db.SaveChangesAsync();

        // Act
        var result = await DeleteOrder.HandleAsync(orderId, _db, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("status");
    }

    [Fact]
    public async Task DeleteOrder_ValidPendingOrder_ReturnsNoContent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            Status = OrderStatus.Pending,
            LocationId = 1,
            ProductId = 1,
            Quantity = 10,
            SubmittedBy = "User"
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var result = await DeleteOrder.HandleAsync(orderId, _db, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NoContent>();
        var deleted = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        deleted.Should().BeNull();
        
        (await _db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateOrder_InvalidQuantity_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateOrderRequest { Quantity = -5 };

        // Act
        var result = await UpdateOrder.HandleAsync(Guid.NewGuid(), request, _db, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("Quantity");
    }

    [Fact]
    public async Task UpdateOrder_InvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateOrderRequest { Quantity = 10, Status = "SuperConfirmed" };

        // Act
        var result = await UpdateOrder.HandleAsync(Guid.NewGuid(), request, _db, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequest<string>>().Subject;
        badRequest.Value.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task UpdateOrder_NotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateOrderRequest { Quantity = 10 };

        // Act
        var result = await UpdateOrder.HandleAsync(Guid.NewGuid(), request, _db, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateOrder_ValidUpdate_ReturnsNoContentAndUpdatesDB()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var originalTimestamp = DateTimeOffset.UtcNow.AddHours(-1);
        var existingOrder = new Order
        {
            Id = orderId,
            OrderDate = new DateOnly(2023, 1, 1),
            Status = OrderStatus.Pending,
            LocationId = 1,
            ProductId = 1,
            Quantity = 10,
            SubmittedBy = "User",
            SubmittedAt = originalTimestamp
        };
        _db.Orders.Add(existingOrder);
        await _db.SaveChangesAsync();

        var request = new UpdateOrderRequest { Quantity = 50, Status = "Confirmed" };

        // Act
        var result = await UpdateOrder.HandleAsync(orderId, request, _db, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NoContent>();

        var updatedOrder = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Quantity.Should().Be(50);
        updatedOrder.Status.Should().Be(OrderStatus.Confirmed);
        updatedOrder.SubmittedAt.Should().BeAfter(originalTimestamp); 
    }

    [Fact]
    public async Task GetOrder_NotFound_ReturnsNotFound()
    {
        // Act
        var result = await GetOrder.HandleAsync(Guid.NewGuid(), _db, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task GetOrder_ExistingId_ReturnsOk()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            OrderDate = new DateOnly(2023, 1, 1),
            Status = OrderStatus.Pending,
            LocationId = 1,
            ProductId = 1,
            Quantity = 10,
            SubmittedBy = "User",
            Location = new Location { Id = 1, Code = "LOC-1" }, 
            Product = new Product { Id = 1, Code = "PROD-1" }
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var result = await GetOrder.HandleAsync(orderId, _db, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<OrderDto>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(orderId);
        okResult.Value.LocationCode.Should().Be("LOC-1"); 
    }
}
