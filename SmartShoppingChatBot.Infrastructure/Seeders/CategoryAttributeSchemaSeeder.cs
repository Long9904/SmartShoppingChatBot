using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Infrastructure.Seeders;

public sealed class CategoryAttributeSchemaSeeder
{
    private const string TemplateDirectoryName = "SeederTemplate";
    private const string SeederActorName = "System Seeder";

    private readonly MongoDbContext _context;
    private readonly ILogger<CategoryAttributeSchemaSeeder> _logger;
    private readonly TimeProvider _timeProvider;

    public CategoryAttributeSchemaSeeder(
        MongoDbContext context,
        ILogger<CategoryAttributeSchemaSeeder> logger,
        TimeProvider timeProvider)
    {
        _context = context;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var templateDirectory = Path.Combine(AppContext.BaseDirectory, TemplateDirectoryName);
        if (!Directory.Exists(templateDirectory))
        {
            _logger.LogWarning(
                "Category attribute schema template directory {TemplateDirectory} was not found. Skipping seeding.",
                templateDirectory);
            return;
        }

        var templateFiles = Directory
            .EnumerateFiles(templateDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (templateFiles.Length == 0)
        {
            _logger.LogWarning(
                "No category attribute schema templates were found in {TemplateDirectory}. Skipping seeding.",
                templateDirectory);
            return;
        }

        var templates = new List<CategoryAttributeSchemaTemplate>(templateFiles.Length);
        foreach (var templateFile in templateFiles)
        {
            var json = await File.ReadAllTextAsync(templateFile, cancellationToken);
            var template = JsonSerializer.Deserialize<CategoryAttributeSchemaTemplate>(json, JsonOptions)
                ?? throw new InvalidDataException(
                    $"Category attribute schema template '{Path.GetFileName(templateFile)}' is empty.");

            ValidateTemplate(template, templateFile);
            templates.Add(template);
        }

        var duplicateCategory = templates
            .GroupBy(template => template.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCategory is not null)
        {
            throw new InvalidDataException(
                $"Category '{duplicateCategory.Key}' is defined by more than one seeder template.");
        }

        var existingCategories = await _context.CategoryAttributeSchemas
            .AsNoTracking()
            .Select(schema => schema.Category)
            .ToListAsync(cancellationToken);
        var existingCategorySet = existingCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = _timeProvider.GetUtcNow();

        var schemas = templates
            .Where(template => !existingCategorySet.Contains(template.Category.Trim()))
            .Select(template => new CategoryAttributeSchema
            {
                Id = ObjectId.GenerateNewId(),
                Category = template.Category.Trim(),
                Version = 1,
                IsActive = true,
                Attributes = template.Attributes,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = CreateSeederActor(),
                ApprovedBy = CreateSeederActor(),
                ApprovedAt = now
            })
            .ToList();

        if (schemas.Count == 0)
        {
            _logger.LogInformation(
                "All {TemplateCount} category attribute schema templates already exist. Skipping seeding.",
                templates.Count);
            return;
        }

        await _context.CategoryAttributeSchemas.AddRangeAsync(schemas, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {SchemaCount} category attribute schemas from {TemplateCount} templates.",
            schemas.Count,
            templates.Count);
    }

    private static void ValidateTemplate(
        CategoryAttributeSchemaTemplate template,
        string templateFile)
    {
        var fileName = Path.GetFileName(templateFile);
        if (string.IsNullOrWhiteSpace(template.Category))
        {
            throw new InvalidDataException($"Template '{fileName}' must define a category.");
        }

        if (template.Attributes.Count == 0)
        {
            throw new InvalidDataException($"Template '{fileName}' must define at least one attribute.");
        }

        var duplicateKey = template.Attributes
            .GroupBy(attribute => attribute.Key?.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);

        if (duplicateKey is not null)
        {
            throw new InvalidDataException(
                $"Template '{fileName}' contains an empty or duplicate attribute key '{duplicateKey.Key}'.");
        }

        foreach (var attribute in template.Attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.DisplayName))
            {
                throw new InvalidDataException(
                    $"Attribute '{attribute.Key}' in template '{fileName}' must define a displayName.");
            }

            if (attribute.IsStrict && attribute.AllowedValues is not { Count: > 0 })
            {
                throw new InvalidDataException(
                    $"Strict attribute '{attribute.Key}' in template '{fileName}' must define allowedValues.");
            }

            if (attribute.AllowedValues is null)
            {
                continue;
            }

            var normalizedValues = attribute.AllowedValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();

            if (normalizedValues.Count != attribute.AllowedValues.Count
                || normalizedValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedValues.Count)
            {
                throw new InvalidDataException(
                    $"Attribute '{attribute.Key}' in template '{fileName}' contains empty or duplicate allowedValues.");
            }

            attribute.AllowedValues = normalizedValues;
        }
    }

    private static UserEmbedded CreateSeederActor()
    {
        return new UserEmbedded
        {
            Id = ObjectId.Empty,
            Name = SeederActorName
        };
    }

    private sealed class CategoryAttributeSchemaTemplate
    {
        public string Category { get; init; } = string.Empty;
        public List<AttributeDefinition> Attributes { get; init; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
