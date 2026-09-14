using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface;

public interface ICategoryAttributeSchemaService
{
    Task<CategoryAttributeSchema?> GetActiveSchemaAsync(string category);
    Task InvalidateAsync(string category);
}
