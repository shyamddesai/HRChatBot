using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HRApp.API.Models
{
    // ---------- Request Models ----------
    public class GroqChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "mixtral-8x7b-32768";

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();

        [JsonPropertyName("tools")]
        public List<GroqTool>? Tools { get; set; }

        [JsonPropertyName("tool_choice")]
        public string? ToolChoice { get; set; } // "auto" or "none"

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    public class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty; // "system", "user", "assistant", "tool"

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<GroqToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
    }

    public class GroqTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public GroqFunction Function { get; set; } = new();
    }

    public class GroqFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("parameters")]
        public object Parameters { get; set; } = new(); // JSON schema object
    }

    // This is the FunctionDefinition type used in IGroqService
    public class FunctionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public object Parameters { get; set; } = new(); // e.g., anonymous object or JObject
    }

    public class GroqToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public GroqFunctionCall Function { get; set; } = new();
    }

    public class GroqFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty; // JSON string
    }

    // ---------- Response Models ----------
    public class GroqChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<GroqChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public GroqUsage? Usage { get; set; }
    }

    public class GroqChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public GroqMessage Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public class GroqUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}