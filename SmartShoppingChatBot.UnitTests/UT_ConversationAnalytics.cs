using System.Linq.Expressions;
using FluentAssertions;
using MassTransit;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Consumers;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ConversationManagement.ReceiveConversationOrderEvent;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Infrastructure.Services;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ConversationAnalytics
{
    [Fact]
    public async Task SaveSearchQueryLogConsumer_NewEvent_PersistsProductScoresAndHighestHitRate()
    {
        var repository = new Mock<ISearchQueryLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        SearchQueryLog? saved = null;
        repository.Setup(item => item.FindAsync(
                It.IsAny<Expression<Func<SearchQueryLog, bool>>>(),
                It.IsAny<Func<IQueryable<SearchQueryLog>, IQueryable<SearchQueryLog>>?>()))
            .ReturnsAsync((SearchQueryLog?)null);
        repository.Setup(item => item.AddAsync(It.IsAny<SearchQueryLog>()))
            .Callback<SearchQueryLog>(item => saved = item)
            .Returns(Task.CompletedTask);
        var message = new SearchQueryLogRequestedEvent
        {
            BusinessId = ObjectId.GenerateNewId().ToString(),
            ConversationId = ObjectId.GenerateNewId().ToString(),
            MessageId = ObjectId.GenerateNewId().ToString(),
            UserRawQuery = "laptop",
            TrendKeywords = ["laptop", "laptop gaming"],
            CreatedAt = TestData.Now,
            RetrievalLatency = 50,
            TopKResult = 2,
            ProductResults =
            [
                new SearchQueryProductSnapshot
                {
                    ProductId = ObjectId.GenerateNewId().ToString(),
                    ProductName = "Laptop",
                    Price = 1000,
                    Category = "Computer",
                    ProductScore = 0.92
                },
                new SearchQueryProductSnapshot
                {
                    ProductId = ObjectId.GenerateNewId().ToString(),
                    ProductName = "Laptop 2",
                    Price = 900,
                    Category = "Computer",
                    ProductScore = 0.75
                }
            ]
        };
        var context = new Mock<ConsumeContext<SearchQueryLogRequestedEvent>>();
        context.SetupGet(item => item.Message).Returns(message);
        context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

        await new SaveSearchQueryLogConsumer(repository.Object, unitOfWork.Object)
            .Consume(context.Object);

        saved.Should().NotBeNull();
        saved!.ZeroResult.Should().BeFalse();
        saved.TrendKeywords.Should().Equal("laptop", "laptop gaming");
        saved.HitRateScore.Should().Be(0.92);
        saved.ProductResults.Should().HaveCount(2);
        saved.ProductResults[0].ProductScore.Should().Be(0.92);
        saved.ProductResults[1].ProductScore.Should().Be(0.75);
        unitOfWork.Verify(item => item.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public void ProductReferenceCollector_DuplicateProduct_KeepsLatestDataAndHighestScore()
    {
        var collector = new ProductReferenceCollector();
        var productId = ObjectId.GenerateNewId().ToString();

        collector.AddRange(
        [
            new ProductResponseV3
            {
                ProductId = productId,
                Name = "Old name",
                Score = 0.91
            }
        ]);
        collector.AddRange(
        [
            new ProductResponseV2
            {
                ProductId = productId.ToUpperInvariant(),
                Name = "Fresh name"
            }
        ]);

        var product = collector.GetProducts().Should().ContainSingle().Which;
        product.Name.Should().Be("Fresh name");
        product.Score.Should().Be(0.91);
    }

    [Fact]
    public async Task SaveProductComparisonConsumer_DuplicateMessage_IsIdempotent()
    {
        var repository = new Mock<IProductComparationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var businessId = ObjectId.GenerateNewId();
        var messageId = ObjectId.GenerateNewId();
        repository.Setup(item => item.FindAsync(
                It.IsAny<Expression<Func<ProductComparation, bool>>>(),
                It.IsAny<Func<IQueryable<ProductComparation>, IQueryable<ProductComparation>>?>()))
            .ReturnsAsync(new ProductComparation
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = businessId,
                MessageId = messageId
            });
        var context = new Mock<ConsumeContext<ProductComparisonDetectedEvent>>();
        context.SetupGet(item => item.Message).Returns(new ProductComparisonDetectedEvent
        {
            BusinessId = businessId.ToString(),
            ConversationId = ObjectId.GenerateNewId().ToString(),
            MessageId = messageId.ToString(),
            CustomerId = ObjectId.GenerateNewId().ToString(),
            CreatedAt = TestData.Now
        });

        await new SaveProductComparisonConsumer(repository.Object, unitOfWork.Object)
            .Consume(context.Object);

        repository.Verify(item => item.AddAsync(It.IsAny<ProductComparation>()), Times.Never);
        unitOfWork.Verify(item =>
            item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ReceiveOrderEventValidator_InvalidPayload_ReturnsValidationErrors()
    {
        var validator = new ReceiveConversationOrderEventCommandValidator();
        var command = new ReceiveConversationOrderEventCommand
        {
            ConversationId = "invalid",
            Event = new ConversationOrderEventRequest
            {
                Status = ConversationOrderEventStatus.None,
                Amount = -1,
                Products = [new ProductOrderSnapshotRequest { ExternalProductId = "", Quantity = 0 }]
            }
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain([
            "ConversationId",
            "Event.Status",
            "Event.Amount",
            "Event.Products[0].ExternalProductId",
            "Event.Products[0].Quantity"
        ]);
    }

    [Fact]
    public async Task ReceiveOrderEvent_ConversationFromAnotherBusiness_ReturnsNotFound()
    {
        var fixture = new OrderEventFixture();
        fixture.ConversationRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Conversation, bool>>>(),
                It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
            .ReturnsAsync((Conversation?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.OrderEventRepository.Verify(repository =>
            repository.AddAsync(It.IsAny<ConversationOrderEvent>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveOrderEvent_ValidPayload_SavesTenantScopedSnapshot()
    {
        var fixture = new OrderEventFixture();
        ConversationOrderEvent? saved = null;
        fixture.OrderEventRepository.Setup(repository => repository.AddAsync(It.IsAny<ConversationOrderEvent>()))
            .Callback<ConversationOrderEvent>(item => saved = item)
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        saved!.BusinessId.Should().Be(fixture.Business.Id);
        saved.ConversationId.Should().Be(fixture.Conversation.Id);
        saved.ExternalOrderId.Should().Be("ORDER-001");
        saved.ProductOrderSnapshotItems.Should().ContainSingle()
            .Which.ExternalProductId.Should().Be("SKU-001");
        fixture.UnitOfWork.Verify(unit =>
            unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveOrderEvent_RepeatedOrderStatus_CreatesSeparateEvents()
    {
        var fixture = new OrderEventFixture();
        var saved = new List<ConversationOrderEvent>();
        fixture.OrderEventRepository.Setup(repository => repository.AddAsync(It.IsAny<ConversationOrderEvent>()))
            .Callback<ConversationOrderEvent>(saved.Add)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);
        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        saved.Should().HaveCount(2);
        saved.Select(item => item.Id).Should().OnlyHaveUniqueItems();
    }

    private sealed class OrderEventFixture
    {
        public Business Business { get; } = TestData.Business();
        public Customer Customer { get; }
        public Conversation Conversation { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public Mock<IConversationOrderEventRepository> OrderEventRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public ReceiveConversationOrderEventCommandHandler Handler { get; }

        public OrderEventFixture()
        {
            Customer = TestData.Customer(Business);
            Conversation = TestData.Conversation(Business, Customer);
            CurrentUser.Setup(service => service.GetBusiness())
                .ReturnsAsync(Result<Business>.Success(Business));
            ConversationRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Conversation, bool>>>(),
                    It.IsAny<Func<IQueryable<Conversation>, IQueryable<Conversation>>?>()))
                .ReturnsAsync(Conversation);
            Handler = new ReceiveConversationOrderEventCommandHandler(
                CurrentUser.Object,
                ConversationRepository.Object,
                OrderEventRepository.Object,
                UnitOfWork.Object,
                new FixedTimeProvider(TestData.Now));
        }

        public ReceiveConversationOrderEventCommand Command() => new()
        {
            ConversationId = Conversation.Id.ToString(),
            Event = new ConversationOrderEventRequest
            {
                ExternalOrderId = " ORDER-001 ",
                Status = ConversationOrderEventStatus.Success,
                Amount = 1000,
                Products =
                [
                    new ProductOrderSnapshotRequest
                    {
                        ExternalProductId = " SKU-001 ",
                        ProductName = " Laptop ",
                        Price = 1000,
                        Quantity = 1
                    }
                ]
            }
        };
    }
}
