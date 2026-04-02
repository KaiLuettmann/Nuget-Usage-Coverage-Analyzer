using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SampleLib.Services;

public class OrderService
{
    public string SerializeOrder(object order)
    {
        return JsonConvert.SerializeObject(order, Formatting.Indented);
    }

    public T? DeserializeOrder<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    public JObject ParseRawOrder(string json)
    {
        return JObject.Parse(json);
    }

    public bool TryGetOrderId(string json, out string? orderId)
    {
        var obj = JObject.Parse(json);
        orderId = obj["orderId"]?.Value<string>();
        return orderId != null;
    }
}
