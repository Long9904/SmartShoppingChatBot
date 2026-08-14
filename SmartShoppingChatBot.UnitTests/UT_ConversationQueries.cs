using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationOrderEvents;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationProductComparisons;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationSearchQueryLogs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ConversationQueries
{
    [Fact]
    public async Task GetChatHistory_WhenConversationIdInvalid_ReturnsEmptySuccessWithoutAuthentication()
    {
        var fixture = new ConversationQueryFixture();
        var query = fixture.ChatHistoryQuery();
        query.ConversationId = "invalid";

        var result = await fixture.ChatHistoryHandler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        fixture.CurrentUser.Verify(service => service.GetBusiness(), Times.Never);
    }

    [Fact]
    public async Task GetChatHistory_WhenCursorInvalid_ReturnsEmptySuccessWithoutAuthentication()
    {
        var fixture = new ConversationQueryFixture();
        var query = fixture.ChatHistoryQuery();
        query.LastCursor = "invalid";

        var result = await fixture.ChatHistoryHandler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        fixture.CurrentUser.Verify(service => service.GetBusiness(), Times.Never);
    }

    [Fact]
    public async Task GetChatHistory_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ConversationQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business", messageCode: "BUSINESS_INVALID"));

        var result = await fixture.ChatHistoryHandler.Handle(fixture.ChatHistoryQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.MessageCode.Should().Be("BUSINESS_INVALID");
    }

    [Fact]
    public async Task GetChatHistory_WhenCustomerMissing_ReturnsEmptySuccess()
    {
        var fixture = new ConversationQueryFixture();
        fixture.CustomerRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
            .ReturnsAsync((Customer?)null);

        var result = await fixture.ChatHistoryHandler.Handle(fixture.ChatHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        fixture.MessageRepository.Verify(repository => repository.MessageCursorPaging(
            It.IsAny<ObjectId>(), It.IsAny<int>(), It.IsAny<ObjectId?>(), It.IsAny<string?>(), It.IsAny<SenderTypeEnum?>()), Times.Never);
    }

    [Fact]
    public async Task GetChatHistory_WhenConversationMissing_ReturnsEmptySuccess()
    {
        var fixture = new ConversationQueryFixture();
        fixture.ConversationRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Conversation, bool>>>(),
                It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
            .ReturnsAsync((Conversation?)null);

        var result = await fixture.ChatHistoryHandler.Handle(fixture.ChatHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChatHistory_ValidRequest_PassesCursorAndMapsPage()
    {
        var fixture = new ConversationQueryFixture();
        var cursor = ObjectId.GenerateNewId();
        var query = fixture.ChatHistoryQuery();
        query.LastCursor = cursor.ToString();
        query.Limit = 7;

        var result = await fixture.ChatHistoryHandler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.HasMore.Should().BeTrue();
        result.Data.NextCursor.Should().Be("next-cursor");
        var message = result.Data.Items.Should().ContainSingle().Which;
        message.Content.Should().Be("Hello");
        message.ProductReferences.Should().ContainSingle()
            .Which.ExternalId.Should().Be(fixture.ResolvedProduct.ExternalProductId);
        message.ProductReferences[0].ProductId.Should().Be(fixture.ResolvedProduct.ProductId);
        fixture.MessageRepository.Verify(repository => repository.MessageCursorPaging(
            fixture.Conversation.Id, 7, cursor, null, null), Times.Once);
    }

    [Fact]
    public async Task GetConversationDetail_WhenConversationIdInvalid_ReturnsBadRequest()
    {
        var fixture = new ConversationQueryFixture();
        var query = fixture.DetailQuery(conversationId: "invalid");

        var result = await fixture.DetailHandler.Handle(query, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.CurrentUser.Verify(service => service.GetBusiness(), Times.Never);
    }

    [Fact]
    public async Task GetConversationDetail_WhenCursorInvalid_ReturnsBadRequest()
    {
        var fixture = new ConversationQueryFixture();
        var query = fixture.DetailQuery(lastCursor: "invalid");

        var result = await fixture.DetailHandler.Handle(query, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("cursor");
    }

    [Fact]
    public async Task GetConversationDetail_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ConversationQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden", messageCode: "BUSINESS_FORBIDDEN"));

        var result = await fixture.DetailHandler.Handle(fixture.DetailQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        result.MessageCode.Should().Be("BUSINESS_FORBIDDEN");
    }

    [Fact]
    public async Task GetConversationDetail_WhenCustomerMissing_ReturnsNotFound()
    {
        var fixture = new ConversationQueryFixture();
        fixture.CustomerRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
            .ReturnsAsync((Customer?)null);

        var result = await fixture.DetailHandler.Handle(fixture.DetailQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Customer not found.");
    }

    [Fact]
    public async Task GetConversationDetail_WhenConversationMissing_ReturnsNotFound()
    {
        var fixture = new ConversationQueryFixture();
        fixture.ConversationRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Conversation, bool>>>(),
                It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
            .ReturnsAsync((Conversation?)null);

        var result = await fixture.DetailHandler.Handle(fixture.DetailQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Conversation not found.");
    }

    [Fact]
    public async Task GetConversationDetail_ValidRequest_TrimsSearchPassesFiltersAndMapsPage()
    {
        var fixture = new ConversationQueryFixture();
        var cursor = ObjectId.GenerateNewId();
        var query = fixture.DetailQuery(lastCursor: cursor.ToString());
        query.Filter.Limit = 9;
        query.Filter.Search = "  laptop  ";
        query.Filter.SenderType = SenderTypeEnum.ChatBot;

        var result = await fixture.DetailHandler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var message = result.Data!.Items.Should().ContainSingle().Which;
        message.SenderType.Should().Be(SenderTypeEnum.ChatBot);
        message.ProductReferences.Should().ContainSingle()
            .Which.ExternalId.Should().Be(fixture.ResolvedProduct.ExternalProductId);
        message.ProductReferences[0].ProductId.Should().Be(fixture.ResolvedProduct.ProductId);
        result.Data.HasMore.Should().BeTrue();
        fixture.MessageRepository.Verify(repository => repository.MessageCursorPaging(
            fixture.Conversation.Id, 9, cursor, "laptop", SenderTypeEnum.ChatBot), Times.Once);
    }

    [Fact]
    public async Task GetConversationOrderEvents_ValidRequest_PassesCursorAndMapsPage()
    {
        var fixture = new ConversationQueryFixture();
        var cursor = ObjectId.GenerateNewId();

        var result = await fixture.OrderEventsHandler.Handle(
            new GetConversationOrderEventsQuery
            {
                CustomerExternalId = fixture.Customer.CustomerExternalId,
                ConversationId = fixture.Conversation.Id.ToString(),
                Filter = new GetConversationOrderEventsFilter
                {
                    LastCursor = cursor.ToString(),
                    Limit = 7
                }
            },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle()
            .Which.ExternalOrderId.Should().Be("ORDER-001");
        result.Data.HasMore.Should().BeTrue();
        result.Data.NextCursor.Should().Be("order-next-cursor");
        fixture.ConversationOrderEventRepository.Verify(repository => repository.CursorPagingAsync(
            fixture.Business.Id,
            fixture.Conversation.Id,
            7,
            cursor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConversationOrderEvents_InvalidCursor_ReturnsBadRequestWithoutRepositoryQuery()
    {
        var fixture = new ConversationQueryFixture();

        var result = await fixture.OrderEventsHandler.Handle(
            new GetConversationOrderEventsQuery
            {
                CustomerExternalId = fixture.Customer.CustomerExternalId,
                ConversationId = fixture.Conversation.Id.ToString(),
                Filter = new GetConversationOrderEventsFilter { LastCursor = "invalid" }
            },
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.ConversationOrderEventRepository.Verify(repository => repository.CursorPagingAsync(
            It.IsAny<ObjectId>(),
            It.IsAny<ObjectId>(),
            It.IsAny<int>(),
            It.IsAny<ObjectId?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetConversationProductComparisons_ValidRequest_ReturnsIndependentCursorPage()
    {
        var fixture = new ConversationQueryFixture();
        var cursor = ObjectId.GenerateNewId();

        var result = await fixture.ProductComparisonsHandler.Handle(
            new GetConversationProductComparisonsQuery
            {
                CustomerExternalId = fixture.Customer.CustomerExternalId,
                ConversationId = fixture.Conversation.Id.ToString(),
                Filter = new GetConversationProductComparisonsFilter
                {
                    LastCursor = cursor.ToString(),
                    Limit = 5
                }
            },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle()
            .Which.MessageId.Should().Be(fixture.Message.Id.ToString());
        result.Data.NextCursor.Should().Be("comparison-next-cursor");
        fixture.ProductComparationRepository.Verify(repository => repository.CursorPagingAsync(
            fixture.Business.Id,
            fixture.Conversation.Id,
            5,
            cursor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConversationSearchQueryLogs_ValidRequest_ReturnsIndependentCursorPage()
    {
        var fixture = new ConversationQueryFixture();
        var cursor = ObjectId.GenerateNewId();

        var result = await fixture.SearchQueryLogsHandler.Handle(
            new GetConversationSearchQueryLogsQuery
            {
                CustomerExternalId = fixture.Customer.CustomerExternalId,
                ConversationId = fixture.Conversation.Id.ToString(),
                Filter = new GetConversationSearchQueryLogsFilter
                {
                    LastCursor = cursor.ToString(),
                    Limit = 6
                }
            },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle()
            .Which.UserRawQuery.Should().Be("laptop");
        result.Data.NextCursor.Should().Be("search-log-next-cursor");
        fixture.SearchQueryLogRepository.Verify(repository => repository.CursorPagingAsync(
            fixture.Business.Id,
            fixture.Conversation.Id,
            6,
            cursor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConversationAnalyticsValidators_InvalidCursors_AreRejected()
    {
        var productResult = new GetConversationProductComparisonsQueryValidator().Validate(
            new GetConversationProductComparisonsQuery
            {
                CustomerExternalId = "customer-1",
                ConversationId = ObjectId.GenerateNewId().ToString(),
                Filter = new GetConversationProductComparisonsFilter { LastCursor = "invalid" }
            });
        var searchResult = new GetConversationSearchQueryLogsQueryValidator().Validate(
            new GetConversationSearchQueryLogsQuery
            {
                CustomerExternalId = "customer-1",
                ConversationId = ObjectId.GenerateNewId().ToString(),
                Filter = new GetConversationSearchQueryLogsFilter { LastCursor = "invalid" }
            });

        productResult.IsValid.Should().BeFalse();
        searchResult.IsValid.Should().BeFalse();
    }

    private sealed class ConversationQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public Customer Customer { get; }
        public Conversation Conversation { get; }
        public Message Message { get; }
        public ProductResponseV2 ResolvedProduct { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ICustomerRepository> CustomerRepository { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public Mock<IMessageRepository> MessageRepository { get; } = new();
        public Mock<IProductReferenceResolver> ProductReferenceResolver { get; } = new();
        public Mock<IProductComparationRepository> ProductComparationRepository { get; } = new();
        public Mock<IConversationOrderEventRepository> ConversationOrderEventRepository { get; } = new();
        public Mock<ISearchQueryLogRepository> SearchQueryLogRepository { get; } = new();
        public GetChatHistoryQueryHandler ChatHistoryHandler { get; }
        public GetCustomerConversationDetailQueryHandler DetailHandler { get; }
        public GetConversationOrderEventsQueryHandler OrderEventsHandler { get; }
        public GetConversationProductComparisonsQueryHandler ProductComparisonsHandler { get; }
        public GetConversationSearchQueryLogsQueryHandler SearchQueryLogsHandler { get; }

        public ConversationQueryFixture()
        {
            Customer = TestData.Customer(Business);
            Conversation = TestData.Conversation(Business, Customer);
            var storedProductId = ObjectId.GenerateNewId().ToString();
            ResolvedProduct = TestData.ProductResponse(
                storedProductId,
                "Referenced product",
                "external-referenced-product");
            Message = new Message
            {
                Id = ObjectId.GenerateNewId(),
                ConversationId = Conversation.Id,
                BusinessId = Business.Id,
                Content = "Hello",
                SenderType = SenderTypeEnum.ChatBot,
                ContentType = ContentTypeEnum.Text,
                Status = MessageStatus.Completed,
                CreatedAt = TestData.Now,
                CacheProductReference =
                [
                    new ProductReference
                    {
                        ProductId = storedProductId,
                        ExternalProductId = ResolvedProduct.ExternalProductId,
                        DisplayName = ResolvedProduct.Name
                    }
                ]
            };
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CustomerRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Customer, bool>>>(),
                    It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
                .ReturnsAsync(Customer);
            ConversationRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Conversation, bool>>>(),
                    It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
                .ReturnsAsync(Conversation);
            MessageRepository.Setup(repository => repository.MessageCursorPaging(
                    It.IsAny<ObjectId>(), It.IsAny<int>(), It.IsAny<ObjectId?>(),
                    It.IsAny<string?>(), It.IsAny<SenderTypeEnum?>()))
                .ReturnsAsync(new CursorPage<Message>
                {
                    Items = [Message],
                    HasMore = true,
                    NextCursor = "next-cursor"
                });
            ProductReferenceResolver.Setup(resolver => resolver.ResolveAsync(
                    It.IsAny<ObjectId>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<ProductResponseV2>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, ProductResponseV2>(StringComparer.OrdinalIgnoreCase)
                {
                    [storedProductId] = ResolvedProduct
                });
            ProductReferenceResolver.Setup(resolver => resolver.GetInOrder(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IReadOnlyDictionary<string, ProductResponseV2>>()))
                .Returns((
                    IEnumerable<string> productIds,
                    IReadOnlyDictionary<string, ProductResponseV2> productById) => productIds
                    .Where(productById.ContainsKey)
                    .Select(productId => productById[productId])
                    .ToList());
            ProductComparationRepository.Setup(repository => repository.CursorPagingAsync(
                    It.IsAny<ObjectId>(),
                    It.IsAny<ObjectId>(),
                    It.IsAny<int>(),
                    It.IsAny<ObjectId?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CursorPage<ProductComparation>
                {
                    Items =
                    [
                        new ProductComparation
                        {
                            Id = ObjectId.GenerateNewId(),
                            BusinessId = Business.Id,
                            ConversationId = Conversation.Id,
                            MessageId = Message.Id,
                            CustomerId = Customer.Id,
                            CreatedAt = TestData.Now
                        }
                    ],
                    HasMore = true,
                    NextCursor = "comparison-next-cursor"
                });
            ConversationOrderEventRepository.Setup(repository => repository.CursorPagingAsync(
                    It.IsAny<ObjectId>(),
                    It.IsAny<ObjectId>(),
                    It.IsAny<int>(),
                    It.IsAny<ObjectId?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CursorPage<ConversationOrderEvent>
                {
                    Items =
                    [
                        new ConversationOrderEvent
                        {
                            Id = ObjectId.GenerateNewId(),
                            BusinessId = Business.Id,
                            ConversationId = Conversation.Id,
                            ExternalOrderId = "ORDER-001",
                            Status = ConversationOrderEventStatus.Success,
                            CreatedAt = TestData.Now
                        }
                    ],
                    HasMore = true,
                    NextCursor = "order-next-cursor"
                });
            SearchQueryLogRepository.Setup(repository => repository.CursorPagingAsync(
                    It.IsAny<ObjectId>(),
                    It.IsAny<ObjectId>(),
                    It.IsAny<int>(),
                    It.IsAny<ObjectId?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CursorPage<SearchQueryLog>
                {
                    Items =
                    [
                        new SearchQueryLog
                        {
                            Id = ObjectId.GenerateNewId(),
                            BusinessId = Business.Id,
                            ConversationId = Conversation.Id,
                            MessageId = Message.Id,
                            UserRawQuery = "laptop",
                            CreatedAt = TestData.Now
                        }
                    ],
                    HasMore = true,
                    NextCursor = "search-log-next-cursor"
                });
            ChatHistoryHandler = new GetChatHistoryQueryHandler(
                CurrentUser.Object,
                CustomerRepository.Object,
                ConversationRepository.Object,
                MessageRepository.Object,
                ProductReferenceResolver.Object);
            OrderEventsHandler = new GetConversationOrderEventsQueryHandler(
                CurrentUser.Object,
                CustomerRepository.Object,
                ConversationRepository.Object,
                ConversationOrderEventRepository.Object);
            ProductComparisonsHandler = new GetConversationProductComparisonsQueryHandler(
                CurrentUser.Object,
                CustomerRepository.Object,
                ConversationRepository.Object,
                ProductComparationRepository.Object);
            SearchQueryLogsHandler = new GetConversationSearchQueryLogsQueryHandler(
                CurrentUser.Object,
                CustomerRepository.Object,
                ConversationRepository.Object,
                SearchQueryLogRepository.Object);
            DetailHandler = new GetCustomerConversationDetailQueryHandler(
                CurrentUser.Object,
                CustomerRepository.Object,
                ConversationRepository.Object,
                MessageRepository.Object,
                ProductReferenceResolver.Object);
        }

        public GetChatHistoryQuery ChatHistoryQuery() => new()
        {
            ConversationId = Conversation.Id.ToString(),
            ExternalCustomerId = Customer.CustomerExternalId,
            Limit = 20
        };

        public GetCustomerConversationDetailQuery DetailQuery(
            string? conversationId = null,
            string? lastCursor = null) => new()
        {
            ConversationId = conversationId ?? Conversation.Id.ToString(),
            CustomerExternalId = Customer.CustomerExternalId,
            Filter = new GetCustomerConversationDetailFilter { LastCursor = lastCursor, Limit = 20 }
        };
    }
}
