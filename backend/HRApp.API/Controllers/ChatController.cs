using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRApp.API.Models;
using HRApp.API.Services;
using HRApp.Core.Entities;
using HRApp.Infrastructure.Data;
using System.Security.Claims;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IGroqService _groqService;
        private readonly AppDbContext _context;
        private readonly ILogger<ChatController> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ChatController(IGroqService groqService, AppDbContext context, ILogger<ChatController> logger)
        {
            _groqService = groqService;
            _context = context;
            _logger = logger;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            var systemPrompt = $@"
You are an intelligent HR Assistant. Today's date is {DateTime.UtcNow:yyyy-MM-dd}.
Role: You assist employees with personal requests and HR managers with organizational data.
Current User Context: Email: {userEmail}, Role: {userRole}.

Capabilities:
1. get_employees: (HR Only) Retrieve all employee details. 
2. get_leave_info: Check leave balances. Defaults to current user if no email provided.
3. create_leave: Submit a leave request.
4. search_policies: (RAG) Search the company handbook for rules on remote work, benefits, or conduct.

Guidelines:
- ALWAYS check if the user is 'HR' before calling get_employees.
- If a user asks a general question (e.g., 'What is the remote work policy?'), use search_policies first.
- Be concise and professional.";

            var functions = GetFunctionDefinitions();

            var messages = new List<GroqMessage>
            {
                new GroqMessage { Role = "system", Content = systemPrompt },
                new GroqMessage { Role = "user", Content = request.Message }
            };

            // First pass: Check for tool calls
            var response = await _groqService.GetChatCompletionAsync(messages, functions);
            var choice = response.Choices.FirstOrDefault();

            if (choice?.Message?.ToolCalls != null && choice.Message.ToolCalls.Any())
            {
                messages.Add(choice.Message); // Add assistant's tool call request

                foreach (var toolCall in choice.Message.ToolCalls)
                {
                    var result = await ExecuteFunctionAsync(toolCall.Function.Name, toolCall.Function.Arguments, userId, userRole, userEmail!);
                    messages.Add(new GroqMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = result
                    });
                }

                // Second pass: Get final answer based on tool results
                var finalResponse = await _groqService.GetChatCompletionAsync(messages, functions);
                return Ok(new { answer = finalResponse.Choices.FirstOrDefault()?.Message?.Content });
            }

            return Ok(new { answer = choice?.Message?.Content ?? "I'm sorry, I couldn't process that." });
        }

        private async Task<string> ExecuteFunctionAsync(string name, string argsJson, Guid userId, string? role, string email)
        {
            try {
                switch (name)
                {
                    case "get_employees":
                        if (role != "HR") return "Access Denied: Only HR users can list all employees.";
                        var emps = await _context.Employees.Where(e => e.Status == "Active")
                                    .Select(e => new { e.FullName, e.Department, e.Grade }).ToListAsync();
                        return JsonSerializer.Serialize(emps);

                    case "get_leave_info":
                        var lArgs = JsonSerializer.Deserialize<LeaveArgs>(argsJson, _jsonOptions);
                        var targetEmail = lArgs?.Email ?? email;
                        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == targetEmail);
                        if (emp == null) return "Employee not found.";
                        // Simulated logic: 30 days entitlement - approved requests this year
                        var used = await _context.LeaveRequests.Where(r => r.EmployeeId == emp.Id && r.Status == "Approved").CountAsync(); 
                        return JsonSerializer.Serialize(new { emp.FullName, RemainingDays = 30 - used });

                    case "search_policies":
                        var sArgs = JsonSerializer.Deserialize<SearchArgs>(argsJson, _jsonOptions);
                        return MockRAGSearch(sArgs?.Query ?? "");

                    default: return "Function not implemented.";
                }
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        private string MockRAGSearch(string query)
        {
            // In a real RAG, you'd use a Vector DB. For the 24h challenge, use a switch/keyword search.
            if (query.Contains("remote", StringComparison.OrdinalIgnoreCase))
                return "Policy: Employees can work remotely up to 2 days per week with manager approval.";
            if (query.Contains("car", StringComparison.OrdinalIgnoreCase))
                return "Policy: Senior Leads and above are eligible for a car loan of up to $50,000.";
            return "General: Refer to the 2026 Employee Handbook for specific details.";
        }

        private List<FunctionDefinition> GetFunctionDefinitions() => new()
        {
            new FunctionDefinition { Name = "get_employees", Description = "HR Only. Lists all active employees.", Parameters = new { type = "object", properties = new { } } },
            new FunctionDefinition { Name = "get_leave_info", Description = "Checks leave balance.", Parameters = new { type = "object", properties = new { email = new { type = "string" } } } },
            new FunctionDefinition { Name = "search_policies", Description = "Searches HR handbook for policy info.", Parameters = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" } } }
        };

        private class LeaveArgs { public string? Email { get; set; } }
        private class SearchArgs { public string? Query { get; set; } }
    }
}