using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage
{
    public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<ConversationResponse>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SendMessageCommandHandler> _logger;
        private readonly TimeProvider _time;
        private readonly IKernelChatService _kernelChatService;

        public SendMessageCommandHandler(
            ICustomerRepository customerRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            ILogger<SendMessageCommandHandler> logger,
            ICurrentUserService currentUserService,
            IKernelChatService kernelChatService)

        {
            _customerRepository = customerRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _time = time;
            _logger = logger;
            _kernelChatService = kernelChatService;
        }

        public async Task<Result<ConversationResponse>> Handle(
            SendMessageCommand request,
            CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess)
                return Result<ConversationResponse>.Failure(
                    statusCode: business.StatusCode,
                    message: business.Message,
                    messageCode: business.MessageCode);

            var customer = await GetOrCreateCustomerAsync(request.ExternalCustomerId, business.Data!);



            Conversation? conversation;
            try
            {
                var createTime = _time.GetUtcNow();

                await _unitOfWork.BeginTransactionAsync(cancellationToken);


                if (string.IsNullOrEmpty(request.ConversationId) || request.ConversationId == null)
                {
                    var title = request.Message.Length > 30
                        ? request.Message.Substring(0, 30) + "..."
                        : request.Message;
                    conversation = new()
                    {
                        Title = title,
                        BusinessId = business.Data!.Id,
                        CreateAt = createTime,
                        CustomerId = customer.Data!.Id,
                        Id = ObjectId.GenerateNewId(),
                        Status = ConversationStatus.Active
                    };
                    await _conversationRepository.AddAsync(conversation);
                }
                else
                {
                    conversation = await _conversationRepository.FindAsync(x =>
                    x.Id == ObjectId.Parse(request.ConversationId));

                    if (conversation == null) return Result<ConversationResponse>
                            .Failure(404, "Conversation not found", null, ConverstationMessageCode.NotFound);

                    conversation.LastMessageAt = createTime;
                    await _conversationRepository.UpdateAsync(conversation);
                }


                var userMessage = new Message
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Data!.Id,
                    ConversationId = conversation.Id,
                    Content = request.Message,
                    ContentType = ContentTypeEnum.Text,
                    CreatedAt = createTime,
                    SenderType = SenderTypeEnum.Customer,
                    Status = MessageStatus.Sent,
                };

                await _messageRepository.AddAsync(userMessage);

                KernelChatRequest req = new()
                {
                    Business = business.Data,
                    UserMessage = request.Message,
                };

                var sematicKernelResponse = await _kernelChatService.ChatAsync(req);

                if (!sematicKernelResponse.IsSuccess) return Result<ConversationResponse>
                    .Failure(500, "Xin lỗi, hiện mình chưa thể trả lời câu hỏi này. Bạn vui lòng thử lại hoặc liên hệ nhân viên hỗ trợ nhé.", null, "MG_SERVER_500");

                var aiMessage = new Message
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Data!.Id,
                    ConversationId = conversation.Id,
                    Content = sematicKernelResponse.Data!,
                    ContentType = ContentTypeEnum.Text,
                    CreatedAt = _time.GetUtcNow(),
                    SenderType = SenderTypeEnum.ChatBot,
                    Status = MessageStatus.Sent,
                };
                await _messageRepository.AddAsync(aiMessage);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                var response = new ConversationResponse
                {
                    ConversationId = conversation.Id.ToString(),
                    ConversationTitle = conversation.Title,
                    MessageResponse = aiMessage.Content,
                };

                return Result<ConversationResponse>.Success(response, 200, "Kernel response success", ConverstationMessageCode.Success);

            }
            catch (Exception ex)
            {
                await _unitOfWork.RollBackAsync(cancellationToken);
                _logger.LogError(ex, "Error when saving user message or kernel response");

                return Result<ConversationResponse>
                    .Failure(500, "Error when saving user message or kernel response", null, "MG_SERVER_500");
            }

        }

        private async Task<Result<Customer>> GetOrCreateCustomerAsync(
            string externalCustomerId,
            Business business)
        {

            var customer = await _customerRepository.FindAsync(x =>
            x.CustomerExternalId == externalCustomerId
            && x.BusinessId == business.Id);

            if (customer != null)
                return Result<Customer>.Success(
                    data: customer,
                    message: "Get customer success",
                    messageCode: CustomerMessageCode.Success);

            var newCustomer = new Customer
            {
                CustomerExternalId = externalCustomerId,
                BusinessId = business.Id,
                Status = CustomerStatus.Active,
                CreatedAt = _time.GetUtcNow(),
                Id = ObjectId.GenerateNewId(),
            };

            await _customerRepository.AddAsync(newCustomer);
            await _unitOfWork.SaveChangesAsync();

            return Result<Customer>.Success(
                data: newCustomer,
                message: "Create customer success",
                messageCode: CustomerMessageCode.Create);
        }
    }
}
