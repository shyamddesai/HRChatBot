using HRApp.API.Models;

namespace HRApp.API.Services
{
    public interface IGroqService
    {
        Task<GroqChatCompletionResponse> GetChatCompletionAsync(string systemPrompt, string userMessage, List<FunctionDefinition>? functions = null);
        Task<GroqChatCompletionResponse> GetChatCompletionAsync(List<GroqMessage> messages);
        Task<GroqChatCompletionResponse> GetChatCompletionAsync(List<GroqMessage> messages, List<FunctionDefinition>? functions = null);
    }
}