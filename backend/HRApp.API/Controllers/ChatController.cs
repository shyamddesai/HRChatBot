using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRApp.API.Models;
using HRApp.API.Services;
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
            public List<GroqMessage>? History { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            // Build schema-aware system prompt
            var schemaInfo = GetDatabaseSchema();
            var systemPrompt = $@"You are an intelligent HR Database Assistant with direct SQL generation capabilities.

            DATABASE SCHEMA:
            {schemaInfo}

            SECURITY RULES:
            - You are logged in as: {userEmail} (Role: {userRole})
            - Employee Role: Can only view own data (WHERE id = '{userId}')
            - HR Role: Can view all data, but respect privacy for sensitive fields
            - NEVER generate DELETE or DROP statements
            - NEVER expose passwords or internal IDs
            - For numeric grade comparisons, use the computed column ""GradeNumber"" (e.g., ""GradeNumber"" >= 10).
            - Always use double quotes around table and column names (e.g., SELECT ""FullName"" FROM ""Employees"").
            - Parameter values can be placed directly in the SQL (we handle sanitization), but use proper escaping if needed.

            INSTRUCTIONS:
            1. Analyze the user's question
            2. Generate a SAFE, read-only SQL query to answer it
            3. Return ONLY a JSON object with this exact format:
            {{
                ""intent"": ""description of what you're doing"",
                ""sql"": ""SELECT ..."",
                ""explanation"": ""human-readable explanation of the query""
            }}

            If the question doesn't require database access (general chat), return:
            {{
                ""intent"": ""conversation"",
                ""response"": ""your helpful response""
            }}

            Examples:
            User: ""Who earns more than 10000?""
            Response: {{""intent"": ""salary_query"", ""sql"": ""SELECT e.""FullName"", e.""Grade"", e.""Department"" FROM ""Employees"" e WHERE e.""Id"" IN (SELECT s.""EmployeeId"" FROM ""Salaries"" s WHERE s.""BaseSalary"" > 10000 AND s.""EffectiveTo"" IS NULL) AND e.""Status"" = 'Active'"", ""explanation"": ""Finding active employees with current salary above 10000""}}

            User: ""How many IT employees are there?""
            Response: {{""intent"": ""count_query"", ""sql"": ""SELECT COUNT(*) FROM ""Employees"" WHERE ""Department"" = 'IT' AND ""Status"" = 'Active'"", ""explanation"": ""Counting active IT employees""}}";

            var messages = new List<GroqMessage> { new GroqMessage { Role = "system", Content = systemPrompt } };

            if (request.History != null)
                messages.AddRange(request.History.TakeLast(4)); // Keep last 4 for context

            messages.Add(new GroqMessage { Role = "user", Content = request.Message });

            try
            {
                // Get LLM response (no function calling - pure text generation)
                var completion = await _groqService.GetChatCompletionAsync(messages, null);
                var llmResponse = completion.Choices.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrEmpty(llmResponse))
                    return Ok(new { answer = "I didn't understand that. Could you rephrase?" });

                _logger.LogInformation("LLM Raw Response: {Response}", llmResponse);

                // Parse the JSON response from LLM
                var (intent, sql, explanation, conversationResponse) = ParseLlmResponse(llmResponse);

                // Handle conversational queries
                if (intent == "conversation" && !string.IsNullOrEmpty(conversationResponse))
                {
                    return Ok(new { answer = conversationResponse, type = "chat" });
                }

                // Validate and execute SQL
                if (!string.IsNullOrEmpty(sql))
                {
                    var validationResult = ValidateSql(sql, userRole!, userId);
                    if (!validationResult.IsValid)
                    {
                        return Ok(new { answer = $"I can't execute that query for security reasons: {validationResult.Reason}", type = "error" });
                    }

                    // Apply row-level security for non-HR users
                    sql = ApplyRowLevelSecurity(sql, userRole!, userId);

                    var (results, columns) = await ExecuteDynamicSqlAsync(sql);
                    
                    // Format results through LLM for natural language
                    var formattedAnswer = await FormatResultsWithLlmAsync(request.Message, results, columns, explanation);
                    
                    return Ok(new 
                    { 
                        answer = formattedAnswer, 
                        type = "data",
                        sql = userRole == "HR" ? sql : null, // Only show SQL to HR
                        rowCount = results.Count,
                        data = results // Raw data for frontend tables
                    });
                }

                return Ok(new { answer = "I'm not sure how to help with that. Try asking about employees, salaries, or leave.", type = "unknown" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat processing error");
                return Ok(new { answer = "I encountered an error processing your request. Please try again.", type = "error" });
            }
        }

        private string GetDatabaseSchema()
        {
            return @"
                Tables (use double‑quoted names exactly as shown):

                ""Employees"" (
                    ""Id"" uuid,
                    ""EmployeeCode"" text,
                    ""FullName"" text,
                    ""Email"" text,
                    ""Role"" text,
                    ""Grade"" text,          -- stored as 'Grade X' format, extract number for comparisons
                    ""Department"" text,
                    ""ManagerId"" uuid,
                    ""Status"" text,         -- 'Active', 'Archived', 'Terminated'
                    ""HireDate"" timestamptz,
                    ""TerminationDate"" timestamptz
                )

                ""Salaries"" (
                    ""Id"" uuid,
                    ""EmployeeId"" uuid,
                    ""BaseSalary"" numeric,
                    ""Currency"" text,
                    ""EffectiveFrom"" timestamptz,
                    ""EffectiveTo"" timestamptz   -- NULL means current salary
                )

                ""LeaveRequests"" (
                    ""Id"" uuid,
                    ""EmployeeId"" uuid,
                    ""StartDate"" timestamptz,
                    ""EndDate"" timestamptz,
                    ""Type"" text,          -- 'Annual', 'Sick', 'Emergency', etc.
                    ""Status"" text,        -- 'Pending', 'Approved', 'Rejected'
                    ""Reason"" text,
                    ""ApprovedById"" uuid
                )

                ""Skills"" (
                    ""Id"" uuid,
                    ""Name"" text
                )

                ""EmployeeSkills"" (
                    ""EmployeeId"" uuid,
                    ""SkillId"" uuid,
                    ""Level"" text          -- 'Beginner', 'Intermediate', 'Expert'
                )

                ""LeaveSummaries"" (
                    ""EmployeeId"" uuid,
                    ""Year"" integer,
                    ""AnnualEntitlement"" integer,
                    ""UsedDays"" integer,
                    ""RemainingDays"" integer
                )
                ";
        }

        private (string intent, string? sql, string? explanation, string? conversationResponse) ParseLlmResponse(string response)
        {
            try
            {
                // Extract JSON from potential markdown code blocks
                var jsonMatch = Regex.Match(response, @"```json\s*(\{.*?\})\s*```", RegexOptions.Singleline);
                if (jsonMatch.Success)
                    response = jsonMatch.Groups[1].Value;
                else
                {
                    // Try to find bare JSON object
                    jsonMatch = Regex.Match(response, @"(\{.*\})", RegexOptions.Singleline);
                    if (jsonMatch.Success)
                        response = jsonMatch.Groups[1].Value;
                }

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var intent = root.GetProperty("intent").GetString() ?? "unknown";
                var sql = root.TryGetProperty("sql", out var sqlProp) ? sqlProp.GetString() : null;
                var explanation = root.TryGetProperty("explanation", out var expProp) ? expProp.GetString() : null;
                var convResponse = root.TryGetProperty("response", out var respProp) ? respProp.GetString() : null;

                return (intent, sql, explanation, convResponse);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM response as JSON: {Response}", response);
                // Fallback: treat as conversation
                return ("conversation", null, null, response);
            }
        }

        private (bool IsValid, string Reason) ValidateSql(string sql, string userRole, Guid userId)
        {
            // Trim and remove trailing semicolon(s) – we'll allow one at the end
            string trimmedSql = sql.Trim();
            while (trimmedSql.EndsWith(';'))
                trimmedSql = trimmedSql[..^1].TrimEnd();

            var upperSql = trimmedSql.ToUpperInvariant();

            // Block dangerous operations
            var forbidden = new[] { "DELETE", "DROP", "TRUNCATE", "UPDATE", "INSERT", "ALTER", "CREATE", "GRANT", "REVOKE" };
            foreach (var word in forbidden)
            {
                if (Regex.IsMatch(upperSql, $@"\b{word}\b"))
                    return (false, $"Operation '{word}' is not permitted");
            }

            // Must be SELECT (case-insensitive start)
            if (!upperSql.TrimStart().StartsWith("SELECT"))
                return (false, "Only SELECT queries are allowed");

            // Check for SQL injection patterns (comment sequences, and any remaining semicolons)
            if (trimmedSql.Contains("--") || trimmedSql.Contains("/*") || trimmedSql.Contains("*/"))
                return (false, "Invalid comment sequences detected");

            // Ensure no semicolons remain (if any are left, they are inside the query – dangerous)
            if (trimmedSql.Contains(';'))
                return (false, "Multiple SQL statements detected");

            return (true, "Valid");
        }

        private string ApplyRowLevelSecurity(string sql, string userRole, Guid userId)
        {
            if (userRole == "HR") return sql;

            // For employees, inject WHERE clause to limit to own data
            // This is a simplified approach - in production use proper parameterized queries
            var employeeIdStr = userId.ToString();
            
            // Add employee_id filter if querying employees table
            if (sql.Contains("employees", StringComparison.OrdinalIgnoreCase))
            {
                if (sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("WHERE", $"WHERE (id = '{employeeIdStr}') AND ", StringComparison.OrdinalIgnoreCase);
                else if (sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("GROUP BY", $"WHERE id = '{employeeIdStr}' GROUP BY", StringComparison.OrdinalIgnoreCase);
                else if (sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("ORDER BY", $"WHERE id = '{employeeIdStr}' ORDER BY", StringComparison.OrdinalIgnoreCase);
                else
                    sql += $" WHERE id = '{employeeIdStr}'";
            }

            return sql;
        }

        private async Task<(List<Dictionary<string, object>> Results, List<string> Columns)> ExecuteDynamicSqlAsync(string sql)
        {
            var results = new List<Dictionary<string, object>>();
            var columns = new List<string>();

            try
            {
                string trimmedSql = sql.Trim().TrimEnd(';');
                _logger.LogInformation("Executing SQL: {Sql}", trimmedSql);

                // Use EF Core's raw SQL execution with Dapper-like safety
                // We use a read-only approach with parameterized safety checks already done
                
                // For PostgreSQL, we need to handle the connection directly for true dynamic SQL
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = trimmedSql;
                command.CommandTimeout = 30;

                using var reader = await command.ExecuteReaderAsync();
                
                // Get column names from schema
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                // Read rows
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        // Handle DB null
                        row[columns[i]] = value == DBNull.Value ? null! : value;
                    }
                    results.Add(row);
                }

                await connection.CloseAsync();
                
                _logger.LogInformation("Executed SQL: {Sql} | Rows: {Count}", trimmedSql, results.Count);
                
                return (results, columns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL execution error for query: {Sql}", sql);
                throw new InvalidOperationException($"Database error: {ex.Message}");
            }
        }

        private async Task<string> FormatResultsWithLlmAsync(string originalQuestion, List<Dictionary<string, object>> results, List<string> columns, string? explanation)
        {
            if (results.Count == 0)
                return "I found no matching records for your query.";

            // For small results, format directly without second LLM call (faster)
            if (results.Count <= 5)
            {
                var summary = $"Found {results.Count} result(s):\n\n";
                foreach (var row in results)
                {
                    summary += "• " + string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}")) + "\n";
                }
                return summary;
            }

            // For larger results, use LLM to summarize
            var dataJson = JsonSerializer.Serialize(new { columns, rows = results.Take(20) }); // Limit to 20 for token economy
            
            var prompt = $@"Original question: ""{originalQuestion}""
Query explanation: {explanation}
Found {results.Count} records. Here's a sample (first {Math.Min(results.Count, 20)}):

{dataJson}

Provide a concise, helpful summary. If salary data is present, note the currency (AED). 
Be conversational but professional.";

            try
            {
                var completion = await _groqService.GetChatCompletionAsync(
                    "You format database results for HR staff.", 
                    prompt, 
                    null
                );
                return completion.Choices.FirstOrDefault()?.Message?.Content ?? "Data retrieved successfully.";
            }
            catch
            {
                // Fallback formatting
                return $"Found {results.Count} records matching your criteria. Sample: {JsonSerializer.Serialize(results.First())}";
            }
        }
    }
}