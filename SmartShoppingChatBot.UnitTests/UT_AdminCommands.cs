using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.CreateSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.DeleteSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.UpdateSystemContent;
using SmartShoppingChatBot.Application.Features.UserManagement.DeleteUser;
using SmartShoppingChatBot.Application.Features.UserManagement.UpdateUser;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_SystemContentCommands
{
    [Fact]
    public async Task Create_WhenKeyDuplicated_ReturnsConflictBeforeCurrentUserLookup()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync(fixture.Content);

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.CurrentUser.Verify(service => service.GetUser(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenCurrentUserFails_ReturnsOriginalFailureWithoutWrite()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null);
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Repository.Verify(repository => repository.AddAsync(It.IsAny<SystemContent>()), Times.Never);
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsParsesAndSetsAuditFields()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null);
        SystemContent? created = null;
        fixture.Repository.Setup(repository => repository.AddAsync(It.IsAny<SystemContent>()))
            .Callback<SystemContent>(value => created = value)
            .Returns(Task.CompletedTask);

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(201);
        created!.Title.Should().Be("Welcome");
        created.Key.Should().Be("WELCOME");
        created.Content.Should().Be("Hello world");
        created.ContentType.Should().Be(ContentType.Markdown);
        created.Status.Should().Be(SystemContentStatus.Published);
        created.Version.Should().Be(1);
        created.CreatedBy!.Id.Should().Be(fixture.Admin.Id);
        created.CreatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task Create_WhenSaveThrows_ReturnsServerFailure()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null);
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WhenContentMissing_ReturnsNotFound()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null);

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Update_WhenNewKeyDuplicated_ReturnsConflict()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.SetupSequence(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync(fixture.Content)
            .ReturnsAsync(new SystemContent { Id = ObjectId.GenerateNewId(), Key = "WELCOME" });

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SystemContent>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenCurrentUserFails_ReturnsOriginalFailure()
    {
        var fixture = new SystemContentFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(403, "Forbidden"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Update_ValidRequest_TrimsIncrementsVersionAndSetsAudit()
    {
        var fixture = new SystemContentFixture();
        fixture.Content.Version = 4;

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Content.Title.Should().Be("Updated title");
        fixture.Content.Key.Should().Be("UPDATED_KEY");
        fixture.Content.Version.Should().Be(5);
        fixture.Content.Status.Should().Be(SystemContentStatus.Draft);
        fixture.Content.UpdatedBy!.Id.Should().Be(fixture.Admin.Id);
        fixture.Content.UpdatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task Update_WhenRepositoryThrows_ReturnsServerFailureWithoutSaveChanges()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.UpdateAsync(It.IsAny<SystemContent>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenContentMissing_ReturnsNotFound()
    {
        var fixture = new SystemContentFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null);

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Delete_WhenCurrentUserFails_ReturnsFailureWithoutMutation()
    {
        var fixture = new SystemContentFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Content.Status.Should().NotBe(SystemContentStatus.Deleted);
    }

    [Fact]
    public async Task Delete_ValidRequest_SoftDeletesAndSetsAuditFields()
    {
        var fixture = new SystemContentFixture();

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Content.Status.Should().Be(SystemContentStatus.Deleted);
        fixture.Content.DeletedAt.Should().Be(TestData.Now);
        fixture.Content.UpdatedAt.Should().Be(TestData.Now);
        fixture.Content.UpdatedBy!.Id.Should().Be(fixture.Admin.Id);
    }

    [Fact]
    public async Task Delete_WhenSaveThrows_ReturnsServerFailure()
    {
        var fixture = new SystemContentFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
    }

    private sealed class SystemContentFixture
    {
        public Business Business { get; } = TestData.Business();
        public User Admin { get; }
        public SystemContent Content { get; }
        public Mock<ISystemContentRepository> Repository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public CreateSystemContentCommandHandler CreateHandler { get; }
        public UpdateSystemContentCommandHandler UpdateHandler { get; }
        public DeleteSystemContentCommandHandler DeleteHandler { get; }

        public SystemContentFixture()
        {
            Admin = TestData.User(Business, role: RoleEnums.ADMIN);
            Content = new SystemContent
            {
                Id = ObjectId.GenerateNewId(),
                Title = "Original",
                Key = "ORIGINAL",
                Content = "Original body",
                ContentType = ContentType.Markdown,
                Version = 1,
                Status = SystemContentStatus.Published,
                CreatedAt = TestData.Now.AddDays(-1),
                UpdatedAt = TestData.Now.AddDays(-1)
            };
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(Admin));
            Repository.SetupSequence(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<SystemContent, bool>>>(),
                    It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
                .ReturnsAsync(Content)
                .ReturnsAsync((SystemContent?)null);
            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<SystemContentResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var content = (SystemContent)source;
                    return new SystemContentResponse
                    {
                        Id = content.Id.ToString(), Title = content.Title, Key = content.Key,
                        Content = content.Content, Version = content.Version, Status = content.Status
                    };
                });
            CreateHandler = new CreateSystemContentCommandHandler(
                Repository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
                Mock.Of<ILogger<CreateSystemContentCommandHandler>>(), new FixedTimeProvider(TestData.Now));
            UpdateHandler = new UpdateSystemContentCommandHandler(
                Repository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
                Mock.Of<ILogger<UpdateSystemContentCommandHandler>>(), new FixedTimeProvider(TestData.Now));
            DeleteHandler = new DeleteSystemContentCommandHandler(
                Repository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
                Mock.Of<ILogger<DeleteSystemContentCommandHandler>>(), new FixedTimeProvider(TestData.Now));
        }

        public CreateSystemContentCommand CreateCommand() => new()
        {
            Title = " Welcome ", Key = " WELCOME ", Content = " Hello world ",
            ContentType = "markdown", Status = "published"
        };

        public UpdateSystemContentCommand UpdateCommand() => new()
        {
            SystemContentId = Content.Id,
            Title = " Updated title ", Key = " UPDATED_KEY ", Content = " Updated body ",
            ContentType = "markdown", Status = "draft"
        };

        public DeleteSystemContentCommand DeleteCommand() => new() { SystemContentId = Content.Id };
    }
}

public class UT_UserAdministrationCommands
{
    [Fact]
    public async Task Update_WhenTargetMissing_ReturnsNotFound()
    {
        var fixture = new UserAdministrationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Update_WhenCurrentUserFails_ReturnsOriginalFailureWithoutMutation()
    {
        var fixture = new UserAdministrationFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(403, "Forbidden"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesFieldsAndAudit()
    {
        var fixture = new UserAdministrationFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Target.FullName.Should().Be("Updated User");
        fixture.Target.PhoneNumber.Should().Be("0909");
        fixture.Target.UpdatedAt.Should().Be(TestData.Now);
        fixture.Target.UpdatedBy!.Id.Should().Be(fixture.Admin.Id);
    }

    [Fact]
    public async Task Update_ValidRequest_PersistsAndMapsResponse()
    {
        var fixture = new UserAdministrationFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Target.Id.ToString());
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.Target), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenRepositoryThrows_PropagatesAndSkipsSaveChanges()
    {
        var fixture = new UserAdministrationFixture();
        fixture.UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenUserIdInvalid_ReturnsBadRequestBeforeLookup()
    {
        var fixture = new UserAdministrationFixture();

        var result = await fixture.DeleteHandler.Handle(new DeleteUserCommand { UserId = "invalid" }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.UserRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenTargetMissing_ReturnsNotFound()
    {
        var fixture = new UserAdministrationFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Delete_WhenCurrentUserFails_ReturnsFailureWithoutMutation()
    {
        var fixture = new UserAdministrationFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Target.UserStatus.Should().NotBe(UserStatus.DELETED);
    }

    [Fact]
    public async Task Delete_ValidRequest_SoftDeletesAuditsPersistsAndMaps()
    {
        var fixture = new UserAdministrationFixture();

        var result = await fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
        result.Data!.Id.Should().Be(fixture.Target.Id.ToString());
        fixture.Target.UserStatus.Should().Be(UserStatus.DELETED);
        fixture.Target.DeletedAt.Should().Be(TestData.Now);
        fixture.Target.UpdatedBy!.Id.Should().Be(fixture.Admin.Id);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenSaveThrows_PropagatesAfterRepositoryUpdate()
    {
        var fixture = new UserAdministrationFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.DeleteHandler.Handle(fixture.DeleteCommand(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.Target), Times.Once);
    }

    private sealed class UserAdministrationFixture
    {
        public Business Business { get; } = TestData.Business();
        public User Admin { get; }
        public User Target { get; }
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public UpdateUserCommandHandler UpdateHandler { get; }
        public DeleteUserCommandHandler DeleteHandler { get; }

        public UserAdministrationFixture()
        {
            Admin = TestData.User(Business, role: RoleEnums.ADMIN);
            Target = TestData.User(Business, role: RoleEnums.CATALOG_TEAM);
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(Admin));
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(Target);
            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<ProfileResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var user = (User)source;
                    return new ProfileResponse { Id = user.Id.ToString(), Email = user.Email, FullName = user.FullName };
                });
            UpdateHandler = new UpdateUserCommandHandler(
                UserRepository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
                new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<UpdateUserCommandHandler>>());
            DeleteHandler = new DeleteUserCommandHandler(
                UserRepository.Object, CurrentUser.Object, UnitOfWork.Object,
                new FixedTimeProvider(TestData.Now), mapper.Object, Mock.Of<ILogger<DeleteUserCommandHandler>>());
        }

        public UpdateUserCommand UpdateCommand() => new()
        {
            UserId = Target.Id,
            FullName = "Updated User",
            PhoneNumber = "0909",
            DateOfBirth = new DateTime(1999, 1, 1),
            Gender = 1
        };

        public DeleteUserCommand DeleteCommand() => new() { UserId = Target.Id.ToString() };
    }
}
