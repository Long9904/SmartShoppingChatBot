using System.Linq.Expressions;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Moq;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.GetBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.ResetBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.UpdateBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessRegistration;
using SmartShoppingChatBot.Application.Features.BusinessManagement.ConfirmBusinessRegistration;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_BusinessConfig
{
    [Fact]
    public async Task Get_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new BusinessConfigFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.GetHandler().Handle(new GetBusinessConfigQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WhenConfigExists_ReturnsItWithoutDatabaseWrite()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);

        var result = await fixture.GetHandler().Handle(new GetBusinessConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TopKDocument.Should().Be(3);
        fixture.BusinessRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Business>()), Times.Never);
    }

    [Fact]
    public async Task Get_WhenConfigMissing_CreatesDefaultConfigWithCurrentUserAudit()
    {
        var fixture = new BusinessConfigFixture();

        var result = await fixture.GetHandler().Handle(new GetBusinessConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.Config.Should().NotBeNull();
        fixture.Business.UpdatedAt.Should().Be(TestData.Now);
        fixture.Business.UpdatedBy!.Id.Should().Be(fixture.User.Id);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenConfigMissing_ReturnsNotFound()
    {
        var fixture = new BusinessConfigFixture();

        var result = await fixture.UpdateHandler().Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Redis.Verify(service => service.SetBusinessConfigAsync(
            It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenCurrentUserFails_ReturnsFailureWithoutWrite()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.UpdateHandler().Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.BusinessRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Business>()), Times.Never);
    }

    [Fact]
    public async Task Update_ValidRequest_TrimsStringsSavesAndRefreshesRedis()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        var command = fixture.UpdateCommand();

        var result = await fixture.UpdateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.Config!.SystemPrompt.Should().Be("Helpful assistant");
        fixture.Business.Config.FallBackMessage.Should().Be("Please retry");
        fixture.Redis.Verify(service => service.SetBusinessConfigAsync(
            fixture.Business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenRedisWriteThrows_ReturnsServerFailure()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        fixture.Redis.Setup(service => service.SetBusinessConfigAsync(
                It.IsAny<Business>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis unavailable"));

        var result = await fixture.UpdateHandler().Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Reset_ReplacesConfigWithDefaultsAndRefreshesRedis()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        fixture.Business.Config!.TopKDocument = 99;

        var result = await fixture.ResetHandler().Handle(new ResetBusinessConfigCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.Config!.TopKDocument.Should().Be(3);
        fixture.Business.Config.ModelTemperature.Should().Be(0.2);
        fixture.Redis.Verify(service => service.SetBusinessConfigAsync(
            fixture.Business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reset_WhenBusinessFails_ReturnsFailureWithoutWrite()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.ResetHandler().Handle(new ResetBusinessConfigCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.BusinessRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Business>()), Times.Never);
    }

    [Fact]
    public async Task Reset_WhenSaveThrows_ReturnsServerFailure()
    {
        var fixture = new BusinessConfigFixture(withConfig: true);
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        var result = await fixture.ResetHandler().Handle(new ResetBusinessConfigCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.Redis.Verify(service => service.SetBusinessConfigAsync(
            It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class BusinessConfigFixture
    {
        public Business Business { get; }
        public User User { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IRedisBusinessConfig> Redis { get; } = new();

        public BusinessConfigFixture(bool withConfig = false)
        {
            Business = TestData.Business(config: withConfig ? new BusinessConfig() : null);
            User = TestData.User(Business);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
        }

        public GetBusinessConfigQueryHandler GetHandler() => new(
            CurrentUser.Object, BusinessRepository.Object, UnitOfWork.Object, TestData.Mapper(),
            Mock.Of<ILogger<GetBusinessConfigQueryHandler>>(), new FixedTimeProvider(TestData.Now));

        public UpdateBusinessConfigCommandHandler UpdateHandler() => new(
            BusinessRepository.Object, CurrentUser.Object, UnitOfWork.Object, TestData.Mapper(),
            Mock.Of<ILogger<UpdateBusinessConfigCommandHandler>>(), Redis.Object,
            new FixedTimeProvider(TestData.Now));

        public ResetBusinessConfigCommandHandler ResetHandler() => new(
            BusinessRepository.Object, CurrentUser.Object, UnitOfWork.Object, TestData.Mapper(),
            Mock.Of<ILogger<ResetBusinessConfigCommandHandler>>(), Redis.Object,
            new FixedTimeProvider(TestData.Now));

        public UpdateBusinessConfigCommand UpdateCommand() => new()
        {
            ModelTemperature = 0.5,
            TopKDocument = 5,
            RerankingScore = 0.8,
            SystemPrompt = "  Helpful assistant  ",
            FallBackMessage = "  Please retry  ",
            MaxOutPutToken = 3000
        };
    }
}

public class UT_BusinessRegistration
{
    [Theory]
    [InlineData(UserStatus.ACTIVE, "already registered")]
    [InlineData(UserStatus.PENDING_APPROVAL, "waiting for admin approval")]
    [InlineData(UserStatus.PENDING_PROFILE_COMPLETION, "waiting for profile completion")]
    [InlineData(UserStatus.REJECTED, "was rejected")]
    [InlineData(UserStatus.DELETED, "already used")]
    public async Task Handle_WhenEmailAlreadyUsed_ReturnsConflict(UserStatus status, string messagePart)
    {
        var fixture = new RegistrationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(TestData.User(fixture.Business, status));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain(messagePart);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHotlineExists_ReturnsConflict()
    {
        var fixture = new RegistrationFixture();
        fixture.BusinessRepository.Setup(repository => repository.GetByHotlineAsync("0900000000"))
            .ReturnsAsync(fixture.Business);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("same hotline");
    }

    [Fact]
    public async Task Handle_ValidRequest_TrimsValuesCreatesOwnerAndCommits()
    {
        var fixture = new RegistrationFixture();
        Business? addedBusiness = null;
        User? addedOwner = null;
        fixture.BusinessRepository.Setup(repository => repository.AddAsync(It.IsAny<Business>()))
            .Callback<Business>(business => addedBusiness = business)
            .Returns(Task.CompletedTask);
        fixture.UserRepository.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .Callback<User>(user => addedOwner = user)
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        addedBusiness!.BusinessName.Should().Be("New Shop");
        addedBusiness.BusinessStatus.Should().Be(BusinessEnums.PENDING_APPROVAL);
        addedOwner!.Business.Id.Should().Be(addedBusiness.Id);
        addedOwner.Business.Role.Should().Be(RoleEnums.BUSINESS_OWNER);
        addedOwner.UserStatus.Should().Be(UserStatus.PENDING_APPROVAL);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new RegistrationFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class RegistrationFixture
    {
        public Business Business { get; } = TestData.Business();
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public BusinessRegistrationCommandHandler Handler { get; }

        public RegistrationFixture()
        {
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync((User?)null);
            BusinessRepository.Setup(repository => repository.GetByHotlineAsync(It.IsAny<string>()))
                .ReturnsAsync((Business?)null);
            Handler = new BusinessRegistrationCommandHandler(
                BusinessRepository.Object, UserRepository.Object, UnitOfWork.Object,
                TestData.Mapper(), Mock.Of<ILogger<BusinessRegistrationCommandHandler>>(),
                new FixedTimeProvider(TestData.Now));
        }

        public BusinessRegistrationCommand Command() => new()
        {
            BusinessName = "  New Shop  ",
            BusinessOwnerEmail = "  owner@newshop.com  ",
            BusinessOwnerName = "  Shop Owner  ",
            HotLine = "  0900000000  ",
            WebsiteUrl = "  https://newshop.example  ",
            AddressLine = "  Bangkok  "
        };
    }
}

public class UT_ConfirmBusiness
{
    [Fact]
    public async Task Handle_WhenBusinessMissing_ReturnsNotFound()
    {
        var fixture = new ConfirmFixture();
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync((Business?)null);

        var result = await fixture.Handler.Handle(fixture.Command(true), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenOwnerMissing_ReturnsNotFound()
    {
        var fixture = new ConfirmFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.Handler.Handle(fixture.Command(true), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenBasicPlanMissing_ReturnsNotFoundWithoutTransaction()
    {
        var fixture = new ConfirmFixture();
        fixture.PlanRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await fixture.Handler.Handle(fixture.Command(true), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Approve_ActivatesBusinessCreatesQuotaTokenSubscriptionAndEmailEvent()
    {
        var fixture = new ConfirmFixture();
        BusinessQuota? quota = null;
        Token? token = null;
        fixture.QuotaRepository.Setup(repository => repository.AddAsync(It.IsAny<BusinessQuota>()))
            .Callback<BusinessQuota>(value => quota = value).Returns(Task.CompletedTask);
        fixture.TokenRepository.Setup(repository => repository.AddAsync(It.IsAny<Token>()))
            .Callback<Token>(value => token = value).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.BusinessStatus.Should().Be(BusinessEnums.ACTIVE);
        fixture.Business.Config.Should().NotBeNull();
        fixture.Owner.UserStatus.Should().Be(UserStatus.PENDING_PROFILE_COMPLETION);
        quota!.TokenLimit.Should().Be(fixture.Plan.TokenLimit);
        quota.MaxProductAllowed.Should().Be(fixture.Plan.MaxProductAllowed);
        token!.ExpiresAt.Should().Be(TestData.Now.AddDays(2));
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.Is<BusinessRegistrationConfirmedEvent>(message =>
                message.TokenVerification != null && message.BusinessStatus == BusinessEnums.ACTIVE),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Reject_MarksBusinessAndOwnerRejectedAndPublishesEventWithoutVerificationUrl()
    {
        var fixture = new ConfirmFixture();

        var result = await fixture.Handler.Handle(fixture.Command(false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.BusinessStatus.Should().Be(BusinessEnums.REJECTED);
        fixture.Owner.UserStatus.Should().Be(UserStatus.REJECTED);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.Is<BusinessRegistrationConfirmedEvent>(message => message.TokenVerification == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndDoesNotPublishEvent()
    {
        var fixture = new ConfirmFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(true), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<BusinessRegistrationConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ConfirmFixture
    {
        public Business Business { get; } = TestData.Business(BusinessEnums.PENDING_APPROVAL);
        public User Owner { get; }
        public SubscriptionPlan Plan { get; } = new()
        {
            Id = ObjectId.GenerateNewId(), Name = "Basic", Duration = 30,
            TokenLimit = 10_000, MessageLimit = 100, MaxProductAllowed = 50,
            MaxDocumentAllowed = 10, Status = StatusEnums.Active
        };
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ITokenRepository> TokenRepository { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<ISubscriptionPlanRepository> PlanRepository { get; } = new();
        public Mock<ISubscriptionRepository> SubscriptionRepository { get; } = new();
        public Mock<ITokenService> TokenService { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public ConfirmBusinessCommandHandler Handler { get; }

        public ConfirmFixture()
        {
            Owner = TestData.User(Business, UserStatus.PENDING_APPROVAL);
            BusinessRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Business, bool>>>(),
                    It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
                .ReturnsAsync(Business);
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(Owner);
            PlanRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                    It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
                .ReturnsAsync(Plan);
            TokenService.Setup(service => service.CreateEmailVerificationToken()).Returns("raw-token");
            Handler = new ConfirmBusinessCommandHandler(
                BusinessRepository.Object, UserRepository.Object, TokenRepository.Object,
                QuotaRepository.Object, PlanRepository.Object, SubscriptionRepository.Object,
                TokenService.Object, UnitOfWork.Object, Publisher.Object,
                new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<ConfirmBusinessCommandHandler>>(),
                Options.Create(new EmailTokenSettings { ExpireDays = 2, UrlBase = "https://app.example/verify?token=" }));
        }

        public ConfirmBusinessCommand Command(bool approved) => new()
        {
            BusinessId = Business.Id,
            IsApproved = approved
        };
    }
}
