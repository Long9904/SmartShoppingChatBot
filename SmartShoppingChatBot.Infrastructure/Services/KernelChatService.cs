using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class KernelChatService : IKernelChatService
    {
        private readonly Kernel _kernel;
        private readonly ILogger<KernelChatService> _logger;

        public KernelChatService(Kernel kernel, ILogger<KernelChatService> logger)
        {

            _kernel = kernel;
            _logger = logger;
        }

        public async Task<Result<string>> ChatAsync(KernelChatRequest request)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var businessPrompt = await BuildBusinessSystemPrompt(request.Business);

            ChatHistory history = new();
            history.AddSystemMessage(businessPrompt);
            history.AddUserMessage(request.UserMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    options: new FunctionChoiceBehaviorOptions
                    {
                        AllowStrictSchemaAdherence = true
                    }),
                Temperature = 0.2,
                MaxTokens = 7000,
            };

            try
            {
                var response = await chatService.GetChatMessageContentAsync(
                history,
                settings,
                _kernel);



                return Result<string>.Success(response.Content, 200, "Function calling success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed from function calling");
                return Result<string>.Failure(500, "Failed from function calling");
            }
        }

        private async Task<string> BuildBusinessSystemPrompt(Business business)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticKernelSystem.md");

            //TODO: nâng cấp lênh thành sẽ load và đọc config của mỗi business từ redis > db

            return systemPrompt.Replace("{business_name}", business.BusinessName);
        }
    }
}
