using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;

public sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, Result<BasePaginatedList<CustomerListResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;

    public GetCustomersQueryHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
    }

    public async Task<Result<BasePaginatedList<CustomerListResponse>>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<BasePaginatedList<CustomerListResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var filter = request.Filter;
        var businessId = businessResult.Data.Id;
        var query = _customerRepository.AsQueryable()
            .Where(customer => customer.BusinessId == businessId);

        if (!string.IsNullOrWhiteSpace(filter.CustomerExternalId))
        {
            var customerExternalId = filter.CustomerExternalId.Trim();
            query = query.Where(customer =>
                customer.CustomerExternalId == customerExternalId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(customer => customer.Status == filter.Status.Value);
        }

        query = ApplyOrder(query, filter.OrderBy);

        var page = await _customerRepository.PaginatedListAsync(
            query,
            filter.PageIndex,
            filter.PageSize);

        var response = new BasePaginatedList<CustomerListResponse>(
            page.Items.Select(MapCustomer).ToList(),
            page.TotalItems,
            page.PageIndex,
            page.PageSize);

        return Result<BasePaginatedList<CustomerListResponse>>.Success(
            response,
            200,
            "Customers retrieved successfully.",
            CustomerMessageCode.Success);
    }

    private static CustomerListResponse MapCustomer(Customer customer)
    {
        return new CustomerListResponse
        {
            Id = customer.Id.ToString(),
            CustomerExternalId = customer.CustomerExternalId,
            Name = customer.Name,
            Status = customer.Status,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };
    }

    private static IQueryable<Customer> ApplyOrder(
        IQueryable<Customer> query,
        string orderBy)
    {
        return orderBy.Trim().ToLowerInvariant() switch
        {
            "customerexternalid" or "customerexternalid asc" =>
                query.OrderBy(customer => customer.CustomerExternalId),
            "customerexternalid desc" =>
                query.OrderByDescending(customer => customer.CustomerExternalId),
            "name" or "name asc" => query.OrderBy(customer => customer.Name),
            "name desc" => query.OrderByDescending(customer => customer.Name),
            "status" or "status asc" => query.OrderBy(customer => customer.Status),
            "status desc" => query.OrderByDescending(customer => customer.Status),
            "createdat" or "createdat asc" => query.OrderBy(customer => customer.CreatedAt),
            "createdat desc" => query.OrderByDescending(customer => customer.CreatedAt),
            "updatedat" or "updatedat asc" => query.OrderBy(customer => customer.UpdatedAt),
            "updatedat desc" => query.OrderByDescending(customer => customer.UpdatedAt),
            _ => query.OrderByDescending(customer => customer.CreatedAt)
        };
    }
}
