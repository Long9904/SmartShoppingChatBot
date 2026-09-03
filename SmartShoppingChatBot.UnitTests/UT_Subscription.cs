using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Mapper;
using SmartShoppingChatBot.Application.Features.SubscriptionManagement.CreateSubscription;
using SmartShoppingChatBot.Application.Features.SubscriptionManagement.DeleteSubscription;
using SmartShoppingChatBot.Application.Features.SubscriptionManagement.GetAllSubscription;
using SmartShoppingChatBot.Application.Features.SubscriptionManagement.ResetSubscription;
using SmartShoppingChatBot.Application.Features.SubscriptionManagement.UpdateSubscription;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_SubscriptionPlanManagement
{
    [Fact]
    public async Task Create_WhenNameAlreadyExists_ReturnsBadRequestWithoutSaving()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.ExistingPlan);

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.Repository.Verify(repository => repository.AddAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ValidRequest_CreatesActivePlanAndSaves()
    {
        var fixture = new SubscriptionFixture();
        SubscriptionPlan? added = null;
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);
        fixture.Repository.Setup(repository => repository.AddAsync(It.IsAny<SubscriptionPlan>()))
            .Callback<SubscriptionPlan>(plan => added = plan)
            .Returns(Task.CompletedTask);

        var result = await fixture.CreateHandler.Handle(fixture.CreateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(201);
        added!.Name.Should().Be("Growth");
        added.Description.Should().Be("For growing shops");
        added.Price.Should().Be(99);
        added.MaxDocumentAllowed.Should().Be(20);
        added.Status.Should().Be(StatusEnums.Active);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenSearchAndStatusProvided_ReturnsMatchingPlansOrderedByLevel()
    {
        var fixture = new SubscriptionFixture();
        var starter = fixture.Plan("Starter", level: 1);
        var archivedGrowth = fixture.Plan("Growth Archived", StatusEnums.Inactive, level: 2);
        var growth = fixture.Plan("Growth", level: 3);
        fixture.Repository.Setup(repository => repository.AsQueryable())
            .Returns(new[] { growth, archivedGrowth, starter }.AsQueryable());
        fixture.Repository.Setup(repository => repository.PaginatedListAsync(
                It.IsAny<IQueryable<SubscriptionPlan>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((IQueryable<SubscriptionPlan> query, int pageIndex, int pageSize) =>
                new BasePaginatedList<SubscriptionPlan>(
                    query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
                    query.Count(),
                    pageIndex,
                    pageSize));

        var result = await fixture.GetAllHandler.Handle(new GetSubscriptionQuery
        {
            Filter = new GetSubscriptionFilter
            {
                Search = "Growth",
                Status = StatusEnums.Active,
                PageIndex = 1,
                PageSize = 10
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Single();
        item.Id.Should().Be(growth.Id.ToString());
        item.Name.Should().Be("Growth");
        item.Status.Should().Be(StatusEnums.Active);
    }

    [Fact]
    public async Task GetAll_WhenPagingRequested_ReturnsPlansOrderedByLevelWithMetadata()
    {
        var fixture = new SubscriptionFixture();
        var starter = fixture.Plan("Starter", level: 1);
        var growth = fixture.Plan("Growth", level: 2);
        var scale = fixture.Plan("Scale", level: 3);
        fixture.Repository.Setup(repository => repository.AsQueryable())
            .Returns(new[] { scale, starter, growth }.AsQueryable());
        fixture.Repository.Setup(repository => repository.PaginatedListAsync(
                It.IsAny<IQueryable<SubscriptionPlan>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((IQueryable<SubscriptionPlan> query, int pageIndex, int pageSize) =>
                new BasePaginatedList<SubscriptionPlan>(
                    query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
                    query.Count(),
                    pageIndex,
                    pageSize));

        var result = await fixture.GetAllHandler.Handle(new GetSubscriptionQuery
        {
            Filter = new GetSubscriptionFilter
            {
                PageIndex = 2,
                PageSize = 1
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Single().Id.Should().Be(growth.Id.ToString());
        result.Data.PageIndex.Should().Be(2);
        result.Data.TotalItems.Should().Be(3);
        result.Data.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Update_WhenIdFormatInvalid_ReturnsBadRequestWithoutLookup()
    {
        var fixture = new SubscriptionFixture();
        var command = fixture.UpdateCommand();
        command.Id = "not-object-id";

        var result = await fixture.UpdateHandler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.Repository.Verify(repository => repository.GetByIdAsync(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenPlanMissing_ReturnsNotFoundWithoutSaving()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.GetByIdAsync(fixture.ExistingPlan.Id))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenAnotherPlanHasSameName_ReturnsConflictWithoutSaving()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.GetByIdAsync(fixture.ExistingPlan.Id))
            .ReturnsAsync(fixture.ExistingPlan);
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.Plan("Growth Plus"));
        var command = fixture.UpdateCommand();
        command.Name = "Growth Plus";

        var result = await fixture.UpdateHandler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenNoValuesChange_ReturnsSuccessWithoutSaving()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.GetByIdAsync(fixture.ExistingPlan.Id))
            .ReturnsAsync(fixture.ExistingPlan);
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await fixture.UpdateHandler.Handle(fixture.UpdateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("No changes to update.");
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenValuesChange_UpdatesPlanAndSaves()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.GetByIdAsync(fixture.ExistingPlan.Id))
            .ReturnsAsync(fixture.ExistingPlan);
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);
        var command = fixture.UpdateCommand();
        command.Name = "Scale";
        command.Price = 149;
        command.MaxDocumentAllowed = 30;

        var result = await fixture.UpdateHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.ExistingPlan.Name.Should().Be("Scale");
        fixture.ExistingPlan.Price.Should().Be(149);
        fixture.ExistingPlan.MaxDocumentAllowed.Should().Be(30);
        fixture.Repository.Verify(repository => repository.UpdateAsync(fixture.ExistingPlan), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenIdFormatInvalid_ReturnsBadRequestWithoutLookup()
    {
        var fixture = new SubscriptionFixture();

        var result = await fixture.DeleteHandler.Handle(new DeleteSubscriptionCommand { Id = "invalid" }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.Repository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
            It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenPlanMissing_ReturnsNotFoundWithoutUpdate()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await fixture.DeleteHandler.Handle(
            new DeleteSubscriptionCommand { Id = fixture.ExistingPlan.Id.ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenActiveBusinessSubscriptionExists_ReturnsBadRequestWithoutUpdate()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.ExistingPlan);
        fixture.SubscriptionRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<BusinessSubscription, bool>>>(),
                It.IsAny<Func<IQueryable<BusinessSubscription>, IQueryable<BusinessSubscription>>?>()))
            .ReturnsAsync(new BusinessSubscription
            {
                Id = ObjectId.GenerateNewId(),
                SubscriptionPlanId = fixture.ExistingPlan.Id,
                Status = StatusEnums.Active
            });

        var result = await fixture.DeleteHandler.Handle(
            new DeleteSubscriptionCommand { Id = fixture.ExistingPlan.Id.ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.Repository.Verify(repository => repository.UpdateAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenNoActiveBusinessSubscriptionExists_MarksPlanInactive()
    {
        var fixture = new SubscriptionFixture();
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.ExistingPlan);
        fixture.SubscriptionRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<BusinessSubscription, bool>>>(),
                It.IsAny<Func<IQueryable<BusinessSubscription>, IQueryable<BusinessSubscription>>?>()))
            .ReturnsAsync((BusinessSubscription?)null);

        var result = await fixture.DeleteHandler.Handle(
            new DeleteSubscriptionCommand { Id = fixture.ExistingPlan.Id.ToString() },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.ExistingPlan.Status.Should().Be(StatusEnums.Inactive);
        fixture.Repository.Verify(repository => repository.UpdateAsync(fixture.ExistingPlan), Times.Once);
    }

    [Fact]
    public async Task ExpirationJob_WhenBasicPlanMissing_DoesNotChangeSubscriptions()
    {
        var fixture = new SubscriptionFixture();
        var expired = fixture.BusinessSubscription(fixture.ExistingPlan.Id, TestData.Now.AddDays(-1));
        fixture.SubscriptionRepository.Setup(repository => repository.FilterByAsync(
                It.IsAny<Expression<Func<BusinessSubscription, bool>>>(),
                It.IsAny<Func<IQueryable<BusinessSubscription>, IQueryable<BusinessSubscription>>?>()))
            .ReturnsAsync([expired]);
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        await fixture.ExpirationJob.Execute(Mock.Of<Quartz.IJobExecutionContext>());

        expired.Status.Should().Be(StatusEnums.Active);
        fixture.SubscriptionRepository.Verify(repository => repository.UpdateRangeAsync(
            It.IsAny<IEnumerable<BusinessSubscription>>()), Times.Never);
        fixture.SubscriptionRepository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<BusinessSubscription>>()), Times.Never);
        fixture.QuotaRepository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<BusinessQuota>>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpirationJob_WhenActiveSubscriptionsExpired_CreatesBasicSubscriptionsAndQuotas()
    {
        var fixture = new SubscriptionFixture();
        var basic = fixture.Plan("Basic", level: 0);
        basic.TokenLimit = 10_000;
        basic.MessageLimit = 100;
        basic.MaxProductAllowed = 100;
        basic.MaxDocumentAllowed = 15;
        var expired = fixture.BusinessSubscription(fixture.ExistingPlan.Id, TestData.Now.AddDays(-1));
        List<BusinessSubscription>? closedSubscriptions = null;
        List<BusinessSubscription>? newSubscriptions = null;
        List<BusinessQuota>? newQuotas = null;
        fixture.SubscriptionRepository.Setup(repository => repository.FilterByAsync(
                It.IsAny<Expression<Func<BusinessSubscription, bool>>>(),
                It.IsAny<Func<IQueryable<BusinessSubscription>, IQueryable<BusinessSubscription>>?>()))
            .ReturnsAsync([expired]);
        fixture.Repository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(basic);
        fixture.SubscriptionRepository.Setup(repository => repository.UpdateRangeAsync(It.IsAny<IEnumerable<BusinessSubscription>>()))
            .Callback<IEnumerable<BusinessSubscription>>(subscriptions => closedSubscriptions = subscriptions.ToList())
            .Returns(Task.CompletedTask);
        fixture.SubscriptionRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<BusinessSubscription>>()))
            .Callback<IEnumerable<BusinessSubscription>>(subscriptions => newSubscriptions = subscriptions.ToList())
            .Returns(Task.CompletedTask);
        fixture.QuotaRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<BusinessQuota>>()))
            .Callback<IEnumerable<BusinessQuota>>(quotas => newQuotas = quotas.ToList())
            .Returns(Task.CompletedTask);

        await fixture.ExpirationJob.Execute(Mock.Of<Quartz.IJobExecutionContext>());

        closedSubscriptions.Should().ContainSingle().Which.Id.Should().Be(expired.Id);
        expired.Status.Should().Be(StatusEnums.Inactive);
        newSubscriptions.Should().ContainSingle();
        newSubscriptions![0].BusinessId.Should().Be(expired.BusinessId);
        newSubscriptions[0].SubscriptionPlanId.Should().Be(basic.Id);
        newSubscriptions[0].Status.Should().Be(StatusEnums.Active);
        newQuotas.Should().ContainSingle();
        newQuotas![0].BusinessId.Should().Be(expired.BusinessId);
        newQuotas[0].BusinessSubscriptionId.Should().Be(newSubscriptions[0].Id);
        newQuotas[0].TokenLimit.Should().Be(basic.TokenLimit);
        newQuotas[0].MessageLimit.Should().Be(basic.MessageLimit);
        newQuotas[0].MaxProductAllowed.Should().Be(basic.MaxProductAllowed);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class SubscriptionFixture
    {
        public SubscriptionPlan ExistingPlan { get; }
        public Mock<ISubscriptionPlanRepository> Repository { get; } = new();
        public Mock<ISubscriptionRepository> SubscriptionRepository { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IActivityLogRepository> ActivityLogRepository { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public SubscriptionAddCommandHandler CreateHandler { get; }
        public GetSubscriptionQueryHandle GetAllHandler { get; }
        public SubscriptionUpdateCommandHandler UpdateHandler { get; }
        public DeleteSubscriptionCommandHandler DeleteHandler { get; }
        public ResetExpiredSubscriptionJob ExpirationJob { get; }

        public SubscriptionFixture()
        {
            ExistingPlan = Plan("Growth", level: 2);
            var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<SubscriptionProfile>(),
                NullLoggerFactory.Instance).CreateMapper();
            CreateHandler = new SubscriptionAddCommandHandler(
                Repository.Object,
                UnitOfWork.Object,
                Mock.Of<ILogger<SubscriptionAddCommandHandler>>(),
                new FixedTimeProvider(TestData.Now),
                mapper,
                Mock.Of<IActivityLogService>());
            GetAllHandler = new GetSubscriptionQueryHandle(Repository.Object, mapper);
            UpdateHandler = new SubscriptionUpdateCommandHandler(
                Repository.Object,
                UnitOfWork.Object,
                Mock.Of<ILogger<SubscriptionUpdateCommandHandler>>(),
                new FixedTimeProvider(TestData.Now),
                mapper,
                Mock.Of<IActivityLogService>());
            DeleteHandler = new DeleteSubscriptionCommandHandler(
                Repository.Object,
                SubscriptionRepository.Object,
                UnitOfWork.Object,
                Mock.Of<IActivityLogService>());
            ExpirationJob = new ResetExpiredSubscriptionJob(
                Repository.Object,
                SubscriptionRepository.Object,
                QuotaRepository.Object,
                UnitOfWork.Object,
                new FixedTimeProvider(TestData.Now),
                Mock.Of<ILogger<ResetExpiredSubscriptionJob>>());
        }

        public SubscriptionPlan Plan(
            string name,
            StatusEnums status = StatusEnums.Active,
            int level = 2) => new()
            {
                Id = ObjectId.GenerateNewId(),
                Name = name,
                Description = "For growing shops",
                Price = 99,
                Duration = 30,
                Level = level,
                TokenLimit = 100_000,
                MessageLimit = 1_000,
                MaxProductAllowed = 500,
                MaxDocumentAllowed = 20,
                Status = status
            };

        public SubscriptionAddCommand CreateCommand() => new()
        {
            Name = "Growth",
            Description = "For growing shops",
            Price = 99,
            Duration = 30,
            Level = 2,
            TokenLimit = 100_000,
            MessageLimit = 1_000,
            MaxProductAllowed = 500,
            MaxDocmentAllowed = 20
        };

        public SubscriptionUpdateCommand UpdateCommand() => new()
        {
            Id = ExistingPlan.Id.ToString(),
            Name = ExistingPlan.Name,
            Description = ExistingPlan.Description,
            Price = ExistingPlan.Price,
            Duration = ExistingPlan.Duration,
            Level = ExistingPlan.Level,
            TokenLimit = ExistingPlan.TokenLimit,
            MessageLimit = ExistingPlan.MessageLimit,
            MaxProductAllowed = ExistingPlan.MaxProductAllowed,
            MaxDocumentAllowed = ExistingPlan.MaxDocumentAllowed
        };

        public BusinessSubscription BusinessSubscription(ObjectId planId, DateTimeOffset endDate) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = ObjectId.GenerateNewId(),
            SubscriptionPlanId = planId,
            StartDate = endDate.AddDays(-30),
            EndDate = endDate,
            Status = StatusEnums.Active
        };
    }
}
