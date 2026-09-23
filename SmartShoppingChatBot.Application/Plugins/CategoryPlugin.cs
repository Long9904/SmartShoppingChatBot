using System.ComponentModel;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Plugins;

public class CategoryPlugin(ICategoryAttributeSchemaService categorySchemas)
{
    [KernelFunction]
    [Description("Bước đầu trước khi tìm sản phẩm: lấy schema đang hoạt động của một hoặc nhiều danh mục trong danh sách ở system prompt. Truyền mảng tên danh mục, kể cả khi chỉ có một. Tool trả Key, DataType, IsFilterable và AllowedValues riêng cho từng danh mục. Sau đó gọi ProductAndCategory.SemanticProductSearch với category và key-value hợp lệ; server lọc chính xác trước, rồi chuyển vector và cuối cùng BM25 khi chưa có kết quả phù hợp. Schema không phải dữ liệu tồn kho.")]
    public async Task<Result<List<CategoryFilterSchema>>> GetCategorySchemas(
        [Description("Ưu tiên một danh mục phù hợp nhất với loại sản phẩm và mục đích khách cần; ưu tiên danh mục con phù hợp. Chỉ truyền nhiều khi cần phân biệt schema hoặc khách tìm nhiều loại. Tối đa 10 tên nguyên văn từ danh sách trong prompt.")] List<string> categories,
        CancellationToken cancellationToken = default)
    {
        if (categories is null || categories.Count is < 1 or > 10
            || categories.Any(string.IsNullOrWhiteSpace))
        {
            return Result<List<CategoryFilterSchema>>.Failure(400, "Cần từ 1 đến 10 tên danh mục không rỗng.");
        }

        var results = new List<CategoryFilterSchema>();
        // Sequential reads: the scoped repository shares a DbContext.
        foreach (var category in categories.Select(value => value.Trim()).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var schema = await categorySchemas.GetActiveSchemaAsync(category);
            results.Add(new CategoryFilterSchema
            {
                Category = schema?.Category ?? category,
                IsAvailable = schema is not null,
                Message = schema is null ? "Danh mục không có schema đang hoạt động; không tìm bằng danh mục này." : "Dùng chính xác Key và AllowedValues khi lọc.",
                Attributes = schema?.Attributes.Select(attribute => new CategoryFilterAttribute
                {
                    Key = attribute.Key,
                    DisplayName = attribute.DisplayName,
                    DataType = attribute.DataType.ToString(),
                    IsFilterable = attribute.IsFilterable,
                    IsStrict = attribute.IsStrict,
                    AllowedValues = attribute.AllowedValues?.ToList() ?? []
                }).ToList() ?? []
            });
        }

        return Result<List<CategoryFilterSchema>>.Success(results);
    }
}

public sealed class CategoryFilterSchema
{
    public string Category { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<CategoryFilterAttribute> Attributes { get; init; } = [];
}

public sealed class CategoryFilterAttribute
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public bool IsFilterable { get; init; }
    public bool IsStrict { get; init; }
    public List<string> AllowedValues { get; init; } = [];
}
