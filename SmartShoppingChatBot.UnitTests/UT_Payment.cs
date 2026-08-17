using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Mapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPayment;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPaymentByUser;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetPaymentByBusinessLogin;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetPaymentByOrderCode;
using SmartShoppingChatBot.Application.Features.PaymentManagement.SendBillCompleted;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using AdminGetPaymentHandler = SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPayment.GetPaymentByUserQueryHandler;
using LoginGetPaymentHandler = SmartShoppingChatBot.Application.Features.PaymentManagement.GetPaymentByBusinessLogin.GetPaymentByLoginQueryHandler;
using UserGetPaymentHandler = SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPaymentByUser.GetPaymentByUserQueryHandler;

namespace SmartShoppingChatBot.UnitTests;

public class UT_PaymentQueries
{
    [Fact]
    public async Task GetAllPayments_WhenFiltersProvided_ReturnsMatchingPaymentsWithBusinessAndPlan()
    {
        var fixture = new PaymentQueryFixture();
        var first = fixture.Payment("Growth invoice", PaymentEnums.Completed, TestData.Now.AddDays(-1));
        var second = fixture.Payment("Starter invoice", PaymentEnums.Pending, TestData.Now);
        fixture.SetupPayments([second, first]);
        fixture.SetupBusinessAndPlans([first]);

        var result = await fixture.AdminHandler.Handle(new GetPaymentQuery
        {
            Filter = new GetPaymentFilter
            {
                Search = "Growth",
                PaymentEnums = PaymentEnums.Completed,
                CreateAtOrderBy = "asc",
                PageIndex = 1,
                PageSize = 10
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Single();
        item.Id.Should().Be(first.Id.ToString());
        item.Bussiness.Id.Should().Be(fixture.Business.Id.ToString());
        item.SubscriptionPlan.Id.Should().Be(fixture.Plan.Id.ToString());
        item.Status.Should().Be(PaymentEnums.Completed);
    }

    [Fact]
    public async Task GetAllPayments_WhenDescendingOrderRequested_ReturnsNewestPaymentFirst()
    {
        var fixture = new PaymentQueryFixture();
        var older = fixture.Payment("Older invoice", PaymentEnums.Pending, TestData.Now.AddDays(-2));
        var newer = fixture.Payment("Newer invoice", PaymentEnums.Pending, TestData.Now);
        fixture.SetupPayments([older, newer]);
        fixture.SetupBusinessAndPlans([older, newer]);

        var result = await fixture.AdminHandler.Handle(new GetPaymentQuery
        {
            Filter = new GetPaymentFilter { CreateAtOrderBy = "desc", PageIndex = 1, PageSize = 10 }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.First().Id.Should().Be(newer.Id.ToString());
    }

    [Fact]
    public async Task GetPaymentsByUser_WhenBusinessMissing_ReturnsNotFoundWithoutQueryingPayments()
    {
        var fixture = new PaymentQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(404, "User not found"));

        var result = await fixture.UserHandler.Handle(new GetPaymentByUserQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.PaymentRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetPaymentsByUser_WhenBusinessExists_ReturnsOnlyCurrentBusinessPayments()
    {
        var fixture = new PaymentQueryFixture();
        var owned = fixture.Payment("Owned invoice", PaymentEnums.Completed, TestData.Now);
        var other = fixture.Payment("Other invoice", PaymentEnums.Completed, TestData.Now, ObjectId.GenerateNewId());
        fixture.SetupPayments([owned, other]);
        fixture.SetupBusinessAndPlans([owned]);

        var result = await fixture.UserHandler.Handle(new GetPaymentByUserQuery
        {
            Filter = new GetPaymentFilterByUser
            {
                PaymentEnums = PaymentEnums.Completed,
                PageIndex = 1,
                PageSize = 10
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.Single().Id.Should().Be(owned.Id.ToString());
    }

    [Fact]
    public async Task GetPaymentByOrderCode_WhenCurrentBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new PaymentQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.OrderCodeHandler.Handle(
            new GetPaymentByOrderCodeQuery { OrderCode = 123456 },
            CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.PaymentRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Payment, bool>>>(),
            It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()), Times.Never);
    }

    [Fact]
    public async Task GetPaymentByOrderCode_WhenPaymentMissing_ReturnsNotFound()
    {
        var fixture = new PaymentQueryFixture();
        fixture.PaymentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Payment, bool>>>(),
                It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
            .ReturnsAsync((Payment?)null);

        var result = await fixture.OrderCodeHandler.Handle(
            new GetPaymentByOrderCodeQuery { OrderCode = 123456 },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Payment not found");
    }

    [Fact]
    public async Task GetPaymentByOrderCode_WhenPaymentExists_ReturnsPaymentWithBusinessAndPlan()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Growth invoice", PaymentEnums.Completed, TestData.Now);
        fixture.PaymentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Payment, bool>>>(),
                It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
            .ReturnsAsync(payment);
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync(fixture.Business);
        fixture.PlanRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.Plan);

        var result = await fixture.OrderCodeHandler.Handle(
            new GetPaymentByOrderCodeQuery { OrderCode = payment.OrderCode },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.OrderCode.Should().Be(payment.OrderCode);
        result.Data.Bussiness.Id.Should().Be(fixture.Business.Id.ToString());
        result.Data.SubscriptionPlan.Id.Should().Be(fixture.Plan.Id.ToString());
    }

    [Fact]
    public async Task GetPaymentByBusinessLogin_WhenCurrentBusinessFails_ReturnsOriginalFailureWithoutLookup()
    {
        var fixture = new PaymentQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.LoginHandler.Handle(
            new GetPaymentByLoginQuery { OrderCode = 987654 },
            CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.PaymentRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Payment, bool>>>(),
            It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()), Times.Never);
    }

    [Fact]
    public async Task GetPaymentByBusinessLogin_WhenPaymentMissing_ReturnsNotFound()
    {
        var fixture = new PaymentQueryFixture();
        fixture.PaymentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Payment, bool>>>(),
                It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
            .ReturnsAsync((Payment?)null);

        var result = await fixture.LoginHandler.Handle(
            new GetPaymentByLoginQuery { OrderCode = 987654 },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Payment not found");
    }

    [Fact]
    public async Task GetPaymentByBusinessLogin_WhenPaymentExists_ReturnsMappedPaymentForCurrentBusiness()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Current business invoice", PaymentEnums.Completed, TestData.Now);
        fixture.PaymentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Payment, bool>>>(),
                It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
            .ReturnsAsync(payment);
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync(fixture.Business);
        fixture.PlanRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync(fixture.Plan);

        var result = await fixture.LoginHandler.Handle(
            new GetPaymentByLoginQuery { OrderCode = payment.OrderCode },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Id.Should().Be(payment.Id.ToString());
        result.Data.Bussiness.Id.Should().Be(fixture.Business.Id.ToString());
        result.Data.SubscriptionPlan.Id.Should().Be(fixture.Plan.Id.ToString());
    }

    [Fact]
    public async Task SendBillCompleted_WhenPaymentIdInvalid_ReturnsBadRequestWithoutLookup()
    {
        var fixture = new PaymentQueryFixture();

        var result = await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = "invalid" },
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.PaymentRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Payment, bool>>>(),
            It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()), Times.Never);
    }

    [Fact]
    public async Task SendBillCompleted_WhenCompletedPaymentMissing_ReturnsNotFound()
    {
        var fixture = new PaymentQueryFixture();
        fixture.PaymentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Payment, bool>>>(),
                It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
            .ReturnsAsync((Payment?)null);

        var result = await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = ObjectId.GenerateNewId().ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Payment not found or not completed");
    }

    [Fact]
    public async Task SendBillCompleted_WhenBusinessMissing_ReturnsNotFoundWithoutSendingEmail()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Completed invoice", PaymentEnums.Completed, TestData.Now);
        fixture.SetupSendBillPayment(payment);
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync((Business?)null);

        var result = await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = payment.Id.ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Business not found");
        fixture.EmailService.Verify(service => service.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendBillCompleted_WhenPlanMissing_ReturnsNotFoundWithoutSendingEmail()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Completed invoice", PaymentEnums.Completed, TestData.Now);
        fixture.SetupSendBillPayment(payment);
        fixture.SetupSendBillBusinessAndUser();
        fixture.PlanRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = payment.Id.ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Subscription plan not found or not active");
        fixture.EmailService.Verify(service => service.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendBillCompleted_WhenEmailServiceThrows_PropagatesFailure()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Completed invoice", PaymentEnums.Completed, TestData.Now);
        fixture.SetupSuccessfulSendBillDependencies(payment);
        fixture.EmailService.Setup(service => service.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var act = async () => await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = payment.Id.ToString() },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SMTP down");
    }

    [Fact]
    public async Task SendBillCompleted_WhenDependenciesExist_RendersTemplateAndSendsBillEmail()
    {
        var fixture = new PaymentQueryFixture();
        var payment = fixture.Payment("Completed invoice", PaymentEnums.Completed, TestData.Now);
        fixture.SetupSuccessfulSendBillDependencies(payment);

        var result = await fixture.SendBillHandler.Handle(
            new SendBillCompletedCommand { PaymentId = payment.Id.ToString() },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("Bill sent successfully");
        fixture.TemplateService.Verify(service => service.RenderEmailTemplateAsync(
            "BillCompleted",
            It.Is<BillCompletedEmailModel>(model =>
                model.BusinessName == fixture.Business.BusinessName
                && model.OrderCode == payment.OrderCode
                && model.SubscriptionName == fixture.Plan.Name
                && model.InvoiceId == payment.Id.ToString())), Times.Once);
        fixture.EmailService.Verify(service => service.SendEmailAsync(
            fixture.Owner.Email,
            "Bill Completed",
            "<html>bill</html>"), Times.Once);
    }

    private sealed class PaymentQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public SubscriptionPlan Plan { get; } = new()
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Growth",
            Price = 99,
            Status = StatusEnums.Active
        };
        public User Owner { get; }

        public Mock<IPaymentRepository> PaymentRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<ISubscriptionPlanRepository> PlanRepository { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IEmailService> EmailService { get; } = new();
        public Mock<IEmailTemplateService> TemplateService { get; } = new();
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<ISubscriptionRepository> SubscriptionRepository { get; } = new();
        public AdminGetPaymentHandler AdminHandler { get; }
        public UserGetPaymentHandler UserHandler { get; }
        public GetPaymentByOrderCodeQueryHandler OrderCodeHandler { get; }
        public LoginGetPaymentHandler LoginHandler { get; }
        public SendBillCompletedCommandHandler SendBillHandler { get; }

        public PaymentQueryFixture()
        {
            Owner = TestData.User(Business);
            var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<PaymentProfile>();
                cfg.AddProfile<SubscriptionProfile>();
            }, NullLoggerFactory.Instance).CreateMapper();

            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            AdminHandler = new AdminGetPaymentHandler(
                PaymentRepository.Object,
                UnitOfWork.Object,
                BusinessRepository.Object,
                PlanRepository.Object,
                mapper,
                CurrentUser.Object);
            UserHandler = new UserGetPaymentHandler(
                PaymentRepository.Object,
                UnitOfWork.Object,
                BusinessRepository.Object,
                PlanRepository.Object,
                mapper,
                CurrentUser.Object);
            OrderCodeHandler = new GetPaymentByOrderCodeQueryHandler(
                PaymentRepository.Object,
                mapper,
                CurrentUser.Object,
                BusinessRepository.Object,
                PlanRepository.Object);
            LoginHandler = new LoginGetPaymentHandler(
                PaymentRepository.Object,
                mapper,
                CurrentUser.Object,
                BusinessRepository.Object,
                PlanRepository.Object);
            SendBillHandler = new SendBillCompletedCommandHandler(
                PaymentRepository.Object,
                EmailService.Object,
                UnitOfWork.Object,
                PlanRepository.Object,
                Mock.Of<ILogger<SendBillCompletedCommandHandler>>(),
                TemplateService.Object,
                UserRepository.Object,
                BusinessRepository.Object,
                SubscriptionRepository.Object);
        }

        public Payment Payment(
            string description,
            PaymentEnums status,
            DateTimeOffset createdAt,
            ObjectId? businessId = null) => new()
            {
                Id = ObjectId.GenerateNewId(),
                BussinessId = businessId ?? Business.Id,
                SubscriptionPlanId = Plan.Id,
                OrderCode = Random.Shared.NextInt64(100_000, 999_999),
                Amount = 99,
                Description = description,
                PayOsPaymentLink = "https://pay.example/checkout",
                Status = status,
                CreatedAt = createdAt
            };

        public void SetupPayments(IReadOnlyCollection<Payment> payments)
        {
            PaymentRepository.Setup(repository => repository.AsQueryable()).Returns(payments.AsQueryable());
            PaymentRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<Payment>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<Payment> query, int pageIndex, int pageSize) =>
                    new BasePaginatedList<Payment>(
                        query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
                        query.Count(),
                        pageIndex,
                        pageSize));
        }

        public void SetupBusinessAndPlans(IReadOnlyCollection<Payment> payments)
        {
            BusinessRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<Business, bool>>>(),
                    It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
                .ReturnsAsync([Business]);
            PlanRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                    It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
                .ReturnsAsync(payments.Count == 0 ? [] : [Plan]);
        }

        public void SetupSendBillPayment(Payment payment)
        {
            PaymentRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Payment, bool>>>(),
                    It.IsAny<Func<IQueryable<Payment>, IQueryable<Payment>>?>()))
                .ReturnsAsync(payment);
        }

        public void SetupSendBillBusinessAndUser()
        {
            BusinessRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Business, bool>>>(),
                    It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
                .ReturnsAsync(Business);
            UserRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>()))
                .ReturnsAsync(Owner);
        }

        public void SetupSuccessfulSendBillDependencies(Payment payment)
        {
            SetupSendBillPayment(payment);
            SetupSendBillBusinessAndUser();
            PlanRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                    It.IsAny<Func<IQueryable<SubscriptionPlan>, IQueryable<SubscriptionPlan>>?>()))
                .ReturnsAsync(Plan);
            SubscriptionRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<BusinessSubscription, bool>>>(),
                    It.IsAny<Func<IQueryable<BusinessSubscription>, IQueryable<BusinessSubscription>>?>()))
                .ReturnsAsync(new BusinessSubscription
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = Business.Id,
                    SubscriptionPlanId = Plan.Id,
                    StartDate = TestData.Now,
                    EndDate = TestData.Now.AddDays(30),
                    Status = StatusEnums.Active
                });
            TemplateService.Setup(service => service.RenderEmailTemplateAsync(
                    It.IsAny<string>(),
                    It.IsAny<BillCompletedEmailModel>()))
                .ReturnsAsync("<html>bill</html>");
            EmailService.Setup(service => service.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        }
    }
}
