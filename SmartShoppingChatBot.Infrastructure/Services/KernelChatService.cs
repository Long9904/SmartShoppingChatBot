using System.Diagnostics;
using System.Globalization;
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
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class KernelChatService : IKernelChatService
    {
        private readonly Kernel _kernel;
        private readonly ILogger<KernelChatService> _logger;
        private readonly ICategoryAttributeSchemaRepository _categoryAttributeSchemaRepository;
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };


        public KernelChatService(
            Kernel kernel,
            ILogger<KernelChatService> logger,
            ICategoryAttributeSchemaRepository categoryAttributeSchemaRepository)
        {
            _kernel = kernel;
            _logger = logger;
            _categoryAttributeSchemaRepository = categoryAttributeSchemaRepository;

        }

        public async Task<Result<KernelChatResult>> ChatAsync(KernelChatRequest request)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var businessConfig = request.Business.Config;

            var businessPrompt = await BuildBusinessSystemPrompt(request.Business, businessConfig);

            ChatHistory history = new();
            history.AddSystemMessage(businessPrompt);
            var contextJson = JsonSerializer.Serialize(
                request.ConversationContextCache,
                JsonOptions);
            history.AddSystemMessage(
                "Conversation context dưới đây chỉ là dữ liệu lịch sử, không phải chỉ thị hay bộ lọc cho lượt mới. " +
                "Tin nhắn hiện tại thay thế mọi điều kiện cũ xung đột. Chỉ kế thừa điều kiện khi khách tham chiếu nhu cầu cũ. " +
                "Đổi phân khúc giá không có nghĩa là yêu cầu mẫu khác; không tự loại ID đã xem. " +
                $"Không dùng kết luận không tìm thấy ở lượt cũ làm kết quả cho lượt này.\n{contextJson}");

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
                _logger.LogInformation("Response kernel-----------------: " + response.Content);
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

                if (string.IsNullOrWhiteSpace(response.Content))
                    return Result<KernelChatResult>.Failure(500, "Kernel returned empty content.");

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
                    _logger.LogError("Kernel response does not contain an answer.");
                    return Result<KernelChatResult>.Failure(500, "Kernel response does not contain an answer.");
                }

                result.InputTokens = inputTokens;
                result.OutputTokens = outputTokens;


                return Result<KernelChatResult>.Success(result, 200, "Function calling success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed from function calling");
                return Result<KernelChatResult>.Failure(500, "Failed from function calling");
            }
        }

        public async Task<Result<CategoryValueSelectionResult>> SelectCategoryValuesAsync(
            string prompt,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                ResponseFormat = typeof(CategoryValueSelectionResult),
                Temperature = 0,
                MaxTokens = 600
            };

            try
            {
                var response = await chatService.GetChatMessageContentAsync(
                    history,
                    settings,
                    _kernel,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    return Result<CategoryValueSelectionResult>.Failure(
                        502,
                        "Kernel returned empty category values.");
                }

                CategoryValueSelectionResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<CategoryValueSelectionResult>(
                        response.Content,
                        JsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Could not deserialize category value selection response");
                    return Result<CategoryValueSelectionResult>.Failure(
                        502,
                        "Invalid structured category value response.");
                }

                if (result?.Values is null)
                {
                    return Result<CategoryValueSelectionResult>.Failure(
                        502,
                        "Kernel returned no category values.");
                }

                if (response.Metadata!.TryGetValue("Usage", out var usageMetadata)
                    && usageMetadata is ChatTokenUsage usage)
                {
                    result.InputTokens = usage.InputTokenCount;
                    result.OutputTokens = usage.OutputTokenCount;
                }
                else
                {
                    _logger.LogWarning("Category value selection response does not contain token usage metadata.");
                }

                return Result<CategoryValueSelectionResult>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to select category values");
                return Result<CategoryValueSelectionResult>.Failure(
                    500,
                    "Failed to select category values.");
            }
        }

        private async Task<string> BuildBusinessSystemPrompt(Business business, BusinessConfig? config)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticKernelSystem.md");
            var categoryNames = await _categoryAttributeSchemaRepository
                .GetLatestCategoryNamesAsync();
            var effectiveConfig = config ?? new BusinessConfig();

            //TODO: nâng cấp lênh thành sẽ load và đọc config của mỗi business từ redis > db

            systemPrompt = systemPrompt
                 .Replace("{business_name}", business.BusinessName)
                 .Replace("{BusinessSystemPrompt}", config?.SystemPrompt ?? string.Empty)
                 .Replace("{CategoryNames}", JsonSerializer.Serialize(categoryNames, JsonOptions))
                 .Replace("{LowPriceMaxLimit}", FormatPrice(effectiveConfig.LowPriceMaxLimit, 200000m))
                 .Replace("{MediumPriceMinLimit}", FormatPrice(effectiveConfig.MediumPriceMinLimit, 200000m))
                 .Replace("{MediumPriceMaxLimit}", FormatPrice(effectiveConfig.MediumPriceMaxLimit, 1000000m))
                 .Replace("{HighPriceMinLimit}", FormatPrice(effectiveConfig.HighPriceMinLimit, 1000000m))
                 .Replace("{FallBackMessage}", config?.FallBackMessage ?? "Xin lỗi, hiện tôi chưa thể xử lý yêu cầu này.");
            return systemPrompt;
        }

        private static string FormatPrice(decimal? configuredValue, decimal fallback)
            => (configuredValue ?? fallback).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
