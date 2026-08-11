using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.GetAllApiKey;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevealKey;
using SmartShoppingChatBot.Application.Features.BusinessMemberManagement.GetAllBusinessMember;
using SmartShoppingChatBot.Application.Features.BusinessMemberManagement.GetBusinessMemberById;
using SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations;
using SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_CustomerQueries
{
    [Fact]
    public async Task GetCustomers_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new CustomerQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.CustomersHandler.Handle(new GetCustomersQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.CustomerRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetCustomers_ScopesToBusinessAndAppliesTrimmedExternalIdStatusAndPagination()
    {
        var fixture = new CustomerQueryFixture();
        fixture.Customers.Add(TestData.Customer(TestData.Business(), "other-business"));
        var query = new GetCustomersQuery
        {
            Filter = new GetCustomersFilter
            {
                CustomerExternalId = " customer-1 ", Status = CustomerStatus.Active,
                PageIndex = 2, PageSize = 3
            }
        };

        var result = await fixture.CustomersHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().ContainSingle().Which.CustomerExternalId.Should().Be("customer-1");
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(3);
    }

    [Theory]
    [InlineData("customerexternalid asc", "customer-1")]
    [InlineData("name desc", "Zulu")]
    public async Task GetCustomers_AppliesRequestedOrdering(string orderBy, string expectedFirst)
    {
        var fixture = new CustomerQueryFixture();
        var result = await fixture.CustomersHandler.Handle(
            new GetCustomersQuery { Filter = new GetCustomersFilter { OrderBy = orderBy } }, CancellationToken.None);

        var first = result.Data!.Items.First();
        (orderBy.StartsWith("name") ? first.Name : first.CustomerExternalId).Should().Be(expectedFirst);
    }

    [Fact]
    public async Task GetCustomers_MapsCustomerFields()
    {
        var fixture = new CustomerQueryFixture();

        var result = await fixture.CustomersHandler.Handle(new GetCustomersQuery(), CancellationToken.None);

        var mapped = result.Data!.Items.First();
        mapped.Id.Should().NotBeNullOrWhiteSpace();
        mapped.Status.Should().Be(CustomerStatus.Active);
        mapped.CreatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public async Task GetConversations_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new CustomerQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.ConversationsHandler.Handle(fixture.ConversationsQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetConversations_WhenCustomerMissing_ReturnsNotFound()
    {
        var fixture = new CustomerQueryFixture();
        fixture.CustomerRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
            .ReturnsAsync((Customer?)null);

        var result = await fixture.ConversationsHandler.Handle(fixture.ConversationsQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.ConversationRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetConversations_ScopesOrdersNewestFirstAndPassesPagination()
    {
        var fixture = new CustomerQueryFixture();
        var query = fixture.ConversationsQuery();
        query.PageIndex = 2;
        query.PageSize = 4;

        var result = await fixture.ConversationsHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.First().Title.Should().Be("Newest");
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(4);
    }

    [Fact]
    public async Task GetConversations_MapsConversationStatusAndTimestamps()
    {
        var fixture = new CustomerQueryFixture();

        var result = await fixture.ConversationsHandler.Handle(fixture.ConversationsQuery(), CancellationToken.None);

        var mapped = result.Data!.Items.First();
        mapped.Status.Should().Be(ConversationStatus.Active);
        mapped.LastMessageAt.Should().Be(TestData.Now);
        mapped.CreateAt.Should().Be(TestData.Now);
    }

    private sealed class CustomerQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public List<Customer> Customers { get; } = [];
        public Customer Customer => Customers[0];
        public List<Conversation> Conversations { get; } = [];
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<ICustomerRepository> CustomerRepository { get; } = new();
        public Mock<IConversationRepository> ConversationRepository { get; } = new();
        public GetCustomersQueryHandler CustomersHandler { get; }
        public CustomerGetConversationsQueryHandler ConversationsHandler { get; }

        public CustomerQueryFixture()
        {
            var first = TestData.Customer(Business, "customer-1");
            first.Name = "Alpha";
            first.CreatedAt = TestData.Now;
            first.UpdatedAt = TestData.Now;
            var second = TestData.Customer(Business, "customer-2");
            second.Name = "Zulu";
            second.CreatedAt = TestData.Now.AddMinutes(-1);
            Customers.AddRange([first, second]);
            var older = TestData.Conversation(Business, first);
            older.Title = "Older";
            older.CreateAt = TestData.Now.AddDays(-1);
            var newest = TestData.Conversation(Business, first);
            newest.Title = "Newest";
            newest.CreateAt = TestData.Now;
            newest.LastMessageAt = TestData.Now;
            Conversations.AddRange([older, newest]);

            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CustomerRepository.Setup(repository => repository.AsQueryable()).Returns(() => Customers.AsQueryable());
            CustomerRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<Customer>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<Customer> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<Customer>(items, items.Count, index, size);
                });
            CustomerRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Customer, bool>>>(),
                    It.IsAny<Func<IQueryable<Customer>, IQueryable<Customer>>?>()))
                .ReturnsAsync(first);
            ConversationRepository.Setup(repository => repository.AsQueryable()).Returns(() => Conversations.AsQueryable());
            ConversationRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<Conversation>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<Conversation> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<Conversation>(items, items.Count, index, size);
                });
            CustomersHandler = new GetCustomersQueryHandler(CurrentUser.Object, CustomerRepository.Object);
            ConversationsHandler = new CustomerGetConversationsQueryHandler(
                CurrentUser.Object, CustomerRepository.Object, ConversationRepository.Object);
        }

        public CustomerGetConversationsQuery ConversationsQuery() => new()
        {
            ExternalCustomerId = Customer.CustomerExternalId,
            PageIndex = 1,
            PageSize = 10
        };
    }
}

public class UT_BusinessMemberQueries
{
    [Fact]
    public async Task GetAll_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new BusinessMemberQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.GetAllHandler.Handle(new GetBusinessMemberQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UserRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetAll_ScopesToCatalogTeamInBusinessAndExcludesDeleted()
    {
        var fixture = new BusinessMemberQueryFixture();
        IQueryable<User>? captured = null;
        fixture.UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<User>, IConfigurationProvider, string?, string?, int, int>((query, _, _, _, _, _) => captured = query)
            .ReturnsAsync(fixture.Page());

        await fixture.GetAllHandler.Handle(new GetBusinessMemberQuery(), CancellationToken.None);

        captured!.Should().ContainSingle().Which.Id.Should().Be(fixture.Member.Id);
    }

    [Fact]
    public async Task GetAll_AppliesFiltersAndDefaultsOrdering()
    {
        var fixture = new BusinessMemberQueryFixture();
        string? capturedOrder = null;
        IQueryable<User>? captured = null;
        fixture.UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<IQueryable<User>, IConfigurationProvider, string?, string?, int, int>((query, _, order, _, _, _) =>
            {
                captured = query;
                capturedOrder = order;
            })
            .ReturnsAsync(fixture.Page());
        var query = new GetBusinessMemberQuery
        {
            Filter = new GetBusinessMemberFilter
            {
                Email = "member", FullName = "Catalog", IsEmailVerified = true,
                Gender = 1, UserStatus = UserStatus.ACTIVE
            }
        };

        await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        capturedOrder.Should().Be("JoinedAt desc");
        captured!.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_PassesRequestedPaginationAndReturnsRepositoryPage()
    {
        var fixture = new BusinessMemberQueryFixture();
        var query = new GetBusinessMemberQuery
        {
            Filter = new GetBusinessMemberFilter { PageIndex = 3, PageSize = 7, OrderBy = "Email asc" }
        };

        var result = await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        result.Data!.PageIndex.Should().Be(3);
        result.Data.PageSize.Should().Be(7);
    }

    [Fact]
    public async Task GetById_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new BusinessMemberQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.GetByIdHandler.Handle(
            new GetBusinessMemberByIdQuery { MemberId = fixture.Member.Id }, CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetById_WhenMemberMissing_ReturnsNotFound()
    {
        var fixture = new BusinessMemberQueryFixture();
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .ReturnsAsync((User?)null);

        var result = await fixture.GetByIdHandler.Handle(
            new GetBusinessMemberByIdQuery { MemberId = ObjectId.GenerateNewId() }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetById_ValidMember_UsesBusinessRoleStatusGuardsAndMapsResponse()
    {
        var fixture = new BusinessMemberQueryFixture();
        Expression<Func<User, bool>>? captured = null;
        fixture.UserRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
            .Callback<Expression<Func<User, bool>>, Func<IQueryable<User>, IQueryable<User>>?>((predicate, _) => captured = predicate)
            .ReturnsAsync(fixture.Member);

        var result = await fixture.GetByIdHandler.Handle(
            new GetBusinessMemberByIdQuery { MemberId = fixture.Member.Id }, CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Member.Id.ToString());
        captured!.Compile()(fixture.Member).Should().BeTrue();
        captured.Compile()(fixture.Owner).Should().BeFalse();
    }

    private sealed class BusinessMemberQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public User Owner { get; }
        public User Member { get; }
        public List<User> Users { get; }
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public GetBusinessMemberQueryHandler GetAllHandler { get; }
        public GetBusinessMemberByIdQueryHandler GetByIdHandler { get; }

        public BusinessMemberQueryFixture()
        {
            Owner = TestData.User(Business);
            Member = TestData.User(Business, role: RoleEnums.CATALOG_TEAM);
            Member.FullName = "Catalog Member";
            Member.Email = "member@example.com";
            Member.Gender = 1;
            var deleted = TestData.User(Business, UserStatus.DELETED, RoleEnums.CATALOG_TEAM);
            var other = TestData.User(TestData.Business(), role: RoleEnums.CATALOG_TEAM);
            Users = [Owner, Member, deleted, other];
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            UserRepository.Setup(repository => repository.AsQueryable()).Returns(() => Users.AsQueryable());
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(Member);
            var mapper = new Mock<IMapper>();
            mapper.SetupGet(value => value.ConfigurationProvider).Returns(Mock.Of<IConfigurationProvider>());
            mapper.Setup(value => value.Map<ProfileResponse>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var user = (User)source;
                    return new ProfileResponse { Id = user.Id.ToString(), Email = user.Email, FullName = user.FullName };
                });
            UserRepository.Setup(repository => repository.GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>(
                    It.IsAny<IQueryable<User>>(), It.IsAny<IConfigurationProvider>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<User> _, IConfigurationProvider _, string? _, string? _, int index, int size) => Page(index, size));
            GetAllHandler = new GetBusinessMemberQueryHandler(UserRepository.Object, CurrentUser.Object, mapper.Object);
            GetByIdHandler = new GetBusinessMemberByIdQueryHandler(UserRepository.Object, CurrentUser.Object, mapper.Object);
        }

        public BasePaginatedList<object> Page(int index = 1, int size = 10) => new(
            [new ProfileResponse { Id = Member.Id.ToString(), Email = Member.Email, FullName = Member.FullName }],
            1, index, size);
    }
}

public class UT_ApiKeyQueries
{
    [Fact]
    public async Task Reveal_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.RevealHandler.Handle(fixture.RevealQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Repository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<ApiKey, bool>>>(),
            It.IsAny<Func<IQueryable<ApiKey>, IQueryable<ApiKey>>?>()), Times.Never);
    }

    [Fact]
    public async Task Reveal_WhenKeyMissing_ReturnsNotFound()
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<ApiKey, bool>>>(),
                It.IsAny<Func<IQueryable<ApiKey>, IQueryable<ApiKey>>?>()))
            .ReturnsAsync((ApiKey?)null);

        var result = await fixture.RevealHandler.Handle(fixture.RevealQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Reveal_WhenKeyBelongsToAnotherBusiness_ReturnsForbidden()
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.Key.BusinessId = ObjectId.GenerateNewId();

        var result = await fixture.RevealHandler.Handle(fixture.RevealQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.Hash.Verify(service => service.Decrypt(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(KeyStatus.Revoked)]
    [InlineData(KeyStatus.Inactive)]
    public async Task Reveal_WhenKeyInactive_ReturnsBadRequest(KeyStatus status)
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.Key.Status = status;

        var result = await fixture.RevealHandler.Handle(fixture.RevealQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Reveal_ValidKey_DecryptsAndReturnsFullKeyOnce()
    {
        var fixture = new ApiKeyQueryFixture();

        var result = await fixture.RevealHandler.Handle(fixture.RevealQuery(), CancellationToken.None);

        result.Data.Should().Be("key-id.secret-value");
        fixture.Hash.Verify(service => service.Decrypt("encrypted"), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.GetAllHandler.Handle(new GetAllApiKeyQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.Repository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyActiveBusinessKeysNewestFirstWithMaskAndPagination()
    {
        var fixture = new ApiKeyQueryFixture();
        fixture.Keys.Add(new ApiKey
        {
            Id = ObjectId.GenerateNewId(), BusinessId = fixture.Business.Id, Name = "Newest",
            KeyId = "new-key", EncryptedSecret = "x", Status = KeyStatus.Active,
            CreatedAt = TestData.Now.AddHours(1)
        });
        fixture.Keys.Add(new ApiKey
        {
            Id = ObjectId.GenerateNewId(), BusinessId = fixture.Business.Id, Name = "Revoked",
            KeyId = "revoked", EncryptedSecret = "x", Status = KeyStatus.Revoked,
            CreatedAt = TestData.Now.AddHours(2)
        });
        var query = new GetAllApiKeyQuery { PageIndex = 2, PageSize = 4 };

        var result = await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.First().KeyId.Should().Be("new-key");
        result.Data.Items.Should().OnlyContain(key => key.Status == KeyStatus.Active && key.MaskedKey.EndsWith("************"));
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(4);
    }

    private sealed class ApiKeyQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public ApiKey Key { get; }
        public List<ApiKey> Keys { get; } = [];
        public Mock<IApiKeyRepository> Repository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IHashService> Hash { get; } = new();
        public RevealKeyQueryHandler RevealHandler { get; }
        public GetAllApiKeyQueryHandler GetAllHandler { get; }

        public ApiKeyQueryFixture()
        {
            Key = new ApiKey
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, Name = "Primary",
                KeyId = "key-id", EncryptedSecret = "encrypted", HashKey = "hash",
                Status = KeyStatus.Active, CreatedAt = TestData.Now
            };
            Keys.Add(Key);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            Repository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<ApiKey, bool>>>(),
                    It.IsAny<Func<IQueryable<ApiKey>, IQueryable<ApiKey>>?>()))
                .ReturnsAsync(Key);
            Repository.Setup(repository => repository.AsQueryable()).Returns(() => Keys.AsQueryable());
            Repository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<ApiKey>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<ApiKey> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<ApiKey>(items, items.Count, index, size);
                });
            Hash.Setup(service => service.Decrypt("encrypted")).Returns("secret-value");
            RevealHandler = new RevealKeyQueryHandler(Repository.Object, CurrentUser.Object, Hash.Object);
            GetAllHandler = new GetAllApiKeyQueryHandler(CurrentUser.Object, Repository.Object);
        }

        public RevealKeyQuery RevealQuery() => new() { KeyId = Key.KeyId };
    }
}
