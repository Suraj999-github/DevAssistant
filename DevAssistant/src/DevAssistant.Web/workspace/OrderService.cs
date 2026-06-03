using System;
using System.Collections.Generic;


public class OrderService
{
    private readonly List<Order> _orders = new();

    public Order CreateOrder(string customerId, decimal amount)
    {
        if (string.IsNullOrEmpty(customerId))
            throw new ArgumentException("CustomerId required");

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");

        var order = new Order(Guid.NewGuid().ToString(), customerId, amount);
        _orders.Add(order);
        return order;
    }

    public Order? GetOrder(string orderId) =>
        _orders.FirstOrDefault(o => o.Id == orderId);
}

public record Order(string Id, string CustomerId, decimal Amount);