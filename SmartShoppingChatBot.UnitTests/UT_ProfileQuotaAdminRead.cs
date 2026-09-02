using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Auth.GetMyProfile;
using SmartShoppingChatBot.Application.Features.BusinessManagement.GetAllBusiness;
using SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;
using SmartShoppingChatBot.Application.Features.ProfileManagement.UpdateProfile;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentById;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentByKey;
using SmartShoppingChatBot.Application.Features.UserManagement.GetAllUser;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ProfileFlows
{
    [Fact]
    public async Task GetMyProfile_WhenCurrentUserFails_ReturnsOriginalFailure()
    {
        var fixture = new ProfileFlowFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.GetHandler.Handle(new GetMyProfileCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyProfile_ValidUser_MapsAndReturnsProfile()
    {
        var fixture = new ProfileFlowFixture();

        var result = await fixture.GetHandler.Handle(new GetMyProfileCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Id.Should().Be(fixture.User.Id.ToString());
        result.Data.Email.Should().Be(fixture.User.Email);
    }

    [Fact]
    public async Task UpdateProfile_WhenCurrentUserFails_ReturnsNotFoundWithoutWrite()
    {
        var fixture = new ProfileFlowFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_ValidRequest_UpdatesEditableFieldsAndTimestamp()
    {
        var fixture = new ProfileFlowFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.User.FullName.Should().Be("Updated Profile");
        fixture.User.PhoneNumber.Should().Be("0909");
        fixture.User.DateOfBirth.Should().Be(new DateTime(2001, 2, 3));
        fixture.User.Gender.Should().Be(2);
        fixture.User.UpdatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task UpdateProfile_DoesNotChangeIdentityStatusOrBusinessMembership()
    {
        var fixture = new ProfileFlowFixture();
        var originalEmail = fixture.User.Email;
        var originalStatus = fixture.User.UserStatus;
        var originalBusinessId = fixture.User.Business.Id;

        await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        fixture.User.Email.Should().Be(originalEmail);
        fixture.User.UserStatus.Should().Be(originalStatus);
        fixture.User.Business.Id.Should().Be(originalBusinessId);
    }

    [Fact]
    public async Task UpdateProfile_ValidRequest_PersistsAndMapsResponse()
    {
        var fixture = new ProfileFlowFixture();

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.Data!.FullName.Should().Be("Updated Profile");
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.User), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_ValidRequest_LogsProfileUpdate()
    {
        var fixture = new ProfileFlowFixture();
        ActivityLogRequest? log = null;
        fixture.ActivityLog.Setup(service => service.LogAsync(It.IsAny<ActivityLogRequest>()))
            .Callback<ActivityLogRequest>(value => log = value)
            .Returns(Task.CompletedTask);

        await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Action.Should().Be(ActionLogEnums.Update);
        log.ActorId.Should().Be(fixture.User.Id.ToString());
        log.TargetType.Should().Be("UserProfile");
        log.TargetId.Should().Be(fixture.User.Id.ToString());
        log.Status.Should().Be(StatusLogEnums.Success);
        log.Severity.Should().Be(SeverityLogEnums.Info);
        log.Description.Should().Be($"User {fixture.User.FullName} updated their profile successfully.");
    }

    [Fact]
    public async Task UpdateProfile_WhenRepositoryThrows_PropagatesAndSkipsSaveChanges()
    {
        var fixture = new ProfileFlowFixture();
        fixture.UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var action = () => fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WhenSaveThrows_PropagatesAfterUserUpdate()
    {
        var fixture = new ProfileFlowFixture();
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        var action = () => fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        fixture.UserRepository.Verify(repository => repository.UpdateAsync(fixture.User), Times.Once);
    }

    private sealed class ProfileFlowFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IActivityLogService> ActivityLog { get; } = new();
        public GetMyProfileCommandHandler GetHandler { get; }
        public UpdateProfileCommandHandler UpdateHandler { get; }

        public ProfileFlowFixture()
        {
            User = TestData.User(Business);
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<ProfileResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var user = (User)source;
                    return new ProfileResponse
                    {
                        Id = user.Id.ToString(), FullName = user.FullName, Email = user.Email,
                        UserStatus = user.UserStatus, Role = user.Business.Role
                    };
                });
            GetHandler = new GetMyProfileCommandHandler(CurrentUser.Object, mapper.Object, ActivityLog.Object);
            UpdateHandler = new UpdateProfileCommandHandler(
                UserRepository.Object, CurrentUser.Object, UnitOfWork.Object, mapper.Object,
                new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<UpdateProfileCommandHandler>>(),
                ActivityLog.Object);
        }

        public UpdateProfileCommand UpdateCommand() => new()
        {
            UserId = User.Id,
            FullName = "Updated Profile",
            PhoneNumber = "0909",
            DateOfBirth = new DateTime(2001, 2, 3),
            Gender = 2
        };
    }
}

public class UT_BusinessQuotaQuery
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new BusinessQuotaFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(new GetBusinessQuotasQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.QuotaRepository.Verify(repository => repository.GetCurrentBusinessQuota(It.IsAny<ObjectId>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentQuotaMissing_ReturnsNotFound()
    {
        var fixture = new BusinessQuotaFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.Handler.Handle(new GetBusinessQuotasQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.LogRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task Handle_ScopesLogsToBusinessQuotaAndAppliesSourceFilterPagination()
    {
        var fixture = new BusinessQuotaFixture();
        fixture.Logs.Add(new UsageQuotaLog
        {
            Id = ObjectId.GenerateNewId(), BusinessId = fixture.Business.Id,
            BusinessQuotaId = ObjectId.GenerateNewId(), SourceId = ObjectId.GenerateNewId(),
            SourceType = SourceTypeEnum.Chat, CreatedAt = TestData.Now
        });
        var query = new GetBusinessQuotasQuery
        {
            Filter = new GetBusinessQuotasFilter
            {
                SourceType = SourceTypeEnum.EmbeddingProduct, PageIndex = 2, PageSize = 4
            }
        };

        var result = await fixture.Handler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().OnlyContain(log => log.SourceType == SourceTypeEnum.EmbeddingProduct);
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(4);
    }

    [Theory]
    [InlineData("inputtokens asc", 2L)]
    [InlineData("outputtokens desc", 9L)]
    public async Task Handle_AppliesRequestedLogOrdering(string orderBy, long expectedFirst)
    {
        var fixture = new BusinessQuotaFixture();
        var result = await fixture.Handler.Handle(
            new GetBusinessQuotasQuery { Filter = new GetBusinessQuotasFilter { OrderBy = orderBy } },
            CancellationToken.None);

        var first = result.Data!.Items.First();
        (orderBy.StartsWith("input") ? first.InputTokens : first.OutputTokens).Should().Be(expectedFirst);
    }

    [Fact]
    public async Task Handle_MapsUsageTokenMessageAndSourceFields()
    {
        var fixture = new BusinessQuotaFixture();

        var result = await fixture.Handler.Handle(new GetBusinessQuotasQuery(), CancellationToken.None);

        var mapped = result.Data!.Items.First();
        mapped.BusinessQuotaId.Should().Be(fixture.Quota.Id.ToString());
        mapped.BillableTokens.Should().BeGreaterThan(0);
        mapped.MessageUsed.Should().BeGreaterThanOrEqualTo(0);
        mapped.SourceId.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class BusinessQuotaFixture
    {
        public Business Business { get; } = TestData.Business();
        public BusinessQuota Quota { get; }
        public List<UsageQuotaLog> Logs { get; } = [];
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IUsageQuotaLogRepository> LogRepository { get; } = new();
        public GetBusinessQuotasQueryHandler Handler { get; }

        public BusinessQuotaFixture()
        {
            Quota = TestData.Quota(Business);
            Logs.Add(new UsageQuotaLog
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, BusinessQuotaId = Quota.Id,
                SourceId = ObjectId.GenerateNewId(), SourceType = SourceTypeEnum.EmbeddingProduct,
                InputTokens = 8, OutputTokens = 1, BillableTokens = 10, MessageUsed = 0,
                CreatedAt = TestData.Now.AddMinutes(-1)
            });
            Logs.Add(new UsageQuotaLog
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, BusinessQuotaId = Quota.Id,
                SourceId = ObjectId.GenerateNewId(), SourceType = SourceTypeEnum.Chat,
                InputTokens = 2, OutputTokens = 9, BillableTokens = 20, MessageUsed = 1,
                CreatedAt = TestData.Now
            });
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id)).ReturnsAsync(Quota);
            LogRepository.Setup(repository => repository.AsQueryable()).Returns(() => Logs.AsQueryable());
            LogRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<UsageQuotaLog>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<UsageQuotaLog> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<UsageQuotaLog>(items, items.Count, index, size);
                });
            Handler = new GetBusinessQuotasQueryHandler(
                CurrentUser.Object, QuotaRepository.Object, LogRepository.Object);
        }
    }
}

public class UT_AdminReadQueries
{
    [Fact]
    public async Task GetBusinesses_AppliesSearchStatusDateOrderAndPagination()
    {
        var fixture = new AdminReadFixture();
        var query = new GetBusinessesQuery
        {
            Filter = new GetBusinessesFilter
            {
                Search = "Shop", Status = BusinessEnums.ACTIVE,
                CreatedFrom = TestData.Now.AddDays(-2), PageIndex = 2, PageSize = 5
            }
        };

        var result = await fixture.BusinessHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().ContainSingle();
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetBusinesses_OrdersNewestFirstAndMapsResponse()
    {
        var fixture = new AdminReadFixture();

        var result = await fixture.BusinessHandler.Handle(new GetBusinessesQuery(), CancellationToken.None);

        result.Data!.Items.First().BusinessName.Should().Be("New Shop");
    }

    [Fact]
    public async Task GetUsers_ExcludesDeletedAndAppliesFiltersWithDefaultOrder()
    {
        var fixture = new AdminReadFixture();
        IQueryable<User>? captured = null;
        string? order = null;
        fixture.UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<User>, IConfigurationProvider, string?, string?, int, int>((query, _, orderBy, _, _, _) =>
            {
                captured = query;
                order = orderBy;
            })
            .ReturnsAsync(fixture.UserPage());
        var query = new GetAllUserQuery
        {
            BusinessName = "Shop", FullName = "Admin", Email = "admin",
            IsEmailVerified = true, Gender = 1, UserStatus = UserStatus.ACTIVE
        };

        await fixture.UserHandler.Handle(query, CancellationToken.None);

        captured!.Should().ContainSingle();
        order.Should().Be("BusinessName asc, JoinedAt desc");
    }

    [Fact]
    public async Task GetUsers_PassesCustomOrderAndPagination()
    {
        var fixture = new AdminReadFixture();
        string? order = null;
        fixture.UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<User>, IConfigurationProvider, string?, string?, int, int>((_, _, orderBy, _, _, _) => order = orderBy)
            .ReturnsAsync(fixture.UserPage(3, 6));

        var result = await fixture.UserHandler.Handle(
            new GetAllUserQuery { OrderBy = "Email desc", PageIndex = 3, PageSize = 6 }, CancellationToken.None);

        order.Should().Be("Email desc");
        result.Data!.PageIndex.Should().Be(3);
        result.Data.PageSize.Should().Be(6);
    }

    [Fact]
    public async Task GetSystemContents_ExcludesDeletedAndAppliesAllFilters()
    {
        var fixture = new AdminReadFixture();
        IQueryable<SystemContent>? captured = null;
        fixture.ContentRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<SystemContent, SystemContentResponse>(
                It.IsAny<IQueryable<SystemContent>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<SystemContent>, IConfigurationProvider, string?, string?, int, int>((query, _, _, _, _, _) => captured = query)
            .ReturnsAsync(fixture.ContentPage());
        var query = new GetAllSystemContentQuery
        {
            Filter = new GetAllSystemContentFilter
            {
                Title = "Welcome", Key = "WELCOME", ContentType = ContentType.Markdown,
                Status = SystemContentStatus.Published
            }
        };

        await fixture.ContentListHandler.Handle(query, CancellationToken.None);

        captured!.Should().ContainSingle().Which.Id.Should().Be(fixture.Content.Id);
    }

    [Fact]
    public async Task GetSystemContents_PassesCustomOrderAndPagination()
    {
        var fixture = new AdminReadFixture();
        string? order = null;
        fixture.ContentRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<SystemContent, SystemContentResponse>(
                It.IsAny<IQueryable<SystemContent>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<SystemContent>, IConfigurationProvider, string?, string?, int, int>((_, _, orderBy, _, _, _) => order = orderBy)
            .ReturnsAsync(fixture.ContentPage(2, 4));

        var result = await fixture.ContentListHandler.Handle(new GetAllSystemContentQuery
        {
            Filter = new GetAllSystemContentFilter { OrderBy = "Title asc", PageIndex = 2, PageSize = 4 }
        }, CancellationToken.None);

        order.Should().Be("Title asc");
        result.Data!.PageIndex.Should().Be(2);
    }

    [Fact]
    public async Task GetSystemContentById_WhenMissingReturnsNotFound_AndWhenFoundMapsResponse()
    {
        var fixture = new AdminReadFixture();
        fixture.ContentRepository.SetupSequence(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .ReturnsAsync((SystemContent?)null)
            .ReturnsAsync(fixture.Content);

        var missing = await fixture.ContentByIdHandler.Handle(
            new GetSystemContentByIdQuery { SystemContentId = fixture.Content.Id }, CancellationToken.None);
        var found = await fixture.ContentByIdHandler.Handle(
            new GetSystemContentByIdQuery { SystemContentId = fixture.Content.Id }, CancellationToken.None);

        missing.StatusCode.Should().Be(404);
        found.Data!.Key.Should().Be("WELCOME");
    }

    [Fact]
    public async Task GetSystemContentByKey_TrimsKeyRequiresPublishedAndMapsResponse()
    {
        var fixture = new AdminReadFixture();
        Expression<Func<SystemContent, bool>>? captured = null;
        fixture.ContentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SystemContent, bool>>>(),
                It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
            .Callback<Expression<Func<SystemContent, bool>>, Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>((predicate, _) => captured = predicate)
            .ReturnsAsync(fixture.Content);

        var result = await fixture.ContentByKeyHandler.Handle(
            new GetSystemContentByKeyQuery { Key = " WELCOME " }, CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Content.Id.ToString());
        captured!.Compile()(fixture.Content).Should().BeTrue();
        var draft = fixture.Content;
        draft.Status = SystemContentStatus.Draft;
        captured.Compile()(draft).Should().BeFalse();
    }

    private sealed class AdminReadFixture
    {
        public List<Business> Businesses { get; } = [];
        public List<User> Users { get; } = [];
        public SystemContent Content { get; }
        public List<SystemContent> Contents { get; } = [];
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ISystemContentRepository> ContentRepository { get; } = new();
        public GetBusinessesQueryHandler BusinessHandler { get; }
        public GetAllUserQueryHandler UserHandler { get; }
        public GetAllSystemContentQueryHandler ContentListHandler { get; }
        public GetSystemContentByIdQueryHandler ContentByIdHandler { get; }
        public GetSystemContentByKeyQueryHandler ContentByKeyHandler { get; }

        public AdminReadFixture()
        {
            var old = TestData.Business();
            old.BusinessName = "Old Shop";
            old.CreatedAt = TestData.Now.AddDays(-3);
            var newest = TestData.Business();
            newest.BusinessName = "New Shop";
            newest.CreatedAt = TestData.Now;
            Businesses.AddRange([old, newest]);
            var admin = TestData.User(newest, role: RoleEnums.ADMIN);
            admin.FullName = "Admin User";
            admin.Email = "admin@example.com";
            admin.Gender = 1;
            var deleted = TestData.User(newest, UserStatus.DELETED, RoleEnums.CATALOG_TEAM);
            Users.AddRange([admin, deleted]);
            Content = new SystemContent
            {
                Id = ObjectId.GenerateNewId(), Title = "Welcome", Key = "WELCOME", Content = "Hello",
                ContentType = ContentType.Markdown, Status = SystemContentStatus.Published,
                CreatedAt = TestData.Now, UpdatedAt = TestData.Now
            };
            Contents.Add(Content);
            Contents.Add(new SystemContent
            {
                Id = ObjectId.GenerateNewId(), Title = "Deleted", Key = "DELETED", Content = "x",
                Status = SystemContentStatus.Deleted, DeletedAt = TestData.Now
            });

            var mapper = new Mock<IMapper>();
            mapper.SetupGet(value => value.ConfigurationProvider).Returns(Mock.Of<IConfigurationProvider>());
            mapper.Setup(value => value.Map<IReadOnlyCollection<BusinessResponse>>(It.IsAny<object>()))
                .Returns((object source) => ((IEnumerable<Business>)source).Select(business => new BusinessResponse
                {
                    Id = business.Id.ToString(), BusinessName = business.BusinessName,
                    BusinessStatus = business.BusinessStatus, CreatedAt = business.CreatedAt
                }).ToList());
            mapper.Setup(value => value.Map<SystemContentResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var content = (SystemContent)source;
                    return new SystemContentResponse { Id = content.Id.ToString(), Title = content.Title, Key = content.Key };
                });

            BusinessRepository.Setup(repository => repository.AsQueryable()).Returns(() => Businesses.AsQueryable());
            BusinessRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<Business>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<Business> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<Business>(items, items.Count, index, size);
                });
            UserRepository.Setup(repository => repository.AsQueryable()).Returns(() => Users.AsQueryable());
            UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                    It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<User> _, IConfigurationProvider _, string? _, string? _, int index, int size) => UserPage(index, size));
            ContentRepository.Setup(repository => repository.AsQueryable()).Returns(() => Contents.AsQueryable());
            ContentRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<SystemContent, SystemContentResponse>(
                    It.IsAny<IQueryable<SystemContent>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<SystemContent> _, IConfigurationProvider _, string? _, string? _, int index, int size) => ContentPage(index, size));
            ContentRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<SystemContent, bool>>>(),
                    It.IsAny<Func<IQueryable<SystemContent>, IQueryable<SystemContent>>?>()))
                .ReturnsAsync(Content);

            BusinessHandler = new GetBusinessesQueryHandler(BusinessRepository.Object, mapper.Object);
            UserHandler = new GetAllUserQueryHandler(UserRepository.Object, mapper.Object);
            ContentListHandler = new GetAllSystemContentQueryHandler(ContentRepository.Object, mapper.Object);
            ContentByIdHandler = new GetSystemContentByIdQueryHandler(ContentRepository.Object, mapper.Object);
            ContentByKeyHandler = new GetSystemContentByKeyQueryHandler(ContentRepository.Object, mapper.Object);
        }

        public BasePaginatedList<object> UserPage(int index = 1, int size = 10) => new(
            [new ProfileResponse { Id = Users[0].Id.ToString(), Email = Users[0].Email }], 1, index, size);

        public BasePaginatedList<object> ContentPage(int index = 1, int size = 10) => new(
            [new SystemContentResponse { Id = Content.Id.ToString(), Key = Content.Key }], 1, index, size);
    }
}
