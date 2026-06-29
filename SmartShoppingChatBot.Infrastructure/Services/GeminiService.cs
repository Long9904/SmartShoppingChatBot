using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly GoogleConfigs _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(
            IOptions<GoogleConfigs> config,
            IHttpClientFactory httpClientFactory,
            ILogger<GeminiService> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }


        private async Task<string> GetAccessTokenAsync()
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }


        public async Task<Result<double[]>> EmbeddingsAsync(string text)
        {
            try
            {
                var projectId = _config.ProjectId;
                var location = _config.EmbeddedLocation ?? "global";
                var embeddingModel = _config.EmbeddedModelId;

                var endpoint = $"https://aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{embeddingModel}:embedContent";


                var requestBody = new
                {
                    content = new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new
                            {
                                text
                            }
                        }
                    },
                    taskType = "RETRIEVAL_QUERY",
                    outputDimensionality = _config.OutputDimensionality,
                    autoTruncate = true
                };

                var json = JsonSerializer.Serialize(requestBody);

                var accessToken = await GetAccessTokenAsync();


                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = _httpClientFactory.CreateClient("gemini");
                using var response = await client.SendAsync(request);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error generating embeddings from Gemini API: {ResponseContent}", responseContent);

                    return Result<double[]>.Failure((int)response.StatusCode, $"Error generating embeddings from Gemini API: {responseContent}");
                }

                using var doc = JsonDocument.Parse(responseContent);

                var values = doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values");

                var data = values.EnumerateArray()
                    .Select(value => value.GetDouble())
                    .ToArray();

                return Result<double[]>.Success(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings from Gemini API");
                return Result<double[]>.Failure(500, "Error generating embeddings from Gemini API");
            }
        }

        public async Task<Result<string>> GenerateTextAsync(
            string prompt,
            int maxTokens = 9000,
            double temperature = 0.7)
        {
            try
            {
                if (maxTokens > _config.GeminiMaxTokens)
                {
                    maxTokens = _config.GeminiMaxTokens;
                }
                var projectId = _config.ProjectId;
                var location = _config.Location ?? "asia-southeast1";
                var model = _config.ModelId ?? "gemini-3.5-flash";

                var host = $"{location}-aiplatform.googleapis.com";
                if ("global".Equals(location))
                {
                    host = "aiplatform.googleapis.com";
                }

                var endpoint = $"https://{host}/v1/projects/{projectId}" +
                    $"/locations/{location}/publishers/google/models/{model}:generateContent";

                var requestBody = new
                {
                    contents = new[]
    {
                         new
                         {
                             role = "user",
                             parts = new []
                             {
                                 new { text = prompt }
                             }
                         }
                    },
                    generationConfig = new
                    {
                        temperature = temperature,
                        maxOutputTokens = maxTokens
                    }
                };


                var json = JsonSerializer.Serialize(requestBody);
                var accessToken = await GetAccessTokenAsync();
      


                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = _httpClientFactory.CreateClient("gemini");
                using var response = await client.SendAsync(request);


                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                string? text = null;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    if (first.TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.GetArrayLength() > 0
                        && parts[0].TryGetProperty("text", out var textElem))
                    {
                        text = textElem.GetString();
                    }
                }

                if (text == null)
                {
                    return Result<string>.Failure(500, "No text generated from Gemini API");
                }

                return Result<string>.Success(text);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating text from Gemini API");
                // Other options:
                // 1. Handle with other recall
                // 2. Call other LLM API
                return Result<string>.Failure(500, "Error generating text from Gemini API");
            }
        }
    }
}
