using System.Text;
using System.Text.Json;
using HRApp.API.Models;
using System.Text.Json.Serialization;

namespace HRApp.API.Services
{
    public class GroqService : IGroqService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.groq.com/openai/v1/chat/completions";
        private readonly ILogger<GroqService> _logger;

        public GroqService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq API key is missing.");
        }

        public async Task<GroqChatCompletionResponse> GetChatCompletionAsync(string systemPrompt, string userMessage, List<FunctionDefinition>? functions = null)
        {
            var messages = new List<GroqMessage>
            {
                new GroqMessage { Role = "system", Content = systemPrompt },
                new GroqMessage { Role = "user", Content = userMessage }
            };
            return await SendRequestAsync(messages, functions);
        }

        public async Task<GroqChatCompletionResponse> GetChatCompletionAsync(List<GroqMessage> messages)
        {
            return await SendRequestAsync(messages, null);
        }

        public async Task<GroqChatCompletionResponse> GetChatCompletionAsync(List<GroqMessage> messages, List<FunctionDefinition>? functions = null)
        {
            return await SendRequestAsync(messages, functions);
        }

        private async Task<GroqChatCompletionResponse> SendRequestAsync(List<GroqMessage> messages, List<FunctionDefinition>? functions)
        {
            var request = new GroqChatRequest
            {
                Model = "moonshotai/kimi-k2-instruct-0905",
                Messages = messages,
                Temperature = 0.1 // LOW temperature for deterministic SQL generation
            };

            if (functions != null && functions.Any())
            {
                request.Tools = functions.Select(f => new GroqTool
                {
                    Function = new GroqFunction
                    {
                        Name = f.Name,
                        Description = f.Description,
                        Parameters = f.Parameters
                    }
                }).ToList();
                request.ToolChoice = "auto";
            }

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            var json = JsonSerializer.Serialize(request, options);
            // _logger.LogInformation("Request: {Json}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync(_baseUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Response: {ResponseJson}", responseJson);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Groq API Error: {response.StatusCode} - {responseJson}");
            }

            return JsonSerializer.Deserialize<GroqChatCompletionResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }
}