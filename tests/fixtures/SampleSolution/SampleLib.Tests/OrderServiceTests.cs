using Xunit;
using SampleLib.Services;

namespace SampleLib.Tests;

public class OrderServiceTests
{
    private readonly OrderService _sut = new();

    [Fact]
    public void SerializeOrder_ReturnsJson()
    {
        var result = _sut.SerializeOrder(new { Id = 1, Name = "Test" });
        Assert.Contains("\"Id\"", result);
    }

    [Fact]
    public void DeserializeOrder_ReturnsObject()
    {
        var result = _sut.DeserializeOrder<Dictionary<string, object>>("{\"key\":\"value\"}");
        Assert.NotNull(result);
    }

    [Fact]
    public void ParseRawOrder_ReturnsJObject()
    {
        var result = _sut.ParseRawOrder("{\"orderId\":\"42\"}");
        Assert.Equal("42", result["orderId"]!.ToString());
    }

    [Fact]
    public void TryGetOrderId_ReturnsTrueAndId_WhenPresent()
    {
        var found = _sut.TryGetOrderId("{\"orderId\":\"99\"}", out var id);
        Assert.True(found);
        Assert.Equal("99", id);
    }

    [Fact]
    public void TryGetOrderId_ReturnsFalse_WhenAbsent()
    {
        var found = _sut.TryGetOrderId("{\"other\":\"value\"}", out var id);
        Assert.False(found);
        Assert.Null(id);
    }
}
