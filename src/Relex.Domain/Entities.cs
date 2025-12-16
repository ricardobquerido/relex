using System;

namespace Relex.Domain;

public class Location
{
    public short Id { get; set; }
    public required string Code { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public required string Code { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    
    public short LocationId { get; set; }
    public Location? Location { get; set; }
    
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    
    public DateOnly OrderDate { get; set; }
    public int Quantity { get; set; }
    public required string SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Cancelled = 3
}
