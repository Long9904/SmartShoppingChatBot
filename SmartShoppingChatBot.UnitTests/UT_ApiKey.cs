using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.CreateNewKey;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevokeApiKey;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_CreateNewApiKey
{
    [Fact]
    public async Task Handle_WhenUserFails_ReturnsFailureWithoutCreatingKey()
    {
        var fixture = new CreateKeyFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(new CreateNewKeyCommand { Name = "Production" }, CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Repository.Verify(repository => repository.AddAsync(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsFailureWithoutCreatingKey()
    {
        var fixture = new CreateKeyFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(404, "Business not found"));

        var result = await fixture.Handler.Handle(new CreateNewKeyCommand { Name = "Production" }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Repository.Verify(repository => repository.AddAsync(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesHashedEncryptedKeyAndReturnsPlainKeyOnce()
    {
        var fixture = new CreateKeyFixture();
        ApiKey? saved = null;
        string? hashedSecret = null;
        string? encryptedSecret = null;
        fixture.Hash.Setup(service => service.HmacSha256(It.IsAny<string>()))
            .Callback<string>(value => hashedSecret = value).Returns("hash");
        fixture.Hash.Setup(service => service.Encrypt(It.IsAny<string>()))
            .Callback<string>(value => encryptedSecret = value).Returns("encrypted");
        fixture.Repository.Setup(repository => repository.AddAsync(It.IsAny<ApiKey>()))
            .Callback<ApiKey>(key => saved = key).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(new CreateNewKeyCommand { Name = "Production" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.FullKey.Should().StartWith("ssc_live_").And.Contain(".");
        saved!.HashKey.Should().Be("hash");
        saved.EncryptedSecret.Should().Be("encrypted");
        saved.Status.Should().Be(KeyStatus.Active);
        saved.BusinessId.Should().Be(fixture.Business.Id);
        hashedSecret.Should().Be(encryptedSecret);
        result.Data.FullKey.Should().EndWith(hashedSecret!);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class CreateKeyFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Mock<IHashService> Hash { get; } = new();
        public Mock<IApiKeyRepository> Repository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public CreateNewKeyCommandHandler Handler { get; }

        public CreateKeyFixture()
        {
            User = TestData.User(Business);
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            Hash.Setup(service => service.HmacSha256(It.IsAny<string>())).Returns("hash");
            Hash.Setup(service => service.Encrypt(It.IsAny<string>())).Returns("encrypted");
            Handler = new CreateNewKeyCommandHandler(Hash.Object, Repository.Object, UnitOfWork.Object,
                CurrentUser.Object, new FixedTimeProvider(TestData.Now));
        }
    }
}

public class UT_RevokeApiKey
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsFailure()
    {
        var fixture = new RevokeKeyFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_WhenKeyMissing_ReturnsNotFound()
    {
        var fixture = new RevokeKeyFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<ApiKey, bool>>>(),
                It.IsAny<Func<IQueryable<ApiKey>, IQueryable<ApiKey>>?>()))
            .ReturnsAsync((ApiKey?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenKeyBelongsToAnotherBusiness_ReturnsForbidden()
    {
        var fixture = new RevokeKeyFixture();
        fixture.Key.BusinessId = ObjectId.GenerateNewId();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAlreadyRevoked_ReturnsBadRequest()
    {
        var fixture = new RevokeKeyFixture();
        fixture.Key.Status = KeyStatus.Revoked;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Handle_WhenUserFails_ReturnsFailureWithoutUpdate()
    {
        var fixture = new RevokeKeyFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_RevokesKeyAndSetsAuditFields()
    {
        var fixture = new RevokeKeyFixture();

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Key.Status.Should().Be(KeyStatus.Revoked);
        fixture.Key.RevokedAt.Should().Be(TestData.Now);
        fixture.Key.RevokedBy!.Id.Should().Be(fixture.User.Id);
        fixture.Key.UpdatedBy!.Id.Should().Be(fixture.User.Id);
        fixture.Repository.Verify(repository => repository.UpdateAsync(fixture.Key), Times.Once);
    }

    private sealed class RevokeKeyFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public ApiKey Key { get; }
        public Mock<IApiKeyRepository> Repository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public RevokeApiKeyCommandHandler Handler { get; }

        public RevokeKeyFixture()
        {
            User = TestData.User(Business);
            Key = new ApiKey
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, Name = "Production",
                KeyId = "ssc_live_abc", HashKey = "hash", EncryptedSecret = "encrypted",
                Status = KeyStatus.Active, CreatedAt = TestData.Now, UpdatedAt = TestData.Now
            };
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            Repository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<ApiKey, bool>>>(),
                    It.IsAny<Func<IQueryable<ApiKey>, IQueryable<ApiKey>>?>()))
                .ReturnsAsync(Key);
            Handler = new RevokeApiKeyCommandHandler(Repository.Object, CurrentUser.Object, UnitOfWork.Object,
                new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<RevokeApiKeyCommandHandler>>());
        }

        public RevokeApiKeyCommand Command() => new() { Id = Key.Id.ToString() };
    }
}
