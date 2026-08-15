using System.Linq.Expressions;
using System.Security.Authentication;
using System.Security.Claims;
using System.IO;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using SmartShoppingChatBot.Application.Commons.Behaviors;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Auth.Login;
using SmartShoppingChatBot.Application.Features.VerifyAccount;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Infrastructure.Services;

namespace SmartShoppingChatBot.UnitTests;

public class UT_Login
{
    [Fact]
    public async Task Handle_WhenEmailDoesNotExist_ReturnsUnauthorized()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);
        var handler = new LoginCommandHandler(repository.Object, Mock.Of<ITokenService>(), Mock.Of<IPasswordService>());

        var result = await handler.Handle(new LoginCommand { Email = "none@example.com", Password = "secret" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsIncorrect_ReturnsUnauthorizedWithoutCreatingToken()
    {
        var fixture = new LoginFixture();
        fixture.Password.Setup(service => service.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Token.Verify(service => service.CreateAccessToken(It.IsAny<AccessTokenPayload>()), Times.Never);
    }

    [Theory]
    [InlineData(UserStatus.PENDING_APPROVAL)]
    [InlineData(UserStatus.PENDING_PROFILE_COMPLETION)]
    [InlineData(UserStatus.REJECTED)]
    [InlineData(UserStatus.DELETED)]
    public async Task Handle_WhenUserIsNotActive_ReturnsForbidden(UserStatus status)
    {
        var fixture = new LoginFixture(status);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        fixture.Token.Verify(service => service.CreateAccessToken(It.IsAny<AccessTokenPayload>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveUser_ReturnsAccessTokenWithAccountFlags()
    {
        var fixture = new LoginFixture();
        AccessTokenPayload? payload = null;
        fixture.Token.Setup(service => service.CreateAccessToken(It.IsAny<AccessTokenPayload>()))
            .Callback<AccessTokenPayload>(value => payload = value)
            .Returns("jwt-token");

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("jwt-token");
        result.Data.IsEmailVerified.Should().BeTrue();
        payload!.UserId.Should().Be(fixture.User.Id.ToString());
        payload.BusinessId.Should().Be(fixture.Business.Id.ToString());
        payload.Role.Should().Be(RoleEnums.BUSINESS_OWNER);
    }

    private sealed class LoginFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Mock<IUserRepository> Repository { get; } = new();
        public Mock<ITokenService> Token { get; } = new();
        public Mock<IPasswordService> Password { get; } = new();
        public LoginCommandHandler Handler { get; }
        public LoginCommand Command { get; } = new() { Email = " OWNER@EXAMPLE.COM ", Password = "secret" };

        public LoginFixture(UserStatus status = UserStatus.ACTIVE)
        {
            User = TestData.User(Business, status);
            Repository.Setup(repo => repo.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(User);
            Password.Setup(service => service.VerifyPassword("secret", User.PasswordHash)).Returns(true);
            Token.Setup(service => service.CreateAccessToken(It.IsAny<AccessTokenPayload>())).Returns("jwt-token");
            Handler = new LoginCommandHandler(Repository.Object, Token.Object, Password.Object);
        }
    }
}

public class UT_VerifyAccount
{
    [Fact]
    public async Task UT_AUTH_01_Handle_ValidToken_DoesNotWriteVerificationTokenHashToConsole()
    {
        var fixture = new VerifyFixture();
        var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        output.ToString().Should().BeEmpty("verification-token hashes must not be exposed in console output");
    }

    [Fact]
    public async Task Handle_WhenTokenDoesNotExist_ReturnsBadRequest()
    {
        var fixture = new VerifyFixture();
        fixture.TokenRepository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<Token, bool>>>(),
                It.IsAny<Func<IQueryable<Token>, IQueryable<Token>>?>()))
            .ReturnsAsync((Token?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Invalid token.");
    }

    [Fact]
    public async Task Handle_WhenTokenExpired_ReturnsBadRequest()
    {
        var fixture = new VerifyFixture();
        fixture.Token.ExpiresAt = TestData.Now.AddSeconds(-1);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Token has expired.");
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsBadRequest()
    {
        var fixture = new VerifyFixture();
        fixture.UserRepository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.Message.Should().Be("User not found.");
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyVerified_ReturnsBadRequest()
    {
        var fixture = new VerifyFixture();
        fixture.User.IsEmailVerified = true;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.Message.Should().Be("Email is already verified.");
    }

    [Fact]
    public async Task Handle_ValidToken_CompletesProfileConsumesTokenAndCommits()
    {
        var fixture = new VerifyFixture();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        fixture.User.IsEmailVerified.Should().BeTrue();
        fixture.User.IsProfileCompleted.Should().BeTrue();
        fixture.User.UserStatus.Should().Be(UserStatus.ACTIVE);
        fixture.User.PasswordHash.Should().Be("new-hash");
        fixture.Token.TokenValue.Should().BeNull();
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndReturnsServerFailure()
    {
        var fixture = new VerifyFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class VerifyFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Token Token { get; }
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ITokenRepository> TokenRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IPasswordService> Password { get; } = new();
        public VerifyAccountCommandHandler Handler { get; }

        public VerifyFixture()
        {
            User = TestData.User(Business, UserStatus.PENDING_PROFILE_COMPLETION);
            User.IsEmailVerified = false;
            User.IsProfileCompleted = false;
            User.EmailVerifiedAt = null;
            Token = new Token
            {
                Id = ObjectId.GenerateNewId(),
                UserId = User.Id,
                TokenValue = "stored-hash",
                Type = TokenType.EMAIL_VERIFICATION,
                CreatedAt = TestData.Now,
                ExpiresAt = TestData.Now.AddDays(1)
            };
            TokenRepository.Setup(repo => repo.FindAsync(
                    It.IsAny<Expression<Func<Token, bool>>>(),
                    It.IsAny<Func<IQueryable<Token>, IQueryable<Token>>?>()))
                .ReturnsAsync(Token);
            UserRepository.Setup(repo => repo.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(User);
            Password.Setup(service => service.HashPassword("new-password")).Returns("new-hash");
            Handler = new VerifyAccountCommandHandler(UnitOfWork.Object, TokenRepository.Object,
                UserRepository.Object, Password.Object, new FixedTimeProvider(TestData.Now),
                Mock.Of<ILogger<VerifyAccountCommandHandler>>());
        }

        public VerifyAccountCommand Command() => new()
        {
            Token = "raw-token",
            Password = "new-password",
            ConfirmPassword = "new-password",
            PhoneNumber = "0900000000",
            DateOfBirth = new DateTime(2000, 1, 1),
            Gender = 1
        };
    }
}

public class UT_CurrentUser
{
    [Fact]
    public async Task GetBusiness_WhenClaimMissing_ReturnsUnauthorized()
    {
        var handler = new CurrentUserService(TestData.HttpContext(), Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var result = await handler.GetBusiness();

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetBusiness_WhenClaimIsInvalid_ReturnsUnauthorized()
    {
        var context = TestData.HttpContext("Bearer", new Claim("business", "not-object-id"));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var result = await handler.GetBusiness();

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetBusiness_WhenRepositoryReturnsNull_ReturnsNotFound()
    {
        var businessId = ObjectId.GenerateNewId();
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync((Business?)null);
        var context = TestData.HttpContext("Bearer", new Claim("business", businessId.ToString()));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), repository.Object);

        var result = await handler.GetBusiness();

        result.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(BusinessEnums.ACTIVE, true, 200)]
    [InlineData(BusinessEnums.PENDING_APPROVAL, false, 400)]
    [InlineData(BusinessEnums.REJECTED, false, 400)]
    [InlineData(BusinessEnums.DELETED, false, 404)]
    [InlineData(BusinessEnums.SUSPENDED, false, 401)]
    public async Task GetBusiness_ReturnsResultForEachBusinessStatus(BusinessEnums status, bool success, int statusCode)
    {
        var business = TestData.Business(status);
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync(business);
        var context = TestData.HttpContext("Bearer", new Claim("business", business.Id.ToString()));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), repository.Object);

        var result = await handler.GetBusiness();

        result.IsSuccess.Should().Be(success);
        result.StatusCode.Should().Be(statusCode);
    }

    [Theory]
    [InlineData(UserStatus.ACTIVE, true, 200)]
    [InlineData(UserStatus.PENDING_APPROVAL, false, 400)]
    [InlineData(UserStatus.PENDING_PROFILE_COMPLETION, false, 400)]
    [InlineData(UserStatus.REJECTED, false, 400)]
    [InlineData(UserStatus.DELETED, false, 404)]
    public async Task GetUser_ReturnsResultForEachUserStatus(UserStatus status, bool success, int statusCode)
    {
        var business = TestData.Business();
        var user = TestData.User(business, status);
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(user);
        var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        var handler = new CurrentUserService(context, repository.Object, Mock.Of<IBusinessRepository>());

        var result = await handler.GetUser();

        result.IsSuccess.Should().Be(success);
        result.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task GetUser_WhenClaimMissing_ReturnsUnauthorized()
    {
        var handler = new CurrentUserService(TestData.HttpContext(), Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var result = await handler.GetUser();

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetUser_WhenClaimInvalid_ReturnsUnauthorized()
    {
        var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, "invalid"));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var result = await handler.GetUser();

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetUser_WhenRepositoryReturnsNull_ReturnsNotFound()
    {
        var userId = ObjectId.GenerateNewId();
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);
        var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        var handler = new CurrentUserService(context, repository.Object, Mock.Of<IBusinessRepository>());

        var result = await handler.GetUser();

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public void GetBusinessId_WhenClaimInvalid_ThrowsAuthenticationException()
    {
        var context = TestData.HttpContext("Bearer", new Claim("business", "bad-id"));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var action = () => handler.GetBusinessId();

        action.Should().Throw<AuthenticationException>();
    }

    [Fact]
    public void GetBusinessId_WhenClaimValid_ReturnsBusinessId()
    {
        var businessId = ObjectId.GenerateNewId();
        var context = TestData.HttpContext("Bearer", new Claim("business", businessId.ToString()));
        var handler = new CurrentUserService(context, Mock.Of<IUserRepository>(), Mock.Of<IBusinessRepository>());

        var result = handler.GetBusinessId();

        result.Should().Be(businessId.ToString());
    }

    [Fact]
    public async Task GetUserId_ActiveUser_ReturnsClaimId()
    {
        var business = TestData.Business();
        var user = TestData.User(business);
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(user);
        var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        var handler = new CurrentUserService(context, repository.Object, Mock.Of<IBusinessRepository>());

        var result = await handler.GetUserId();

        result.Should().Be(user.Id.ToString());
    }

    [Fact]
    public async Task GetUserId_PendingUser_ThrowsAuthenticationException()
    {
        var business = TestData.Business();
        var user = TestData.User(business, UserStatus.PENDING_APPROVAL);
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync(user);
        var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        var handler = new CurrentUserService(context, repository.Object, Mock.Of<IBusinessRepository>());

        var action = () => handler.GetUserId();

        await action.Should().ThrowAsync<AuthenticationException>();
    }
}

public class UT_ValidationBehavior
{
    [Fact]
    public async Task Handle_WithNoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<ValidationRequest, Result<string>>([]);
        var calls = 0;

        var result = await behavior.Handle(new ValidationRequest("ok"), _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Success("next"));
        }, CancellationToken.None);

        calls.Should().Be(1);
        result.Data.Should().Be("next");
    }

    [Fact]
    public async Task Handle_WithValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<ValidationRequest, Result<string>>([new ValidationRequestValidator()]);
        var calls = 0;

        var result = await behavior.Handle(new ValidationRequest("valid"), _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Success("next"));
        }, CancellationToken.None);

        calls.Should().Be(1);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ReturnsStructuredFailureWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<ValidationRequest, Result<string>>([new ValidationRequestValidator()]);
        var calls = 0;

        var result = await behavior.Handle(new ValidationRequest(""), _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Success("next"));
        }, CancellationToken.None);

        calls.Should().Be(0);
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.MessageCode.Should().Be("MG_VALIDATOR_400");
        result.Errors.Should().ContainKey("value");
    }

    private sealed record ValidationRequest(string Value) : IRequest<Result<string>>;

    private sealed class ValidationRequestValidator : AbstractValidator<ValidationRequest>
    {
        public ValidationRequestValidator() => RuleFor(request => request.Value)
            .NotEmpty().WithMessage("Value is required");
    }
}
