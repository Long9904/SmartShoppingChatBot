namespace SmartShoppingChatBot.Domain.Commons;

public static class QueryHelper
{
  
    public static string GetValidFields<TDto>(string? fields)
    {
        var allowedFields = typeof(TDto).GetProperties()
            .Select(p => p.Name)
            .ToList();

        if (string.IsNullOrWhiteSpace(fields))
            return string.Join(", ", allowedFields); // Trả về tất cả field của DTO nếu ko truyền

        var requestedFields = fields.Split(',')
            .Select(f => f.Trim())
            .Where(f => allowedFields.Any(af => string.Equals(af, f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Luôn đảm bảo có _id (nếu DTO có _id)
        if (allowedFields.Contains("_id") && !requestedFields.Any(f => f.Equals("_id", StringComparison.OrdinalIgnoreCase)))
            requestedFields.Insert(0, "_id");

        return requestedFields.Any() ? string.Join(", ", requestedFields) : string.Join(", ", allowedFields);
    }

    public static string? GetValidOrderBy<TDto>(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return null;

        var allowedFields = typeof(TDto).GetProperties().Select(p => p.Name).ToList();

        var parts = orderBy.Split(',')
            .Select(p => p.Trim().Split(' ')[0])
            .ToList();

        bool isValid = parts.All(p => allowedFields.Any(af => string.Equals(af, p, StringComparison.OrdinalIgnoreCase)));

        return isValid ? orderBy : null;
    }
}