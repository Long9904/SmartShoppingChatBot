using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly GoogleConfigs _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeminiService> _logger;
        private readonly GoogleAccessTokenProvider _accessTokenProvider;

        public GeminiService(
            IOptions<GoogleConfigs> config,
            IHttpClientFactory httpClientFactory,
            GoogleAccessTokenProvider accessTokenProvider,
            ILogger<GeminiService> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _accessTokenProvider = accessTokenProvider;
        }


        private async Task<string> GetAccessTokenAsync()
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }


        public Task<Result<double[]>> EmbeddingsAsync(string text, string taskType = "RETRIEVAL_QUERY")
        {
            return EmbeddingsAsyncV2(text, taskType);
        }

        public async Task<Result<double[]>> EmbeddingsAsyncV2(
            string text,
            string taskType = "RETRIEVAL_QUERY",
            CancellationToken ct = default)
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
                    taskType,
                    outputDimensionality = _config.OutputDimensionality,
                    autoTruncate = true
                };

                var json = JsonSerializer.Serialize(requestBody);

                var accessToken = await _accessTokenProvider.GetAccessTokenAsync(ct);


                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = _httpClientFactory.CreateClient("gemini");
                using var response = await client.SendAsync(request, ct);

                var responseContent = await response.Content.ReadAsStringAsync(ct);

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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings from Gemini API");
                return Result<double[]>.Failure(500, "Error generating embeddings from Gemini API", messageCode: "MG_SERVER_500");
            }
        }

        public async Task<Result<IReadOnlyList<double[]>>> EmbeddingsAsyncV3(
            IReadOnlyList<string> texts,
            string taskType = "RETRIEVAL_QUERY",
            CancellationToken ct = default)
        {
            if (texts.Count == 0)
            {
                return Result<IReadOnlyList<double[]>>.Success(Array.Empty<double[]>());
            }

            var embeddingTasks = texts
                .Select(text => EmbeddingsAsyncV2(text, taskType, ct))
                .ToArray();

            var results = await Task.WhenAll(embeddingTasks);

            foreach (var result in results)
            {
                if (!result.IsSuccess)
                {
                    return Result<IReadOnlyList<double[]>>.Failure(
                        result.StatusCode,
                        result.Message,
                        result.Errors,
                        result.MessageCode);
                }
            }

            var embeddings = results
                .Select(result => result.Data!)
                .ToArray();

            return Result<IReadOnlyList<double[]>>.Success(embeddings);
        }

        public async Task<Result<string>> GenerateTextAsync(
            string prompt,
            int maxTokens = 9000,
            double temperature = 0.7,
            string systemPrompt = "")
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

                    systemInstruction = new
                    {
                        parts = new[]
                            {
                                new { text = systemPrompt }
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
                return Result<string>.Failure(500, "Error generating text from Gemini API", messageCode: "MG_SERVER_500");
            }
        }

        public async Task<Result<string>> GenerateTextAsyncV2(GeminiRequest geminiRequest, CancellationToken ct = default)
        {
            if (geminiRequest.GenerationConfig.MaxOutputTokens > _config.GeminiMaxTokens)
            {
                geminiRequest.GenerationConfig.MaxOutputTokens = _config.GeminiMaxTokens;
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
                            new { text = geminiRequest.Prompt }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = geminiRequest.SystemPrompt }
                    }
                },

                generationConfig = new
                {
                    temperature = geminiRequest.GenerationConfig.Temperature,
                    maxOutputTokens = geminiRequest.GenerationConfig.MaxOutputTokens,
                }
            };


            try
            {
                var json = JsonSerializer.Serialize(requestBody, JsonOptions);

                var accessToken = await _accessTokenProvider.GetAccessTokenAsync(ct);


                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = _httpClientFactory.CreateClient("gemini");

                var sw = Stopwatch.StartNew();

                using var response = await client.SendAsync(request);
                sw.Stop();
                var latencyMs = sw.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Gemini Error ({Status})\nRequest:\n{Request}\nResponse:\n{Response}",
                        response.StatusCode,
                        json,
                        errorBody);

                    return Result<string>.Failure(
                        (int)response.StatusCode,
                        errorBody);
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


                int promptTokenCount = 0;
                int candidatesTokenCount = 0;
                int totalTokenCount = 0;

                if (root.TryGetProperty("usageMetadata", out var usageMetadata))
                {
                    if (usageMetadata.TryGetProperty("promptTokenCount", out var promptToken))
                    {
                        promptTokenCount = promptToken.GetInt32();
                    }

                    if (usageMetadata.TryGetProperty("candidatesTokenCount", out var candidateToken))
                    {
                        candidatesTokenCount = candidateToken.GetInt32();
                    }

                    if (usageMetadata.TryGetProperty("totalTokenCount", out var totalToken))
                    {
                        totalTokenCount = totalToken.GetInt32();
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
                return Result<string>.Failure(500, "Error generating text from Gemini API", messageCode: "MG_SERVER_500");
            }

        }

        public async Task<Result<ICollection<RankedRecord>>> RerankerAsyncV2(
            string userQuery,
            IEnumerable<RankRecord> records,
            CancellationToken ct)
        {
            var endpoint =
            $"https://discoveryengine.googleapis.com/v1/projects/{_config.ProjectId}/locations/global/rankingConfigs/default_ranking_config:rank";

            var req = new RankRequest
            {
                Query = userQuery,
                Records = records.ToList(),
                Model = "semantic-ranker-fast-004",
                IgnoreRecordDetailsInResponse = false
            };

            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);

            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);

            message.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            message.Content = JsonContent.Create(req);

            using var client = _httpClientFactory.CreateClient("gemini");

            try
            {
                var response = await client.SendAsync(message, ct);

                response.EnsureSuccessStatusCode();

                var result =
                    await response.Content.ReadFromJsonAsync<RankResponse>(cancellationToken: ct);

                if (result == null)
                {
                    return Result<ICollection<RankedRecord>>.Failure(502, "Reranker returned an empty response");
                }

                return Result<ICollection<RankedRecord>>.Success(result.Records, 200, "Reranker success");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error when reranker");
                return Result<ICollection<RankedRecord>>.Failure(400, "Reranker fail");
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

