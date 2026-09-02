using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ChangePassword;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ForgotPassword;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ResetPassword;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ForgotPassword
{
    [Fact]
    public async Task Handle_WhenEmailUnknown_ReturnsNeutralSuccessWithoutCreatingToken()
    {
        var fixture = new ForgotPasswordFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.Handler.Handle(new ForgotPasswordCommand { Email = "missing@example.com" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().Contain("If the email exists");
        fixture.TokenRepository.Verify(repository => repository.AddAsync(It.IsAny<Token>()), Times.Never);
        fixture.Email.Verify(service => service.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NormalizesEmailAndOnlyMatchesActiveUser()
    {
        var fixture = new ForgotPasswordFixture();
        Expression<Func<User, bool>>? captured = null;
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .Callback<Expression<Func<User, bool>>, Func<IQueryable<User>, IQueryable<User>>?>((predicate, _) => captured = predicate)
            .ReturnsAsync(fixture.User);

        await fixture.Handler.Handle(new ForgotPasswordCommand { Email = " OWNER@EXAMPLE.COM " }, CancellationToken.None);

        captured!.Compile()(fixture.User).Should().BeTrue();
        var inactive = TestData.User(fixture.Business, UserStatus.REJECTED);
        inactive.Email = fixture.User.Email;
        captured.Compile()(inactive).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExistingUser_StoresHashedPasswordResetTokenWithConfiguredExpiry()
    {
        var fixture = new ForgotPasswordFixture();
        Token? stored = null;
        var before = DateTimeOffset.UtcNow;
        fixture.TokenRepository.Setup(repository => repository.AddAsync(It.IsAny<Token>()))
            .Callback<Token>(token => stored = token)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        stored!.TokenValue.Should().NotBe("raw reset+/=");
        stored.Type.Should().Be(TokenType.PASSWORD_RESET);
        stored.UserId.Should().Be(fixture.User.Id);
        stored.ExpiresAt.Should().BeOnOrAfter(before.AddMinutes(30)).And.BeOnOrBefore(after.AddMinutes(30));
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingUser_RendersTemplateAndSendsEncodedResetLink()
    {
        var fixture = new ForgotPasswordFixture();
        ResetPasswordEmailModel? model = null;
        fixture.Template.Setup(service => service.RenderEmailTemplateAsync(
                "ResetPassword", It.IsAny<ResetPasswordEmailModel>()))
            .Callback<string, ResetPasswordEmailModel>((_, value) => model = value)
            .ReturnsAsync("rendered-body");

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        model!.FullName.Should().Be(fixture.User.FullName);
        model.ResetPasswordUrl.Should().Contain("raw%20reset%2B%2F%3D")
            .And.Contain("email=owner%40example.com");
        model.ExpireMinutes.Should().Be(30);
        fixture.Email.Verify(service => service.SendEmailAsync(
            fixture.User.Email, "Password Reset", "rendered-body"), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingUser_LogsPasswordResetRequest()
    {
        var fixture = new ForgotPasswordFixture();
        ActivityLogRequest? log = null;
        fixture.ActivityLog.Setup(service => service.LogAsync(It.IsAny<ActivityLogRequest>()))
            .Callback<ActivityLogRequest>(value => log = value)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Action.Should().Be(ActionLogEnums.PasswordReset);
        log.ActorId.Should().Be(fixture.User.Id.ToString());
        log.TargetType.Should().Be("User");
        log.TargetId.Should().Be(fixture.User.Id.ToString());
        log.Status.Should().Be(StatusLogEnums.Success);
        log.Severity.Should().Be(SeverityLogEnums.Info);
        log.Description.Should().Be($"User {fixture.User.FullName} requested a password reset successfully.");
    }

    [Fact]
    public async Task Handle_WhenTokenPersistenceThrows_PropagatesAndDoesNotSendEmail()
    {
        var fixture = new ForgotPasswordFixture();
        fixture.TokenRepository.Setup(repository => repository.AddAsync(It.IsAny<Token>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.Email.Verify(service => service.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class ForgotPasswordFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ITokenRepository> TokenRepository { get; } = new();
        public Mock<ITokenService> TokenService { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IEmailTemplateService> Template { get; } = new();
        public Mock<IActivityLogService> ActivityLog { get; } = new();
        public ForgotPasswordCommandHandler Handler { get; }

        public ForgotPasswordFixture()
        {
            User = TestData.User(Business);
            User.Email = "owner@example.com";
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(User);
            TokenService.Setup(service => service.CreateEmailVerificationToken()).Returns("raw reset+/=");
            Template.Setup(service => service.RenderEmailTemplateAsync(
                    It.IsAny<string>(), It.IsAny<ResetPasswordEmailModel>()))
                .ReturnsAsync("rendered-body");
            Handler = new ForgotPasswordCommandHandler(
                UserRepository.Object, TokenRepository.Object, TokenService.Object, Email.Object,
                UnitOfWork.Object, Options.Create(new PasswordResetTokenSettings
                {
                    ExpireMinutes = 30,
                    UrlBase = "https://shop.example/reset?token="
                }), Template.Object, ActivityLog.Object);
        }

        public ForgotPasswordCommand Command() => new() { Email = " OWNER@EXAMPLE.COM " };
    }
}

public class UT_ResetPassword
{
    [Fact]
    public async Task Handle_WhenTokenMissing_ReturnsBadRequest()
    {
        var fixture = new ResetPasswordFixture();
        fixture.TokenRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Token, bool>>>(),
                It.IsAny<Func<IQueryable<Token>, IQueryable<Token>>?>()))
            .ReturnsAsync((Token?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.UserRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenExpired_ReturnsBadRequest()
    {
        var fixture = new ResetPasswordFixture();
        fixture.Token.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("expired token");
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsNotFound()
    {
        var fixture = new ResetPasswordFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Password.Verify(service => service.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordsDoNotMatch_ReturnsBadRequestWithoutUpdates()
    {
        var fixture = new ResetPasswordFixture();
        var command = new ResetPasswordCommand
        {
            Token = "raw-reset-token",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "different"
        };

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_HashesPasswordConsumesTokenAndSaves()
    {
        var fixture = new ResetPasswordFixture();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.User.PasswordHash.Should().Be("new-hash");
        fixture.User.UpdatedBy!.Id.Should().Be(fixture.User.Id);
        fixture.Token.TokenValue.Should().BeNull();
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.User), Times.Once);
        fixture.TokenRepository.Verify(repository => repository.UpdateAsync(fixture.Token), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_LogsPasswordResetCompletion()
    {
        var fixture = new ResetPasswordFixture();
        ActivityLogRequest? log = null;
        fixture.ActivityLog.Setup(service => service.LogAsync(It.IsAny<ActivityLogRequest>()))
            .Callback<ActivityLogRequest>(value => log = value)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Action.Should().Be(ActionLogEnums.PasswordReset);
        log.ActorId.Should().Be(fixture.User.Id.ToString());
        log.TargetType.Should().Be("User");
        log.TargetId.Should().Be(fixture.User.Id.ToString());
        log.Status.Should().Be(StatusLogEnums.Success);
        log.Severity.Should().Be(SeverityLogEnums.Info);
        log.Description.Should().Be($"User {fixture.User.FullName} reset password successfully.");
    }

    [Fact]
    public async Task Handle_WhenUserUpdateThrows_PropagatesAndDoesNotConsumeToken()
    {
        var fixture = new ResetPasswordFixture();
        fixture.UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.Token.TokenValue.Should().NotBeNull();
        fixture.TokenRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Token>()), Times.Never);
    }

    private sealed class ResetPasswordFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Token Token { get; }
        public Mock<ITokenRepository> TokenRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IPasswordService> Password { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IActivityLogService> ActivityLog { get; } = new();
        public ResetPasswordCommandHandler Handler { get; }

        public ResetPasswordFixture()
        {
            User = TestData.User(Business);
            Token = new Token
            {
                Id = ObjectId.GenerateNewId(),
                UserId = User.Id,
                TokenValue = "hashed-reset-token",
                Type = TokenType.PASSWORD_RESET,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            TokenRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Token, bool>>>(),
                    It.IsAny<Func<IQueryable<Token>, IQueryable<Token>>?>()))
                .ReturnsAsync(Token);
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(User);
            Password.Setup(service => service.HashPassword("NewPassword1!")).Returns("new-hash");
            Handler = new ResetPasswordCommandHandler(
                Mock.Of<ITokenService>(), TokenRepository.Object, UserRepository.Object,
                Password.Object, UnitOfWork.Object, ActivityLog.Object);
        }

        public ResetPasswordCommand Command() => new()
        {
            Token = "raw-reset-token",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        };
    }
}

public class UT_ChangePassword
{
    [Fact]
    public async Task Handle_WhenCurrentUserResultNull_ReturnsNotFound()
    {
        var fixture = new ChangePasswordFixture();
        fixture.CurrentUser.Setup(service => service.GetUser()).ReturnsAsync((Result<User>)null!);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserDataMissing_ReturnsNotFound()
    {
        var fixture = new ChangePasswordFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Password.Verify(service => service.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordWrong_ReturnsBadRequest()
    {
        var fixture = new ChangePasswordFixture();
        fixture.Password.Setup(service => service.VerifyPassword("old-password", fixture.User.PasswordHash)).Returns(false);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Current password");
    }

    [Fact]
    public async Task Handle_WhenConfirmationDiffers_ReturnsBadRequestWithoutHashing()
    {
        var fixture = new ChangePasswordFixture();
        var command = new ChangePasswordCommand
        {
            currentPassword = "old-password",
            newPassword = "new-password",
            confirmPassword = "different"
        };

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.Password.Verify(service => service.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesHashAuditAndSaves()
    {
        var fixture = new ChangePasswordFixture();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.User.PasswordHash.Should().Be("new-hash");
        fixture.User.UpdatedBy!.Id.Should().Be(fixture.User.Id);
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.User), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_LogsPasswordChange()
    {
        var fixture = new ChangePasswordFixture();
        ActivityLogRequest? log = null;
        fixture.ActivityLog.Setup(service => service.LogAsync(It.IsAny<ActivityLogRequest>()))
            .Callback<ActivityLogRequest>(value => log = value)
            .Returns(Task.CompletedTask);

        await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Action.Should().Be(ActionLogEnums.PasswordChange);
        log.ActorId.Should().Be(fixture.User.Id.ToString());
        log.TargetType.Should().Be("User");
        log.TargetId.Should().Be(fixture.User.Id.ToString());
        log.Status.Should().Be(StatusLogEnums.Success);
        log.Severity.Should().Be(SeverityLogEnums.Info);
        log.Description.Should().Be($"User {fixture.User.FullName} changed password successfully.");
    }

    [Fact]
    public async Task Handle_WhenUpdateThrows_PropagatesAndSkipsSaveChanges()
    {
        var fixture = new ChangePasswordFixture();
        fixture.UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ChangePasswordFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IPasswordService> Password { get; } = new();
        public Mock<IActivityLogService> ActivityLog { get; } = new();
        public ChangePasswordCommandHandler Handler { get; }

        public ChangePasswordFixture()
        {
            User = TestData.User(Business);
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            Password.Setup(service => service.VerifyPassword("old-password", User.PasswordHash)).Returns(true);
            Password.Setup(service => service.HashPassword("new-password")).Returns("new-hash");
            Handler = new ChangePasswordCommandHandler(
                CurrentUser.Object, UserRepository.Object, UnitOfWork.Object,
                Mock.Of<ILogger<ChangePasswordCommandHandler>>(), Password.Object,
                ActivityLog.Object);
        }

        public ChangePasswordCommand Command() => new()
        {
            currentPassword = "old-password",
            newPassword = "new-password",
            confirmPassword = "new-password"
        };
    }
}
