using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BOUpdateBusiness;
using SmartShoppingChatBot.Application.Features.BusinessMemberManagement.BusinessMemberRegistration;
using SmartShoppingChatBot.Application.Features.BusinessMemberManagement.DeleteBusinessMember;
using SmartShoppingChatBot.Application.Features.BusinessMemberManagement.UpdateBusinessMember;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_BusinessMemberRegistration
{
    [Fact]
    public async Task Handle_WhenCurrentUserFails_ReturnsOriginalFailureBeforeDuplicateCheck()
    {
        var fixture = new MemberRegistrationFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UserRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyBelongsToBusiness_ReturnsConflict()
    {
        var fixture = new MemberRegistrationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(TestData.User(fixture.Business, role: RoleEnums.CATALOG_TEAM));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesPendingCatalogMemberWithOwnerAudit()
    {
        var fixture = new MemberRegistrationFixture();
        User? employee = null;
        fixture.UserRepository.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .Callback<User>(value => employee = value)
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        employee!.Email.Should().Be("employee@example.com");
        employee.UserStatus.Should().Be(UserStatus.PENDING_PROFILE_COMPLETION);
        employee.Business.Role.Should().Be(RoleEnums.CATALOG_TEAM);
        employee.Business.Id.Should().Be(fixture.Business.Id);
        employee.CreatedBy!.Id.Should().Be(fixture.Owner.Id);
        employee.CreatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task Handle_ValidRequest_HashesVerificationTokenAndUsesConfiguredExpiry()
    {
        var fixture = new MemberRegistrationFixture();
        Token? storedToken = null;
        fixture.TokenRepository.Setup(repository => repository.AddAsync(It.IsAny<Token>()))
            .Callback<Token>(value => storedToken = value)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        storedToken.Should().NotBeNull();
        storedToken!.TokenValue.Should().NotBe("raw token+/=");
        storedToken.Type.Should().Be(TokenType.EMAIL_VERIFICATION);
        storedToken.ExpiresAt.Should().Be(TestData.Now.AddDays(2));
    }

    [Fact]
    public async Task Handle_ValidRequest_CommitsBeforePublishingEmailEvent()
    {
        var fixture = new MemberRegistrationFixture();

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<EmployeeRegistrationConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesEncodedVerificationUrlAndEmployeeDetails()
    {
        var fixture = new MemberRegistrationFixture();
        EmployeeRegistrationConfirmedEvent? published = null;
        fixture.Publisher.Setup(endpoint => endpoint.Publish(
                It.IsAny<EmployeeRegistrationConfirmedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<EmployeeRegistrationConfirmedEvent, CancellationToken>((value, _) => published = value)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        published!.BusinessName.Should().Be(fixture.Business.BusinessName);
        published.EmployeeName.Should().Be("Employee One");
        published.EmployeeEmail.Should().Be("employee@example.com");
        published.TokenVerification.Should().Contain("raw%20token%2B%2F%3D")
            .And.Contain("email=employee%40example.com");
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndDoesNotPublish()
    {
        var fixture = new MemberRegistrationFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<EmployeeRegistrationConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class MemberRegistrationFixture
    {
        public Business Business { get; } = TestData.Business();
        public User Owner { get; }
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public Mock<ITokenService> TokenService { get; } = new();
        public Mock<ITokenRepository> TokenRepository { get; } = new();
        public MemberRegistrationCommandHandler Handler { get; }

        public MemberRegistrationFixture()
        {
            Owner = TestData.User(Business);
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(Owner));
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync((User?)null);
            TokenService.Setup(service => service.CreateEmailVerificationToken()).Returns("raw token+/=");
            Handler = new MemberRegistrationCommandHandler(
                UserRepository.Object, UnitOfWork.Object, Mock.Of<IMapper>(),
                Mock.Of<ILogger<MemberRegistrationCommandHandler>>(), new FixedTimeProvider(TestData.Now),
                CurrentUser.Object, Publisher.Object, TokenService.Object, TokenRepository.Object,
                Options.Create(new EmailTokenSettings
                {
                    ExpireDays = 2,
                    UrlBase = "https://shop.example/verify?token="
                }));
        }

        public MemberRegistrationCommand Command() => new()
        {
            Email = "employee@example.com",
            FullName = "Employee One"
        };
    }
}

public class UT_BusinessMemberUpdate
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsOriginalFailureBeforeLookup()
    {
        var fixture = new MemberMutationFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UserRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMemberMissing_ReturnsNotFound()
    {
        var fixture = new MemberMutationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserFails_ReturnsFailureWithoutTransaction()
    {
        var fixture = new MemberMutationFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_TrimsFieldsAndSetsAuditData()
    {
        var fixture = new MemberMutationFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Member.FullName.Should().Be("Updated Member");
        fixture.Member.Email.Should().Be("updated@example.com");
        fixture.Member.PhoneNumber.Should().Be("0909");
        fixture.Member.UpdatedAt.Should().Be(TestData.Now);
        fixture.Member.UpdatedBy!.Id.Should().Be(fixture.Owner.Id);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsCommitsAndMapsResponse()
    {
        var fixture = new MemberMutationFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Member.Id.ToString());
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.Member), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new MemberMutationFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class UT_BusinessMemberDelete
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsOriginalFailureBeforeLookup()
    {
        var fixture = new MemberMutationFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.UserRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMemberMissing_ReturnsNotFound()
    {
        var fixture = new MemberMutationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserFails_DoesNotDeleteMember()
    {
        var fixture = new MemberMutationFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Member.UserStatus.Should().NotBe(UserStatus.DELETED);
    }

    [Fact]
    public async Task Handle_ValidRequest_SoftDeletesAndSetsAuditFields()
    {
        var fixture = new MemberMutationFixture();

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Member.UserStatus.Should().Be(UserStatus.DELETED);
        fixture.Member.DeletedAt.Should().Be(TestData.Now);
        fixture.Member.UpdatedAt.Should().Be(TestData.Now);
        fixture.Member.UpdatedBy!.Id.Should().Be(fixture.Owner.Id);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsCommitsAndMapsDeletedMember()
    {
        var fixture = new MemberMutationFixture();

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Member.Id.ToString());
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.Member), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUpdateThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new MemberMutationFixture();
        fixture.UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class UT_BusinessUpdate
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsOriginalFailureBeforeHotlineLookup()
    {
        var fixture = new BusinessUpdateFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.BusinessRepository.Verify(repository => repository.GetByHotlineAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHotlineBelongsToAnotherBusiness_ReturnsConflict()
    {
        var fixture = new BusinessUpdateFixture();
        fixture.BusinessRepository.Setup(repository => repository.GetByHotlineAsync("0999"))
            .ReturnsAsync(TestData.Business());

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserFails_ReturnsFailureBeforeLoadingBusinessUsers()
    {
        var fixture = new BusinessUpdateFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UserRepository.Verify(repository => repository.FindAllAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHotlineBelongsToCurrentBusiness_AllowsUpdate()
    {
        var fixture = new BusinessUpdateFixture();
        fixture.BusinessRepository.Setup(repository => repository.GetByHotlineAsync("0999"))
            .ReturnsAsync(fixture.Business);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_TrimsBusinessAndPropagatesNameToActiveUsers()
    {
        var fixture = new BusinessUpdateFixture();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Business.BusinessName.Should().Be("New Shop");
        fixture.Business.HotLine.Should().Be("0999");
        fixture.Business.WebsiteUrl.Should().Be("https://new.example");
        fixture.Business.AddressLine.Should().Be("New address");
        fixture.Business.UpdatedBy!.Id.Should().Be(fixture.Owner.Id);
        fixture.BusinessUsers.Should().OnlyContain(user =>
            user.Business.BusinessName == "New Shop" && user.UpdatedAt == TestData.Now);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new BusinessUpdateFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class BusinessUpdateFixture
    {
        public Business Business { get; } = TestData.Business();
        public User Owner { get; }
        public List<User> BusinessUsers { get; }
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public UpdateBusinessCommandHandler Handler { get; }

        public BusinessUpdateFixture()
        {
            Owner = TestData.User(Business);
            BusinessUsers = [Owner, TestData.User(Business, role: RoleEnums.CATALOG_TEAM)];
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(Owner));
            BusinessRepository.Setup(repository => repository.GetByHotlineAsync(It.IsAny<string>()))
                .ReturnsAsync((Business?)null);
            UserRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(BusinessUsers);
            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<BusinessResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var business = (Business)source;
                    return new BusinessResponse { Id = business.Id.ToString(), BusinessName = business.BusinessName };
                });
            Handler = new UpdateBusinessCommandHandler(
                BusinessRepository.Object, UserRepository.Object, CurrentUser.Object, UnitOfWork.Object,
                mapper.Object, Mock.Of<ILogger<UpdateBusinessCommandHandler>>(), new FixedTimeProvider(TestData.Now));
        }

        public UpdateBusinessCommand Command() => new()
        {
            BusinessName = "  New Shop  ",
            HotLine = " 0999 ",
            WebsiteUrl = " https://new.example ",
            AddressLine = " New address "
        };
    }
}

internal sealed class MemberMutationFixture
{
    public Business Business { get; } = TestData.Business();
    public User Owner { get; }
    public User Member { get; }
    public Mock<IUserRepository> UserRepository { get; } = new();
    public Mock<ICurrentUserService> CurrentUser { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public UpdateBusinessMemberCommandHandler UpdateHandler { get; }
    public DeleteBusinessMemberCommandHandler DeleteHandler { get; }

    public MemberMutationFixture()
    {
        Owner = TestData.User(Business);
        Member = TestData.User(Business, role: RoleEnums.CATALOG_TEAM);
        Member.Email = "member@example.com";
        CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
        CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(Owner));
        UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(Member);
        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.Map<ProfileResponse>(It.IsAny<object>()))
            .Returns((object source) =>
            {
                var user = (User)source;
                return new ProfileResponse { Id = user.Id.ToString(), Email = user.Email, FullName = user.FullName };
            });
        UpdateHandler = new UpdateBusinessMemberCommandHandler(
            UserRepository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
            Mock.Of<ILogger<UpdateBusinessMemberCommandHandler>>(), new FixedTimeProvider(TestData.Now));
        DeleteHandler = new DeleteBusinessMemberCommandHandler(
            UserRepository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
            Mock.Of<ILogger<DeleteBusinessMemberCommandHandler>>(), new FixedTimeProvider(TestData.Now));
    }

    public UpdateBusinessMemberCommand UpdateCommand() => new()
    {
        MemberId = Member.Id,
        FullName = " Updated Member ",
        Email = " updated@example.com ",
        PhoneNumber = " 0909 ",
        DateOfBirth = new DateTime(2000, 1, 2),
        Gender = 1
    };

    public DeleteBusinessMemberCommand DeleteCommand() => new() { MemberId = Member.Id };
}
