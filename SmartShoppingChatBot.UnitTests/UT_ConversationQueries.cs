using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;
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
        result.Data.Items.Should().ContainSingle().Which.Content.Should().Be("Hello");
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
        result.Data!.Items.Should().ContainSingle().Which.SenderType.Should().Be(SenderTypeEnum.ChatBot);
        result.Data.HasMore.Should().BeTrue();
        fixture.MessageRepository.Verify(repository => repository.MessageCursorPaging(
            fixture.Conversation.Id, 9, cursor, "laptop", SenderTypeEnum.ChatBot), Times.Once);
    }

    private sealed class ConversationQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public Customer Customer { get; }
        public Conversation Conversation { get; }
        public Message Message { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ICustomerRepository> CustomerRepository { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public Mock<IMessageRepository> MessageRepository { get; } = new();
        public GetChatHistoryQueryHandler ChatHistoryHandler { get; }
        public GetCustomerConversationDetailQueryHandler DetailHandler { get; }

        public ConversationQueryFixture()
        {
            Customer = TestData.Customer(Business);
            Conversation = TestData.Conversation(Business, Customer);
            Message = new Message
            {
                Id = ObjectId.GenerateNewId(),
                ConversationId = Conversation.Id,
                BusinessId = Business.Id,
                Content = "Hello",
                SenderType = SenderTypeEnum.ChatBot,
                ContentType = ContentTypeEnum.Text,
                Status = MessageStatus.Completed,
                CreatedAt = TestData.Now
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
            ChatHistoryHandler = new GetChatHistoryQueryHandler(
                CurrentUser.Object, CustomerRepository.Object, ConversationRepository.Object, MessageRepository.Object);
            DetailHandler = new GetCustomerConversationDetailQueryHandler(
                CurrentUser.Object, CustomerRepository.Object, ConversationRepository.Object, MessageRepository.Object);
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
