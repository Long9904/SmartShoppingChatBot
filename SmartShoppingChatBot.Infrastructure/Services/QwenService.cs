using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class QwenService : IQwenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly QwenConfigs _config;
        private readonly ILogger<QwenService> _logger;

        public QwenService(
            IHttpClientFactory httpClientFactory,
            IOptions<QwenConfigs> options,
            ILogger<QwenService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = options.Value;
            _logger = logger;
        }

        public async Task<Result<string>> GenerateTextAsync(
            string prompt, int maxTokens, double temperature, bool enableThinking)
        {
            if (maxTokens > _config.MaxTokens)
            {
                maxTokens = _config.MaxTokens;
            }

            try
            {
                var requestBody = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = temperature,
                    max_tokens = maxTokens,
                    chat_template_kwargs = new
                    {
                        enable_thinking = enableThinking
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                using var reqest = new HttpRequestMessage(HttpMethod.Post, _config.ApiUrl);
                
                reqest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                reqest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

                var client = _httpClientFactory.CreateClient("qwen");
                var response = await client.SendAsync(reqest);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Qwen API returned an error: {StatusCode} - {ResponseContent}", response.StatusCode, responseContent);
                    return Result<string>.Failure((int)response.StatusCode, "Error generating text from Qwen API");
                }

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                string? text = null;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        text = content.GetString();
                    }
                }

                if (text == null)
                {
                    _logger.LogError("Qwen API response does not contain expected 'choices' or 'message.content' fields: {ResponseContent}", responseContent);
                    return Result<string>.Failure(500, "Unexpected response format from Qwen API");
                }
                return Result<string>.Success(text, 200, "Text generated successfully");


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating text from Qwen API");

                return Result<string>.Failure(500, "Error generating text from Qwen: " + ex.Message);
            }
        }
    }
}
