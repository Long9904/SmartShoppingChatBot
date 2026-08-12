using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_SendMessage
{
    [Fact]
    public async Task Handle_WhenBusinessAuthenticationFails_ReturnsOriginalFailureAndStops()
    {
        var fixture = new SendMessageFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid token", messageCode: "AUTH-401"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.MessageCode.Should().Be("AUTH-401");
        fixture.CustomerRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Customer, bool>>>(),
            It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuotaDoesNotExist_ReturnsNotFoundWithoutStartingTransaction()
    {
        var fixture = new SendMessageFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Business quota not found");
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMessageUsageIsOverLimit_ReturnsLimitFailure()
    {
        var fixture = new SendMessageFixture();
        fixture.Quota.MessageLimit = 10;
        fixture.Quota.UsedMessages = 11;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        fixture.Kernel.Verify(service => service.ChatAsync(It.IsAny<KernelChatRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenUsageIsOverLimit_ReturnsTooManyRequests()
    {
        var fixture = new SendMessageFixture();
        fixture.Quota.TokenLimit = 100;
        fixture.Quota.UsedTokens = 101;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(429);
        fixture.Kernel.Verify(service => service.ChatAsync(It.IsAny<KernelChatRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUsageEqualsBothLimits_AllowsRequestAndChargesUsage()
    {
        var fixture = new SendMessageFixture();
        fixture.Quota.MessageLimit = fixture.Quota.UsedMessages = 10;
        fixture.Quota.TokenLimit = fixture.Quota.UsedTokens = 100;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Quota.UsedMessages.Should().Be(11);
        fixture.Quota.UsedTokens.Should().Be(140); // 10 input + 5 output * 6
    }

    [Fact]
    public async Task Handle_NewConversation_CreatesConversationMessagesUsageAndCache()
    {
        var fixture = new SendMessageFixture();
        Conversation? savedConversation = null;
        var messages = new List<Message>();
        fixture.ConversationRepository.Setup(repository => repository.AddAsync(It.IsAny<Conversation>()))
            .Callback<Conversation>(conversation => savedConversation = conversation)
            .Returns(Task.CompletedTask);
        fixture.MessageRepository.Setup(repository => repository.AddAsync(It.IsAny<Message>()))
            .Callback<Message>(message => messages.Add(message))
            .Returns(Task.CompletedTask);
        var command = fixture.Command(message: new string('A', 35));

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ConversationId.Should().Be(savedConversation!.Id.ToString());
        savedConversation.Title.Should().Be(new string('A', 30) + "...");
        messages.Should().HaveCount(2);
        messages[0].SenderType.Should().Be(Domain.Enums.SenderTypeEnum.Customer);
        messages[1].SenderType.Should().Be(Domain.Enums.SenderTypeEnum.ChatBot);
        fixture.UsageRepository.Verify(repository => repository.AddAsync(It.Is<UsageQuotaLog>(log =>
            log.MessageUsed == 1 && log.BillableTokens == 40)), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.ContextService.Verify(service => service.SaveConversationCacheAsync(
            It.Is<ConversationContextCache>(cache => cache.RecentTurns.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingConversation_UpdatesItAndDoesNotCreateAnotherConversation()
    {
        var fixture = new SendMessageFixture();
        var conversation = TestData.Conversation(fixture.Business, fixture.Customer);
        fixture.ConversationRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Conversation, bool>>>(),
                It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
            .ReturnsAsync(conversation);

        var result = await fixture.Handler.Handle(
            fixture.Command(conversationId: conversation.Id.ToString()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.ConversationRepository.Verify(repository => repository.UpdateAsync(conversation), Times.Once);
        fixture.ConversationRepository.Verify(repository => repository.AddAsync(It.IsAny<Conversation>()), Times.Never);
        conversation.Summary.Should().Be("Conversation summary");
        conversation.LastMessageAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task Handle_WhenExistingConversationIsMissing_ReturnsNotFound()
    {
        var fixture = new SendMessageFixture();
        fixture.ConversationRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Conversation, bool>>>(),
                It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
            .ReturnsAsync((Conversation?)null);

        var result = await fixture.Handler.Handle(
            fixture.Command(conversationId: ObjectId.GenerateNewId().ToString()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Conversation not found");
        fixture.MessageRepository.Verify(repository => repository.AddAsync(It.IsAny<Message>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenKernelReturnsFailure_RollsBackAndReturnsFriendlySuccessfulFallback()
    {
        var fixture = new SendMessageFixture();
        fixture.Kernel.Setup(service => service.ChatAsync(It.IsAny<KernelChatRequest>()))
            .ReturnsAsync(Result<KernelChatResult>.Failure(500, "AI unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().BeNull();
        result.Message.Should().Contain("chưa thể trả lời");
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.QuotaRepository.Verify(repository => repository.UpdateAsync(It.IsAny<BusinessQuota>()), Times.Never);
        fixture.ContextService.Verify(service => service.SaveConversationCacheAsync(
            It.IsAny<ConversationContextCache>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDependencyThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new SendMessageFixture();
        fixture.ContextService.Setup(service => service.GetOrLoadAsyncConversationCache(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.MessageCode.Should().Be("MG_SERVER_500");
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SelectedProducts_UsesCurrentProductsOverHistoryAndRemovesDuplicates()
    {
        var fixture = new SendMessageFixture();
        var historyProductId = ObjectId.GenerateNewId().ToString();
        var currentProductId = ObjectId.GenerateNewId().ToString();
        const string historyExternalProductId = "external-history-product";
        const string currentExternalProductId = "external-current-product";
        fixture.Context.RecentTurns.Add(new CachedConversationTurn
        {
            TurnId = "old-turn",
            UserMessage = new CachedUserMessage { MessageId = "u1", Content = "old" },
            AssistantMessage = new CachedAssistantMessage
            {
                MessageId = "a1",
                Content = "old answer",
                ProductReferences =
                [
                    new CachedProductReference
                    {
                        ProductId = historyProductId,
                        ExternalProductId = historyExternalProductId,
                        DisplayName = "Historical product",
                        DisplayOrder = 9
                    },
                    new CachedProductReference
                    {
                        ProductId = currentProductId,
                        ExternalProductId = "stale-external-product",
                        DisplayName = "Stale name",
                        DisplayOrder = 8
                    }
                ]
            }
        });
        fixture.ProductCollector.Setup(collector => collector.GetProducts())
            .Returns([TestData.ProductResponse(currentProductId, "Fresh name", currentExternalProductId)]);
        fixture.Kernel.Setup(service => service.ChatAsync(It.IsAny<KernelChatRequest>()))
            .ReturnsAsync(Result<KernelChatResult>.Success(TestData.KernelResult(
                selectedProductIds: [historyProductId, currentProductId, currentProductId.ToUpperInvariant(), "missing"])));
        var messages = new List<Message>();
        fixture.MessageRepository.Setup(repository => repository.AddAsync(It.IsAny<Message>()))
            .Callback<Message>(message => messages.Add(message))
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var aiMessage = messages.Single(message => message.SenderType == Domain.Enums.SenderTypeEnum.ChatBot);
        aiMessage.CacheProductReference.Should().HaveCount(2);
        aiMessage.CacheProductReference[0].ProductId.Should().Be(historyProductId);
        aiMessage.CacheProductReference[0].ExternalProductId.Should().Be(historyExternalProductId);
        aiMessage.CacheProductReference[1].ProductId.Should().Be(currentProductId);
        aiMessage.CacheProductReference[1].ExternalProductId.Should().Be(currentExternalProductId);
        aiMessage.CacheProductReference[0].DisplayName.Should().Be("Historical product");
        aiMessage.CacheProductReference[1].DisplayName.Should().Be("Fresh name");
        result.Data!.ProductReferences.Should().ContainSingle()
            .Which.ExternalId.Should().Be(currentExternalProductId);
        result.Data.ProductReferences[0].ProductId.Should().Be(currentProductId);
    }

    [Fact]
    public async Task Handle_WhenRecentTurnsExceedLimit_KeepsOnlyNewestTurns()
    {
        var fixture = new SendMessageFixture(recentTurnLimit: 2);
        fixture.Context.RecentTurns.AddRange(
        [
            SendMessageFixture.Turn("turn-1"),
            SendMessageFixture.Turn("turn-2"),
            SendMessageFixture.Turn("turn-3")
        ]);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Context.RecentTurns.Should().HaveCount(2);
        fixture.Context.RecentTurns[0].TurnId.Should().Be("turn-3");
        fixture.Context.RecentTurns[1].TurnId.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class SendMessageFixture
    {
        public Business Business { get; } = TestData.Business();
        public Customer Customer { get; }
        public BusinessQuota Quota { get; }
        public ConversationContextCache Context { get; } = new();
        public Mock<ICustomerRepository> CustomerRepository { get; } = new();
        public Mock<IMessageRepository> MessageRepository { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IUsageQuotaLogRepository> UsageRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IKernelChatService> Kernel { get; } = new();
        public Mock<IProductReferenceCollector> ProductCollector { get; } = new();
        public Mock<IProductReferenceResolver> ProductReferenceResolver { get; } = new();
        public Mock<IConversationContextService> ContextService { get; } = new();
        public SendMessageCommandHandler Handler { get; }

        public SendMessageFixture(int recentTurnLimit = 8)
        {
            Customer = TestData.Customer(Business);
            Quota = TestData.Quota(Business);
            CurrentUser.Setup(service => service.GetBusiness())
                .ReturnsAsync(Result<Business>.Success(Business));
            CustomerRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Customer, bool>>>(),
                    It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
                .ReturnsAsync(Customer);
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id))
                .ReturnsAsync(Quota);
            ContextService.Setup(service => service.GetOrLoadAsyncConversationCache(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Context);
            Kernel.Setup(service => service.ChatAsync(It.IsAny<KernelChatRequest>()))
                .ReturnsAsync(Result<KernelChatResult>.Success(TestData.KernelResult()));
            ProductCollector.Setup(collector => collector.GetProducts())
                .Returns([]);
            ProductReferenceResolver.Setup(resolver => resolver.ResolveAsync(
                    It.IsAny<ObjectId>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IEnumerable<ProductResponseV2>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ObjectId businessId,
                    IEnumerable<string> productIds,
                    IEnumerable<ProductResponseV2>? knownProducts,
                    CancellationToken cancellationToken) =>
                {
                    var requestedIds = productIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    IReadOnlyDictionary<string, ProductResponseV2> productsById = (knownProducts ?? [])
                        .Where(product => requestedIds.Contains(product.ProductId))
                        .ToDictionary(
                            product => product.ProductId,
                            StringComparer.OrdinalIgnoreCase);

                    return productsById;
                });
            ProductReferenceResolver.Setup(resolver => resolver.GetInOrder(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IReadOnlyDictionary<string, ProductResponseV2>>()))
                .Returns((
                    IEnumerable<string> productIds,
                    IReadOnlyDictionary<string, ProductResponseV2> productById) => productIds
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(productById.ContainsKey)
                    .Select(productId => productById[productId])
                    .ToList());

            Handler = new SendMessageCommandHandler(
                CustomerRepository.Object,
                MessageRepository.Object,
                ConversationRepository.Object,
                QuotaRepository.Object,
                UsageRepository.Object,
                UnitOfWork.Object,
                new FixedTimeProvider(TestData.Now),
                Options.Create(new RedisOptions { RecentTurnLimit = recentTurnLimit }),
                Mock.Of<ILogger<SendMessageCommandHandler>>(),
                CurrentUser.Object,
                ProductCollector.Object,
                ProductReferenceResolver.Object,
                ContextService.Object,
                Kernel.Object);
        }

        public SendMessageCommand Command(
            string message = "Which laptop should I buy?",
            string conversationId = "") => new()
        {
            Message = message,
            ExternalCustomerId = Customer.CustomerExternalId,
            ConversationId = conversationId
        };

        public static CachedConversationTurn Turn(string id) => new()
        {
            TurnId = id,
            UserMessage = new CachedUserMessage { MessageId = id + "-u", Content = "question" },
            AssistantMessage = new CachedAssistantMessage { MessageId = id + "-a", Content = "answer" }
        };
    }
}
