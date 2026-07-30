using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations
{
    public class CustomerGetConversationsQueryHandler : IRequestHandler<CustomerGetConversationsQuery, Result<BasePaginatedList<CustomerConversationResponse>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ICustomerRepository _customerRepository;
        private readonly IConversationRepository _conversationRepository;

        public CustomerGetConversationsQueryHandler(
            ICurrentUserService currentUserService,
            ICustomerRepository customerRepository,
            IConversationRepository conversationRepository)
        {
            _currentUserService = currentUserService;
            _customerRepository = customerRepository;
            _conversationRepository = conversationRepository;
        }

        public async Task<Result<BasePaginatedList<CustomerConversationResponse>>> Handle(
            CustomerGetConversationsQuery request,
            CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess || business.Data is null)
            {
                return Result<BasePaginatedList<CustomerConversationResponse>>.Failure(
                    statusCode: business.StatusCode,
                    message: business.Message,
                    errors: business.Errors,
                    messageCode: business.MessageCode);
            }

            var businessId = business.Data.Id;
            var customer = await _customerRepository.FindAsync(x =>
                x.CustomerExternalId == request.ExternalCustomerId
                && x.BusinessId == businessId);

            if (customer == null)
            {
                return Result<BasePaginatedList<CustomerConversationResponse>>.Failure(
                    statusCode: 404,
                    message: "Customer not found",
                    messageCode: CustomerMessageCode.NotFound);
            }

            var query = _conversationRepository.AsQueryable()
                .Where(x => x.BusinessId == businessId && x.CustomerId == customer.Id)
                .OrderByDescending(x => x.CreateAt);

            var conversations = await _conversationRepository.PaginatedListAsync(
                query,
                request.PageIndex,
                request.PageSize);

            var response = new BasePaginatedList<CustomerConversationResponse>
            {
                Items = conversations.Items.Select(x => new CustomerConversationResponse
                {
                    Id = x.Id.ToString(),
                    Title = x.Title,
                    Status = x.Status,
                    LastMessageAt = x.LastMessageAt,
                    CreateAt = x.CreateAt
                }).ToList(),
                TotalItems = conversations.TotalItems,
                PageIndex = conversations.PageIndex,
                TotalPages = conversations.TotalPages,
                PageSize = conversations.PageSize
            };

            return Result<BasePaginatedList<CustomerConversationResponse>>.Success(
                data: response,
                statusCode: 200,
                message: "Get customer conversations successfully",
                messageCode: ConversationMessageCode.Success);
        }
    }
}
