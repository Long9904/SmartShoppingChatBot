using System.Diagnostics;
using SmartShoppingChatBot.Domain.Interface;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class KernelChatServiceV3 : IKernelChatServiceV3
    {
        private readonly Kernel _kernel;
        private readonly ILogger<KernelChatServiceV3> _logger;
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };
        private readonly IRedisBusinessConfig _redisBusinessConfig;
        private readonly ICategoryAttributeSchemaRepository _schemas;

        public KernelChatServiceV3(Kernel kernel, ILogger<KernelChatServiceV3> logger, IRedisBusinessConfig redisBusinessConfig,
            ICategoryAttributeSchemaRepository schemas)
        {

            _kernel = kernel;
            _logger = logger;
            _redisBusinessConfig = redisBusinessConfig;
            _schemas = schemas;
        }

        public async Task<Result<KernelChatResultV3>> ChatAsync(KernelChatRequestV3 request)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var businessConfig = request.Business.Config;

            var businessPrompt = await BuildBusinessSystemPrompt(request.Business, businessConfig);

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
                ResponseFormat = typeof(KernelChatResultV3),
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

                // Preserve the original token source and calculation exactly.
                long inputTokens = 0;
                long outputTokens = 0;

                if (response.Metadata!.TryGetValue("Usage", out var usageMetadata)
                    && usageMetadata is ChatTokenUsage usage)
                {
                    inputTokens = usage.InputTokenCount;
                    outputTokens = usage.OutputTokenCount;
                }
                else
                {
                    _logger.LogWarning("Kernel response does not contain token usage metadata.");
                }

                sw.Stop();
                Console.WriteLine("----------------------------------");
                _logger.LogInformation("3. Kernel response: {kernel} ms", sw.ElapsedMilliseconds);
                Console.WriteLine("----------------------------------");

                if (string.IsNullOrWhiteSpace(response.Content)) return Result<KernelChatResultV3>.Failure(
                        500, "Kernel returned empty content.");

                KernelChatResultV3? result;

                try
                {
                    result = JsonSerializer.Deserialize<KernelChatResultV3>(response.Content, JsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Could not deserialize kernel structured response");

                    return Result<KernelChatResultV3>.Failure(500, "Invalid structured response from kernel.");
                }

                if (result is null || string.IsNullOrWhiteSpace(result.Answer))
                {
                    _logger.LogError("Kernel response does not contain an answer.");
                    return Result<KernelChatResultV3>.Failure(500, "Kernel response does not contain an answer.");
                }

                result.InputTokens = inputTokens;
                result.OutputTokens = outputTokens;


                return Result<KernelChatResultV3>.Success(result, 200, "Function calling success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed from function calling");
                return Result<KernelChatResultV3>.Failure(500, "Failed from function calling");
            }
        }

        private async Task<string> BuildBusinessSystemPrompt(Business business, BusinessConfig? config)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticKernelSystemV3.md");


            systemPrompt = systemPrompt
                 .Replace("{business_name}", business.BusinessName)
                 .Replace("{BusinessSystemPrompt}", config?.SystemPrompt ?? string.Empty)
                 .Replace("{ProductDisplayLimitV3}", Math.Max(1, config?.TopKDocument ?? 5).ToString())
                 .Replace("{FallBackMessage}", config?.FallBackMessage ?? "Xin lỗi, hiện tôi chưa thể xử lý yêu cầu này.");
            // Inject the same active schema names used to validate tool arguments.
            var schemas = await _schemas.FindAllAsync(schema => schema.IsActive);
            var categories = schemas.GroupBy(schema => schema.Category)
                .Select(group => group.OrderByDescending(schema => schema.Version).First())
                .Select(schema => new
                {
                    schema.Category,
                    Attributes = schema.Attributes.Where(attribute => attribute.IsFilterable)
                        .Select(attribute => new
                        {
                            attribute.Key, DataType = attribute.DataType.ToString(),
                            attribute.IsStrict, attribute.AllowedValues
                        })
                });
            return systemPrompt + "\nDanh mục và thuộc tính hợp lệ:\n"
                + JsonSerializer.Serialize(categories, JsonOptions);
        }
    }
}
