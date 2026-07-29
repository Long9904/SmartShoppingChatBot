using System.Diagnostics;
using System.Text.Json;
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
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };
        private readonly IRedisBusinessConfig _redisBusinessConfig;

        public KernelChatService(Kernel kernel, ILogger<KernelChatService> logger, IRedisBusinessConfig redisBusinessConfig)
        {

            _kernel = kernel;
            _logger = logger;
            _redisBusinessConfig = redisBusinessConfig;
        }

        public async Task<Result<KernelChatResult>> ChatAsync(KernelChatRequest request)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var businessConfig = await _redisBusinessConfig.GetBusinessConfigAsync();

            var businessPrompt = await BuildBusinessSystemPrompt(request.Business, businessConfig!);

            ChatHistory history = new();
            history.AddSystemMessage(businessPrompt);

            var contextJson = JsonSerializer.Serialize(
                request.ConversationContextCache,
                JsonOptions);
            history.AddSystemMessage($"Conversation context:\n{contextJson}");
            history.AddUserMessage(request.UserMessage);



            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    options: new FunctionChoiceBehaviorOptions
                    {
                        AllowStrictSchemaAdherence = true
                    }),
                ResponseFormat = typeof(KernelChatResult),
                Temperature = businessConfig?.ModelTemperature ?? 0.2,
                MaxTokens = businessConfig?.MaxOutPutToken ?? 2000,
            };

            try
            {
                var sw = Stopwatch.StartNew();

                var response = await chatService.GetChatMessageContentAsync(
                history,
                settings,
                _kernel);

                sw.Stop();
                Console.WriteLine("----------------------------------");
                _logger.LogInformation("3. Kernel response: {kernel} ms", sw.ElapsedMilliseconds);
                Console.WriteLine("----------------------------------");

                if (string.IsNullOrWhiteSpace(response.Content)) return Result<KernelChatResult>.Failure(
                        500, "Kernel returned empty content.");

                KernelChatResult? result;

                try
                {
                    result = JsonSerializer.Deserialize<KernelChatResult>(response.Content, JsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Could not deserialize kernel structured response");

                    return Result<KernelChatResult>.Failure(500, "Invalid structured response from kernel.");
                }

                if (result is null || string.IsNullOrWhiteSpace(result.Answer))
                {
                    return Result<KernelChatResult>.Failure(500, "Kernel response does not contain an answer.");
                }

                if (string.IsNullOrWhiteSpace(result.Summary) || string.IsNullOrWhiteSpace(result.AISummaryContent))
                {
                    return Result<KernelChatResult>.Failure(500, "Kernel response does not contain a summary.");
                }

                if (result.SelectedProductIds is null)
                {
                    return Result<KernelChatResult>.Failure(500, "Kernel response does not contain selectedProductIds.");
                }

                _logger.LogInformation(
                    "Kernel selected product IDs: {SelectedProductIds}",
                    string.Join(", ", result.SelectedProductIds));

                return Result<KernelChatResult>.Success(result, 200, "Function calling success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed from function calling");
                return Result<KernelChatResult>.Failure(500, "Failed from function calling");
            }
        }

        private async Task<string> BuildBusinessSystemPrompt(Business business, BusinessConfig config)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticKernelSystem.md");

            //TODO: nâng cấp lênh thành sẽ load và đọc config của mỗi business từ redis > db

            systemPrompt = systemPrompt
                 .Replace("{business_name}", business.BusinessName)
                 .Replace("{BusinessSystemPrompt}", config.SystemPrompt ?? string.Empty)
                 .Replace("{FallBackMessage}", config.FallBackMessage ?? string.Empty);
            return systemPrompt;
        }
    }
}
