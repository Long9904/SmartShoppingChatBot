using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Mapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ActivityLogManagement.GetActivityLog;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System.Text.Json;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ActivityLog
{
    [Fact]
    public async Task Handle_AdminDoesNotRequireCurrentBusiness()
    {
        var fixture = new ActivityLogFixture(RoleEnums.ADMIN);
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Business claim is not required for admin"));

        var result = await fixture.Handler.Handle(new GetActivityLogQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.CurrentUser.Verify(service => service.GetBusiness(), Times.Never);
    }

    [Fact]
    public async Task Handle_CustomerRole_ReturnsForbidden()
    {
        var fixture = new ActivityLogFixture(RoleEnums.CUSTOMER);

        var result = await fixture.Handler.Handle(new GetActivityLogQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(403);
        result.IsSuccess.Should().BeFalse();
        fixture.ActivityLogRepository.Verify(repository => repository.PaginatedListAsync(
            It.IsAny<IQueryable<ActivityLog>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BusinessUserScopesToOwnBusinessAndAppliesKeywordSeverityAndInclusiveToDate()
    {
        var fixture = new ActivityLogFixture(RoleEnums.BUSINESS_OWNER);
        var otherBusinessId = ObjectId.GenerateNewId().ToString();
        fixture.Logs.Add(new ActivityLog
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = otherBusinessId,
            ActorId = fixture.User.Id.ToString(),
            ActorEmail = "owner@example.com",
            Action = ActionLogEnums.Update,
            TargetType = nameof(SubscriptionPlan),
            TargetId = ObjectId.GenerateNewId().ToString(),
            Status = StatusLogEnums.Success,
            Severity = SeverityLogEnums.Error,
            Description = "Updated subscription plan",
            CreatedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)
        });

        var result = await fixture.Handler.Handle(new GetActivityLogQuery
        {
            Filter = new GetActivityLogFilter
            {
                Keyword = "subscription",
                Severity = SeverityLogEnums.Error,
                ToDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)
            }
        }, CancellationToken.None);

        result.Data!.Items.Should().ContainSingle();
        var item = result.Data.Items.Single();
        item.BusinessId.Should().Be(fixture.Business.Id.ToString());
        item.Description.Should().Be("Updated subscription plan");
    }

    [Fact]
    public async Task Handle_MapsMetadataJsonToResponseMetadata()
    {
        var fixture = new ActivityLogFixture(RoleEnums.ADMIN);

        var result = await fixture.Handler.Handle(new GetActivityLogQuery
        {
            Filter = new GetActivityLogFilter { Keyword = "plan-1" }
        }, CancellationToken.None);

        var metadata = result.Data!.Items.Single().Metadata.Should().BeOfType<JsonElement>().Subject;
        metadata.GetProperty("SubscriptionId").GetString().Should().Be("plan-1");
    }

    private sealed class ActivityLogFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public List<ActivityLog> Logs { get; } = [];
        public Mock<IActivityLogRepository> ActivityLogRepository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public GetActivityLogQueryHandler Handler { get; }

        public ActivityLogFixture(RoleEnums role)
        {
            User = TestData.User(Business, role: role);
            Logs.Add(new ActivityLog
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = Business.Id.ToString(),
                ActorId = User.Id.ToString(),
                ActorEmail = "owner@example.com",
                ActorRole = role,
                Action = ActionLogEnums.Update,
                TargetType = nameof(SubscriptionPlan),
                TargetId = ObjectId.GenerateNewId().ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Error,
                Description = "Updated subscription plan",
                MetadataJson = """{"SubscriptionId":"plan-1"}""",
                CreatedAt = new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)
            });
            Logs.Add(new ActivityLog
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = Business.Id.ToString(),
                ActorId = User.Id.ToString(),
                ActorEmail = "owner@example.com",
                ActorRole = role,
                Action = ActionLogEnums.Delete,
                TargetType = nameof(SubscriptionPlan),
                TargetId = ObjectId.GenerateNewId().ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = "Deleted subscription plan",
                CreatedAt = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)
            });

            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            ActivityLogRepository.Setup(repository => repository.AsQueryable()).Returns(() => Logs.AsQueryable());
            ActivityLogRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<ActivityLog>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<ActivityLog> query, int index, int pageSize) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<ActivityLog>(items, items.Count, index, pageSize);
                });

            var mapper = new MapperConfiguration(config =>
            {
                config.AddProfile<AutoMapperDI>();
                config.AddProfile<ActivityLogProfile>();
            }, NullLoggerFactory.Instance).CreateMapper();

            Handler = new GetActivityLogQueryHandler(
                UnitOfWork.Object,
                ActivityLogRepository.Object,
                mapper,
                CurrentUser.Object);
        }
    }
}
