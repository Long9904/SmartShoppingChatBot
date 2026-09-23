using System.Globalization;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;

public static class ProductQdrantPayloadReader
{
    public static async Task<Dictionary<string, string>> LoadAsync(
        IQdrantService qdrantService,
        ObjectId businessId,
        ObjectId productId,
        CancellationToken cancellationToken)
    {
        var filter = new Filter();
        filter.Must.Add(Keyword(ProductPayloadNames.BusinessId, businessId.ToString()));
        filter.Must.Add(Keyword(ProductPayloadNames.ProductId, productId.ToString()));

        var points = await qdrantService.ScrollAsync(
            QdrantCollections.Products, filter, 1, cancellationToken);
        var point = points.FirstOrDefault();
        if (point is null) return new Dictionary<string, string>(StringComparer.Ordinal);

        return FromPayload(point.Payload);
    }

    public static Dictionary<string, string> FromPayload(IEnumerable<KeyValuePair<string, Value>> payload)
        => payload
            .Select(item => (item.Key, Value: ScalarValue(item.Value)))
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Key, item => item.Value!, StringComparer.Ordinal);
    private static string? ScalarValue(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.StringValue => value.StringValue,
        Value.KindOneofCase.IntegerValue => value.IntegerValue.ToString(CultureInfo.InvariantCulture),
        Value.KindOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
        Value.KindOneofCase.BoolValue => value.BoolValue ? "true" : "false",
        _ => null
    };

    private static Condition Keyword(string key, string value) => new()
    {
        Field = new FieldCondition { Key = key, Match = new Match { Keyword = value } }
    };
}
