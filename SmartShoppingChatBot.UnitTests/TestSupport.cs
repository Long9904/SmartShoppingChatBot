using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.UnitTests;

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);

    public static Business Business(
        BusinessEnums status = BusinessEnums.ACTIVE,
        BusinessConfig? config = null) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessName = "Mahiru Shop",
        HotLine = "0900000000",
        WebsiteUrl = "https://shop.example",
        AddressLine = "Bangkok",
        BusinessStatus = status,
        Config = config,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    public static User User(
        Business business,
        UserStatus status = UserStatus.ACTIVE,
        RoleEnums role = RoleEnums.BUSINESS_OWNER) => new()
    {
        Id = ObjectId.GenerateNewId(),
        Email = "owner@example.com",
        FullName = "Mahiru Owner",
        PasswordHash = "hashed-password",
        UserStatus = status,
        IsEmailVerified = true,
        IsProfileCompleted = true,
        Business = new BusinessEmbedded
        {
            Id = business.Id,
            BusinessName = business.BusinessName,
            Role = role,
            JoinedAt = Now
        },
        CreatedAt = Now,
        UpdatedAt = Now
    };

    public static BusinessQuota Quota(Business business, long usedTokens = 0, int usedMessages = 0) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = business.Id,
        BusinessSubscriptionId = ObjectId.GenerateNewId(),
        TokenLimit = 100_000,
        MessageLimit = 100,
        UsedTokens = usedTokens,
        UsedMessages = usedMessages,
        MaxProductAllowed = 100,
        ResetDate = Now.AddDays(30)
    };

    public static Product Product(Business business, ProductStatus status = ProductStatus.Active) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = business.Id,
        ExternalId = "SKU-001",
        ExternalProductUrl = "https://shop.example/p/1",
        Name = "Laptop",
        Description = "Gaming laptop",
        Price = 25_000_000,
        Currency = "VND",
        Brand = "Brand A",
        StockQuantity = 5,
        Category = "Laptop",
        Status = status,
        Images = ["https://shop.example/1.png"],
        Metadata = new Dictionary<string, string> { ["ram"] = "16GB" },
        QdrantPointId = Guid.NewGuid(),
        SearchContent = "Laptop",
        CreatedAt = Now,
        UpdatedAt = Now,
        CreatedBy = new UserEmbedded { Name = "Owner" },
        UpdatedBy = new UserEmbedded { Name = "Owner" }
    };

    public static Customer Customer(Business business, string externalId = "customer-1") => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = business.Id,
        CustomerExternalId = externalId,
        Status = CustomerStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    public static Conversation Conversation(Business business, Customer customer) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = business.Id,
        CustomerId = customer.Id,
        Title = "Existing conversation",
        Status = ConversationStatus.Active,
        CreateAt = Now,
        LastMessageAt = Now
    };

    public static KernelChatResult KernelResult(
        string answer = "AI answer",
        long inputTokens = 10,
        long outputTokens = 5,
        List<string>? selectedProductIds = null) => new()
    {
        Answer = answer,
        Summary = "Conversation summary",
        AISummaryContent = "Short AI summary",
        SelectedProductIds = selectedProductIds ?? [],
        InteractionType = "General",
        ComparedProductIds = [],
        InputTokens = inputTokens,
        OutputTokens = outputTokens
    };

    public static ProductResponseV2 ProductResponse(string id, string name) => new()
    {
        ProductId = id,
        Name = name
    };

    public static HttpContextAccessor HttpContext(string? authenticationType = null, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType);
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    public static IMapper Mapper() => new MapperConfiguration(cfg =>
    {
        cfg.CreateMap<BusinessConfig, BusinessConfigResponse>();
        cfg.CreateMap<Business, BusinessRegistrationResponse>();
        cfg.CreateMap<ImportJob, ImportJobResponse>()
            .ForMember(destination => destination.Id, option => option.MapFrom(source => source.Id.ToString()));
    }, NullLoggerFactory.Instance).CreateMapper();
}
