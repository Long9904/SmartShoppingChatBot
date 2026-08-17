using AutoMapper;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.DashboardManagement.AIUsageDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.BusinessDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.RevenueDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.SummaryDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.SubscriptionsDashboard;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_SubscriptionDashboard
{
    [Fact]
    public async Task Handle_WhenSubscriptionsExist_ReturnsBusinessCountAndRatePerPlan()
    {
        var basic = Plan("Basic");
        var pro = Plan("Pro");
        var subscriptions = new[]
        {
            BusinessSubscription(basic.Id),
            BusinessSubscription(basic.Id),
            BusinessSubscription(pro.Id)
        };
        var fixture = new DashboardFixture([basic, pro], subscriptions);

        var result = await fixture.Handler.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        var basicResponse = result.Data.Items.Single(item => item.Id == basic.Id.ToString());
        basicResponse.Detail.BusinessCount.Should().Be(2);
        basicResponse.Detail.Rate.Should().BeApproximately(66.666, 0.01);
        result.Data.Items.Single(item => item.Id == pro.Id.ToString())
            .Detail.Rate.Should().BeApproximately(33.333, 0.01);
    }

    [Fact]
    public async Task Handle_WhenNoBusinessSubscriptionExists_ReturnsZeroRate()
    {
        var plan = Plan("Starter");
        var fixture = new DashboardFixture([plan], []);

        var result = await fixture.Handler.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Single();
        item.Detail.BusinessCount.Should().Be(0);
        item.Detail.Rate.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenStatusFilterIsInactive_ReturnsOnlyInactivePlans()
    {
        var active = Plan("Active", StatusEnums.Active);
        var inactive = Plan("Inactive", StatusEnums.Inactive);
        var fixture = new DashboardFixture([active, inactive], [BusinessSubscription(inactive.Id)]);

        var result = await fixture.Handler.Handle(Query(StatusEnums.Inactive), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Single();
        item.Id.Should().Be(inactive.Id.ToString());
        item.Status.Should().Be(StatusEnums.Inactive);
        item.Detail.BusinessCount.Should().Be(1);
        item.Detail.Rate.Should().Be(100);
    }

    [Fact]
    public async Task Handle_WhenPagingRequested_ReturnsRequestedPageMetadataAndItems()
    {
        var first = Plan("First");
        var second = Plan("Second");
        var fixture = new DashboardFixture([first, second], []);

        var result = await fixture.Handler.Handle(Query(pageIndex: 2, pageSize: 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(1);
        result.Data.TotalItems.Should().Be(2);
        result.Data.TotalPages.Should().Be(2);
        result.Data.Items.Single().Id.Should().Be(second.Id.ToString());
    }

    private static SubscriptionDashboardQuery Query(
        StatusEnums status = StatusEnums.Active,
        int pageIndex = 1,
        int pageSize = 10) => new()
        {
            Filter = new SubscriptionDashboardFilter
            {
                Status = status,
                PageIndex = pageIndex,
                PageSize = pageSize
            }
        };

    private static SubscriptionPlan Plan(string name, StatusEnums status = StatusEnums.Active) => new()
    {
        Id = ObjectId.GenerateNewId(),
        Name = name,
        Status = status
    };

    private static BusinessSubscription BusinessSubscription(ObjectId planId) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = ObjectId.GenerateNewId(),
        SubscriptionPlanId = planId,
        Status = StatusEnums.Active
    };

    private sealed class DashboardFixture
    {
        public SubscriptionDashboardQueryHandler Handler { get; }

        public DashboardFixture(IReadOnlyCollection<SubscriptionPlan> plans, IReadOnlyCollection<BusinessSubscription> subscriptions)
        {
            var planRepository = new Mock<ISubscriptionPlanRepository>();
            var subscriptionRepository = new Mock<ISubscriptionRepository>();

            planRepository.Setup(repository => repository.AsQueryable()).Returns(plans.AsQueryable());
            planRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<SubscriptionPlan>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<SubscriptionPlan> query, int pageIndex, int pageSize) =>
                    new BasePaginatedList<SubscriptionPlan>(
                        query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
                        query.Count(),
                        pageIndex,
                        pageSize));
            subscriptionRepository.Setup(repository => repository.AsQueryable()).Returns(subscriptions.AsQueryable());

            Handler = new SubscriptionDashboardQueryHandler(
                planRepository.Object,
                subscriptionRepository.Object,
                Mock.Of<IBusinessRepository>(),
                Mock.Of<ICurrentUserService>(),
                Mock.Of<IMapper>());
        }
    }
}

public class UT_RevenueDashboard
{
    [Fact]
    public async Task Handle_WhenSubscriptionsExist_ReturnsRevenueAndSubscriptionCounts()
    {
        var currentMonth = DateTime.Now.Month;
        var basic = Plan("Basic", 50);
        var pro = Plan("Pro", 120);
        var subscriptions = new[]
        {
            BusinessSubscription(basic.Id, StatusEnums.Active, new DateTimeOffset(DateTime.Now.Year, currentMonth, 5, 0, 0, 0, TimeSpan.Zero)),
            BusinessSubscription(pro.Id, StatusEnums.Active, new DateTimeOffset(DateTime.Now.Year, currentMonth, 7, 0, 0, 0, TimeSpan.Zero)),
            BusinessSubscription(pro.Id, StatusEnums.Inactive, new DateTimeOffset(DateTime.Now.Year, currentMonth, 9, 0, 0, 0, TimeSpan.Zero)),
            BusinessSubscription(basic.Id, StatusEnums.Active, new DateTimeOffset(DateTime.Now.Year - 1, currentMonth, 9, 0, 0, 0, TimeSpan.Zero))
        };
        var fixture = new RevenueDashboardFixture([basic, pro], subscriptions);

        var result = await fixture.Handler.Handle(new RevenueDashboardQuery
        {
            Filter = new RevenueDashboardFiter { Month = currentMonth }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Single();
        item.TotalRevenue.Should().Be(220);
        item.TotalRevenueThisMonth.Should().Be(170);
        item.ActiveSubscriptionCount.Should().Be(3);
        item.TotalSubscriptionCount.Should().Be(4);
        item.CancelledSubscriptionCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ReturnsFailure()
    {
        var planRepository = new Mock<ISubscriptionPlanRepository>();
        var subscriptionRepository = new Mock<ISubscriptionRepository>();
        subscriptionRepository.Setup(repository => repository.AsQueryable()).Throws(new InvalidOperationException("db down"));
        var handler = new RevenueDashboardQueryHandler(planRepository.Object, subscriptionRepository.Object);

        var result = await handler.Handle(new RevenueDashboardQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.Message.Should().Contain("db down");
    }

    private static SubscriptionPlan Plan(string name, decimal price) => new()
    {
        Id = ObjectId.GenerateNewId(),
        Name = name,
        Price = price,
        Status = StatusEnums.Active
    };

    private static BusinessSubscription BusinessSubscription(
        ObjectId planId,
        StatusEnums status,
        DateTimeOffset startDate) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = ObjectId.GenerateNewId(),
            SubscriptionPlanId = planId,
            StartDate = startDate,
            EndDate = startDate.AddDays(30),
            Status = status
        };

    private sealed class RevenueDashboardFixture
    {
        public RevenueDashboardQueryHandler Handler { get; }

        public RevenueDashboardFixture(
            IReadOnlyCollection<SubscriptionPlan> plans,
            IReadOnlyCollection<BusinessSubscription> subscriptions)
        {
            var planRepository = new Mock<ISubscriptionPlanRepository>();
            var subscriptionRepository = new Mock<ISubscriptionRepository>();

            planRepository.Setup(repository => repository.AsQueryable()).Returns(plans.AsQueryable());
            subscriptionRepository.Setup(repository => repository.AsQueryable()).Returns(subscriptions.AsQueryable());

            Handler = new RevenueDashboardQueryHandler(planRepository.Object, subscriptionRepository.Object);
        }
    }
}

public class UT_BusinessDashboard
{
    [Fact]
    public async Task Handle_WhenBusinessMissing_ReturnsOriginalFailure()
    {
        var fixture = new BusinessDashboardFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.Handler.Handle(Query(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.ProductRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDateRangeMissing_ReturnsBadRequest()
    {
        var fixture = new BusinessDashboardFixture();

        var result = await fixture.Handler.Handle(new BusinessDashboardQuery
        {
            From = DateOnly.FromDateTime(TestData.Now.Date)
        }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Both from and to dates are required.");
    }

    [Fact]
    public async Task Handle_WhenFromAfterTo_ReturnsBadRequest()
    {
        var fixture = new BusinessDashboardFixture();

        var result = await fixture.Handler.Handle(new BusinessDashboardQuery
        {
            From = DateOnly.FromDateTime(TestData.Now.AddDays(1).Date),
            To = DateOnly.FromDateTime(TestData.Now.Date)
        }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("The from date must be earlier than or equal to the to date.");
    }

    [Fact]
    public async Task Handle_WhenDataExists_FiltersCurrentBusinessAndCalculatesDashboardMetrics()
    {
        var fixture = new BusinessDashboardFixture();
        var from = DateOnly.FromDateTime(TestData.Now.Date);
        var otherBusiness = ObjectId.GenerateNewId();
        var customer = TestData.Customer(fixture.Business);
        var conversation = TestData.Conversation(fixture.Business, customer);
        var paidOrder = Order(fixture.Business.Id, conversation.Id, ConversationOrderEventStatus.Success, TestData.Now);
        var pendingOrder = Order(fixture.Business.Id, conversation.Id, ConversationOrderEventStatus.OrderCreated, TestData.Now);
        fixture.SetupData(
            products:
            [
                TestData.Product(fixture.Business),
                TestData.Product(new Business { Id = otherBusiness, BusinessName = "Other" })
            ],
            documents:
            [
                fixture.Document("policy.pdf", KnowledgeDocumentStatus.Embedded, TestData.Now),
                fixture.Document("deleted.pdf", KnowledgeDocumentStatus.Deleted, TestData.Now)
            ],
            conversations: [conversation],
            messages:
            [
                Message(fixture.Business.Id, conversation.Id, SenderTypeEnum.Customer, TestData.Now),
                Message(fixture.Business.Id, conversation.Id, SenderTypeEnum.Customer, TestData.Now.AddHours(1)),
                Message(fixture.Business.Id, conversation.Id, SenderTypeEnum.ChatBot, TestData.Now),
                Message(otherBusiness, ObjectId.GenerateNewId(), SenderTypeEnum.Customer, TestData.Now)
            ],
            orders: [paidOrder, pendingOrder],
            searchLogs:
            [
                SearchLog(fixture.Business.Id, "refund", true, "Purchase", ["refund", "policy"], 100, 80, TestData.Now),
                SearchLog(fixture.Business.Id, "refund", true, "Purchase", ["refund"], 200, 90, TestData.Now.AddHours(1)),
                SearchLog(fixture.Business.Id, "warranty", false, null, ["warranty"], 300, null, TestData.Now),
                SearchLog(otherBusiness, "ignored", true, "Ignored", ["ignored"], 999, 10, TestData.Now)
            ]);

        var result = await fixture.Handler.Handle(new BusinessDashboardQuery
        {
            From = from,
            To = from
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalProducts.Should().Be(1);
        result.Data.TotalKnowledgeDocuments.Should().Be(1);
        result.Data.TotalChatSessions.Should().Be(1);
        result.Data.TotalChatMessages.Should().Be(2);
        result.Data.TotalOrders.Should().Be(2);
        result.Data.PaidOrders.Should().Be(1);
        result.Data.ConversionRate.Should().Be(100);
        result.Data.AverageRetrievalLatencyMilliseconds.Should().Be(200);
        result.Data.AverageSearchHitRatePercentage.Should().Be(85);
        result.Data.ChatTraffic.Should().ContainSingle();
        result.Data.ChatTraffic[0].Messages.Should().Be(2);
        result.Data.ChatTraffic[0].Sessions.Should().Be(1);
        result.Data.ZeroResultQueries.Single().Count.Should().Be(2);
        result.Data.Intents.Should().Contain(item => item.Intent == "Purchase" && item.Count == 2);
        result.Data.Intents.Should().Contain(item => item.Intent == "Unknown" && item.Count == 1);
        result.Data.TrendingKeywords.First().Keyword.Should().Be("refund");
        result.Data.TrendingKeywords.First().Count.Should().Be(2);
    }

    private static BusinessDashboardQuery Query() => new()
    {
        From = DateOnly.FromDateTime(TestData.Now.Date),
        To = DateOnly.FromDateTime(TestData.Now.Date)
    };

    private static Message Message(
        ObjectId businessId,
        ObjectId conversationId,
        SenderTypeEnum senderType,
        DateTimeOffset createdAt) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = conversationId,
            SenderType = senderType,
            Content = "message",
            ContentType = ContentTypeEnum.Text,
            Status = MessageStatus.Completed,
            CreatedAt = createdAt
        };

    private static ConversationOrder Order(
        ObjectId businessId,
        ObjectId conversationId,
        ConversationOrderEventStatus status,
        DateTimeOffset createdAt) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = conversationId,
            ExternalOrderId = Guid.NewGuid().ToString(),
            Status = status,
            Amount = 100,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static SearchQueryLog SearchLog(
        ObjectId businessId,
        string query,
        bool zeroResult,
        string? interactionType,
        List<string> keywords,
        long retrievalLatency,
        double? hitRate,
        DateTimeOffset createdAt) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = ObjectId.GenerateNewId(),
            MessageId = ObjectId.GenerateNewId(),
            UserRawQuery = query,
            ZeroResult = zeroResult,
            InteractionType = interactionType,
            TrendKeywords = keywords,
            RetrievalLatency = retrievalLatency,
            HitRateScore = hitRate,
            CreatedAt = createdAt
        };

    private sealed class BusinessDashboardFixture
    {
        public Business Business { get; } = TestData.Business();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IKnowledgeDocumentRepository> DocumentRepository { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public Mock<IMessageRepository> MessageRepository { get; } = new();
        public Mock<IConversationOrderRepository> OrderRepository { get; } = new();
        public Mock<ISearchQueryLogRepository> SearchQueryLogRepository { get; } = new();
        public BusinessDashboardQueryHandler Handler { get; }

        public BusinessDashboardFixture()
        {
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            SetupData([], [], [], [], [], []);
            Handler = new BusinessDashboardQueryHandler(
                CurrentUser.Object,
                ProductRepository.Object,
                DocumentRepository.Object,
                ConversationRepository.Object,
                MessageRepository.Object,
                OrderRepository.Object,
                SearchQueryLogRepository.Object);
        }

        public void SetupData(
            IReadOnlyCollection<Product> products,
            IReadOnlyCollection<KnowledgeDocument> documents,
            IReadOnlyCollection<Conversation> conversations,
            IReadOnlyCollection<Message> messages,
            IReadOnlyCollection<ConversationOrder> orders,
            IReadOnlyCollection<SearchQueryLog> searchLogs)
        {
            ProductRepository.Setup(repository => repository.AsQueryable()).Returns(products.AsQueryable());
            DocumentRepository.Setup(repository => repository.AsQueryable()).Returns(documents.AsQueryable());
            ConversationRepository.Setup(repository => repository.AsQueryable()).Returns(conversations.AsQueryable());
            MessageRepository.Setup(repository => repository.AsQueryable()).Returns(messages.AsQueryable());
            OrderRepository.Setup(repository => repository.AsQueryable()).Returns(orders.AsQueryable());
            SearchQueryLogRepository.Setup(repository => repository.AsQueryable()).Returns(searchLogs.AsQueryable());
        }

        public KnowledgeDocument Document(string fileName, KnowledgeDocumentStatus status, DateTimeOffset createdAt) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = Business.Id,
            Title = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            PublicId = fileName,
            FileUrl = $"https://cdn.example/{fileName}",
            ContentType = "application/pdf",
            Type = "pdf",
            SizeInBytes = 100,
            Status = status,
            CreatedAt = createdAt
        };
    }
}

public class UT_AIUsageDashboard
{
    [Fact]
    public async Task Handle_WhenRangeInvalid_DefaultsToSevenDaysAndAggregatesUsage()
    {
        var now = DateTimeOffset.Now;
        var logs = new[]
        {
            UsageLog(now.AddDays(-1), input: 10, output: 5, messages: 2),
            UsageLog(now.AddDays(-1), input: 20, output: 7, messages: 3),
            UsageLog(now.AddDays(-8), input: 100, output: 100, messages: 9)
        };
        var fixture = new AIUsageDashboardFixture(logs);

        var result = await fixture.Handler.Handle(new AIUsageDashboardQuery
        {
            Filter = new AIUsageDashboardFilter { Range = 0 }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.InputTokenUsed.Should().Be(30);
        result.Data.OutputTokenUsed.Should().Be(12);
        result.Data.TotalTokenUsed.Should().Be(42);
        result.Data.TotalMessageUsed.Should().Be(5);
        result.Data.ChartData.Should().ContainSingle();
        result.Data.ChartData[0].TotalTokenUsed.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ReturnsFailure()
    {
        var repository = new Mock<IUsageQuotaLogRepository>();
        repository.Setup(repo => repo.AsQueryable()).Throws(new InvalidOperationException("usage db down"));
        var handler = new AIUsageDashboardQueryHandler(repository.Object);

        var result = await handler.Handle(new AIUsageDashboardQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.Message.Should().Contain("usage db down");
    }

    private static UsageQuotaLog UsageLog(DateTimeOffset createdAt, long input, long output, int messages) => new()
    {
        Id = ObjectId.GenerateNewId(),
        BusinessId = ObjectId.GenerateNewId(),
        BusinessQuotaId = ObjectId.GenerateNewId(),
        InputTokens = input,
        OutputTokens = output,
        BillableTokens = input + output,
        MessageUsed = messages,
        CreatedAt = createdAt
    };

    private sealed class AIUsageDashboardFixture
    {
        public AIUsageDashboardQueryHandler Handler { get; }

        public AIUsageDashboardFixture(IReadOnlyCollection<UsageQuotaLog> logs)
        {
            var repository = new Mock<IUsageQuotaLogRepository>();
            repository.Setup(repo => repo.AsQueryable()).Returns(logs.AsQueryable());
            Handler = new AIUsageDashboardQueryHandler(repository.Object);
        }
    }
}

public class UT_SummaryDashboard
{
    [Fact]
    public async Task Handle_WhenDataExists_ReturnsSummaryCountsAndRevenue()
    {
        var activeBusiness = TestData.Business();
        var inactiveBusiness = TestData.Business(BusinessEnums.SUSPENDED);
        var plan = new SubscriptionPlan
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Growth",
            Price = 99,
            Status = StatusEnums.Active
        };
        var fixture = new SummaryDashboardFixture(
            businesses: [activeBusiness, inactiveBusiness],
            users: [TestData.User(activeBusiness), TestData.User(inactiveBusiness)],
            products: [TestData.Product(activeBusiness)],
            documents:
            [
                new KnowledgeDocument
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = activeBusiness.Id,
                    Title = "Policy",
                    FileName = "policy.pdf",
                    FileUrl = "https://cdn.example/policy.pdf",
                    PublicId = "policy",
                    ContentType = "application/pdf",
                    SizeInBytes = 100,
                    Type = "pdf",
                    Status = KnowledgeDocumentStatus.Embedded
                }
            ],
            conversations: [TestData.Conversation(activeBusiness, TestData.Customer(activeBusiness))],
            messages:
            [
                new Message
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = activeBusiness.Id,
                    ConversationId = ObjectId.GenerateNewId(),
                    Content = "hello",
                    SenderType = SenderTypeEnum.Customer,
                    ContentType = ContentTypeEnum.Text,
                    Status = MessageStatus.Completed,
                    CreatedAt = TestData.Now
                }
            ],
            subscriptions:
            [
                new BusinessSubscription
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = activeBusiness.Id,
                    SubscriptionPlanId = plan.Id,
                    Status = StatusEnums.Active
                },
                new BusinessSubscription
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = inactiveBusiness.Id,
                    SubscriptionPlanId = plan.Id,
                    Status = StatusEnums.Inactive
                }
            ],
            plans: [plan]);

        var result = await fixture.Handler.Handle(new SummaryDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalBusiness.Should().Be(2);
        result.Data.ActiveBusiness.Should().Be(1);
        result.Data.TotalUsers.Should().Be(2);
        result.Data.TotalProduct.Should().Be(1);
        result.Data.TotalDocument.Should().Be(1);
        result.Data.TotalChatSession.Should().Be(1);
        result.Data.TotalMessage.Should().Be(1);
        result.Data.TotalTokenUsed.Should().Be(1);
        result.Data.TotalRevenue.Should().Be(99);
        result.Data.ActiveSubscriptionCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ReturnsFailure()
    {
        var businessRepository = new Mock<IBusinessRepository>();
        businessRepository.Setup(repository => repository.AsQueryable()).Throws(new InvalidOperationException("summary db down"));
        var handler = new SummaryDashboardQueryHandler(
            businessRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IProductRepository>(),
            Mock.Of<IKnowledgeDocumentRepository>(),
            Mock.Of<IConversationRepository>(),
            Mock.Of<IMessageRepository>(),
            Mock.Of<ISubscriptionRepository>(),
            Mock.Of<ISubscriptionPlanRepository>());

        var result = await handler.Handle(new SummaryDashboardQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.Message.Should().Contain("summary db down");
    }

    private sealed class SummaryDashboardFixture
    {
        public SummaryDashboardQueryHandler Handler { get; }

        public SummaryDashboardFixture(
            IReadOnlyCollection<Business> businesses,
            IReadOnlyCollection<User> users,
            IReadOnlyCollection<Product> products,
            IReadOnlyCollection<KnowledgeDocument> documents,
            IReadOnlyCollection<Conversation> conversations,
            IReadOnlyCollection<Message> messages,
            IReadOnlyCollection<BusinessSubscription> subscriptions,
            IReadOnlyCollection<SubscriptionPlan> plans)
        {
            var businessRepository = new Mock<IBusinessRepository>();
            var userRepository = new Mock<IUserRepository>();
            var productRepository = new Mock<IProductRepository>();
            var documentRepository = new Mock<IKnowledgeDocumentRepository>();
            var conversationRepository = new Mock<IConversationRepository>();
            var messageRepository = new Mock<IMessageRepository>();
            var subscriptionRepository = new Mock<ISubscriptionRepository>();
            var planRepository = new Mock<ISubscriptionPlanRepository>();

            businessRepository.Setup(repository => repository.AsQueryable()).Returns(businesses.AsQueryable());
            userRepository.Setup(repository => repository.AsQueryable()).Returns(users.AsQueryable());
            productRepository.Setup(repository => repository.AsQueryable()).Returns(products.AsQueryable());
            documentRepository.Setup(repository => repository.AsQueryable()).Returns(documents.AsQueryable());
            conversationRepository.Setup(repository => repository.AsQueryable()).Returns(conversations.AsQueryable());
            messageRepository.Setup(repository => repository.AsQueryable()).Returns(messages.AsQueryable());
            subscriptionRepository.Setup(repository => repository.AsQueryable()).Returns(subscriptions.AsQueryable());
            planRepository.Setup(repository => repository.AsQueryable()).Returns(plans.AsQueryable());

            Handler = new SummaryDashboardQueryHandler(
                businessRepository.Object,
                userRepository.Object,
                productRepository.Object,
                documentRepository.Object,
                conversationRepository.Object,
                messageRepository.Object,
                subscriptionRepository.Object,
                planRepository.Object);
        }
    }
}
