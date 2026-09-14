using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface ICategoryAttributeSchemaRepository : IGenericRepository<CategoryAttributeSchema>
    {
        Task<CategoryAttributeSchema?> GetActiveSchemaAsync(string category);
        Task<CategoryAttributeSchema?> GetByVersionAsync(string category, int version);
        Task<List<CategoryAttributeSchema>> GetByCategoryAsync(string category);
        Task<List<CategoryAttributeSchema>> GetLatestSchemasAsync();
        Task<List<CategoryAttributeSchema>> GetPendingApprovalAsync();
    }
}
