using Microsoft.EntityFrameworkCore;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class CategoryAttributeSchemaRepository :
        GenericRepository<CategoryAttributeSchema>, ICategoryAttributeSchemaRepository
    {
        public CategoryAttributeSchemaRepository(MongoDbContext context) : base(context)
        {
        }

        public Task<CategoryAttributeSchema?> GetActiveSchemaAsync(string category)
        {
            return _context.CategoryAttributeSchemas
                .AsNoTracking()
                .FirstOrDefaultAsync(schema => schema.Category == category && schema.IsActive);
        }

        public Task<CategoryAttributeSchema?> GetByVersionAsync(string category, int version)
        {
            return _context.CategoryAttributeSchemas
                .FirstOrDefaultAsync(schema => schema.Category == category && schema.Version == version);
        }

        public Task<List<CategoryAttributeSchema>> GetByCategoryAsync(string category)
        {
            return _context.CategoryAttributeSchemas
                .Where(schema => schema.Category == category)
                .OrderByDescending(schema => schema.Version)
                .ToListAsync();
        }

        public async Task<List<CategoryAttributeSchema>> GetLatestSchemasAsync()
        {
            var schemas = await _context.CategoryAttributeSchemas
                .AsNoTracking()
                .ToListAsync();

            return schemas
                .GroupBy(schema => schema.Category)
                .Select(group => group.MaxBy(schema => schema.Version)!)
                .OrderBy(schema => schema.Category)
                .ToList();
        }

        public async Task<List<string>> GetLatestCategoryNamesAsync(
            CancellationToken cancellationToken = default)
        {
            var categoryNames = await _context.CategoryAttributeSchemas
                .AsNoTracking()
                .Where(schema => schema.IsActive)
                .Select(schema => schema.Category)
                .ToListAsync(cancellationToken);

            return categoryNames
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<List<CategoryAttributeSchema>> GetPendingApprovalAsync()
        {
            return _context.CategoryAttributeSchemas
                .AsNoTracking()
                .Where(schema => !schema.IsActive && schema.ApprovedAt == null)
                .OrderByDescending(schema => schema.CreatedAt)
                .ToListAsync();
        }
    }
}
